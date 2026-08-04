namespace HRMS_BACKEND.Dto.Attendance;

public class AttendanceExportDto
{
    public string EmployeeName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Branch { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string Shift { get; set; } = string.Empty;
    public DateTime? ClockIn { get; set; }
    public DateTime? ClockOut { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public bool IsLate { get; set; } = false;
    public int MinutesLate { get; set; } = 0;
    public string Status { get; set; } = string.Empty;
    public DateOnly Date { get; set; }
}