using HRMS_BACKEND.Data;
using HRMS_BACKEND.Dto.Attendance;
using HRMS_BACKEND.Entities;
using HRMS_BACKEND.Entities.Enums;
using HRMS_BACKEND.Helpers;
using HRMS_BACKEND.IServices;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;


public class AttendanceService : IAttendanceService
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;
    private readonly IWorkingDaysService _workingDaysService;
    private readonly IShiftAssignmentService _shiftAssignmentService;

    public AttendanceService(AppDbContext db, IConfiguration config, IWorkingDaysService workingDaysService, IShiftAssignmentService shiftAssignmentService)
    {
        _db = db;
        _config = config;
        _workingDaysService = workingDaysService;
        _shiftAssignmentService = shiftAssignmentService;
    }

    // #region agent log
    private static string AgentDebugLogPath =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "debug-f60ed9.log"));

    private static void AgentDebugLog(string hypothesisId, string location, string message, object? data = null, string runId = "pre-fix")
    {
        try
        {
            var payload = JsonSerializer.Serialize(new
            {
                sessionId = "f60ed9",
                hypothesisId,
                location,
                message,
                data,
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                runId
            });
            File.AppendAllText(AgentDebugLogPath, payload + Environment.NewLine);
        }
        catch { }
    }

    private static bool IsOnApprovedLeave(DateOnly targetDate, LeaveMaster leave)
    {
        var dayStart = targetDate.ToDateTime(TimeOnly.MinValue);
        return leave.StartDate.Date <= dayStart && leave.EndDate.Date >= dayStart;
    }
    // #endregion

    private async Task<DateOnly> DetermineActiveShiftDateAsync(int employeeId, Shift shift, DateTime now, DateOnly today)
    {
        if (shift.EndTime <= shift.StartTime)
        {
            var yesterday = today.AddDays(-1);
            
            // Check if employee was assigned to this shift on yesterday
            var yesterdayShift = await _shiftAssignmentService.GetEffectiveShiftForDateAsync(employeeId, yesterday);
            if (yesterdayShift != null && yesterdayShift.Id == shift.Id)
            {
                var yesterdayWorkingDays = await _workingDaysService.GetScheduleForDateAsync(employeeId, yesterday);
                if (yesterdayWorkingDays != null && IsWorkingDay(yesterdayWorkingDays, yesterday.DayOfWeek))
                {
                    var (yStart, yEnd) = BuildShiftWindow(shift, yesterday);
                    if (now >= yStart && now < yEnd)
                    {
                        return yesterday;
                    }
                }
            }
        }

        return today;
    }

    public async Task<AttendanceResponseDto> ClockInAsync(int employeeId)
    {
        var now = CompanyTime.Now(_config);
        var today = CompanyTime.Today(_config);

        var employee = await LoadEmployeeAttendanceContextAsync(employeeId)
            ?? throw new KeyNotFoundException("Employee not found.");

        ValidateAttendanceEligibility(employee);

        // Get the effective shift for today using the new assignment system
        var effectiveShift = await _shiftAssignmentService.GetEffectiveShiftForDateAsync(employeeId, today);
        if (effectiveShift == null)
            throw new InvalidOperationException("No shift is assigned to you for today. Please contact Admin.");

        var targetDate = await DetermineActiveShiftDateAsync(employeeId, effectiveShift, now, today);

        var workingDays = await _workingDaysService.GetScheduleForDateAsync(employeeId, targetDate);
        if (workingDays is null)
            throw new InvalidOperationException("Working days have not been configured for you. Please contact Admin.");

        if (!IsWorkingDay(workingDays, targetDate.DayOfWeek))
            throw new InvalidOperationException("Today is not a scheduled working day for you.");

        var existingOpenAttendance = await _db.EmployeeAttendances
            .Include(a => a.Employee)
            .Include(a => a.Shift)
            .Where(a => a.EmployeeId == employeeId && a.ClockOut == null)
            .OrderByDescending(a => a.Date)
            .FirstOrDefaultAsync();

        if (existingOpenAttendance is not null)
        {
            if (existingOpenAttendance.Date == targetDate)
                throw new InvalidOperationException("You have already clocked in today.");

            throw new InvalidOperationException($"You still have an open attendance record from {existingOpenAttendance.Date:yyyy-MM-dd}.");
        }

        var existingTodayAttendance = await _db.EmployeeAttendances
            .AnyAsync(a => a.EmployeeId == employeeId && a.Date == targetDate);

        if (existingTodayAttendance)
            throw new InvalidOperationException("You have already clocked in today.");

        // Shift Limit enforcement.
        await EnforceShiftLimitAsync(effectiveShift, targetDate);

        var (startTime, endTime) = BuildShiftWindow(effectiveShift, targetDate);

        if (now < startTime)
            throw new InvalidOperationException($"You cannot clock in before your shift starts at {startTime:hh:mm tt}.");

        if (now >= endTime)
            throw new InvalidOperationException("Your shift window for today has already ended.");

        var attendance = new EmployeeAttendance
        {
            EmployeeId = employee.Id,
            ShiftId = effectiveShift.Id,
            Date = targetDate,
            ClockIn = now,
            StartTime = startTime,
            EndTime = endTime,
            LateThresholdMinutesSnapshot = effectiveShift.LateThresholdMinutes,
            ShiftNameSnapshot = effectiveShift.Name
        };

        _db.EmployeeAttendances.Add(attendance);

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            throw new InvalidOperationException("You have already clocked in today.");
        }

        var createdAttendance = await LoadAttendanceAsync(attendance.Id);
        return MapAttendanceToDto(createdAttendance);
    }

    public async Task<AttendanceResponseDto> ClockOutAsync(int employeeId, ClockOutRequestDto dto)
    {
        var now = CompanyTime.Now(_config);

        var attendance = await _db.EmployeeAttendances
            .Include(a => a.Employee)
            .Include(a => a.Shift)
            .Where(a => a.EmployeeId == employeeId && a.ClockOut == null)
            .OrderByDescending(a => a.Date)
            .FirstOrDefaultAsync()
            ?? throw new InvalidOperationException("You do not have an active attendance record to clock out from.");

        // Boundary inclusivity: exact EndTime allowed (>=).
        if (now >= attendance.EndTime)
        {
            attendance.ClockOut = now;
            ClearEmergencyClockOutState(attendance);
            await _db.SaveChangesAsync();
            return MapAttendanceToDto(attendance);
        }

        // Auto clock-out: force clock-out when shift ends (no emergency reason needed)
        if (dto.AutoClockOut)
        {
            attendance.ClockOut = now;
            ClearEmergencyClockOutState(attendance);
            await _db.SaveChangesAsync();
            return MapAttendanceToDto(attendance);
        }

        if (string.IsNullOrWhiteSpace(dto.EmergencyReason))
            throw new InvalidOperationException($"You cannot clock out before your shift ends at {attendance.EndTime:hh:mm tt}. Submit an emergency reason to request an early clock-out.");

        // 1:many emergency clock-out requests per attendance; max one Pending ACROSS ALL attendances for this employee.
        // Resubmit-after-rejection UX: user can re-request on same or later attendance after rejection resolved.
        var hasPendingSystemWide = await _db.EmployeeAttendances
            .AnyAsync(a =>
                a.EmployeeId == employeeId &&
                a.EmergencyClockOutStatus == EmergencyClockOutStatus.Pending);

        if (hasPendingSystemWide)
            throw new InvalidOperationException("You already have a pending emergency clock-out request. Please wait for Admin review or contact your manager.");

        if (attendance.EmergencyClockOutStatus == EmergencyClockOutStatus.Pending)
            throw new InvalidOperationException("You already have a pending emergency clock-out request for this attendance.");

        // EmergencyClockOut requested time = submission time (DateTime.Now at request).
        // Employee does NOT provide a target clock-out timestamp; Admin later approves/rejects; on approval ClockOut = RequestedAt (not approval time).
        attendance.EmergencyClockOutReason = dto.EmergencyReason.Trim();
        attendance.EmergencyClockOutStatus = EmergencyClockOutStatus.Pending;
        attendance.EmergencyClockOutRequestedAt = now;

        await _db.SaveChangesAsync();
        return MapAttendanceToDto(attendance);
    }

    public async Task<AttendanceResponseDto> ApproveEmergencyClockOutAsync(int attendanceId)
    {
        var attendance = await LoadAttendanceAsync(attendanceId);
        if (attendance.EmergencyClockOutStatus != EmergencyClockOutStatus.Pending || !attendance.EmergencyClockOutRequestedAt.HasValue)
            throw new InvalidOperationException("There is no pending emergency clock-out request for this attendance record.");

        // ClockOut = the current approval time (the moment Admin approves it).
        attendance.ClockOut = CompanyTime.Now(_config);
        attendance.EmergencyClockOutStatus = EmergencyClockOutStatus.Approved;

        await _db.SaveChangesAsync();
        return MapAttendanceToDto(attendance);
    }

    public async Task<AttendanceResponseDto> RejectEmergencyClockOutAsync(int attendanceId)
    {
        var attendance = await LoadAttendanceAsync(attendanceId);
        if (attendance.EmergencyClockOutStatus != EmergencyClockOutStatus.Pending)
            throw new InvalidOperationException("There is no pending emergency clock-out request for this attendance record.");

        attendance.EmergencyClockOutStatus = EmergencyClockOutStatus.Rejected;
        // Keep RequestedAt + Reason intact for audit trail after rejection; allow re-submission elsewhere.

        await _db.SaveChangesAsync();
        return MapAttendanceToDto(attendance);
    }

    public async Task<AdminTimesheetResponseDto> GetAdminTimesheetAsync(DateOnly? date)
    {
        var targetDate = date ?? CompanyTime.Today(_config);

        var activeEmployees = await _db.Employees
            .Include(e => e.Department)
            .Include(e => e.Shift)
            .Where(e => e.IsActive && e.Role != Role.Admin)
            .OrderBy(e => e.FullName)
            .ToListAsync();

        var employeeIds = activeEmployees.Select(e => e.Id).ToList();

        // Get first assignment date from EmployeeShiftAssignments table
        var firstAssignmentDates = await _db.EmployeeShiftAssignments
            .Where(esa => employeeIds.Contains(esa.EmployeeId))
            .GroupBy(esa => esa.EmployeeId)
            .Select(g => new { EmployeeId = g.Key, FirstAssignmentDate = g.Min(esa => esa.EffectiveFrom) })
            .ToDictionaryAsync(x => x.EmployeeId, x => x.FirstAssignmentDate);

        // Filter to eligible employees: either have EmployeeShiftAssignments record OR have Employee.ShiftId set
        // This ensures we include employees assigned via the old Employee.ShiftId method
        var eligibleEmployees = activeEmployees
            .Where(e => 
                (firstAssignmentDates.TryGetValue(e.Id, out var fDate) && fDate <= targetDate) ||
                (e.ShiftId.HasValue) // Employee has a shift assigned via Employee.ShiftId
            )
            .ToList();

        var eligibleEmployeeIds = eligibleEmployees.Select(e => e.Id).ToHashSet();

        if (!eligibleEmployees.Any())
        {
            return new AdminTimesheetResponseDto
            {
                Date = targetDate,
                IsCompanyOffDay = false,
                NoShiftAssignmentsYet = true,
                PresentEmployees = new List<TimesheetEmployeeDto>(),
                LateEmployees = new List<TimesheetEmployeeDto>(),
                AbsentEmployees = new List<TimesheetEmployeeDto>(),
                HolidayEmployees = new List<TimesheetEmployeeDto>(),
                PendingEmergencyClockOutRequests = new List<PendingEmergencyClockOutDto>()
            };
        }

        var presentAttendances = await _db.EmployeeAttendances
            .Include(a => a.Employee)
                .ThenInclude(e => e.Department)
            .Include(a => a.Shift)
            .Where(a => a.Date == targetDate && a.ClockIn != null && a.Employee.Role != Role.Admin && eligibleEmployeeIds.Contains(a.EmployeeId))
            .OrderBy(a => a.Employee.FullName)
            .ToListAsync();

        // Keep assignment rows—not just live shifts—as the source of truth.  A deleted
        // shift deliberately leaves its assignment history in place, so past dates can
        // still be classified as absent/holiday instead of disappearing from the sheet.
        var assignmentHistory = await _db.EmployeeShiftAssignments
            .Where(esa => eligibleEmployeeIds.Contains(esa.EmployeeId) && esa.EffectiveFrom <= targetDate)
            .Include(esa => esa.Shift)
            .OrderByDescending(esa => esa.EffectiveFrom)
            .ToListAsync();

        var schedules = await _db.WorkingDays
            .Where(wd =>
                eligibleEmployeeIds.Contains(wd.EmployeeId) &&
                wd.EffectiveFromDate <= targetDate &&
                (wd.EffectiveToDate == null || wd.EffectiveToDate.Value >= targetDate))
            .OrderBy(wd => wd.EffectiveFromDate)
            .ToListAsync();

        var presentEmployeeIds = presentAttendances.Select(a => a.EmployeeId).ToHashSet();

        var presentEmployees = new List<TimesheetEmployeeDto>();
        var lateEmployees = new List<TimesheetEmployeeDto>();
        var absentEmployees = new List<TimesheetEmployeeDto>();
        var holidayEmployees = new List<TimesheetEmployeeDto>();
        
        // Process present attendances - separate into present and late
        foreach (var attendance in presentAttendances)
        {
            bool isLate = false;
            int minutesLate = 0;

            // Late calculation: clockIn > (startTime + lateThresholdSnapshot)
            // Use snapshot, not live Shift FK — survives shift deletion
            if (attendance.ClockIn.HasValue)
            {
                var lateThreshold = attendance.StartTime.AddMinutes(attendance.LateThresholdMinutesSnapshot);
                if (attendance.ClockIn.Value > lateThreshold)
                {
                    isLate = true;
                    minutesLate = (int)Math.Ceiling((attendance.ClockIn.Value - lateThreshold).TotalMinutes);
                }
            }

            var employeeDto = new TimesheetEmployeeDto
            {
                EmployeeId = attendance.EmployeeId,
                EmployeeName = attendance.Employee.FullName,
                Role = attendance.Employee.Role.ToString(),
                DepartmentName = attendance.Employee.Department?.Name,
                ShiftName = attendance.ShiftNameSnapshot ?? attendance.Shift?.Name ?? "Deleted Shift",
                ClockIn = attendance.ClockIn,
                ClockOut = attendance.ClockOut,
                StartTime = attendance.StartTime,
                EndTime = attendance.EndTime,
                IsLate = isLate,
                MinutesLate = minutesLate
            };

            if (isLate)
            {
                lateEmployees.Add(employeeDto);
            }
            else
            {
                presentEmployees.Add(employeeDto);
            }
        }
        
        // Bulk-load approved leaves overlapping target date for eligible employees
        var targetDayStart = targetDate.ToDateTime(TimeOnly.MinValue);
        var approvedLeavesToday = await _db.LeaveMasters
            .Where(l => eligibleEmployeeIds.Contains(l.EmployeeId) &&
                        l.Status == LeaveStatus.Approved &&
                        l.StartDate.Date <= targetDayStart &&
                        l.EndDate.Date >= targetDayStart)
            .Include(l => l.LeaveType)
            .ToListAsync();
        var leaveByEmployeeId = approvedLeavesToday
            .GroupBy(l => l.EmployeeId)
            .ToDictionary(g => g.Key, g => g.First());

        // Process eligible employees who are not present today
        foreach (var employee in eligibleEmployees.Where(e => !presentEmployeeIds.Contains(e.Id)))
        {
            leaveByEmployeeId.TryGetValue(employee.Id, out var leaveToday);

            // #region agent log
            if (employee.FullName.Contains("john", StringComparison.OrdinalIgnoreCase) ||
                employee.Email.Contains("john", StringComparison.OrdinalIgnoreCase))
            {
                var allEmployeeLeaves = await _db.LeaveMasters
                    .Where(l => l.EmployeeId == employee.Id && l.Status == LeaveStatus.Approved)
                    .Select(l => new { l.StartDate, l.EndDate, l.Status })
                    .ToListAsync();
                AgentDebugLog("H1", "AttendanceService.GetAdminTimesheetAsync:leaveCheck", "John leave classification", new
                {
                    employeeId = employee.Id,
                    employeeName = employee.FullName,
                    targetDate = targetDate.ToString("yyyy-MM-dd"),
                    targetDayStart,
                    matchedLeaveToday = leaveToday != null,
                    leaveStart = leaveToday?.StartDate,
                    leaveEnd = leaveToday?.EndDate,
                    allApprovedLeaves = allEmployeeLeaves
                });
            }
            // #endregion
                
            if (leaveToday != null)
            {
                holidayEmployees.Add(new TimesheetEmployeeDto
                {
                    EmployeeId = employee.Id,
                    EmployeeName = employee.FullName,
                    Role = employee.Role.ToString(),
                    DepartmentName = employee.Department?.Name,
                    HolidayReason = "On Leave",
                    LeaveType = leaveToday.LeaveType?.Name ?? "Leave"
                });
                continue;
            }
            
            // Get effective shift: try EmployeeShiftAssignments first, then fall back to Employee.ShiftId
            var effectiveAssignment = assignmentHistory.FirstOrDefault(esa => esa.EmployeeId == employee.Id);
            var shiftName = effectiveAssignment?.Shift?.Name ?? employee.Shift?.Name ?? "Deleted Shift";
            var hasShift = effectiveAssignment != null || employee.ShiftId.HasValue;
            
            if (!hasShift)
            {
                // This employee has no shift assignment at all - skip
                continue;
            }

            var applicableSchedule = schedules
                .Where(wd => wd.EmployeeId == employee.Id)
                .OrderByDescending(wd => wd.EffectiveFromDate)
                .FirstOrDefault();
            
            if (applicableSchedule != null && IsWorkingDay(applicableSchedule, targetDate.DayOfWeek))
            {
                // Working day, but employee did not clock in -> Absent
                absentEmployees.Add(new TimesheetEmployeeDto
                {
                    EmployeeId = employee.Id,
                    EmployeeName = employee.FullName,
                    Role = employee.Role.ToString(),
                    DepartmentName = employee.Department?.Name,
                    ShiftName = shiftName
                });
            }
            else
            {
                // Scheduled day off -> Holiday (Scheduled Off)
                holidayEmployees.Add(new TimesheetEmployeeDto
                {
                    EmployeeId = employee.Id,
                    EmployeeName = employee.FullName,
                    Role = employee.Role.ToString(),
                    DepartmentName = employee.Department?.Name,
                    ShiftName = shiftName,
                    HolidayReason = "Scheduled Off"
                });
            }
        }

        var pendingRequests = await _db.EmployeeAttendances
            .Include(a => a.Employee)
            .Include(a => a.Shift)
            .Where(a => a.Date == targetDate && a.EmergencyClockOutStatus == EmergencyClockOutStatus.Pending)
            .OrderBy(a => a.Employee.FullName)
            .ToListAsync();

        bool hasAnyWorkingEmployee = eligibleEmployees.Any(employee =>
        {
            var hasAssignment = assignmentHistory.Any(esa => esa.EmployeeId == employee.Id);
            var applicableSchedule = schedules
                .Where(wd => wd.EmployeeId == employee.Id)
                .OrderByDescending(wd => wd.EffectiveFromDate)
                .FirstOrDefault();
            return hasAssignment && applicableSchedule != null && IsWorkingDay(applicableSchedule, targetDate.DayOfWeek);
        });

        bool isCompanyOffDay = eligibleEmployees.Count > 0 && !hasAnyWorkingEmployee && presentAttendances.Count == 0;

        return new AdminTimesheetResponseDto
        {
            Date = targetDate,
            IsCompanyOffDay = isCompanyOffDay,
            PresentEmployees = presentEmployees,
            LateEmployees = lateEmployees,
            AbsentEmployees = absentEmployees,
            HolidayEmployees = holidayEmployees,
            PendingEmergencyClockOutRequests = pendingRequests
                .Where(a => a.EmergencyClockOutRequestedAt.HasValue && !string.IsNullOrWhiteSpace(a.EmergencyClockOutReason))
                .Select(a => new PendingEmergencyClockOutDto
                {
                    AttendanceId = a.Id,
                    EmployeeId = a.EmployeeId,
                    EmployeeName = a.Employee.FullName,
                    ShiftName = a.ShiftNameSnapshot ?? a.Shift?.Name ?? "Deleted Shift",
                    Date = a.Date,
                    RequestedClockOutAt = a.EmergencyClockOutRequestedAt!.Value,
                    Reason = a.EmergencyClockOutReason!,
                    Status = a.EmergencyClockOutStatus!.Value.ToString()
                })
                .ToList()
        };
    }

    private async Task EnforceShiftLimitAsync(Shift shift, DateOnly today)
    {
        // Shift.Limit is the cap (e.g. "max 20 people on night shift).
        var employeesClockedInToday = await _db.EmployeeAttendances
            .CountAsync(a =>
                a.ShiftId == shift.Id &&
                a.Date == today &&
                a.ClockIn != null);

        if (employeesClockedInToday >= shift.Limit)
        {
            throw new InvalidOperationException(
                $"{shift.Name} is full for today ({employeesClockedInToday}/{shift.Limit}). Contact your manager."
            );
        }
    }

    private async Task<Employee?> LoadEmployeeAttendanceContextAsync(int employeeId)
    {
        return await _db.Employees
            .Include(e => e.Shift)
            .Include(e => e.PendingShift)
            .FirstOrDefaultAsync(e => e.Id == employeeId);
    }

    private async Task<EmployeeAttendance> LoadAttendanceAsync(int attendanceId)
    {
        return await _db.EmployeeAttendances
            .Include(a => a.Employee)
            .Include(a => a.Shift)
            .FirstOrDefaultAsync(a => a.Id == attendanceId)
            ?? throw new KeyNotFoundException("Attendance record not found.");
    }

    private static void ValidateAttendanceEligibility(Employee employee)
    {
        if (!employee.IsActive)
            throw new InvalidOperationException("Inactive employees cannot use attendance.");

        if (employee.Role == Role.Admin)
            throw new InvalidOperationException("Admin users are not part of attendance clock-in and clock-out.");
    }

    private async Task ApplyDueShiftChangeAsync(Employee employee, DateOnly today)
    {
        // DOCUMENTED DECISION: mid-day shift change.
        // If a PendingShiftEffectiveFromDate <= today: promote PendingShift to live Shift NOW.
        // If the employee already clocked in EARLIER TODAY under the OLD shift config, that earlier attendance row
        // already has StartTime/EndTime/ShiftId snapshotted and is NOT rewritten.
        // Effect: only attendance-rows-created-after-this-moment use the newly promoted shift.
        if (!employee.PendingShiftEffectiveFromDate.HasValue || employee.PendingShiftEffectiveFromDate.Value > today)
        {
            return;
        }

        employee.ShiftId = employee.PendingShiftId;
        employee.PendingShiftId = null;
        employee.PendingShiftEffectiveFromDate = null;
        await _db.SaveChangesAsync();
    }

    private static bool IsWorkingDay(WorkingDays workingDays, DayOfWeek dayOfWeek)
    {
        return dayOfWeek switch
        {
            DayOfWeek.Monday => workingDays.Monday,
            DayOfWeek.Tuesday => workingDays.Tuesday,
            DayOfWeek.Wednesday => workingDays.Wednesday,
            DayOfWeek.Thursday => workingDays.Thursday,
            DayOfWeek.Friday => workingDays.Friday,
            DayOfWeek.Saturday => workingDays.Saturday,
            DayOfWeek.Sunday => workingDays.Sunday,
            _ => false
        };
    }

    private static (DateTime StartTime, DateTime EndTime) BuildShiftWindow(Shift shift, DateOnly date)
    {
        var startTime = date.ToDateTime(shift.StartTime);
        var endTime = date.ToDateTime(shift.EndTime);

        if (endTime <= startTime)
        {
            endTime = endTime.AddDays(1);
        }

        return (startTime, endTime);
    }

    private static void ClearEmergencyClockOutState(EmployeeAttendance attendance)
    {
        attendance.EmergencyClockOutReason = null;
        attendance.EmergencyClockOutStatus = null;
        attendance.EmergencyClockOutRequestedAt = null;
    }

    public async Task<MyTodayAttendanceResponseDto> GetMyTodayAttendanceAsync(int employeeId)
    {
        var now = CompanyTime.Now(_config);
        var today = CompanyTime.Today(_config);

        var employee = await LoadEmployeeAttendanceContextAsync(employeeId)
            ?? throw new KeyNotFoundException("Employee not found.");

        var firstAssignmentDate = await _shiftAssignmentService.GetFirstAssignmentDateAsync(employeeId);
        var hasEverBeenAssigned = firstAssignmentDate.HasValue;

        // Check if employee is on leave today
        var leaveToday = await _db.LeaveMasters
            .Where(l => l.EmployeeId == employeeId && 
                       l.Status == LeaveStatus.Approved &&
                       l.StartDate.Date <= today.ToDateTime(TimeOnly.MinValue) && 
                       l.EndDate.Date >= today.ToDateTime(TimeOnly.MinValue))
            .Include(l => l.LeaveType)
            .FirstOrDefaultAsync();

        // If employee is on leave, return leave status
        if (leaveToday != null)
        {
            return new MyTodayAttendanceResponseDto
            {
                HasShift = false,
                HasEverBeenAssigned = hasEverBeenAssigned,
                IsWorkingDayToday = false,
                IsOnLeaveToday = true,
                LeaveType = leaveToday.LeaveType.Name,
                LeaveStatus = leaveToday.Status.ToString(),
                TodayAttendance = null,
                ServerTime = now
            };
        }

        // Check if employee has been assigned a shift before today (pre-assignment state logic)
        if (!firstAssignmentDate.HasValue || firstAssignmentDate.Value > today)
        {
            // Employee not yet assigned - return pre-assignment state
            return new MyTodayAttendanceResponseDto
            {
                HasShift = false,
                HasEverBeenAssigned = false,
                IsOnLeaveToday = false,
                ServerTime = now
            };
        }

        // Get the effective shift for today using the new assignment system
        var effectiveShift = await _shiftAssignmentService.GetEffectiveShiftForDateAsync(employeeId, today);
        if (effectiveShift == null)
        {
            return new MyTodayAttendanceResponseDto
            {
                HasShift = false,
                HasEverBeenAssigned = true,
                IsOnLeaveToday = false,
                ServerTime = now
            };
        }

        var targetDate = await DetermineActiveShiftDateAsync(employeeId, effectiveShift, now, today);

        var workingDays = await _workingDaysService.GetScheduleForDateAsync(employeeId, targetDate);
        bool isWorkingDayToday = workingDays != null && IsWorkingDay(workingDays, targetDate.DayOfWeek);

        var todayAttendance = await _db.EmployeeAttendances
            .Include(a => a.Employee)
            .Include(a => a.Shift)
            .FirstOrDefaultAsync(a => a.EmployeeId == employeeId && a.Date == targetDate);

        var (startTime, endTime) = BuildShiftWindow(effectiveShift, targetDate);

        // Calculate late threshold and late duration
        var lateThresholdMinutes = effectiveShift.LateThresholdMinutes;
        var allowedLateUntil = startTime.AddMinutes(lateThresholdMinutes);
        
        bool isLateToday = false;
        int minutesLateToday = 0;
        
        if (todayAttendance?.ClockIn != null && lateThresholdMinutes > 0)
        {
            if (todayAttendance.ClockIn.Value > allowedLateUntil)
            {
                isLateToday = true;
                minutesLateToday = (int)Math.Ceiling((todayAttendance.ClockIn.Value - allowedLateUntil).TotalMinutes);
            }
        }

        return new MyTodayAttendanceResponseDto
        {
            HasShift = true,
            HasEverBeenAssigned = true,
            ShiftId = effectiveShift.Id,
            ShiftName = effectiveShift.Name,
            ShiftStartTime = effectiveShift.StartTime,
            ShiftEndTime = effectiveShift.EndTime,
            ShiftStartDateTime = startTime,
            ShiftEndDateTime = endTime,
            IsWorkingDayToday = isWorkingDayToday,
            TodayAttendance = todayAttendance != null ? MapAttendanceToDto(todayAttendance) : null,
            IsOnLeaveToday = false,
            LateThresholdMinutes = lateThresholdMinutes,
            AllowedLateUntil = allowedLateUntil,
            IsLateToday = isLateToday,
            MinutesLateToday = minutesLateToday,
            ServerTime = now
        };
    }

    public async Task<List<AttendanceResponseDto>> GetMyAttendanceHistoryAsync(int employeeId, DateOnly? startDate, DateOnly? endDate)
    {
        var firstAssignmentDate = await _shiftAssignmentService.GetFirstAssignmentDateAsync(employeeId);
        if (!firstAssignmentDate.HasValue)
            return new List<AttendanceResponseDto>();

        // Determine the date range
        var rangeStart = startDate ?? firstAssignmentDate.Value;
        var rangeEnd = endDate ?? CompanyTime.Today(_config);

        // Query all attendance records for this employee in the date range
        var attendances = await _db.EmployeeAttendances
            .Include(a => a.Employee)
            .Include(a => a.Shift)
            .Where(a => a.EmployeeId == employeeId && a.Date >= rangeStart && a.Date <= rangeEnd)
            .OrderByDescending(a => a.Date)
            .ThenByDescending(a => a.ClockIn)
            .ToListAsync();

        // Get shift assignments to check for absents
        var shiftAssignments = await _db.EmployeeShiftAssignments
            .Where(esa => esa.EmployeeId == employeeId)
            .Select(esa => new { esa.EffectiveFrom, esa.ShiftId })
            .ToListAsync();

        // Get working days schedule
        var workingDaysSchedule = await _db.WorkingDays
            .Where(wd => wd.EmployeeId == employeeId)
            .ToListAsync();

        // Build a list of results that includes both actual attendance and synthesized absents
        var results = new List<AttendanceResponseDto>();
        var attendanceByDate = attendances.ToDictionary(a => a.Date);
        var currentDate = rangeStart;

        while (currentDate <= rangeEnd)
        {
            if (attendanceByDate.TryGetValue(currentDate, out var attendance))
            {
                // Actual attendance record exists
                results.Add(MapAttendanceToDto(attendance));
            }
            else
            {
                // Check if this day should have an attendance record (working day + assigned shift)
                // Get effective shift for this date
                var effectiveAssignment = shiftAssignments.FirstOrDefault(a => a.EffectiveFrom <= currentDate);
                if (effectiveAssignment != null)
                {
                    // Check if it's a working day
                    WorkingDays? applicableWd = null;
                    foreach (var wd in workingDaysSchedule.OrderByDescending(w => w.EffectiveFromDate))
                    {
                        if (wd.EffectiveFromDate <= currentDate && (wd.EffectiveToDate == null || wd.EffectiveToDate.Value >= currentDate))
                        {
                            applicableWd = wd;
                            break;
                        }
                    }

                    bool isWorkingDay = false;
                    if (applicableWd == null)
                    {
                        // No working days schedule configured; treat Mon-Fri as working days
                        isWorkingDay = currentDate.DayOfWeek != DayOfWeek.Saturday && currentDate.DayOfWeek != DayOfWeek.Sunday;
                    }
                    else
                    {
                        isWorkingDay = IsWorkingDay(applicableWd, currentDate.DayOfWeek);
                    }

                    // If it's a working day with assigned shift but no attendance record = ABSENT
                    if (isWorkingDay)
                    {
                        // Create synthetic absent record
                        var employee = await _db.Employees.FindAsync(employeeId);
                        results.Add(new AttendanceResponseDto
                        {
                            Id = 0, // Synthetic record
                            EmployeeId = employeeId,
                            EmployeeName = employee?.FullName ?? "Unknown",
                            ShiftId = null,
                            ShiftName = "N/A",
                            Date = currentDate,
                            ClockIn = null,
                            ClockOut = null,
                            StartTime = DateTime.MinValue, // Synthetic
                            EndTime = DateTime.MinValue, // Synthetic
                            EmergencyClockOutReason = null,
                            EmergencyClockOutStatus = null,
                            EmergencyClockOutRequestedAt = null,
                            IsLate = false,
                            MinutesLate = 0
                        });
                    }
                }
            }

            currentDate = currentDate.AddDays(1);
        }

        // Sort by date descending
        return results.OrderByDescending(r => r.Date).ToList();
    }

    private static AttendanceResponseDto MapAttendanceToDto(EmployeeAttendance attendance)
    {
        // Calculate late status
        bool isLate = false;
        int minutesLate = 0;

        if (attendance.ClockIn.HasValue)
        {
            // Use the value captured at clock-in, so editing/deleting a shift never rewrites history.
            var lateThreshold = attendance.StartTime.AddMinutes(attendance.LateThresholdMinutesSnapshot);
            
            // If clocked in after late threshold, it's late
            if (attendance.ClockIn.Value > lateThreshold)
            {
                isLate = true;
                minutesLate = (int)Math.Ceiling((attendance.ClockIn.Value - lateThreshold).TotalMinutes);
            }
        }

        return new AttendanceResponseDto
        {
            Id = attendance.Id,
            EmployeeId = attendance.EmployeeId,
            EmployeeName = attendance.Employee.FullName,
            ShiftId = attendance.ShiftId,
            ShiftName = attendance.ShiftNameSnapshot ?? attendance.Shift?.Name ?? "Deleted Shift",
            Date = attendance.Date,
            ClockIn = attendance.ClockIn,
            ClockOut = attendance.ClockOut,
            StartTime = attendance.StartTime,
            EndTime = attendance.EndTime,
            EmergencyClockOutReason = attendance.EmergencyClockOutReason,
            EmergencyClockOutStatus = attendance.EmergencyClockOutStatus?.ToString(),
            EmergencyClockOutRequestedAt = attendance.EmergencyClockOutRequestedAt,
            IsLate = isLate,
            MinutesLate = minutesLate
        };
    }

    public async Task<List<AttendanceExportDto>> GetFilteredForExportAsync(string? branch, string? department, string? search, DateOnly? date, string? status = null)
    {
        var targetDate = date ?? CompanyTime.Today(_config);

        var query = _db.EmployeeAttendances
            .Include(a => a.Employee)
                .ThenInclude(e => e.Department)
                    .ThenInclude(d => d != null ? d.Branch : null!)
            .Include(a => a.Shift)
            .Where(a => a.Date == targetDate && a.Employee.Role != Role.Admin);

        // Filter by branch
        if (!string.IsNullOrWhiteSpace(branch))
        {
            query = query.Where(a => a.Employee.Department.Branch.Name.Contains(branch));
        }

        // Filter by department
        if (!string.IsNullOrWhiteSpace(department))
        {
            query = query.Where(a => a.Employee.Department.Name.Contains(department));
        }

        // Filter by search term (employee name or email)
        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(a => 
                a.Employee.FullName.Contains(search) || 
                a.Employee.Email.Contains(search));
        }

        var attendances = await query
            .OrderBy(a => a.Employee.FullName)
            .ToListAsync();

        // Convert to export DTOs with late calculation
        var exportDtos = attendances.Select(a =>
        {
            // Calculate late status using snapshot, not live Shift FK
            bool isLate = false;
            int minutesLate = 0;

            if (a.ClockIn.HasValue)
            {
                if (a.ClockIn.HasValue)
                {
                    var lateThreshold = a.StartTime.AddMinutes(a.LateThresholdMinutesSnapshot);

                    if (a.ClockIn.Value > lateThreshold)
                    {
                        isLate = true;
                        minutesLate = (int)Math.Ceiling((a.ClockIn.Value - lateThreshold).TotalMinutes);
                    }
                }
            }

            return new AttendanceExportDto
            {
                EmployeeName = a.Employee.FullName,
                Email = a.Employee.Email,
                Branch = a.Employee.Department?.Branch?.Name ?? "No Branch",
                Department = a.Employee.Department?.Name ?? "No Department",
                Role = a.Employee.Role.ToString(),
                Shift = a.ShiftNameSnapshot ?? a.Shift?.Name ?? "Deleted Shift",
                ClockIn = a.ClockIn,
                ClockOut = a.ClockOut,
                StartTime = a.StartTime,
                EndTime = a.EndTime,
                IsLate = isLate,
                MinutesLate = minutesLate,
                Status = isLate ? "Late" : (a.ClockIn.HasValue ? "Present" : "Absent"),
                Date = a.Date
            };
        }).ToList();

        // Filter by status after late calculation
        if (!string.IsNullOrWhiteSpace(status) && status != "all")
        {
            exportDtos = status switch
            {
                "present" => exportDtos.Where(e => e.Status == "Present").ToList(),
                "absent" => exportDtos.Where(e => e.Status == "Absent").ToList(),
                "late" => exportDtos.Where(e => e.Status == "Late").ToList(),
                "holiday" => await BuildHolidayExportDtosAsync(targetDate),
                "emergency" => attendances.Where(a => a.EmergencyClockOutStatus == EmergencyClockOutStatus.Pending)
                    .Select(a =>
                    {
                        // Recalculate for emergency records
                        bool isLate = false;
                        int minutesLate = 0;

                        if (a.ClockIn.HasValue)
                        {
                            var lateThreshold = a.StartTime.AddMinutes(a.LateThresholdMinutesSnapshot);

                            if (a.ClockIn.Value > lateThreshold)
                            {
                                isLate = true;
                                minutesLate = (int)Math.Ceiling((a.ClockIn.Value - lateThreshold).TotalMinutes);
                            }
                        }

                        return new AttendanceExportDto
                        {
                            EmployeeName = a.Employee.FullName,
                            Email = a.Employee.Email,
                            Branch = a.Employee.Department?.Branch?.Name ?? "No Branch",
                            Department = a.Employee.Department?.Name ?? "No Department",
                            Role = a.Employee.Role.ToString(),
                            Shift = a.ShiftNameSnapshot ?? a.Shift?.Name ?? "Deleted Shift",
                            ClockIn = a.ClockIn,
                            ClockOut = a.ClockOut,
                            StartTime = a.StartTime,
                            EndTime = a.EndTime,
                            IsLate = isLate,
                            MinutesLate = minutesLate,
                            Status = isLate ? "Late" : (a.ClockIn.HasValue ? "Present" : "Absent"),
                            Date = a.Date
                        };
                    }).ToList(),
                _ => exportDtos
            };
        }

        return exportDtos;
    }

    private async Task<List<AttendanceExportDto>> BuildHolidayExportDtosAsync(DateOnly targetDate)
    {
        // Get timesheet data to access holiday employees
        var timesheet = await GetAdminTimesheetAsync(targetDate);
        
        // Convert holiday employees to export DTOs
        return timesheet.HolidayEmployees.Select(emp => new AttendanceExportDto
        {
            EmployeeName = emp.EmployeeName,
            Email = "", // Holiday employees not in attendance records, so no email available here
            Branch = "N/A",
            Department = emp.DepartmentName ?? "N/A",
            Role = emp.Role,
            Shift = "N/A",
            ClockIn = null,
            ClockOut = null,
            StartTime = DateTime.MinValue,
            EndTime = DateTime.MinValue,
            IsLate = false,
            MinutesLate = 0,
            Status = emp.HolidayReason ?? "Holiday",
            Date = targetDate
        }).ToList();
    }


    public async Task<Dictionary<int, object>> GetAttendanceStatsAsync(DateOnly? date)
    {
        var stats = new Dictionary<int, object>();

        // An assignment row is permanent history.  Do not use only Employee.ShiftId here:
        // deleting a shift clears that live FK but must not stop absence tracking for
        // employees who were previously assigned.
        var employeesWithShift = await _db.Employees
            .Where(e => e.IsActive && e.Role != Role.Admin &&
                        (e.FirstShiftAssignmentDate.HasValue || e.ShiftId.HasValue ||
                         _db.EmployeeShiftAssignments.Any(esa => esa.EmployeeId == e.Id)))
            .Select(e => e.Id)
            .ToListAsync();

        // #region agent log
        var debugEmployeeRows = await _db.Employees
            .Where(e => e.Email.Contains("usman") || e.Email.Contains("moosarehan") ||
                        e.FullName.Contains("john") || e.Email.Contains("john"))
            .Select(e => new { e.Id, e.Email, e.FullName, e.ShiftId, e.IsActive, e.FirstShiftAssignmentDate })
            .ToListAsync();
        AgentDebugLog("H5", "AttendanceService.GetAttendanceStatsAsync:entry", "Employee shift eligibility", new
        {
            debugEmployees = debugEmployeeRows,
            inEmployeesWithShift = debugEmployeeRows.Select(e => employeesWithShift.Contains(e.Id)).ToList(),
            employeesWithShiftCount = employeesWithShift.Count
        });
        // #endregion

        // Get first assignment date for each employee from EmployeeShiftAssignments (bulk)
        var firstAssignmentDates = await _db.EmployeeShiftAssignments
            .Where(esa => employeesWithShift.Contains(esa.EmployeeId))
            .GroupBy(esa => esa.EmployeeId)
            .Select(g => new { EmployeeId = g.Key, FirstDate = g.Min(esa => esa.EffectiveFrom) })
            .ToDictionaryAsync(x => x.EmployeeId, x => x.FirstDate);

        var today = CompanyTime.Today(_config);
        var now = CompanyTime.Now(_config);

        // The immutable first-assignment date is the source of truth. A clock-in
        // date and employee creation date are not assignment dates.
        var employeeShiftDetails = await _db.Employees
            .Where(e => employeesWithShift.Contains(e.Id))
            .Select(e => new { e.Id, e.ShiftId, e.FirstShiftAssignmentDate })
            .ToDictionaryAsync(x => x.Id);

        var employeeShiftIds = employeeShiftDetails
            .ToDictionary(x => x.Key, x => x.Value.ShiftId);

        // Combine both dictionaries
        var allFirstDates = new Dictionary<int, DateOnly>();
        foreach (var kvp in firstAssignmentDates)
        {
            allFirstDates[kvp.Key] = kvp.Value;
        }
        foreach (var employee in employeeShiftDetails)
        {
            if (employee.Value.FirstShiftAssignmentDate.HasValue)
                allFirstDates[employee.Key] = employee.Value.FirstShiftAssignmentDate.Value;
        }

        // Legacy employees may have Employee.ShiftId set without assignment history
        // or FirstShiftAssignmentDate (pre-tracking data). Derive anchor from attendance.
        var missingFirstDateIds = employeesWithShift
            .Where(id => !allFirstDates.ContainsKey(id))
            .ToList();

        if (missingFirstDateIds.Count > 0)
        {
            var legacyEarliestAttendance = await _db.EmployeeAttendances
                .Where(a => missingFirstDateIds.Contains(a.EmployeeId))
                .GroupBy(a => a.EmployeeId)
                .Select(g => new { EmployeeId = g.Key, FirstDate = g.Min(a => a.Date) })
                .ToListAsync();

            foreach (var row in legacyEarliestAttendance)
                allFirstDates[row.EmployeeId] = row.FirstDate;

            foreach (var id in missingFirstDateIds.Where(id => !allFirstDates.ContainsKey(id)))
            {
                if (employeeShiftIds.TryGetValue(id, out var shiftId) && shiftId.HasValue)
                    allFirstDates[id] = today;
            }

            // #region agent log
            AgentDebugLog("H1", "AttendanceService.GetAttendanceStatsAsync:legacyFallback", "Resolved missing first assignment dates", new
            {
                missingFirstDateIds,
                resolvedFromAttendance = legacyEarliestAttendance.Select(r => new { r.EmployeeId, firstDate = r.FirstDate.ToString("yyyy-MM-dd") }),
                resolvedFromToday = missingFirstDateIds
                    .Where(id => !legacyEarliestAttendance.Any(r => r.EmployeeId == id) && employeeShiftIds.GetValueOrDefault(id).HasValue)
                    .Select(id => new { employeeId = id, firstDate = today.ToString("yyyy-MM-dd") })
            });
            // #endregion
        }

        // Determine the minimum first date for loading attendance records
        var minFirstDate = allFirstDates.Values.Any()
            ? allFirstDates.Values.Min()
            : today;

        var allAttendances = await _db.EmployeeAttendances
            .Where(a => employeesWithShift.Contains(a.EmployeeId) && a.Date >= minFirstDate && a.Date <= today)
            .Select(a => new
            {
                a.EmployeeId,
                a.Date,
                a.ClockIn,
                a.StartTime,
                a.LateThresholdMinutesSnapshot,
                a.Id
            })
            .ToListAsync();

        // Group by employee for fast lookups
        var attendanceByEmployee = allAttendances
            .GroupBy(a => a.EmployeeId)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Bulk-load working days schedules
        var workingDaysAll = await _db.WorkingDays
            .Where(wd => employeesWithShift.Contains(wd.EmployeeId))
            .ToListAsync();

        var workingDaysByEmployee = workingDaysAll
            .GroupBy(wd => wd.EmployeeId)
            .ToDictionary(g => g.Key, g => g.OrderBy(wd => wd.EffectiveFromDate).ToList());

        // Bulk-load shift assignments for efficient effective shift lookup per day
        var allAssignments = await _db.EmployeeShiftAssignments
            .Where(esa => employeesWithShift.Contains(esa.EmployeeId))
            .Select(esa => new { esa.EmployeeId, esa.EffectiveFrom, esa.ShiftId })
            .ToListAsync();

        var assignmentsByEmployee = allAssignments
            .GroupBy(a => a.EmployeeId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(a => a.EffectiveFrom).ToList());

        // Bulk-load approved leaves from min first-assignment date through today
        var approvedLeavesByEmployee = (await _db.LeaveMasters
            .Where(l => employeesWithShift.Contains(l.EmployeeId) &&
                        l.Status == LeaveStatus.Approved &&
                        l.EndDate.Date >= minFirstDate.ToDateTime(TimeOnly.MinValue))
            .Select(l => new { l.EmployeeId, l.StartDate, l.EndDate })
            .ToListAsync())
            .GroupBy(l => l.EmployeeId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var shiftIdsNeeded = allAssignments
            .Where(a => a.ShiftId.HasValue)
            .Select(a => a.ShiftId!.Value)
            .Concat(employeeShiftIds.Values.Where(id => id.HasValue).Select(id => id!.Value))
            .Distinct()
            .ToList();

        var shiftsById = await _db.Shifts
            .Where(s => shiftIdsNeeded.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id);

        foreach (var employeeId in employeesWithShift)
        {
            if (!allFirstDates.TryGetValue(employeeId, out var firstAssignmentDate))
            {
                stats[employeeId] = new { presentCount = 0, lateCount = 0, absentCount = 0 };
                continue;
            }

            var empAttendances = attendanceByEmployee.TryGetValue(employeeId, out var eAttList) ? eAttList : new();
            var attendanceByDate = empAttendances.ToDictionary(a => a.Date);

            var empAssignments = assignmentsByEmployee.TryGetValue(employeeId, out var eAssignList) ? eAssignList : new();
            var empWorkingDays = workingDaysByEmployee.TryGetValue(employeeId, out var wdList) ? wdList : new();

            int presentCount = 0;
            int lateCount = 0;
            int absentCount = 0;

            // Step 1: Classify days with an attendance record (Present/Late)
            foreach (var att in empAttendances)
            {
                if (!att.ClockIn.HasValue) continue; // no clock-in = skip (edge case)

                // Match MapAttendanceToDto / admin timesheet: clock-in after start + threshold snapshot = late
                var lateThreshold = att.StartTime.AddMinutes(att.LateThresholdMinutesSnapshot);
                bool isLate = att.ClockIn.Value > lateThreshold;

                // #region agent log
                var historyLateThreshold = att.StartTime.AddMinutes(att.LateThresholdMinutesSnapshot);
                var historyWouldBeLate = att.ClockIn.Value > historyLateThreshold;
                if (debugEmployeeRows.Any(e => e.Id == employeeId) && historyWouldBeLate != isLate)
                {
                    AgentDebugLog("A", "AttendanceService.GetAttendanceStatsAsync:lateMismatch", "Stats vs history late mismatch", new
                    {
                        employeeId,
                        att.Date,
                        att.LateThresholdMinutesSnapshot,
                        att.StartTime,
                        att.ClockIn,
                        statsIsLate = isLate,
                        historyIsLate = historyWouldBeLate,
                        thresholdGuardBlocked = false
                    });
                }
                // #endregion

                if (isLate) lateCount++;
                else presentCount++;
            }

            // Step 2: Count absent days from first assignment (inclusive) when shift has ended and employee did not clock in
            var countUpTo = today;
            var currentDate = firstAssignmentDate;
            var isDebugEmployee = debugEmployeeRows.Any(e => e.Id == employeeId);
            var absentSkipShiftNotEnded = 0;
            var absentSkipNoAssignment = 0;
            var absentSkipNonWorking = 0;
            var absentSkipHasClockIn = 0;
            var absentCounted = 0;
            var ghostRecordDates = new List<string>();
            var skippedNoShiftDates = new List<object>();

            if (isDebugEmployee)
            {
                ghostRecordDates = empAttendances
                    .Where(a => !a.ClockIn.HasValue)
                    .Select(a => a.Date.ToString("yyyy-MM-dd"))
                    .ToList();
            }

            while (currentDate <= countUpTo)
            {
                var effectiveAssignment = empAssignments.FirstOrDefault(a => a.EffectiveFrom <= currentDate);
                var assignmentShiftId = effectiveAssignment?.ShiftId;
                var fallbackShiftId = employeeShiftIds.GetValueOrDefault(employeeId);
                // Only use the denormalized employee FK for legacy employees that have
                // no assignment history.  It must not replace a deleted historical
                // assignment with a later, unrelated shift.
                var shiftId = effectiveAssignment == null ? fallbackShiftId : assignmentShiftId;
                // A deleted shift leaves the assignment row in place but nulls its FK.
                // The assignment still means this employee is eligible for absence
                // tracking from its effective date.  Its end time is unavailable, so
                // only count completed dates; never mark the current date absent early.
                if (!shiftId.HasValue || !shiftsById.TryGetValue(shiftId.Value, out var shift))
                {
                    if (effectiveAssignment == null || currentDate == today)
                    {
                        if (isDebugEmployee)
                        {
                            absentSkipNoAssignment++;
                            skippedNoShiftDates.Add(new
                            {
                                date = currentDate.ToString("yyyy-MM-dd"),
                                hasAssignmentRow = effectiveAssignment != null,
                                assignmentShiftId,
                                fallbackShiftId,
                                resolvedShiftId = shiftId,
                                shiftInLookup = shiftId.HasValue && shiftsById.ContainsKey(shiftId.Value),
                                assignmentEffectiveFrom = effectiveAssignment?.EffectiveFrom.ToString("yyyy-MM-dd")
                            });
                        }
                        currentDate = currentDate.AddDays(1);
                        continue;
                    }

                    // Continue below using the employee's working-days schedule.  For
                    // a completed date, the missing live shift cannot erase an absence.
                    shift = null!;
                }

                WorkingDays? applicableWd = null;
                foreach (var wd in empWorkingDays.OrderByDescending(w => w.EffectiveFromDate))
                {
                    if (wd.EffectiveFromDate <= currentDate && (wd.EffectiveToDate == null || wd.EffectiveToDate.Value >= currentDate))
                    {
                        applicableWd = wd;
                        break;
                    }
                }

                bool isWorkingDayForDate;
                if (applicableWd == null)
                {
                    isWorkingDayForDate = currentDate.DayOfWeek != DayOfWeek.Saturday && currentDate.DayOfWeek != DayOfWeek.Sunday;
                }
                else
                {
                    isWorkingDayForDate = IsWorkingDay(applicableWd, currentDate.DayOfWeek);
                }

                if (!isWorkingDayForDate)
                {
                    if (isDebugEmployee) absentSkipNonWorking++;
                    currentDate = currentDate.AddDays(1);
                    continue;
                }

                if (shift != null)
                {
                    var (_, shiftEnd) = BuildShiftWindow(shift, currentDate);
                    if (now < shiftEnd)
                    {
                        if (isDebugEmployee) absentSkipShiftNotEnded++;
                        currentDate = currentDate.AddDays(1);
                        continue;
                    }
                }

                attendanceByDate.TryGetValue(currentDate, out var dayAttendance);
                if (dayAttendance?.ClockIn.HasValue == true)
                {
                    if (isDebugEmployee) absentSkipHasClockIn++;
                    currentDate = currentDate.AddDays(1);
                    continue;
                }

                var onLeaveThisDay = approvedLeavesByEmployee.TryGetValue(employeeId, out var empLeaves) &&
                    empLeaves.Any(l => IsOnApprovedLeave(currentDate, new LeaveMaster
                    {
                        StartDate = l.StartDate,
                        EndDate = l.EndDate
                    }));

                // #region agent log
                if (onLeaveThisDay)
                {
                    AgentDebugLog("H2", "AttendanceService.GetAttendanceStatsAsync:leaveDayAbsent", "Absent loop reached leave day - excluding from absent count", new
                    {
                        employeeId,
                        date = currentDate.ToString("yyyy-MM-dd"),
                        onLeaveThisDay,
                        willCountAbsent = false
                    });
                }
                // #endregion

                // Do NOT count as absent if employee is on approved leave
                if (!onLeaveThisDay)
                {
                    absentCount++;
                    if (isDebugEmployee) absentCounted++;
                }
                else if (isDebugEmployee)
                {
                    absentSkipNonWorking++; // Track leave days as a type of skip
                }

                currentDate = currentDate.AddDays(1);
            }

            stats[employeeId] = new { presentCount, lateCount, absentCount };

            // #region agent log
            if (isDebugEmployee)
            {
                AgentDebugLog("H1-H4", "AttendanceService.GetAttendanceStatsAsync:absentLoop", "Absent count loop breakdown", new
                {
                    employeeId,
                    firstAssignmentDate = firstAssignmentDate.ToString("yyyy-MM-dd"),
                    countUpTo = countUpTo.ToString("yyyy-MM-dd"),
                    assignmentCount = empAssignments.Count,
                    assignments = empAssignments.Select(a => new
                    {
                        a.EffectiveFrom,
                        a.ShiftId,
                        shiftExists = a.ShiftId.HasValue && shiftsById.ContainsKey(a.ShiftId.Value)
                    }),
                    fallbackShiftId = employeeShiftIds.GetValueOrDefault(employeeId),
                    workingDaysScheduleCount = empWorkingDays.Count,
                    attendanceTotal = empAttendances.Count,
                    attendanceWithClockIn = empAttendances.Count(a => a.ClockIn.HasValue),
                    attendanceWithoutClockIn = empAttendances.Count(a => !a.ClockIn.HasValue),
                    ghostRecordDates,
                    skippedNoShiftDates,
                    absentSkipShiftNotEnded,
                    absentSkipNoAssignment,
                    absentSkipNonWorking,
                    absentSkipHasClockIn,
                    absentCounted,
                    finalAbsentCount = absentCount,
                    presentCount,
                    lateCount
                });
            }
            // #endregion
        }

        // #region agent log
        foreach (var debugEmp in debugEmployeeRows)
        {
            if (!stats.ContainsKey(debugEmp.Id))
            {
                AgentDebugLog("H5", "AttendanceService.GetAttendanceStatsAsync:missingStats", "Debug employee missing from stats dictionary", new
                {
                    debugEmp.Id,
                    debugEmp.Email,
                    debugEmp.ShiftId,
                    inEmployeesWithShift = employeesWithShift.Contains(debugEmp.Id)
                });
            }
        }
        // #endregion

        return stats;
    }


    public async Task<int> BackfillShiftAssignmentsAsync()
    {
        var today = CompanyTime.Today(_config);
        
        // Find employees who have ShiftId set but no EmployeeShiftAssignments records
        var employeesNeedingBackfill = await _db.Employees
            .Where(e => e.ShiftId != null && 
                        !_db.EmployeeShiftAssignments.Any(esa => esa.EmployeeId == e.Id))
            .Select(e => new { e.Id, e.ShiftId, e.FirstShiftAssignmentDate })
            .ToListAsync();

        int backfilledCount = 0;

        foreach (var emp in employeesNeedingBackfill)
        {
            DateOnly effectiveFrom;
            if (emp.FirstShiftAssignmentDate.HasValue)
            {
                effectiveFrom = emp.FirstShiftAssignmentDate.Value;
            }
            else
            {
                var earliestAttendance = await _db.EmployeeAttendances
                    .Where(a => a.EmployeeId == emp.Id)
                    .OrderBy(a => a.Date)
                    .Select(a => a.Date)
                    .FirstOrDefaultAsync();

                if (earliestAttendance == default)
                    continue;

                effectiveFrom = earliestAttendance;

                var employee = await _db.Employees.FindAsync(emp.Id);
                if (employee != null)
                    employee.FirstShiftAssignmentDate = effectiveFrom;
            }

            // Create the backfill assignment record
            var assignment = new EmployeeShiftAssignment
            {
                EmployeeId = emp.Id,
                ShiftId = emp.ShiftId!.Value,
                EffectiveFrom = effectiveFrom,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = null // System backfill
            };

            _db.EmployeeShiftAssignments.Add(assignment);
            backfilledCount++;
        }

        if (backfilledCount > 0)
        {
            await _db.SaveChangesAsync();
        }

        return backfilledCount;
    }
}
