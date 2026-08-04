using System.Security.Claims;
using HRMS_BACKEND.Dto.Branch;
using HRMS_BACKEND.Entities.Enums;
using HRMS_BACKEND.GenericResponse;
using HRMS_BACKEND.IServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRMS_BACKEND.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class BranchController : ControllerBase
{
    private readonly IBranchService _service;
    public BranchController(IBranchService service) => _service = service;

    private Role CurrentRole => Enum.Parse<Role>(User.FindFirstValue(ClaimTypes.Role)!);
    private int CurrentId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var result = await _service.GetAllAsync(CurrentRole, CurrentId);
        return Ok(ApiResponse<List<BranchResponseDto>>.SuccessResponse(result));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        try
        {
            var result = await _service.GetByIdAsync(id);
            return Ok(ApiResponse<BranchResponseDto>.SuccessResponse(result));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<string>.FailResponse(ex.Message));
        }
    }

    [HttpGet("{branchId}/departments")]
    public async Task<IActionResult> GetDepartmentsByBranch(int branchId)
    {
        try
        {
            var result = await _service.GetDepartmentsByBranchAsync(branchId);
            return Ok(ApiResponse<List<Dto.Department.DepartmentResponseDto>>.SuccessResponse(result));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<string>.FailResponse(ex.Message));
        }
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create(CreateBranchDto dto)
    {
        try
        {
            var result = await _service.CreateAsync(dto);
            return Ok(ApiResponse<BranchResponseDto>.SuccessResponse(result, "Branch created."));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<string>.FailResponse(ex.Message));
        }
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(int id, UpdateBranchDto dto)
    {
        try
        {
            var result = await _service.UpdateAsync(id, dto);
            return Ok(ApiResponse<BranchResponseDto>.SuccessResponse(result, "Branch updated."));
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

    [HttpGet("{id}/delete-impact")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetDeleteImpact(int id)
    {
        try
        {
            var impact = await _service.GetDeleteImpactAsync(id);
            return Ok(ApiResponse<BranchDeleteImpactDto>.SuccessResponse(impact));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<string>.FailResponse(ex.Message));
        }
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            await _service.DeleteAsync(id);
            return Ok(ApiResponse<string>.SuccessResponse("", "Branch deleted."));
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
}
