using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRMOzem.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddOverrideToExpenseDetail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "OverrideCycleMonth",
                table: "ExpenseDetails",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OverrideCycleYear",
                table: "ExpenseDetails",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OverrideCycleMonth",
                table: "ExpenseDetails");

            migrationBuilder.DropColumn(
                name: "OverrideCycleYear",
                table: "ExpenseDetails");
        }
    }
}
