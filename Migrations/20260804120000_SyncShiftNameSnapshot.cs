using HRMS_BACKEND.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HRMS_BACKEND.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260804120000_SyncShiftNameSnapshot")]
    public partial class SyncShiftNameSnapshot : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Ensure the ShiftNameSnapshot column exists on EmployeeShiftAssignments.
            // Use defensive SQL so applying this migration is idempotent.
            migrationBuilder.Sql(@"
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.EmployeeShiftAssignments')
      AND name = 'ShiftNameSnapshot'
)
BEGIN
    ALTER TABLE dbo.EmployeeShiftAssignments
    ADD ShiftNameSnapshot nvarchar(max) NULL;
END
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.EmployeeShiftAssignments')
      AND name = 'ShiftNameSnapshot'
)
BEGIN
    ALTER TABLE dbo.EmployeeShiftAssignments
    DROP COLUMN ShiftNameSnapshot;
END
");
        }
    }
}
