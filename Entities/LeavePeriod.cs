using System.ComponentModel.DataAnnotations;

namespace HRMS_BACKEND.Entities;

public class LeavePeriod
{
    [Key]
    public int Id { get; set; }

    [Required, MaxLength(20)]
    public string Name { get; set; } = string.Empty;

    [Required]
    public DateTime StartDate { get; set; }

    [Required]
    public DateTime EndDate { get; set; }

    [Required]
    public int TotalAllowedDays { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<LeaveQuota> LeaveQuotas { get; set; } = new List<LeaveQuota>();
    public ICollection<LeaveMaster> LeaveMasters { get; set; } = new List<LeaveMaster>();
}
