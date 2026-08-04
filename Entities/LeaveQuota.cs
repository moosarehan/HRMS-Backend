using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HRMS_BACKEND.Entities;

public class LeaveQuota
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int EmployeeId { get; set; }

    [ForeignKey(nameof(EmployeeId))]
    public Employee? Employee { get; set; }

    [Required]
    public int LeaveTypeId { get; set; }

    [ForeignKey(nameof(LeaveTypeId))]
    public LeaveType? LeaveType { get; set; }

    [Required]
    public int LeavePeriodId { get; set; }

    [ForeignKey(nameof(LeavePeriodId))]
    public LeavePeriod? LeavePeriod { get; set; }

    [Required]
    public int AllocatedDays { get; set; }

    [Required]
    public int UsedDays { get; set; } = 0;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
