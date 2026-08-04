using System.ComponentModel.DataAnnotations;

namespace HRMS_BACKEND.Dto.Leave;

public class CreateLeaveRequestDto
{
    [Required]
    public DateTime StartDate { get; set; }

    [Required]
    public DateTime EndDate { get; set; }

    [Required]
    public int LeaveTypeId { get; set; }

    [Required]
    [Range(1, int.MaxValue, ErrorMessage = "NoOfDays must be greater than 0")]
    public int NoOfDays { get; set; }

    public string? Description { get; set; }
}
