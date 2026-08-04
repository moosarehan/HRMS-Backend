using System.Security.Claims;
using HRMS_BACKEND.Dto.Employee;
using HRMS_BACKEND.Entities.Enums;
using HRMS_BACKEND.GenericResponse;
using HRMS_BACKEND.IServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRMS_BACKEND.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class EmployeeController : ControllerBase
{
    private readonly IEmployeeService _service;
    public EmployeeController(IEmployeeService service) => _service = service;

    private Role CurrentRole => Enum.Parse<Role>(User.FindFirstValue(ClaimTypes.Role)!);
    private int CurrentId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private int? CurrentDeptId
    {
        get
        {
            var val = User.FindFirstValue("departmentId");
            return string.IsNullOrEmpty(val) ? null : int.Parse(val);
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var result = await _service.GetAllAsync(CurrentRole, CurrentId, CurrentDeptId);
        return Ok(ApiResponse<List<EmployeeResponseDto>>.SuccessResponse(result));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        try
        {
            var result = await _service.GetByIdAsync(id, CurrentRole, CurrentId, CurrentDeptId);
            if (result is null) return NotFound(ApiResponse<string>.FailResponse("Employee not found."));
            return Ok(ApiResponse<EmployeeResponseDto>.SuccessResponse(result));
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    [HttpGet("me")]
    public async Task<IActionResult> GetMyProfile()
    {
        var result = await _service.GetByIdAsync(CurrentId, CurrentRole, CurrentId, CurrentDeptId);
        return Ok(ApiResponse<EmployeeResponseDto>.SuccessResponse(result!));
    }

    [HttpPost]
    [Authorize(Roles = "Admin,HR")]
    public async Task<IActionResult> Create(CreateEmployeeDto dto)
    {
        try
        {
            var result = await _service.CreateAsync(dto, CurrentRole);
            return Ok(ApiResponse<EmployeeResponseDto>.SuccessResponse(result, "Employee created."));
        }
        catch (Exception ex) when (ex is InvalidOperationException or UnauthorizedAccessException)
        {
            return BadRequest(ApiResponse<string>.FailResponse(ex.Message));
        }
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Admin,HR")]
    public async Task<IActionResult> Update(int id, UpdateEmployeeDto dto)
    {
        try
        {
            var result = await _service.UpdateAsync(id, dto, CurrentRole);
            return Ok(ApiResponse<EmployeeResponseDto>.SuccessResponse(result, "Employee updated."));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<string>.FailResponse(ex.Message));
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    [HttpPut("{id}/shift")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> AssignShift(int id, AssignEmployeeShiftDto dto)
    {
        try
        {
            var result = await _service.AssignShiftAsync(id, dto);
            return Ok(ApiResponse<EmployeeResponseDto>.SuccessResponse(result, "Employee shift assignment saved."));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<string>.FailResponse(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<string>.FailResponse(ex.Message));
        }
    }

    [HttpPut("me")]
    public async Task<IActionResult> UpdateMyProfile(UpdateOwnProfileDto dto)
    {
        var result = await _service.UpdateOwnProfileAsync(CurrentId, dto);
        return Ok(ApiResponse<EmployeeResponseDto>.SuccessResponse(result, "Profile updated."));
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin,HR")]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            await _service.DeleteAsync(id, CurrentRole);
            return Ok(ApiResponse<string>.SuccessResponse("", "Employee deleted."));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<string>.FailResponse(ex.Message));
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }
}
