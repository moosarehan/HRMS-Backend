using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using HRMS_BACKEND.Entities.Enums;

namespace HRMS_BACKEND.Entities;

public class EmployeeAttendance
{
    [Key]
    public int Id { get; set; }

    public int EmployeeId { get; set; }

    [ForeignKey(nameof(EmployeeId))]
    public Employee Employee { get; set; } = null!;

    public int? ShiftId { get; set; }

    [ForeignKey(nameof(ShiftId))]
    public Shift? Shift { get; set; }

    public DateOnly Date { get; set; }

    public DateTime? ClockIn { get; set; }

    public DateTime? ClockOut { get; set; }

    public DateTime StartTime { get; set; }

    public DateTime EndTime { get; set; }
    
    /// <summary>
    /// Snapshot of shift's LateThresholdMinutes at time of attendance creation.
    /// Preserved even if shift is deleted later, so late/present calculation is always accurate.
    /// </summary>
    public int LateThresholdMinutesSnapshot { get; set; } = 0;

    /// <summary>
    /// Snapshot of shift's Name at time of attendance creation.
    /// Preserved even if shift is deleted later, so we can always display the original shift name.
    /// </summary>
    public string? ShiftNameSnapshot { get; set; }

    public string? EmergencyClockOutReason { get; set; }

    public EmergencyClockOutStatus? EmergencyClockOutStatus { get; set; }

    public DateTime? EmergencyClockOutRequestedAt { get; set; }
}
