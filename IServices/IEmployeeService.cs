using HRMS_BACKEND.Dto.Employee;
using HRMS_BACKEND.Entities.Enums;

namespace HRMS_BACKEND.IServices;

public interface IEmployeeService
{
    Task<List<EmployeeResponseDto>> GetAllAsync(Role currentRole, int currentId, int? currentDeptId);
    Task<EmployeeResponseDto?> GetByIdAsync(int id, Role currentRole, int currentId, int? currentDeptId);
    Task<EmployeeResponseDto> CreateAsync(CreateEmployeeDto dto, Role currentRole);
    Task<EmployeeResponseDto> UpdateAsync(int id, UpdateEmployeeDto dto, Role currentRole);
    Task<EmployeeResponseDto> AssignShiftAsync(int id, AssignEmployeeShiftDto dto);
    Task<EmployeeResponseDto> UpdateOwnProfileAsync(int currentId, UpdateOwnProfileDto dto);
    Task DeleteAsync(int id, Role currentRole);
}
