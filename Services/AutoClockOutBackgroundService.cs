using HRMS_BACKEND.Data;
using HRMS_BACKEND.Entities;
using HRMS_BACKEND.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace HRMS_BACKEND.Services;

/// <summary>
/// Background service that automatically clocks out employees when their shift ends.
/// Runs every minute to check for shifts that have ended.
/// </summary>
public class AutoClockOutBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AutoClockOutBackgroundService> _logger;

    public AutoClockOutBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<AutoClockOutBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Auto Clock-Out Background Service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessAutoClockOutAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred in Auto Clock-Out service");
            }

            // Check every minute
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }

        _logger.LogInformation("Auto Clock-Out Background Service stopped");
    }

    private async Task ProcessAutoClockOutAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();

        var now = CompanyTime.Now(config);
        var today = CompanyTime.Today(config);

        // #region agent log
        static void AgentDebugLog(string hypothesisId, string location, string message, object? data)
        {
            try
            {
                var logPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "debug-f60ed9.log"));
                var payload = JsonSerializer.Serialize(new
                {
                    sessionId = "f60ed9",
                    hypothesisId,
                    location,
                    message,
                    data,
                    timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    runId = "pre-fix"
                });
                File.AppendAllText(logPath, payload + Environment.NewLine);
            }
            catch { }
        }
        // #endregion

        var openAttendancesAll = await dbContext.EmployeeAttendances
            .Include(a => a.Employee)
            .Where(a =>
                a.ClockIn != null &&
                a.ClockOut == null &&
                a.EmergencyClockOutStatus != Entities.Enums.EmergencyClockOutStatus.Pending
            )
            .ToListAsync();

        // #region agent log
        if (openAttendancesAll.Count > 0)
        {
            AgentDebugLog("H4", "AutoClockOutBackgroundService:openAttendances", "Open attendance records found", new
            {
                today = today.ToString("yyyy-MM-dd"),
                now,
                openCount = openAttendancesAll.Count,
                records = openAttendancesAll.Select(a => new
                {
                    a.EmployeeId,
                    employeeName = a.Employee.FullName,
                    a.Date,
                    a.EndTime,
                    shiftEnded = now >= a.EndTime,
                    excludedByTodayFilter = a.Date != today
                })
            });
        }
        // #endregion

        var employeesToClockOut = openAttendancesAll
            .Where(a => a.Date == today)
            .ToList();

        foreach (var attendance in employeesToClockOut)
        {
            try
            {
                // Use the attendance record's own EndTime (computed correctly at clock-in including overnight shifts)
                // Don't recompute from live Employee.Shift.EndTime as shift could have been deleted/changed
                if (now >= attendance.EndTime)
                {
                    // Auto clock-out
                    attendance.ClockOut = now;

                    _logger.LogInformation(
                        "Auto clocked-out Employee {EmployeeId} ({EmployeeName}) at {ClockOutTime}. Shift ended at {ShiftEnd}",
                        attendance.EmployeeId,
                        attendance.Employee.FullName,
                        now,
                        attendance.EndTime
                    );
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to auto clock-out Employee {EmployeeId}",
                    attendance.EmployeeId
                );
            }
        }

        if (employeesToClockOut.Any(a => a.ClockOut.HasValue))
        {
            await dbContext.SaveChangesAsync();
            _logger.LogInformation(
                "Auto clocked-out {Count} employees",
                employeesToClockOut.Count(a => a.ClockOut.HasValue)
            );
        }
    }
}
