using HRMS_BACKEND.Dto.WorkingDays;
using HRMS_BACKEND.GenericResponse;
using HRMS_BACKEND.IServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRMS_BACKEND.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class WorkingDaysController : ControllerBase
{
    private readonly IWorkingDaysService _service;

    public WorkingDaysController(IWorkingDaysService service) => _service = service;

    [HttpGet("employee/{employeeId}")]
    public async Task<IActionResult> GetByEmployeeId(int employeeId)
    {
        try
        {
            var result = await _service.GetByEmployeeIdAsync(employeeId);
            return Ok(ApiResponse<WorkingDaysResponseDto>.SuccessResponse(result));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<string>.FailResponse(ex.Message));
        }
    }

    [HttpPut("employee/{employeeId}")]
    public async Task<IActionResult> Upsert(int employeeId, UpsertWorkingDaysDto dto)
    {
        try
        {
            var result = await _service.UpsertAsync(employeeId, dto);
            return Ok(ApiResponse<WorkingDaysResponseDto>.SuccessResponse(result, "Working days updated."));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<string>.FailResponse(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<string>.FailResponse(ex.Message));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<string>.FailResponse(ex.Message));
        }
    }
}
