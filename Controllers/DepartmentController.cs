using HRMS_BACKEND.Dto.Department;
using HRMS_BACKEND.GenericResponse;
using HRMS_BACKEND.IServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRMS_BACKEND.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DepartmentController : ControllerBase
{
    private readonly IDepartmentService _service;
    public DepartmentController(IDepartmentService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var result = await _service.GetAllAsync();
        return Ok(ApiResponse<List<DepartmentResponseDto>>.SuccessResponse(result));
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create(CreateDepartmentDto dto)
    {
        try
        {
            var result = await _service.CreateAsync(dto);
            return Ok(ApiResponse<DepartmentResponseDto>.SuccessResponse(result, "Department created."));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<string>.FailResponse(ex.Message));
        }
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(int id, CreateDepartmentDto dto)
    {
        try
        {
            var result = await _service.UpdateAsync(id, dto);
            return Ok(ApiResponse<DepartmentResponseDto>.SuccessResponse(result, "Department updated."));
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
            return Ok(ApiResponse<string>.SuccessResponse("", "Department deleted."));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<string>.FailResponse(ex.Message));
        }
    }
}
