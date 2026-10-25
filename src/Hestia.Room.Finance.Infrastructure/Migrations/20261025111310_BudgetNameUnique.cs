using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hestia.Room.Finance.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class BudgetNameUnique : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Budgets_Name",
                schema: "finance",
                table: "Budgets",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Budgets_Name",
                schema: "finance",
                table: "Budgets");
        }
    }
}
