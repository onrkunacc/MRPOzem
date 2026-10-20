using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRMOzem.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddHospitalEslesmesi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HospitalEslesmesi",
                schema: "ozem",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ExternalMusteriKodu = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ExternalKurumAdi = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    InternalHospitalID = table.Column<int>(type: "int", nullable: true),
                    Yoksay = table.Column<bool>(type: "bit", nullable: false),
                    Not = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    OlusturmaTarihi = table.Column<DateTime>(type: "datetime2", nullable: false),
                    GuncellemeTarihi = table.Column<DateTime>(type: "datetime2", nullable: true),
                    OlusturanKullanici = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HospitalEslesmesi", x => x.ID);
                    table.ForeignKey(
                        name: "FK_HospitalEslesmesi_Hospitals_InternalHospitalID",
                        column: x => x.InternalHospitalID,
                        principalSchema: "ozem",
                        principalTable: "Hospitals",
                        principalColumn: "HospitalID");
                });

            migrationBuilder.CreateIndex(
                name: "IX_HospitalEslesmesi_ExternalMusteriKodu",
                schema: "ozem",
                table: "HospitalEslesmesi",
                column: "ExternalMusteriKodu",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HospitalEslesmesi_InternalHospitalID",
                schema: "ozem",
                table: "HospitalEslesmesi",
                column: "InternalHospitalID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HospitalEslesmesi",
                schema: "ozem");
        }
    }
}
