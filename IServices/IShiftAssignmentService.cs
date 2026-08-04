using HRMS_BACKEND.Entities;

namespace HRMS_BACKEND.IServices;

public interface IShiftAssignmentService
{
    /// <summary>
    /// Gets the effective shift for an employee on a specific date.
    /// Returns the shift that was assigned and effective on that date.
    /// Falls back to Employee.ShiftId if no assignment history exists.
    /// </summary>
    /// <param name="employeeId">Employee ID</param>
    /// <param name="date">Target date to check</param>
    /// <returns>The effective shift or null if no assignment found</returns>
    Task<Shift?> GetEffectiveShiftForDateAsync(int employeeId, DateOnly date);

    /// <summary>
    /// Assigns a new shift to an employee, effective from a specific date.
    /// </summary>
    /// <param name="employeeId">Employee ID</param>
    /// <param name="shiftId">New shift ID</param>
    /// <param name="effectiveFrom">Date when assignment becomes effective</param>
    /// <param name="assignedBy">ID of admin making the assignment</param>
    Task AssignShiftAsync(int employeeId, int shiftId, DateOnly effectiveFrom, int? assignedBy = null);

    /// <summary>
    /// Reassigns a shift to an employee effective from TOMORROW (prevents retroactive conflicts).
    /// This is the recommended method for all admin shift reassignments.
    /// </summary>
    /// <param name="employeeId">Employee ID</param>
    /// <param name="shiftId">New shift ID</param>
    /// <param name="assignedBy">ID of admin making the assignment</param>
    /// <returns>The created assignment</returns>
    Task<EmployeeShiftAssignment> ReassignShiftForTomorrowAsync(int employeeId, int shiftId, int assignedBy);

    /// <summary>
    /// Gets the shift assignment history for an employee.
    /// </summary>
    /// <param name="employeeId">Employee ID</param>
    /// <returns>List of shift assignments ordered by effective date descending</returns>
    Task<List<EmployeeShiftAssignment>> GetAssignmentHistoryAsync(int employeeId);

    /// <summary>
    /// Gets the date of the first shift assignment for an employee.
    /// Returns null if no assignment exists.
    /// </summary>
    Task<DateOnly?> GetFirstAssignmentDateAsync(int employeeId);
}