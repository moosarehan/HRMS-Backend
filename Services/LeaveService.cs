using HRMS_BACKEND.Data;
using HRMS_BACKEND.Dto.Leave;
using HRMS_BACKEND.Entities;
using HRMS_BACKEND.Entities.Enums;
using HRMS_BACKEND.IServices;
using Microsoft.EntityFrameworkCore;

namespace HRMS_BACKEND.Services;

public class LeaveService : ILeaveService
{
    private readonly AppDbContext _db;

    public LeaveService(AppDbContext db) => _db = db;

    // ===== LeavePeriod Management (Admin only) =====
    public async Task<LeavePeriodResponseDto> CreateLeavePeriodAsync(CreateLeavePeriodDto dto)
    {
        // Check if a leave period already exists that overlaps with this date range
        var overlapping = await _db.LeavePeriods
            .Where(lp => (dto.StartDate >= lp.StartDate && dto.StartDate <= lp.EndDate) ||
                         (dto.EndDate >= lp.StartDate && dto.EndDate <= lp.EndDate) ||
                         (dto.StartDate <= lp.StartDate && dto.EndDate >= lp.EndDate))
            .FirstOrDefaultAsync();

        if (overlapping != null)
            throw new InvalidOperationException("A leave period already exists that overlaps with the provided date range.");

        var period = new LeavePeriod
        {
            StartDate = dto.StartDate,
            EndDate = dto.EndDate,
            TotalAllowedDays = dto.TotalAllowedDays
        };

        _db.LeavePeriods.Add(period);
        await _db.SaveChangesAsync();

        return MapLeavePeriodToDto(period);
    }

    public async Task<LeavePeriodResponseDto> GetCurrentLeavePeriodAsync()
    {
        var today = DateTime.UtcNow.Date;
        var currentPeriod = await _db.LeavePeriods
            .FirstOrDefaultAsync(lp => lp.StartDate.Date <= today && today <= lp.EndDate.Date);

        if (currentPeriod == null)
            throw new InvalidOperationException("No leave period exists for the current date.");

        return MapLeavePeriodToDto(currentPeriod);
    }

    // ===== LeaveQuota Management (Admin only) =====
    public async Task<LeaveQuotaResponseDto> CreateOrUpdateLeaveQuotaAsync(CreateLeaveQuotaDto dto)
    {
        // Validate that employee exists
        var employee = await _db.Employees.FirstOrDefaultAsync(e => e.Id == dto.EmployeeId)
            ?? throw new KeyNotFoundException($"Employee with ID {dto.EmployeeId} not found.");

        // Validate that leave type exists
        var leaveType = await _db.LeaveTypes.FirstOrDefaultAsync(lt => lt.Id == dto.LeaveTypeId)
            ?? throw new KeyNotFoundException($"Leave type with ID {dto.LeaveTypeId} not found.");

        // Validate that leave period exists
        var leavePeriod = await _db.LeavePeriods.FirstOrDefaultAsync(lp => lp.Id == dto.LeavePeriodId)
            ?? throw new KeyNotFoundException($"Leave period with ID {dto.LeavePeriodId} not found.");

        // Check if quota already exists
        var existingQuota = await _db.LeaveQuotas
            .FirstOrDefaultAsync(lq => lq.EmployeeId == dto.EmployeeId &&
                                      lq.LeaveTypeId == dto.LeaveTypeId &&
                                      lq.LeavePeriodId == dto.LeavePeriodId);

        if (existingQuota != null)
        {
            // Update existing quota
            existingQuota.AllocatedDays = dto.AllocatedDays;
            existingQuota.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            return MapLeaveQuotaToDto(existingQuota, employee, leaveType);
        }

        // Create new quota
        var quota = new LeaveQuota
        {
            EmployeeId = dto.EmployeeId,
            LeaveTypeId = dto.LeaveTypeId,
            LeavePeriodId = dto.LeavePeriodId,
            AllocatedDays = dto.AllocatedDays,
            UsedDays = 0
        };

        _db.LeaveQuotas.Add(quota);
        await _db.SaveChangesAsync();

        return MapLeaveQuotaToDto(quota, employee, leaveType);
    }

    public async Task<LeaveQuotaResponseDto> UpdateLeaveQuotaAllocatedDaysAsync(int quotaId, UpdateLeaveQuotaDto dto)
    {
        var quota = await _db.LeaveQuotas
            .Include(lq => lq.Employee)
            .Include(lq => lq.LeaveType)
            .FirstOrDefaultAsync(lq => lq.Id == quotaId)
            ?? throw new KeyNotFoundException($"Leave quota with ID {quotaId} not found.");

        quota.AllocatedDays = dto.AllocatedDays;
        quota.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return MapLeaveQuotaToDto(quota, quota.Employee!, quota.LeaveType!);
    }

