namespace HRMS_BACKEND.Dto.Leave;

public class LeaveQuotaResponseDto
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public int LeaveTypeId { get; set; }
    public string LeaveTypeName { get; set; } = string.Empty;
    public int LeavePeriodId { get; set; }
    public int AllocatedDays { get; set; }
    public int UsedDays { get; set; }
    public int RemainingDays => AllocatedDays - UsedDays;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
