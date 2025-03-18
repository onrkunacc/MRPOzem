using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRMOzem.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddInvoices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CariGruplaris",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GrupAdi = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CariGruplaris", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Carilers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CariKodu = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Unvan = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    KapanisTarihi = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TeklifNo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IhaleNo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Tevkifat = table.Column<bool>(type: "bit", nullable: false),
                    CariGrubuId = table.Column<int>(type: "int", nullable: false),
                    Il = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Ilce = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Carilers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Carilers_CariGruplaris_CariGrubuId",
                        column: x => x.CariGrubuId,
                        principalTable: "CariGruplaris",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Invoices",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Donemi = table.Column<int>(type: "int", nullable: false),
                    Ay = table.Column<int>(type: "int", nullable: false),
                    Tutar = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    KapanisTarihi = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CariId = table.Column<int>(type: "int", nullable: false),
                    Ekler = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Invoices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Invoices_Carilers_CariId",
                        column: x => x.CariId,
                        principalTable: "Carilers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Carilers_CariGrubuId",
                table: "Carilers",
                column: "CariGrubuId");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_CariId",
                table: "Invoices",
                column: "CariId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Invoices");

            migrationBuilder.DropTable(
                name: "Carilers");

            migrationBuilder.DropTable(
                name: "CariGruplaris");
        }
    }
}
