using System.ComponentModel.DataAnnotations;

namespace HRMS_BACKEND.Dto.Leave;

public class ApproveLeaveRequestDto
{
    [Required]
    public int LeaveRequestId { get; set; }
}
