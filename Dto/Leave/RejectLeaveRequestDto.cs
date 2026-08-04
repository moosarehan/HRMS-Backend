using System.ComponentModel.DataAnnotations;

namespace HRMS_BACKEND.Dto.Leave;

public class RejectLeaveRequestDto
{
    [Required]
    public int LeaveRequestId { get; set; }

    [Required]
    [MaxLength(500, ErrorMessage = "Rejection reason cannot exceed 500 characters")]
    public string RejectionReason { get; set; } = string.Empty;
}
