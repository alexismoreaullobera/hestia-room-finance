using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hestia.Room.Finance.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RecurringLineNameOverPeriods : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RecurringLines_Name",
                schema: "finance",
                table: "RecurringLines");

            migrationBuilder.CreateIndex(
                name: "IX_RecurringLines_Name",
                schema: "finance",
                table: "RecurringLines",
                column: "Name",
                unique: true,
                filter: "[EndDate] IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RecurringLines_Name",
                schema: "finance",
                table: "RecurringLines");

            migrationBuilder.CreateIndex(
                name: "IX_RecurringLines_Name",
                schema: "finance",
                table: "RecurringLines",
                column: "Name",
                unique: true);
        }
    }
}
