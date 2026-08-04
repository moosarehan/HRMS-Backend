namespace HRMS_BACKEND.Dto.Leave;

public class LeaveSetupStatusDto
{
    public bool IsSetupComplete { get; set; }
    public bool HasPeriod { get; set; }
    public bool HasQuota { get; set; }
    public string? Message { get; set; }
    public LeavePeriodResponseDto? CurrentLeavePeriod { get; set; }
    public List<LeaveQuotaResponseDto> EmployeeQuotas { get; set; } = new List<LeaveQuotaResponseDto>();
}
