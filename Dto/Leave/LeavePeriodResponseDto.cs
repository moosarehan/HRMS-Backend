namespace HRMS_BACKEND.Dto.Leave;

public class LeavePeriodResponseDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int TotalAllowedDays { get; set; }
    public DateTime CreatedAt { get; set; }
}