    public async Task<List<LeaveQuotaResponseDto>> GetEmployeeQuotasForCurrentPeriodAsync(int employeeId)
    {
        var today = DateTime.UtcNow.Date;
        var currentPeriod = await _db.LeavePeriods
            .FirstOrDefaultAsync(lp => lp.StartDate.Date <= today && today <= lp.EndDate.Date);

        if (currentPeriod == null)
            return new List<LeaveQuotaResponseDto>();

        var quotas = await _db.LeaveQuotas
            .Include(lq => lq.Employee)
            .Include(lq => lq.LeaveType)
            .Where(lq => lq.EmployeeId == employeeId && lq.LeavePeriodId == currentPeriod.Id)
            .ToListAsync();

        return quotas.Select(q => MapLeaveQuotaToDto(q, q.Employee!, q.LeaveType!)).ToList();
    }

    public async Task<List<LeaveQuotaResponseDto>> GetAllQuotasForPeriodAsync(int leavePeriodId)
    {
        var quotas = await _db.LeaveQuotas
            .Include(lq => lq.Employee)
            .Include(lq => lq.LeaveType)
            .Where(lq => lq.LeavePeriodId == leavePeriodId)
            .ToListAsync();

        return quotas.Select(q => MapLeaveQuotaToDto(q, q.Employee!, q.LeaveType!)).ToList();
    }

    // ===== LeaveType (seeded, read-only) =====
    public async Task<List<LeaveTypeDto>> GetAllLeaveTypesAsync()
    {
        var types = await _db.LeaveTypes.ToListAsync();
        return types.Select(t => new LeaveTypeDto
        {
            Id = t.Id,
            Name = t.Name,
            Description = t.Description
        }).ToList();
    }

    // ===== Leave Setup Validation (for frontend) =====
    public async Task<LeaveSetupStatusDto> GetLeaveSetupStatusAsync(int employeeId)
    {
        var today = DateTime.UtcNow.Date;
        Console.WriteLine($"[GetLeaveSetupStatus] Checking for employeeId: {employeeId}, today: {today}");

        // Check if current leave period exists
        var allPeriods = await _db.LeavePeriods.ToListAsync();
        Console.WriteLine($"[GetLeaveSetupStatus] Total periods in DB: {allPeriods.Count}");
        foreach (var p in allPeriods)
        {
            Console.WriteLine($"  Period ID {p.Id}: {p.StartDate.Date} to {p.EndDate.Date}");
        }

        var currentPeriod = await _db.LeavePeriods
            .FirstOrDefaultAsync(lp => lp.StartDate.Date <= today && today <= lp.EndDate.Date);

        Console.WriteLine($"[GetLeaveSetupStatus] Current period found: {(currentPeriod != null ? $"ID {currentPeriod.Id}" : "NULL")}");

        if (currentPeriod == null)
        {
            return new LeaveSetupStatusDto
            {
                IsSetupComplete = false,
                HasPeriod = false,
                HasQuota = false,
                Message = "Contact Admin — leave period hasn't been registered."
            };
        }

        // Check if employee has quota for current period
        var employeeQuotas = await _db.LeaveQuotas
            .Include(lq => lq.Employee)
            .Include(lq => lq.LeaveType)
            .Where(lq => lq.EmployeeId == employeeId && lq.LeavePeriodId == currentPeriod.Id)
            .ToListAsync();

        Console.WriteLine($"[GetLeaveSetupStatus] Employee {employeeId} quotas for period {currentPeriod.Id}: {employeeQuotas.Count}");
        foreach (var q in employeeQuotas)
        {
            Console.WriteLine($"  Quota ID {q.Id}: {q.LeaveType?.Name} - {q.AllocatedDays} days");
        }

        if (employeeQuotas.Count == 0)
        {
            return new LeaveSetupStatusDto
            {
                IsSetupComplete = false,
                HasPeriod = true,
                HasQuota = false,
                Message = "Contact Admin — your leave quota hasn't been registered."
            };
        }

        // Setup is complete
        var quotaDtos = employeeQuotas.Select(q => MapLeaveQuotaToDto(q, q.Employee!, q.LeaveType!)).ToList();

        return new LeaveSetupStatusDto
        {
            IsSetupComplete = true,
            HasPeriod = true,
            HasQuota = true,
            Message = string.Empty,
            CurrentLeavePeriod = MapLeavePeriodToDto(currentPeriod),
            EmployeeQuotas = quotaDtos
        };
    }

