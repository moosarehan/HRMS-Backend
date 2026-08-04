namespace HRMS_BACKEND.Dto.Attendance;

public class AttendanceResponseDto
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public int? ShiftId { get; set; }
    public string? ShiftName { get; set; }
    public DateOnly Date { get; set; }
    public DateTime? ClockIn { get; set; }
    public DateTime? ClockOut { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public bool IsLate { get; set; } = false;
    public int MinutesLate { get; set; } = 0;
    public string? EmergencyClockOutReason { get; set; }
    public string? EmergencyClockOutStatus { get; set; }
    public DateTime? EmergencyClockOutRequestedAt { get; set; }
}
