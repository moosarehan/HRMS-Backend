using System.ComponentModel.DataAnnotations;

namespace HRMS_BACKEND.Dto.Leave;

public class CreateLeaveQuotaDto
{
    [Required]
    public int EmployeeId { get; set; }

    [Required]
    public int LeaveTypeId { get; set; }

    [Required]
    public int LeavePeriodId { get; set; }

    [Required]
    [Range(0, int.MaxValue, ErrorMessage = "AllocatedDays must be non-negative")]
    public int AllocatedDays { get; set; }
}
