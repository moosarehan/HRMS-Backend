namespace HRMS_BACKEND.Dto.Employee;

public class EmployeeShiftStatsDto
{
    /// <summary>
    /// Total number of times the employee was late (clocked in after shift start + grace period).
    /// Counted from the employee's first shift assignment date onwards.
    /// </summary>
    public int LateCount { get; set; } = 0;

    /// <summary>
    /// Total number of times the employee was absent (scheduled to work but didn't clock in).
    /// Counted from the employee's first shift assignment date onwards.
    /// </summary>
    public int AbsentCount { get; set; } = 0;

    /// <summary>
    /// Date of the employee's first shift assignment.
    /// Attendance counting starts from this date.
    /// </summary>
    public DateTime? FirstAssignmentDate { get; set; }

    /// <summary>
    /// Last updated timestamp for these statistics.
    /// </summary>
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}
