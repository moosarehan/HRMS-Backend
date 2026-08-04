using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HRMS_BACKEND.Migrations
{
    /// <inheritdoc />
    public partial class SeedShiftData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Shifts",
                columns: new[] { "Id", "Name", "StartTime", "EndTime", "Limit" },
                values: new object[,]
                {
                    { 1, "Morning Shift", new TimeOnly(9, 0), new TimeOnly(17, 0), 100 },
                    { 2, "Night Shift", new TimeOnly(20, 0), new TimeOnly(4, 0), 100 },
                    { 3, "Remote Shift", new TimeOnly(9, 0), new TimeOnly(17, 0), 100 }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Shifts",
                keyColumn: "Id",
                keyValues: new object[] { 1, 2, 3 });
        }
    }
}
