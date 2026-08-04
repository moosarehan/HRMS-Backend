using System.ComponentModel.DataAnnotations;

namespace HRMS_BACKEND.Dto.Leave;

public class CreateLeavePeriodDto
{
    [Required, MaxLength(20)]
    public string Name { get; set; } = string.Empty;

    [Required]
    public DateTime StartDate { get; set; }

    [Required]
    public DateTime EndDate { get; set; }

    [Required]
    [Range(1, int.MaxValue, ErrorMessage = "TotalAllowedDays must be greater than 0")]
    public int TotalAllowedDays { get; set; }
}
