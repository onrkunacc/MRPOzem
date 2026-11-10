using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRMOzem.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class IhaleKisimlar : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "KisimNo",
                schema: "ozem",
                table: "PersonelGiderler",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "KisimSayisi",
                schema: "ozem",
                table: "Ihaleler",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "KisimNo",
                schema: "ozem",
                table: "IhaleKalemler",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "KisimNo",
                schema: "ozem",
                table: "EkGiderler",
                type: "int",
                nullable: true);

            migrationBuilder.Sql("UPDATE ozem.Ihaleler SET KisimSayisi = 1 WHERE KisimSayisi IS NULL OR KisimSayisi <1;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "KisimNo",
                schema: "ozem",
                table: "PersonelGiderler");

            migrationBuilder.DropColumn(
                name: "KisimSayisi",
                schema: "ozem",
                table: "Ihaleler");

            migrationBuilder.DropColumn(
                name: "KisimNo",
                schema: "ozem",
                table: "IhaleKalemler");

            migrationBuilder.DropColumn(
                name: "KisimNo",
                schema: "ozem",
                table: "EkGiderler");
        }
    }
}
