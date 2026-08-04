namespace HRMS_BACKEND.Dto.Branch;

public class BranchResponseDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? City { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public int DepartmentCount { get; set; }
    public int EmployeeCount { get; set; }
}
