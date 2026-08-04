using HRMS_BACKEND.Dto.Auth;

namespace HRMS_BACKEND.IServices;

public interface IAuthService
{
    Task<AuthResponseDto> RegisterFirstAdminAsync(RegisterAdminDto dto);
    Task<AuthResponseDto> LoginAsync(LoginDto dto);
    Task<bool> AnyAdminExistsAsync();
    Task ResetPasswordAsync(ResetPasswordDto dto);
}