    // ===== Leave Application (Employee, Manager, HR can apply) =====
    public async Task<LeaveRequestResponseDto> ApplyForLeaveAsync(int employeeId, CreateLeaveRequestDto dto)
    {
        // Validate employee exists
        var employee = await _db.Employees.FirstOrDefaultAsync(e => e.Id == employeeId)
            ?? throw new KeyNotFoundException($"Employee with ID {employeeId} not found.");

        // Validate leave type exists
        var leaveType = await _db.LeaveTypes.FirstOrDefaultAsync(lt => lt.Id == dto.LeaveTypeId)
            ?? throw new KeyNotFoundException($"Leave type with ID {dto.LeaveTypeId} not found.");

        var today = DateTime.UtcNow.Date;

        // Find current leave period
        var currentPeriod = await _db.LeavePeriods
            .FirstOrDefaultAsync(lp => lp.StartDate.Date <= today && today <= lp.EndDate.Date)
            ?? throw new InvalidOperationException("No leave period exists for the current date.");

        // Get employee's quota for the requested leave type in current period
        var employeeQuota = await _db.LeaveQuotas
            .FirstOrDefaultAsync(lq => lq.EmployeeId == employeeId &&
                                      lq.LeaveTypeId == dto.LeaveTypeId &&
                                      lq.LeavePeriodId == currentPeriod.Id)
            ?? throw new InvalidOperationException($"No quota found for this leave type in the current period.");

        // === VALIDATION 1: Per-type check ===
        int remainingForType = employeeQuota.AllocatedDays - employeeQuota.UsedDays;
        if (dto.NoOfDays > remainingForType)
        {
            string message = remainingForType == 0
                ? $"No more {leaveType.Name}s left"
                : $"Only {remainingForType} {leaveType.Name}(s) remaining";
            throw new InvalidOperationException(message);
        }

        // === VALIDATION 2: Total check across all types ===
        var allEmployeeQuotas = await _db.LeaveQuotas
            .Where(lq => lq.EmployeeId == employeeId && lq.LeavePeriodId == currentPeriod.Id)
            .ToListAsync();

        int totalUsedDays = allEmployeeQuotas.Sum(q => q.UsedDays);
        int totalRemainingDays = currentPeriod.TotalAllowedDays - totalUsedDays;

        if (dto.NoOfDays > totalRemainingDays)
        {
            string message = totalRemainingDays == 0
                ? "You have used all your leaves for the current year"
                : $"Only {totalRemainingDays} total days remaining for this year";
            throw new InvalidOperationException(message);
        }

        // Create leave request
        var leaveRequest = new LeaveMaster
        {
            EmployeeId = employeeId,
            StartDate = dto.StartDate,
            EndDate = dto.EndDate,
            LeaveTypeId = dto.LeaveTypeId,
            LeavePeriodId = currentPeriod.Id,
            NoOfDays = dto.NoOfDays,
            Status = LeaveStatus.Pending,
            Description = dto.Description
        };

        _db.LeaveMasters.Add(leaveRequest);
        await _db.SaveChangesAsync();

        return MapLeaveMasterToDto(leaveRequest, employee, leaveType);
    }

    public async Task<List<LeaveRequestResponseDto>> GetEmployeePendingLeavesAsync(int employeeId)
    {
        var leaves = await _db.LeaveMasters
            .Include(lm => lm.Employee)
            .Include(lm => lm.LeaveType)
            .Where(lm => lm.EmployeeId == employeeId && lm.Status == LeaveStatus.Pending)
            .OrderByDescending(lm => lm.CreatedAt)
            .ToListAsync();

        return leaves.Select(l => MapLeaveMasterToDto(l, l.Employee!, l.LeaveType!)).ToList();
    }

    public async Task<List<LeaveRequestResponseDto>> GetAllPendingLeavesAsync()
    {
        var leaves = await _db.LeaveMasters
            .Include(lm => lm.Employee)
            .Include(lm => lm.LeaveType)
            .Where(lm => lm.Status == LeaveStatus.Pending)
            .OrderByDescending(lm => lm.CreatedAt)
            .ToListAsync();

        return leaves.Select(l => MapLeaveMasterToDto(l, l.Employee!, l.LeaveType!)).ToList();
    }

    // ===== Leave Approval/Rejection (Admin only) =====
    public async Task<LeaveRequestResponseDto> ApproveLeaveRequestAsync(int leaveRequestId)
    {
        var leaveRequest = await _db.LeaveMasters
            .Include(lm => lm.Employee)
            .Include(lm => lm.LeaveType)
            .FirstOrDefaultAsync(lm => lm.Id == leaveRequestId)
            ?? throw new KeyNotFoundException($"Leave request with ID {leaveRequestId} not found.");

        if (leaveRequest.Status != LeaveStatus.Pending)
            throw new InvalidOperationException($"Cannot approve a leave request with status: {leaveRequest.Status}");

        // Find the corresponding quota and increment UsedDays
        var quota = await _db.LeaveQuotas
            .FirstOrDefaultAsync(lq => lq.EmployeeId == leaveRequest.EmployeeId &&
                                      lq.LeaveTypeId == leaveRequest.LeaveTypeId &&
                                      lq.LeavePeriodId == leaveRequest.LeavePeriodId)
            ?? throw new InvalidOperationException("Could not find corresponding leave quota.");

        // Update the quota's UsedDays
        quota.UsedDays += leaveRequest.NoOfDays;
        quota.UpdatedAt = DateTime.UtcNow;

        // Update leave request status
        leaveRequest.Status = LeaveStatus.Approved;
        leaveRequest.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return MapLeaveMasterToDto(leaveRequest, leaveRequest.Employee!, leaveRequest.LeaveType!);
    }

