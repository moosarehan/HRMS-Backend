using HRMS_BACKEND.Dto.Auth;
using HRMS_BACKEND.GenericResponse;
using HRMS_BACKEND.IServices;
using Microsoft.AspNetCore.Mvc;

namespace HRMS_BACKEND.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    public AuthController(IAuthService authService) => _authService = authService;

    [HttpPost("register-admin")]
    public async Task<IActionResult> RegisterAdmin(RegisterAdminDto dto)
    {
        try
        {
            var result = await _authService.RegisterFirstAdminAsync(dto);
            return Ok(ApiResponse<AuthResponseDto>.SuccessResponse(result, "Admin registered successfully."));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<string>.FailResponse(ex.Message));
        }
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginDto dto)
    {
        try
        {
            var result = await _authService.LoginAsync(dto);
            return Ok(ApiResponse<AuthResponseDto>.SuccessResponse(result, "Login successful."));
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(ApiResponse<string>.FailResponse(ex.Message));
        }
    }

    [HttpGet("admin-exists")]
    public async Task<IActionResult> AdminExists()
    {
        var exists = await _authService.AnyAdminExistsAsync();
        return Ok(ApiResponse<object>.SuccessResponse(new { adminExists = exists }));
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword(ResetPasswordDto dto)
    {
        try
        {
            await _authService.ResetPasswordAsync(dto);
            return Ok(ApiResponse<string>.SuccessResponse("Password reset successfully."));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<string>.FailResponse(ex.Message));
        }
    }
}
