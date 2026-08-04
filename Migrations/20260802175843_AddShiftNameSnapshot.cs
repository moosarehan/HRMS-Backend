using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HRMS_BACKEND.Migrations
{
    /// <inheritdoc />
    public partial class AddShiftNameSnapshot : Migration
        {
            /// <inheritdoc />
            protected override void Up(MigrationBuilder migrationBuilder)
            {
                migrationBuilder.AddColumn<string>(
                    name: "ShiftNameSnapshot",
                    table: "EmployeeAttendances",
                    type: "nvarchar(max)",
                    nullable: true);
            }

            /// <inheritdoc />
            protected override void Down(MigrationBuilder migrationBuilder)
            {
                migrationBuilder.DropColumn(
                    name: "ShiftNameSnapshot",
                    table: "EmployeeAttendances");
            }
        }
}
