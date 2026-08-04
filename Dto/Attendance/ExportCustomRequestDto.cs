namespace HRMS_BACKEND.Dto.Attendance;

public class ExportCustomRequestDto
{
    public string? Format { get; set; }
    public List<dynamic>? Records { get; set; }
    public string? Status { get; set; }
}
