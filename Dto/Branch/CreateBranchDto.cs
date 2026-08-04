using System.ComponentModel.DataAnnotations;

namespace HRMS_BACKEND.Dto.Branch;

public class CreateBranchDto
{
    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? City { get; set; }

    [MaxLength(20)]
    public string? Phone { get; set; }

    [MaxLength(200)]
    public string? Address { get; set; }
}
