using System.ComponentModel.DataAnnotations;

namespace HRMS_BACKEND.Dto.Leave;

public class UpdateLeaveQuotaDto
{
    [Required]
    [Range(0, int.MaxValue, ErrorMessage = "AllocatedDays must be non-negative")]
    public int AllocatedDays { get; set; }
}
