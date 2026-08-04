namespace HRMS_BACKEND.Dto.Attendance;

public class ClockOutRequestDto
{
    public string? EmergencyReason { get; set; }
    public bool AutoClockOut { get; set; } = false;
}
