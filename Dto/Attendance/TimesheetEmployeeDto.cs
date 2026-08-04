namespace HRMS_BACKEND.Dto.Attendance;

public class TimesheetEmployeeDto
{
    public int EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string? DepartmentName { get; set; }
    public string? ShiftName { get; set; }
    public DateTime? ClockIn { get; set; }
    public DateTime? ClockOut { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public bool IsLate { get; set; } = false;
    public int MinutesLate { get; set; } = 0;
    
    // Additional fields for holiday status
    public string? HolidayReason { get; set; } // "On Leave", "Scheduled Off", etc.
    public string? LeaveType { get; set; } // "Sick Leave", "Annual Leave", etc.
}