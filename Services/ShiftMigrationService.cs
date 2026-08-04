using HRMS_BACKEND.Data;
using HRMS_BACKEND.Entities;
using HRMS_BACKEND.Helpers;
using Microsoft.EntityFrameworkCore;

namespace HRMS_BACKEND.Services;

/// <summary>
/// One-time migration service to move existing Employee.ShiftId values to EmployeeShiftAssignment table.
/// This preserves existing shift assignments while enabling the new temporal assignment system.
/// </summary>
public class ShiftMigrationService
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;

    public ShiftMigrationService(AppDbContext db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

    /// <summary>
    /// Migrates existing Employee.ShiftId values to EmployeeShiftAssignment records.
    /// Sets EffectiveFrom to today so existing assignments continue working immediately.
    /// </summary>
    public async Task MigrateExistingShiftAssignmentsAsync()
    {
        var today = CompanyTime.Today(_config);
        
        // Find all employees who have a ShiftId but no EmployeeShiftAssignment records yet
        var employeesWithShifts = await _db.Employees
            .Where(e => e.ShiftId.HasValue && e.IsActive)
            .Include(e => e.Shift)
            .ToListAsync();

        if (!employeesWithShifts.Any())
        {
            Console.WriteLine("No employees with existing shift assignments found to migrate.");
            return;
        }

        Console.WriteLine($"Migrating {employeesWithShifts.Count} existing shift assignments...");

        foreach (var employee in employeesWithShifts)
        {
            // Check if this employee already has shift assignments (avoid duplicates)
            var existingAssignment = await _db.EmployeeShiftAssignments
                .FirstOrDefaultAsync(a => a.EmployeeId == employee.Id);
                
            if (existingAssignment != null)
            {
                Console.WriteLine($"Employee {employee.Id} ({employee.FullName}) already has shift assignments, skipping.");
                continue;
            }

            // Create new assignment record effective from today
            var assignment = new EmployeeShiftAssignment
            {
                EmployeeId = employee.Id,
                ShiftId = employee.ShiftId!.Value,
                EffectiveFrom = today,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = null // System migration, no specific admin
            };

            _db.EmployeeShiftAssignments.Add(assignment);
            employee.FirstShiftAssignmentDate ??= today;
            Console.WriteLine($"Migrated: {employee.FullName} → {employee.Shift?.Name} (effective {today:yyyy-MM-dd})");
        }

        await _db.SaveChangesAsync();
        Console.WriteLine($"Successfully migrated {employeesWithShifts.Count} shift assignments to new system.");
    }

    /// <summary>
    /// Optionally removes the old ShiftId and PendingShiftId columns from Employee table.
    /// This should only be called after confirming the migration worked correctly.
    /// </summary>
    public async Task CleanupLegacyShiftColumnsAsync()
    {
        // This would require a new migration to drop the columns
        // For now, we'll just set them to null to indicate they're no longer used
        var employees = await _db.Employees.ToListAsync();
        
        foreach (var employee in employees)
        {
            employee.ShiftId = null;
            employee.PendingShiftId = null;
            employee.PendingShiftEffectiveFromDate = null;
        }
        
        await _db.SaveChangesAsync();
        Console.WriteLine("Cleared legacy ShiftId columns from Employee table.");
    }
}
