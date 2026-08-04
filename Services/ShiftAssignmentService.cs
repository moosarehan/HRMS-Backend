using HRMS_BACKEND.Data;
using HRMS_BACKEND.Entities;
using HRMS_BACKEND.Helpers;
using HRMS_BACKEND.IServices;
using Microsoft.EntityFrameworkCore;

namespace HRMS_BACKEND.Services;

public class ShiftAssignmentService : IShiftAssignmentService
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;

    public ShiftAssignmentService(AppDbContext db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

    public async Task<Shift?> GetEffectiveShiftForDateAsync(int employeeId, DateOnly date)
    {
        // First check the new assignment system (dated history)
        var assignment = await _db.EmployeeShiftAssignments
            .Where(a => a.EmployeeId == employeeId && a.EffectiveFrom <= date)
            .OrderByDescending(a => a.EffectiveFrom)
            .Include(a => a.Shift)
            .FirstOrDefaultAsync();

        if (assignment != null)
        {
            return assignment.Shift ?? (assignment.ShiftId.HasValue
                ? await _db.Shifts.FindAsync(assignment.ShiftId.Value)
                : null);
        }

        // Fall back to the old system (Employee.ShiftId) if no assignments exist
        var employee = await _db.Employees
            .Where(e => e.Id == employeeId)
            .Include(e => e.Shift)
            .Include(e => e.PendingShift)
            .FirstOrDefaultAsync();

        if (employee == null) return null;

        if (employee.PendingShiftEffectiveFromDate.HasValue && employee.PendingShiftEffectiveFromDate.Value <= date && employee.PendingShift != null)
        {
            return employee.PendingShift;
        }

        return employee.Shift;
    }

    public async Task AssignShiftAsync(int employeeId, int shiftId, DateOnly effectiveFrom, int? assignedBy = null)
    {
        // Validate employee exists
        var employee = await _db.Employees.FindAsync(employeeId);
        if (employee == null)
            throw new KeyNotFoundException($"Employee with ID {employeeId} not found.");

        // Validate shift exists
        var shift = await _db.Shifts.FindAsync(shiftId);
        if (shift == null)
            throw new KeyNotFoundException($"Shift with ID {shiftId} not found.");

        // Create new assignment record
        var assignment = new EmployeeShiftAssignment
        {
            EmployeeId = employeeId,
            ShiftId = shiftId,
            EffectiveFrom = effectiveFrom,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = assignedBy
        };

        _db.EmployeeShiftAssignments.Add(assignment);

        if (!employee.FirstShiftAssignmentDate.HasValue || effectiveFrom < employee.FirstShiftAssignmentDate.Value)
            employee.FirstShiftAssignmentDate = effectiveFrom;

        // Also update Employee.ShiftId for backwards compatibility
        // This field is now denormalized and should not be used for date-specific queries
        employee.ShiftId = shiftId;

        await _db.SaveChangesAsync();
    }

    public async Task<EmployeeShiftAssignment> ReassignShiftForTomorrowAsync(int employeeId, int shiftId, int assignedBy)
    {
        // Calculate tomorrow's date using company timezone
        var companyNow = CompanyTime.Now(_config);
        var tomorrow = DateOnly.FromDateTime(companyNow.Date.AddDays(1));

        // Validate employee exists
        var employee = await _db.Employees
            .Include(e => e.Shift)
            .FirstOrDefaultAsync(e => e.Id == employeeId);
        
        if (employee == null)
            throw new KeyNotFoundException($"Employee with ID {employeeId} not found.");

        // Validate shift exists
        var shift = await _db.Shifts.FindAsync(shiftId);
        if (shift == null)
            throw new KeyNotFoundException($"Shift with ID {shiftId} not found.");

        // Create new assignment effective from tomorrow
        var assignment = new EmployeeShiftAssignment
        {
            EmployeeId = employeeId,
            ShiftId = shiftId,
            EffectiveFrom = tomorrow,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = assignedBy
        };

        _db.EmployeeShiftAssignments.Add(assignment);

        if (!employee.FirstShiftAssignmentDate.HasValue || tomorrow < employee.FirstShiftAssignmentDate.Value)
            employee.FirstShiftAssignmentDate = tomorrow;

        // Update Employee.ShiftId for backwards compatibility
        employee.ShiftId = shiftId;

        await _db.SaveChangesAsync();

        // Reload with navigation properties for controller response
        await _db.Entry(assignment).Reference(a => a.Employee).LoadAsync();
        await _db.Entry(assignment).Reference(a => a.Shift).LoadAsync();

        return assignment;
    }

    public async Task<List<EmployeeShiftAssignment>> GetAssignmentHistoryAsync(int employeeId)
    {
        return await _db.EmployeeShiftAssignments
            .Where(a => a.EmployeeId == employeeId)
            .Include(a => a.Shift)
            .OrderByDescending(a => a.EffectiveFrom)
            .ToListAsync();
    }

    public async Task<DateOnly?> GetFirstAssignmentDateAsync(int employeeId)
    {
        var employee = await _db.Employees.FindAsync(employeeId);
        if (employee?.FirstShiftAssignmentDate is DateOnly firstShiftAssignmentDate)
            return firstShiftAssignmentDate;

        var firstAssignment = await _db.EmployeeShiftAssignments
            .Where(esa => esa.EmployeeId == employeeId)
            .OrderBy(esa => esa.EffectiveFrom)
            .FirstOrDefaultAsync();
            
        if (firstAssignment != null)
            return firstAssignment.EffectiveFrom;

        // Legacy: shift assigned via Employee.ShiftId before assignment tracking existed
        if (employee?.ShiftId != null)
        {
            var earliestAttendance = await _db.EmployeeAttendances
                .Where(a => a.EmployeeId == employeeId)
                .OrderBy(a => a.Date)
                .Select(a => a.Date)
                .FirstOrDefaultAsync();

            if (earliestAttendance != default)
                return earliestAttendance;
        }
            
        return null;
    }
}
