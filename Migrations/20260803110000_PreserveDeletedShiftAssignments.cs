using HRMS_BACKEND.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HRMS_BACKEND.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260803110000_PreserveDeletedShiftAssignments")]
public partial class PreserveDeletedShiftAssignments : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(name: "FK_EmployeeShiftAssignments_Shifts_ShiftId", table: "EmployeeShiftAssignments");
        migrationBuilder.AlterColumn<int>(name: "ShiftId", table: "EmployeeShiftAssignments", type: "int", nullable: true, oldClrType: typeof(int), oldType: "int");
        migrationBuilder.AddForeignKey(name: "FK_EmployeeShiftAssignments_Shifts_ShiftId", table: "EmployeeShiftAssignments", column: "ShiftId", principalTable: "Shifts", principalColumn: "Id", onDelete: ReferentialAction.SetNull);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(name: "FK_EmployeeShiftAssignments_Shifts_ShiftId", table: "EmployeeShiftAssignments");
        migrationBuilder.Sql("DELETE FROM EmployeeShiftAssignments WHERE ShiftId IS NULL");
        migrationBuilder.AlterColumn<int>(name: "ShiftId", table: "EmployeeShiftAssignments", type: "int", nullable: false, oldClrType: typeof(int), oldType: "int", oldNullable: true);
        migrationBuilder.AddForeignKey(name: "FK_EmployeeShiftAssignments_Shifts_ShiftId", table: "EmployeeShiftAssignments", column: "ShiftId", principalTable: "Shifts", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
    }
}
