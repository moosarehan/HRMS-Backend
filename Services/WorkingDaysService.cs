using HRMS_BACKEND.Data;
using HRMS_BACKEND.Dto.WorkingDays;
using HRMS_BACKEND.Entities;
using HRMS_BACKEND.Helpers;
using HRMS_BACKEND.IServices;
using Microsoft.EntityFrameworkCore;

namespace HRMS_BACKEND.Services;

public class WorkingDaysService : IWorkingDaysService
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;

    public WorkingDaysService(AppDbContext db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

    public async Task<WorkingDaysResponseDto> GetByEmployeeIdAsync(int employeeId)
    {
        await EnsureEmployeeExistsAsync(employeeId);

        var today = CompanyTime.Today(_config);
        var schedule = await _db.WorkingDays
            .FirstOrDefaultAsync(wd => wd.EmployeeId == employeeId);

        // If no working days configured, create default ones (Mon-Fri)
        if (schedule == null)
        {
            schedule = new WorkingDays
            {
                EmployeeId = employeeId,
                EffectiveFromDate = today,
                EffectiveToDate = null,
                Monday = true,
                Tuesday = true,
                Wednesday = true,
                Thursday = true,
                Friday = true,
                Saturday = false,
                Sunday = false
            };
            _db.WorkingDays.Add(schedule);
            await _db.SaveChangesAsync();
        }

        return MapToDto(employeeId, schedule, null);
    }

    public async Task<WorkingDaysResponseDto> UpsertAsync(int employeeId, UpsertWorkingDaysDto dto)
    {
        await EnsureEmployeeExistsAsync(employeeId);

        var today = CompanyTime.Today(_config);

        // Check if employee is currently clocked in (any active attendance row)
        var activeAttendance = await _db.EmployeeAttendances
            .FirstOrDefaultAsync(a => a.EmployeeId == employeeId && a.ClockIn != null && a.ClockOut == null);

        if (activeAttendance != null)
        {
            var clockedInDayOfWeek = activeAttendance.Date.DayOfWeek;
            bool isDayEnabledInDto = clockedInDayOfWeek switch
            {
                DayOfWeek.Monday => dto.Monday,
                DayOfWeek.Tuesday => dto.Tuesday,
                DayOfWeek.Wednesday => dto.Wednesday,
                DayOfWeek.Thursday => dto.Thursday,
                DayOfWeek.Friday => dto.Friday,
                DayOfWeek.Saturday => dto.Saturday,
                DayOfWeek.Sunday => dto.Sunday,
                _ => true
            };

            if (!isDayEnabledInDto)
            {
                throw new InvalidOperationException($"Cannot edit or disable working day ({clockedInDayOfWeek}) while this employee is currently clocked in for that shift.");
            }
        }

        // Delete all existing schedules for this employee
        var existingSchedules = await _db.WorkingDays
            .Where(wd => wd.EmployeeId == employeeId)
            .ToListAsync();

        if (existingSchedules.Count > 0)
        {
            _db.WorkingDays.RemoveRange(existingSchedules);
            await _db.SaveChangesAsync();
        }

        // Create the new schedule
        var newSchedule = new WorkingDays
        {
            EmployeeId = employeeId,
            EffectiveFromDate = today,
            EffectiveToDate = null,
            Monday = dto.Monday,
            Tuesday = dto.Tuesday,
            Wednesday = dto.Wednesday,
            Thursday = dto.Thursday,
            Friday = dto.Friday,
            Saturday = dto.Saturday,
            Sunday = dto.Sunday
        };

        _db.WorkingDays.Add(newSchedule);
        await _db.SaveChangesAsync();

        return MapToDto(employeeId, newSchedule, null);
    }

    public async Task<WorkingDays?> GetScheduleForDateAsync(int employeeId, DateOnly targetDate)
    {
        return await _db.WorkingDays
            .FirstOrDefaultAsync(wd => wd.EmployeeId == employeeId);
    }

    private async Task EnsureEmployeeExistsAsync(int employeeId)
    {
        var exists = await _db.Employees.AnyAsync(e => e.Id == employeeId);
        if (!exists)
            throw new KeyNotFoundException("Employee not found.");
    }

    private static WorkingDaysResponseDto MapToDto(int employeeId, WorkingDays current, WorkingDays? pending)
    {
        return new WorkingDaysResponseDto
        {
            EmployeeId = employeeId,
            CurrentSchedule = MapSchedule(current),
            PendingSchedule = pending is null || pending.Id == current.Id
                ? null
                : MapSchedule(pending)
        };
    }

    private static WorkingDaysScheduleDto MapSchedule(WorkingDays schedule)
    {
        return new WorkingDaysScheduleDto
        {
            EffectiveFromDate = schedule.EffectiveFromDate,
            EffectiveToDate = schedule.EffectiveToDate,
            Monday = schedule.Monday,
            Tuesday = schedule.Tuesday,
            Wednesday = schedule.Wednesday,
            Thursday = schedule.Thursday,
            Friday = schedule.Friday,
            Saturday = schedule.Saturday,
            Sunday = schedule.Sunday
        };
    }
}
