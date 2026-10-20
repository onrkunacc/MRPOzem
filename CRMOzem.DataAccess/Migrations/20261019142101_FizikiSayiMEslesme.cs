using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRMOzem.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class FizikiSayiMEslesme : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "HastaneTalepKutu",
                schema: "ozem",
                table: "SiparisTalepKalem",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.CreateTable(
                name: "FizikiSayimHastaneEslesmesi",
                schema: "ozem",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ApiHospitalName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    InternalHospitalID = table.Column<int>(type: "int", nullable: true),
                    Yoksay = table.Column<bool>(type: "bit", nullable: false),
                    OlusturmaTarihi = table.Column<DateTime>(type: "datetime2", nullable: false),
                    GuncellemeTarihi = table.Column<DateTime>(type: "datetime2", nullable: true),
                    OlusturanKullanici = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FizikiSayimHastaneEslesmesi", x => x.ID);
                    table.ForeignKey(
                        name: "FK_FizikiSayimHastaneEslesmesi_Hospitals_InternalHospitalID",
                        column: x => x.InternalHospitalID,
                        principalSchema: "ozem",
                        principalTable: "Hospitals",
                        principalColumn: "HospitalID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FizikiSayimHastaneEslesmesi_ApiHospitalName",
                schema: "ozem",
                table: "FizikiSayimHastaneEslesmesi",
                column: "ApiHospitalName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FizikiSayimHastaneEslesmesi_InternalHospitalID",
                schema: "ozem",
                table: "FizikiSayimHastaneEslesmesi",
                column: "InternalHospitalID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FizikiSayimHastaneEslesmesi",
                schema: "ozem");

            migrationBuilder.AlterColumn<int>(
                name: "HastaneTalepKutu",
                schema: "ozem",
                table: "SiparisTalepKalem",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);
        }
    }
}
