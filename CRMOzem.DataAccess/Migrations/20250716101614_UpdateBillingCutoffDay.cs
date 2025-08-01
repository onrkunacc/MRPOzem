using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRMOzem.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class UpdateBillingCutoffDay : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {

            migrationBuilder.RenameColumn(
                name: "BillingCycleWeek",
                table: "AspNetUsers",
                newName: "BillingCutoffDay");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

            migrationBuilder.RenameColumn(
                name: "BillingCutoffDay",
                table: "AspNetUsers",
                newName: "BillingCycleWeek");
        }
    }
}
