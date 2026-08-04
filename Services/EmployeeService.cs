using HRMS_BACKEND.Data;
using HRMS_BACKEND.Dto.Employee;
using HRMS_BACKEND.Dto.Attendance;
using HRMS_BACKEND.Entities;
using HRMS_BACKEND.Entities.Enums;
using HRMS_BACKEND.Helpers;
using HRMS_BACKEND.IServices;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HRMS_BACKEND.Services;

public class EmployeeService : IEmployeeService
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;
    private readonly PasswordHasher<Employee> _passwordHasher = new();
    private readonly IAttendanceService _attendanceService;

    public EmployeeService(AppDbContext db, IConfiguration config, IAttendanceService attendanceService)
    {
        _db = db;
        _config = config;
        _attendanceService = attendanceService;
    }

    private static (int presentCount, int lateCount, int absentCount) ExtractStats(object? statsObj)
    {
        if (statsObj == null) return (0, 0, 0);

        var type = statsObj.GetType();
        var presentCount = (int?)type.GetProperty("presentCount")?.GetValue(statsObj) ?? 0;
        var lateCount = (int?)type.GetProperty("lateCount")?.GetValue(statsObj) ?? 0;
        var absentCount = (int?)type.GetProperty("absentCount")?.GetValue(statsObj) ?? 0;

        return (presentCount, lateCount, absentCount);
    }

    public async Task<List<EmployeeResponseDto>> GetAllAsync(Role currentRole, int currentId, int? currentDeptId)
    {
        if ((currentRole == Role.HR || currentRole == Role.Manager) && currentDeptId == null)
        {
            return new List<EmployeeResponseDto>();
        }

        await ApplyDueShiftChangesAsync();

        var query = _db.Employees
            .Include(e => e.Department)
            .Include(e => e.Branch)
            .Include(e => e.Manager)
            .Include(e => e.Shift)
            .Include(e => e.PendingShift)
            .AsQueryable();

        query = currentRole switch
        {
            Role.Admin => query,
            Role.HR => query.Where(e =>
                (e.Role == Role.Employee || e.Role == Role.Manager) &&
                e.DepartmentId == currentDeptId),
            Role.Manager => query.Where(e => e.DepartmentId == currentDeptId),
            Role.Employee => query.Where(e => e.Id == currentId),
            _ => query.Where(e => e.Id == -1)
        };

        var employees = await query.ToListAsync();
        
        // Get attendance stats for all employees
        var allStats = await _attendanceService.GetAttendanceStatsAsync(null);

        return employees.Select(e =>
        {
            var stat = allStats.TryGetValue(e.Id, out var s) ? s : null;
            var (presentCount, lateCount, absentCount) = ExtractStats(stat);

            return MapToDto(e, lateCount, absentCount);
        }).ToList();
    }

    public async Task<EmployeeResponseDto?> GetByIdAsync(int id, Role currentRole, int currentId, int? currentDeptId)
    {
        await ApplyDueShiftChangesAsync(id);

        var employee = await _db.Employees
            .Include(e => e.Department)
            .Include(e => e.Branch)
            .Include(e => e.Manager)
            .Include(e => e.Shift)
            .Include(e => e.PendingShift)
            .FirstOrDefaultAsync(e => e.Id == id);

        if (employee is null) return null;

        bool authorized = currentRole switch
        {
            Role.Admin => true,
            Role.HR => employee.Role is Role.Employee or Role.Manager || employee.Id == currentId,
            Role.Manager => employee.DepartmentId == currentDeptId,
            Role.Employee => employee.Id == currentId,
            _ => false
        };

        if (!authorized) throw new UnauthorizedAccessException("You cannot view this profile.");

        // Get attendance stats
        var allStats = await _attendanceService.GetAttendanceStatsAsync(null);
        var stat = allStats.TryGetValue(id, out var s) ? s : null;
        var (presentCount, lateCount, absentCount) = ExtractStats(stat);

        return MapToDto(employee, lateCount, absentCount);
    }

    public async Task<EmployeeResponseDto> CreateAsync(CreateEmployeeDto dto, Role currentRole)
    {
        if (currentRole == Role.HR && dto.Role is Role.Admin or Role.HR)
            throw new UnauthorizedAccessException("HR cannot create Admin or HR accounts.");

        if (await _db.Employees.AnyAsync(e => e.Email == dto.Email))
            throw new InvalidOperationException("Email already exists.");

        int? derivedBranchId = null;
        if (dto.DepartmentId.HasValue)
        {
            var dept = await _db.Departments.FindAsync(dto.DepartmentId.Value);
            if (dept != null)
            {
                derivedBranchId = dept.BranchId;
            }
        }

        var employee = new Employee
        {
            FullName = dto.FullName,
            Email = dto.Email,
            Role = dto.Role,
            Phone = dto.Phone,
            Address = dto.Address,
            Designation = dto.Designation,
            Salary = dto.Salary,
            DepartmentId = dto.DepartmentId,
            BranchId = derivedBranchId,
            ManagerId = dto.ManagerId,
            IsActive = true
        };

        employee.PasswordHash = _passwordHasher.HashPassword(employee, dto.Password);

        _db.Employees.Add(employee);
        await _db.SaveChangesAsync();

        var createdEmp = await LoadEmployeeForResponseAsync(employee.Id);
        return MapToDto(createdEmp, 0, 0); // New employees have no stats yet
    }

    public async Task<EmployeeResponseDto> UpdateAsync(int id, UpdateEmployeeDto dto, Role currentRole)
    {
        var employee = await _db.Employees.FindAsync(id)
            ?? throw new KeyNotFoundException("Employee not found.");

        if (currentRole == Role.HR && employee.Role is Role.Admin or Role.HR)
            throw new UnauthorizedAccessException("HR cannot modify Admin or HR accounts.");

        int? derivedBranchId = null;
        if (dto.DepartmentId.HasValue)
        {
            var dept = await _db.Departments.FindAsync(dto.DepartmentId.Value);
            if (dept != null)
            {
                derivedBranchId = dept.BranchId;
            }
        }

        employee.FullName = dto.FullName;
        employee.Phone = dto.Phone;
        employee.Address = dto.Address;
        employee.Designation = dto.Designation;
        employee.Salary = dto.Salary;
        employee.DepartmentId = dto.DepartmentId;
        employee.BranchId = derivedBranchId;
        employee.ManagerId = dto.ManagerId;
        employee.IsActive = dto.IsActive;

        await _db.SaveChangesAsync();

        var updatedEmp = await LoadEmployeeForResponseAsync(employee.Id);

        // Get attendance stats
        var allStats = await _attendanceService.GetAttendanceStatsAsync(null);
        var stat = allStats.TryGetValue(id, out var s) ? s : null;
        var (presentCount, lateCount, absentCount) = ExtractStats(stat);

        return MapToDto(updatedEmp, lateCount, absentCount);
    }

    public async Task<EmployeeResponseDto> AssignShiftAsync(int id, AssignEmployeeShiftDto dto)
    {
        var employee = await _db.Employees
            .Include(e => e.Shift)
            .Include(e => e.PendingShift)
            .FirstOrDefaultAsync(e => e.Id == id)
            ?? throw new KeyNotFoundException("Employee not found.");

        if (employee.Role == Role.Admin)
            throw new InvalidOperationException("Admin users are not assigned to attendance shifts.");

        await ApplyDueShiftChangeAsync(employee);

        var today = CompanyTime.Today(_config);

        var isClockedIn = await _db.EmployeeAttendances
            .AnyAsync(a => a.EmployeeId == id && a.ClockIn.HasValue && !a.ClockOut.HasValue);

        // Check if employee has an attendance record today (either clocked in or completed shift today)
        var hasAttendanceToday = await _db.EmployeeAttendances
            .AnyAsync(a => a.EmployeeId == id && a.Date == today);

        if (dto.ShiftId.HasValue)
        {
            var shiftExists = await _db.Shifts.AnyAsync(s => s.Id == dto.ShiftId.Value);
            if (!shiftExists)
                throw new KeyNotFoundException("Shift not found.");
        }

        if (employee.ShiftId == dto.ShiftId && !employee.PendingShiftEffectiveFromDate.HasValue)
        {
            var unchanged = await LoadEmployeeForResponseAsync(employee.Id);

            // Get attendance stats
            var allStats = await _attendanceService.GetAttendanceStatsAsync(null);
            var stat = allStats.TryGetValue(id, out var s) ? s : null;
            var (presentCount, lateCount, absentCount) = ExtractStats(stat);

            return MapToDto(unchanged, lateCount, absentCount);
        }

        // If employee has clocked in or worked today, today's shift is locked. Schedule change for tomorrow.
        if (hasAttendanceToday || isClockedIn)
        {
            employee.PendingShiftId = dto.ShiftId;
            employee.PendingShiftEffectiveFromDate = today.AddDays(1);
            
            // Also record in EmployeeShiftAssignments for future effective date
            if (dto.ShiftId.HasValue)
            {
                var tomorrowAssignment = await _db.EmployeeShiftAssignments
                    .FirstOrDefaultAsync(esa => esa.EmployeeId == id && esa.EffectiveFrom == today.AddDays(1));
                
                if (tomorrowAssignment == null)
                {
                    _db.EmployeeShiftAssignments.Add(new EmployeeShiftAssignment
                    {
                        EmployeeId = id,
                        ShiftId = dto.ShiftId.Value,
                        EffectiveFrom = today.AddDays(1),
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = null
                    });
                }

                if (!employee.FirstShiftAssignmentDate.HasValue || today.AddDays(1) < employee.FirstShiftAssignmentDate.Value)
                    employee.FirstShiftAssignmentDate = today.AddDays(1);
            }
        }
        else
        {
            employee.ShiftId = dto.ShiftId;
            employee.PendingShiftId = null;
            employee.PendingShiftEffectiveFromDate = null;
            
            // Record in EmployeeShiftAssignments for today's effective date
            if (dto.ShiftId.HasValue)
            {
                var existingAssignment = await _db.EmployeeShiftAssignments
                    .FirstOrDefaultAsync(esa => esa.EmployeeId == id && esa.EffectiveFrom == today);
                
                if (existingAssignment == null)
                {
                    _db.EmployeeShiftAssignments.Add(new EmployeeShiftAssignment
                    {
                        EmployeeId = id,
                        ShiftId = dto.ShiftId.Value,
                        EffectiveFrom = today,
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = null
                    });
                }

                if (!employee.FirstShiftAssignmentDate.HasValue || today < employee.FirstShiftAssignmentDate.Value)
                    employee.FirstShiftAssignmentDate = today;
            }
        }

        await _db.SaveChangesAsync();

        var updated = await LoadEmployeeForResponseAsync(employee.Id);

        // Get attendance stats
        var updatedStats = await _attendanceService.GetAttendanceStatsAsync(null);
        var updatedStat = updatedStats.TryGetValue(id, out var us) ? us : null;
        var (updatedPresentCount, updatedLateCount, updatedAbsentCount) = ExtractStats(updatedStat);

        return MapToDto(updated, updatedLateCount, updatedAbsentCount);
    }

    public async Task<EmployeeResponseDto> UpdateOwnProfileAsync(int currentId, UpdateOwnProfileDto dto)
    {
        await ApplyDueShiftChangesAsync(currentId);

        var employee = await _db.Employees
            .Include(e => e.Department)
            .Include(e => e.Branch)
            .Include(e => e.Manager)
            .Include(e => e.Shift)
            .Include(e => e.PendingShift)
            .FirstOrDefaultAsync(e => e.Id == currentId)
            ?? throw new KeyNotFoundException("Employee not found.");

        employee.Phone = dto.Phone ?? employee.Phone;
        employee.Address = dto.Address ?? employee.Address;

        await _db.SaveChangesAsync();

        // Get attendance stats
        var allStats = await _attendanceService.GetAttendanceStatsAsync(null);
        var stat = allStats.TryGetValue(currentId, out var s) ? s : null;
        var (presentCount, lateCount, absentCount) = ExtractStats(stat);

        return MapToDto(employee, lateCount, absentCount);
    }

    public async Task DeleteAsync(int id, Role currentRole)
    {
        var employee = await _db.Employees.FindAsync(id)
            ?? throw new KeyNotFoundException("Employee not found.");

        if (currentRole == Role.HR && employee.Role is Role.Admin or Role.HR)
            throw new UnauthorizedAccessException("HR cannot delete Admin or HR accounts.");

        _db.Employees.Remove(employee);
        await _db.SaveChangesAsync();
    }

    private async Task<Employee> LoadEmployeeForResponseAsync(int id)
    {
        return await _db.Employees
            .Include(e => e.Department)
            .Include(e => e.Branch)
            .Include(e => e.Manager)
            .Include(e => e.Shift)
            .Include(e => e.PendingShift)
            .FirstAsync(e => e.Id == id);
    }

    private async Task ApplyDueShiftChangesAsync(int? employeeId = null)
    {
        var today = CompanyTime.Today(_config);

        var query = _db.Employees.Where(e =>
            e.PendingShiftEffectiveFromDate.HasValue &&
            e.PendingShiftEffectiveFromDate.Value <= today);

        if (employeeId.HasValue)
        {
            query = query.Where(e => e.Id == employeeId.Value);
        }

        var employees = await query.ToListAsync();
        if (employees.Count == 0)
        {
            return;
        }

        foreach (var employee in employees)
        {
            employee.ShiftId = employee.PendingShiftId;
            employee.PendingShiftId = null;
            employee.PendingShiftEffectiveFromDate = null;
        }

        await _db.SaveChangesAsync();
    }

    private async Task ApplyDueShiftChangeAsync(Employee employee)
    {
        var today = CompanyTime.Today(_config);
        if (!employee.PendingShiftEffectiveFromDate.HasValue || employee.PendingShiftEffectiveFromDate.Value > today)
        {
            return;
        }

        employee.ShiftId = employee.PendingShiftId;
        employee.PendingShiftId = null;
        employee.PendingShiftEffectiveFromDate = null;
        await _db.SaveChangesAsync();
        
        // Reload the employee to get the new shift
        await _db.Entry(employee).ReloadAsync();
        await _db.Entry(employee).Reference(e => e.Shift).LoadAsync();
    }

    private EmployeeResponseDto MapToDto(Employee e, int lateCount = 0, int absentCount = 0)
    {
        return new EmployeeResponseDto
        {
            Id = e.Id,
            FullName = e.FullName,
            Email = e.Email,
            Role = e.Role.ToString(),
            Phone = e.Phone,
            Address = e.Address,
            Designation = e.Designation,
            Salary = e.Salary,
            DepartmentId = e.DepartmentId,
            DepartmentName = e.Department?.Name,
            BranchId = e.BranchId,
            BranchName = e.Branch?.Name,
            ShiftId = e.ShiftId,
            ShiftName = e.Shift?.Name,
            PendingShiftId = e.PendingShiftId,
            PendingShiftName = e.PendingShift?.Name,
            PendingShiftEffectiveFromDate = e.PendingShiftEffectiveFromDate,
            ManagerId = e.ManagerId,
            ManagerName = e.Manager?.FullName,
            IsActive = e.IsActive,
            LateCount = lateCount,
            AbsentCount = absentCount
        };
    }
}
