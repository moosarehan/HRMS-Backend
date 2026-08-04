using System.ComponentModel.DataAnnotations;

namespace HRMS_BACKEND.Entities;

public class LeaveType
{
    [Key]
    public int Id { get; set; }

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public ICollection<LeaveQuota> LeaveQuotas { get; set; } = new List<LeaveQuota>();
    public ICollection<LeaveMaster> LeaveMasters { get; set; } = new List<LeaveMaster>();
}
