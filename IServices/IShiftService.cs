using HRMS_BACKEND.Dto.Shift;

namespace HRMS_BACKEND.IServices;

public interface IShiftService
{
    Task<List<ShiftResponseDto>> GetAllAsync();
    Task<ShiftResponseDto> CreateAsync(CreateShiftDto dto);
    Task<ShiftResponseDto> UpdateAsync(int id, UpdateShiftDto dto);
    Task DeleteAsync(int id);
}
