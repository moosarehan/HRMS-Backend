namespace HRMS_BACKEND.Dto.Shift;

public class UpdateShiftDto
{
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public int Limit { get; set; }
    public int LateThresholdMinutes { get; set; }
}
