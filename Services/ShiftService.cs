using HRMS_BACKEND.Data;
using HRMS_BACKEND.Dto.Shift;
using HRMS_BACKEND.Entities;
using HRMS_BACKEND.Helpers;
using HRMS_BACKEND.IServices;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace HRMS_BACKEND.Services;

public class ShiftService : IShiftService
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;

    public static bool IsFixedShift(Shift shift)
    {
        if (shift.Id >= 1 && shift.Id <= 3) return true;
        var name = shift.Name?.ToLower() ?? "";
        return name.Contains("morning") || name.Contains("night") || name.Contains("remote");
    }

    public ShiftService(AppDbContext db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

    public async Task<List<ShiftResponseDto>> GetAllAsync()
    {
        var shifts = await _db.Shifts.OrderBy(s => s.Id).ToListAsync();
        return shifts.Select(MapToDto).ToList();
    }

    public async Task<ShiftResponseDto> CreateAsync(CreateShiftDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
            throw new InvalidOperationException("Shift name is required.");

        if (dto.StartTime == dto.EndTime)
            throw new InvalidOperationException("Start time and end time cannot be equal.");

        if (dto.Limit <= 0)
            throw new InvalidOperationException("Shift limit must be greater than 0.");

        if (dto.LateThresholdMinutes < 0)
            throw new InvalidOperationException("Late allowed time cannot be negative.");

        // Check if shift with same name already exists
        var existingShift = await _db.Shifts.FirstOrDefaultAsync(s => s.Name == dto.Name);
        if (existingShift != null)
            throw new InvalidOperationException($"A shift named '{dto.Name}' already exists.");

        var shift = new Shift
        {
            Name = dto.Name,
            StartTime = dto.StartTime,
            EndTime = dto.EndTime,
            Limit = dto.Limit,
            LateThresholdMinutes = dto.LateThresholdMinutes
        };

        _db.Shifts.Add(shift);
        await _db.SaveChangesAsync();

        return MapToDto(shift);
    }

    public async Task<ShiftResponseDto> UpdateAsync(int id, UpdateShiftDto dto)
    {
        var shift = await _db.Shifts.FirstOrDefaultAsync(s => s.Id == id)
            ?? throw new KeyNotFoundException("Shift not found.");

        if (dto.StartTime == dto.EndTime)
            throw new InvalidOperationException("Start time and end time cannot be equal.");

        if (dto.Limit < 0)
            throw new InvalidOperationException("Shift limit cannot be negative.");

        if (dto.LateThresholdMinutes < 0)
            throw new InvalidOperationException("Late allowed time cannot be negative.");

        var assignedEmployeeIds = await _db.Employees
            .Where(e => e.ShiftId == id || e.PendingShiftId == id)
            .Select(e => e.Id)
            .ToListAsync();

        // Check if any employee assigned to this shift is currently clocked in
        var isAnyClockedIn = await _db.EmployeeAttendances
            .AnyAsync(a => (a.ShiftId == id || assignedEmployeeIds.Contains(a.EmployeeId)) &&
                           a.ClockIn != null &&
                           a.ClockOut == null);

        if (isAnyClockedIn)
            throw new InvalidOperationException("Cannot edit shift times while an employee assigned to it is currently clocked in.");

        shift.StartTime = dto.StartTime;
        shift.EndTime = dto.EndTime;
        shift.Limit = dto.Limit;
        shift.LateThresholdMinutes = dto.LateThresholdMinutes;

        await _db.SaveChangesAsync();
        return MapToDto(shift);
    }

    public async Task DeleteAsync(int id)
    {
        var shift = await _db.Shifts.FirstOrDefaultAsync(s => s.Id == id)
            ?? throw new KeyNotFoundException("Shift not found.");

        if (IsFixedShift(shift))
            throw new InvalidOperationException("Fixed shifts (Morning, Night, Remote) cannot be deleted.");

        var assignedEmployeeIdsForDelete = await _db.Employees
            .Where(e => e.ShiftId == id || e.PendingShiftId == id)
            .Select(e => e.Id)
            .ToListAsync();

        var openAttendances = await _db.EmployeeAttendances
            .Include(a => a.Employee)
            .Where(a => (a.ShiftId == id || assignedEmployeeIdsForDelete.Contains(a.EmployeeId)) &&
                        a.ClockIn != null &&
                        a.ClockOut == null)
            .ToListAsync();

        var isAnyAssignedEmployeeClockedIn = openAttendances.Count > 0;

        // #region agent log
        try
        {
            var logPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "debug-f60ed9.log"));
            var payload = JsonSerializer.Serialize(new
            {
                sessionId = "f60ed9",
                hypothesisId = "H4",
                location = "ShiftService.DeleteAsync:precheck",
                message = "Shift delete blocked check",
                data = new
                {
                    shiftId = id,
                    shiftName = shift.Name,
                    isAnyAssignedEmployeeClockedIn,
                    openAttendanceCount = openAttendances.Count,
                    openAttendances = openAttendances.Select(a => new
                    {
                        a.EmployeeId,
                        employeeName = a.Employee.FullName,
                        a.Date,
                        a.EndTime,
                        a.ClockIn,
                        shiftEnded = CompanyTime.Now(_config) >= a.EndTime
                    })
                },
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                runId = "pre-fix"
            });
            File.AppendAllText(logPath, payload + Environment.NewLine);
        }
        catch { }
        // #endregion

        if (isAnyAssignedEmployeeClockedIn)
            throw new InvalidOperationException("Cannot delete this shift while an employee assigned to it is currently clocked in.");

        var employeesToNullify = await _db.Employees
            .Where(e => e.ShiftId == id || e.PendingShiftId == id)
            .ToListAsync();

        foreach (var emp in employeesToNullify)
        {
            if (emp.ShiftId == id)
            {
                emp.ShiftId = null;
            }
            if (emp.PendingShiftId == id)
            {
                emp.PendingShiftId = null;
                emp.PendingShiftEffectiveFromDate = null;
            }
        }

        // Null out ShiftId on all historical attendance rows referencing this shift
        // to avoid FK_EmployeeAttendances_Shifts_ShiftId constraint violation.
        var attendanceRows = await _db.EmployeeAttendances
            .Where(a => a.ShiftId == id)
            .ToListAsync();

        foreach (var row in attendanceRows)
        {
            row.ShiftId = null;
        }

        _db.Shifts.Remove(shift);
        await _db.SaveChangesAsync();
    }

    private static ShiftResponseDto MapToDto(Shift shift)
    {
        return new ShiftResponseDto
        {
            Id = shift.Id,
            Name = shift.Name,
            StartTime = shift.StartTime,
            EndTime = shift.EndTime,
            Limit = shift.Limit,
            LateThresholdMinutes = shift.LateThresholdMinutes
        };
    }
}