    public async Task<LeaveRequestResponseDto> RejectLeaveRequestAsync(int leaveRequestId, RejectLeaveRequestDto dto)
    {
        var leaveRequest = await _db.LeaveMasters
            .Include(lm => lm.Employee)
            .Include(lm => lm.LeaveType)
            .FirstOrDefaultAsync(lm => lm.Id == leaveRequestId)
            ?? throw new KeyNotFoundException($"Leave request with ID {leaveRequestId} not found.");

        if (leaveRequest.Status != LeaveStatus.Pending)
            throw new InvalidOperationException($"Cannot reject a leave request with status: {leaveRequest.Status}");

        // Update leave request status with rejection reason
        leaveRequest.Status = LeaveStatus.Rejected;
        leaveRequest.RejectionReason = dto.RejectionReason;
        leaveRequest.UpdatedAt = DateTime.UtcNow;

        // Note: UsedDays is NOT incremented for rejected requests

        await _db.SaveChangesAsync();

        return MapLeaveMasterToDto(leaveRequest, leaveRequest.Employee!, leaveRequest.LeaveType!);
    }

    // ===== Leave History/Reporting =====
    public async Task<List<LeaveRequestResponseDto>> GetEmployeeLeaveHistoryAsync(int employeeId)
    {
        var leaves = await _db.LeaveMasters
            .Include(lm => lm.Employee)
            .Include(lm => lm.LeaveType)
            .Where(lm => lm.EmployeeId == employeeId)
            .OrderByDescending(lm => lm.CreatedAt)
            .ToListAsync();

        return leaves.Select(l => MapLeaveMasterToDto(l, l.Employee!, l.LeaveType!)).ToList();
    }

    public async Task<List<LeaveRequestResponseDto>> GetAllLeavesAsync()
    {
        var leaves = await _db.LeaveMasters
            .Include(lm => lm.Employee)
            .Include(lm => lm.LeaveType)
            .OrderByDescending(lm => lm.CreatedAt)
            .ToListAsync();

        return leaves.Select(l => MapLeaveMasterToDto(l, l.Employee!, l.LeaveType!)).ToList();
    }

    // ===== Helper Mapping Methods =====
    private LeavePeriodResponseDto MapLeavePeriodToDto(LeavePeriod period)
    {
        return new LeavePeriodResponseDto
        {
            Id = period.Id,
            Name = period.Name,
            StartDate = period.StartDate,
            EndDate = period.EndDate,
            TotalAllowedDays = period.TotalAllowedDays,
            CreatedAt = period.CreatedAt
        };
    }

    private LeaveQuotaResponseDto MapLeaveQuotaToDto(LeaveQuota quota, Employee employee, LeaveType leaveType)
    {
        return new LeaveQuotaResponseDto
        {
            Id = quota.Id,
            EmployeeId = quota.EmployeeId,
            EmployeeName = employee.FullName,
            LeaveTypeId = quota.LeaveTypeId,
            LeaveTypeName = leaveType.Name,
            LeavePeriodId = quota.LeavePeriodId,
            AllocatedDays = quota.AllocatedDays,
            UsedDays = quota.UsedDays,
            CreatedAt = quota.CreatedAt,
            UpdatedAt = quota.UpdatedAt
        };
    }

    private LeaveRequestResponseDto MapLeaveMasterToDto(LeaveMaster leave, Employee employee, LeaveType leaveType)
    {
        return new LeaveRequestResponseDto
        {
            Id = leave.Id,
            EmployeeId = leave.EmployeeId,
            EmployeeName = employee.FullName,
            StartDate = leave.StartDate,
            EndDate = leave.EndDate,
            LeaveTypeId = leave.LeaveTypeId,
            LeaveTypeName = leaveType.Name,
            NoOfDays = leave.NoOfDays,
            Status = leave.Status.ToString(),
            Description = leave.Description,
            RejectionReason = leave.RejectionReason,
            CreatedAt = leave.CreatedAt,
            UpdatedAt = leave.UpdatedAt
        };
    }
}
