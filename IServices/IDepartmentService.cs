using HRMS_BACKEND.Dto.Department;

namespace HRMS_BACKEND.IServices;

public interface IDepartmentService
{
    Task<List<DepartmentResponseDto>> GetAllAsync();
    Task<DepartmentResponseDto> CreateAsync(CreateDepartmentDto dto);
    Task<DepartmentResponseDto> UpdateAsync(int id, CreateDepartmentDto dto);
    Task DeleteAsync(int id);
}
