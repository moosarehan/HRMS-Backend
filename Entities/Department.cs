using System.ComponentModel.DataAnnotations;

namespace HRMS_BACKEND.Entities;

public class Department
{
    [Key]
    public int Id { get; set; }

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public int BranchId { get; set; }
    public Branch Branch { get; set; } = null!;

    public ICollection<Employee> Employees { get; set; } = new List<Employee>();
}
