using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRMOzem.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddTestGirisTipi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GirisTipi",
                schema: "ozem",
                table: "Tests",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Adet");

            migrationBuilder.AddColumn<int>(
                name: "KutuBasinaAdet",
                schema: "ozem",
                table: "Tests",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GirisTipi",
                schema: "ozem",
                table: "Tests");

            migrationBuilder.DropColumn(
                name: "KutuBasinaAdet",
                schema: "ozem",
                table: "Tests");
        }
    }
}
