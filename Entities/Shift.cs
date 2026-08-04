using System.ComponentModel.DataAnnotations;

namespace HRMS_BACKEND.Entities;

public class Shift
{
    [Key]
    public int Id { get; set; }

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    public TimeOnly StartTime { get; set; }

    public TimeOnly EndTime { get; set; }

    public int Limit { get; set; }

    public int LateThresholdMinutes { get; set; } = 0;

    public ICollection<Employee> Employees { get; set; } = new List<Employee>();
    public ICollection<EmployeeAttendance> Attendances { get; set; } = new List<EmployeeAttendance>();
}
