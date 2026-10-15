using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRMOzem.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddKotaHospitalRelation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "HospitalID",
                schema: "ozem",
                table: "KotaHareketleri",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "KotaHospitalleri",
                schema: "ozem",
                columns: table => new
                {
                    KotaHospitalID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    KotaID = table.Column<int>(type: "int", nullable: false),
                    HospitalID = table.Column<int>(type: "int", nullable: false),
                    EklenmeTarihi = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Aciklama = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KotaHospitalleri", x => x.KotaHospitalID);
                    table.ForeignKey(
                        name: "FK_KotaHospitalleri_Hospitals_HospitalID",
                        column: x => x.HospitalID,
                        principalSchema: "ozem",
                        principalTable: "Hospitals",
                        principalColumn: "HospitalID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_KotaHospitalleri_Kotalar_KotaID",
                        column: x => x.KotaID,
                        principalSchema: "ozem",
                        principalTable: "Kotalar",
                        principalColumn: "KotaID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_KotaHareketleri_HospitalID",
                schema: "ozem",
                table: "KotaHareketleri",
                column: "HospitalID");

            migrationBuilder.CreateIndex(
                name: "IX_KotaHospitalleri_HospitalID",
                schema: "ozem",
                table: "KotaHospitalleri",
                column: "HospitalID");

            migrationBuilder.CreateIndex(
                name: "IX_KotaHospitalleri_KotaID_HospitalID",
                schema: "ozem",
                table: "KotaHospitalleri",
                columns: new[] { "KotaID", "HospitalID" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_KotaHareketleri_Hospitals_HospitalID",
                schema: "ozem",
                table: "KotaHareketleri",
                column: "HospitalID",
                principalSchema: "ozem",
                principalTable: "Hospitals",
                principalColumn: "HospitalID",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_KotaHareketleri_Hospitals_HospitalID",
                schema: "ozem",
                table: "KotaHareketleri");

            migrationBuilder.DropTable(
                name: "KotaHospitalleri",
                schema: "ozem");

            migrationBuilder.DropIndex(
                name: "IX_KotaHareketleri_HospitalID",
                schema: "ozem",
                table: "KotaHareketleri");

            migrationBuilder.DropColumn(
                name: "HospitalID",
                schema: "ozem",
                table: "KotaHareketleri");
        }
    }
}
