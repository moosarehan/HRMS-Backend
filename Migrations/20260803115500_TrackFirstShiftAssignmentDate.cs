using HRMS_BACKEND.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HRMS_BACKEND.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260803115500_TrackFirstShiftAssignmentDate")]
public partial class TrackFirstShiftAssignmentDate : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateOnly>(
            name: "FirstShiftAssignmentDate",
            table: "Employees",
            type: "date",
            nullable: true);

        migrationBuilder.Sql(@"
            UPDATE e
            SET FirstShiftAssignmentDate = assignments.FirstAssignmentDate
            FROM Employees AS e
            INNER JOIN (
                SELECT EmployeeId, MIN(EffectiveFrom) AS FirstAssignmentDate
                FROM EmployeeShiftAssignments
                GROUP BY EmployeeId
            ) AS assignments ON assignments.EmployeeId = e.Id
            WHERE e.FirstShiftAssignmentDate IS NULL;");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "FirstShiftAssignmentDate",
            table: "Employees");
    }
}
