using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HRMS_BACKEND.Migrations
{
    /// <inheritdoc />
    public partial class AddAttendanceModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "PendingShiftEffectiveFromDate",
                table: "Employees",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PendingShiftId",
                table: "Employees",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ShiftId",
                table: "Employees",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Shifts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "time", nullable: false),
                    EndTime = table.Column<TimeOnly>(type: "time", nullable: false),
                    Limit = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Shifts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WorkingDays",
                columns: table => new
                {
                    EmployeeId = table.Column<int>(type: "int", nullable: false),
                    EffectiveFromDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Monday = table.Column<bool>(type: "bit", nullable: false),
                    Tuesday = table.Column<bool>(type: "bit", nullable: false),
                    Wednesday = table.Column<bool>(type: "bit", nullable: false),
                    Thursday = table.Column<bool>(type: "bit", nullable: false),
                    Friday = table.Column<bool>(type: "bit", nullable: false),
                    Saturday = table.Column<bool>(type: "bit", nullable: false),
                    Sunday = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkingDays", x => new { x.EmployeeId, x.EffectiveFromDate });
                    table.ForeignKey(
                        name: "FK_WorkingDays_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EmployeeAttendances",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmployeeId = table.Column<int>(type: "int", nullable: false),
                    ShiftId = table.Column<int>(type: "int", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    ClockIn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClockOut = table.Column<DateTime>(type: "datetime2", nullable: true),
                    StartTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EmergencyClockOutReason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EmergencyClockOutStatus = table.Column<int>(type: "int", nullable: true),
                    EmergencyClockOutRequestedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeAttendances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmployeeAttendances_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EmployeeAttendances_Shifts_ShiftId",
                        column: x => x.ShiftId,
                        principalTable: "Shifts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "Shifts",
                columns: new[] { "Id", "EndTime", "Limit", "Name", "StartTime" },
                values: new object[,]
                {
                    { 1, new TimeOnly(17, 0, 0), 100, "Morning Shift", new TimeOnly(9, 0, 0) },
                    { 2, new TimeOnly(4, 0, 0), 100, "Night Shift", new TimeOnly(20, 0, 0) },
                    { 3, new TimeOnly(17, 0, 0), 100, "Remote Shift", new TimeOnly(9, 0, 0) }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Employees_PendingShiftId",
                table: "Employees",
                column: "PendingShiftId");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_ShiftId",
                table: "Employees",
                column: "ShiftId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeAttendances_ShiftId",
                table: "EmployeeAttendances",
                column: "ShiftId");

            migrationBuilder.CreateIndex(
                name: "UX_EmployeeAttendance_Employee_Date",
                table: "EmployeeAttendances",
                columns: new[] { "EmployeeId", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Shifts_Name",
                table: "Shifts",
                column: "Name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Employees_Shifts_PendingShiftId",
                table: "Employees",
                column: "PendingShiftId",
                principalTable: "Shifts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Employees_Shifts_ShiftId",
                table: "Employees",
                column: "ShiftId",
                principalTable: "Shifts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Employees_Shifts_PendingShiftId",
                table: "Employees");

            migrationBuilder.DropForeignKey(
                name: "FK_Employees_Shifts_ShiftId",
                table: "Employees");

            migrationBuilder.DropTable(
                name: "EmployeeAttendances");

            migrationBuilder.DropTable(
                name: "WorkingDays");

            migrationBuilder.DropTable(
                name: "Shifts");

            migrationBuilder.DropIndex(
                name: "IX_Employees_PendingShiftId",
                table: "Employees");

            migrationBuilder.DropIndex(
                name: "IX_Employees_ShiftId",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "PendingShiftEffectiveFromDate",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "PendingShiftId",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "ShiftId",
                table: "Employees");
        }
    }
}
