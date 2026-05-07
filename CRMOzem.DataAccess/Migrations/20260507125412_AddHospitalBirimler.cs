using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRMOzem.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddHospitalBirimler : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "ozem");

            migrationBuilder.CreateTable(
                name: "HospitalBirimler",
                schema: "ozem",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    HospitalID = table.Column<int>(type: "int", nullable: false),
                    BirimId = table.Column<int>(type: "int", nullable: false),
                    Aktif = table.Column<bool>(type: "bit", nullable: false),
                    BaslangicTarihi = table.Column<DateTime>(type: "datetime2", nullable: true),
                    BitisTarihi = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IhaleKayitNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Aciklama = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    OlusturmaTarihi = table.Column<DateTime>(type: "datetime2", nullable: false),
                    GuncellenmeTarihi = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HospitalBirimler", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HospitalBirimler_Birimler_BirimId",
                        column: x => x.BirimId,
                        principalSchema: "ozem",
                        principalTable: "Birimler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HospitalBirimler_Hospitals_HospitalID",
                        column: x => x.HospitalID,
                        principalSchema: "ozem",
                        principalTable: "Hospitals",
                        principalColumn: "HospitalID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HospitalBirimler_BirimId",
                schema: "ozem",
                table: "HospitalBirimler",
                column: "BirimId");

            migrationBuilder.CreateIndex(
                name: "IX_HospitalBirimler_HospitalID_BirimId_Aktif",
                schema: "ozem",
                table: "HospitalBirimler",
                columns: new[] { "HospitalID", "BirimId", "Aktif" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HospitalBirimler",
                schema: "ozem");
        }
    }
}
