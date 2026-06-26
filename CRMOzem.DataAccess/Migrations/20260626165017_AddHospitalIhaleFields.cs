using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRMOzem.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddHospitalIhaleFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Aktif",
                schema: "ozem",
                table: "Hospitals",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "IhaleNumarasi",
                schema: "ozem",
                table: "Hospitals",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "IhaleTarihi",
                schema: "ozem",
                table: "Hospitals",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "HospitalAylikIstatistikler",
                schema: "ozem",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    HospitalID = table.Column<int>(type: "int", nullable: false),
                    Yil = table.Column<int>(type: "int", nullable: false),
                    Ay = table.Column<int>(type: "int", nullable: false),
                    GeldiMi = table.Column<bool>(type: "bit", nullable: false),
                    Not = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HospitalAylikIstatistikler", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HospitalAylikIstatistikler_Hospitals_HospitalID",
                        column: x => x.HospitalID,
                        principalSchema: "ozem",
                        principalTable: "Hospitals",
                        principalColumn: "HospitalID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HospitalAylikIstatistikler_HospitalID_Yil_Ay",
                schema: "ozem",
                table: "HospitalAylikIstatistikler",
                columns: new[] { "HospitalID", "Yil", "Ay" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HospitalAylikIstatistikler",
                schema: "ozem");

            migrationBuilder.DropColumn(
                name: "Aktif",
                schema: "ozem",
                table: "Hospitals");

            migrationBuilder.DropColumn(
                name: "IhaleNumarasi",
                schema: "ozem",
                table: "Hospitals");

            migrationBuilder.DropColumn(
                name: "IhaleTarihi",
                schema: "ozem",
                table: "Hospitals");
        }
    }
}
