using HRMS_BACKEND.Entities.Enums;

namespace HRMS_BACKEND.Dto.Employee;

public class CreateEmployeeDto
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public Role Role { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string? Designation { get; set; }
    public decimal Salary { get; set; }
    public int? DepartmentId { get; set; }
    public int? ManagerId { get; set; }
}
