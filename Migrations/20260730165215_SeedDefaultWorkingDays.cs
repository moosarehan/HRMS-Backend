using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HRMS_BACKEND.Migrations
{
    /// <inheritdoc />
    public partial class SeedDefaultWorkingDays : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Get today's date from the context
            // For seeding, we'll use a fixed date approach - this will be executed during migration
            // Set EffectiveFromDate to 2026-07-30 (assuming that's around migration time)
            migrationBuilder.Sql(@"
                -- Seed default working days (Mon-Fri) for all employees who don't have working days set
                INSERT INTO WorkingDays (EmployeeId, EffectiveFromDate, EffectiveToDate, Monday, Tuesday, Wednesday, Thursday, Friday, Saturday, Sunday)
                SELECT 
                    e.Id,
                    CAST(GETDATE() AS DATE),
                    NULL,
                    1, 1, 1, 1, 1, 0, 0
                FROM Employees e
                LEFT JOIN WorkingDays wd ON e.Id = wd.EmployeeId
                WHERE wd.Id IS NULL
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Delete working days that were created as defaults
            migrationBuilder.Sql(@"
                DELETE FROM WorkingDays
                WHERE EffectiveToDate IS NULL AND Monday = 1 AND Tuesday = 1 
                      AND Wednesday = 1 AND Thursday = 1 AND Friday = 1 
                      AND Saturday = 0 AND Sunday = 0
            ");
        }
    }
}
