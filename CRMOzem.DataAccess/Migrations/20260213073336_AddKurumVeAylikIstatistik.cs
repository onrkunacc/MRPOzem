using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRMOzem.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddKurumVeAylikIstatistik : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Kurumlar",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Ad = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IhaleTarihi = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IhaleNumarasi = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    AktifMi = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Kurumlar", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "KurumIstatistikAylik",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    KurumId = table.Column<int>(type: "int", nullable: false),
                    Yil = table.Column<int>(type: "int", nullable: false),
                    Ay = table.Column<int>(type: "int", nullable: false),
                    GeldiMi = table.Column<bool>(type: "bit", nullable: false),
                    Not = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KurumIstatistikAylik", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KurumIstatistikAylik_Kurumlar_KurumId",
                        column: x => x.KurumId,
                        principalTable: "Kurumlar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_KurumIstatistikAylik_KurumId_Yil_Ay",
                table: "KurumIstatistikAylik",
                columns: new[] { "KurumId", "Yil", "Ay" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "KurumIstatistikAylik");

            migrationBuilder.DropTable(
                name: "Kurumlar");
        }
    }
}
