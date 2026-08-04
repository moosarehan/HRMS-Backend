namespace HRMS_BACKEND.Dto.Attendance;

public class PendingEmergencyClockOutDto
{
    public int AttendanceId { get; set; }
    public int EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string? ShiftName { get; set; }
    public DateOnly Date { get; set; }
    public DateTime RequestedClockOutAt { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}
