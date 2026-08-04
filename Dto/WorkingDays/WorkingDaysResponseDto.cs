namespace HRMS_BACKEND.Dto.WorkingDays;

public class WorkingDaysResponseDto
{
    public int EmployeeId { get; set; }
    public WorkingDaysScheduleDto CurrentSchedule { get; set; } = new();
    public WorkingDaysScheduleDto? PendingSchedule { get; set; }
}
