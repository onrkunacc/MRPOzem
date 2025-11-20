using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRMOzem.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddTenderModels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Tenders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RegistrationNumber = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TenderDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DurationMonths = table.Column<int>(type: "int", nullable: false),
                    TotalTestCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalExtraCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalPersonnelCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    OverallCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tenders", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TenderExtraCosts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenderId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ExchangeRate = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenderExtraCosts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TenderExtraCosts_Tenders_TenderId",
                        column: x => x.TenderId,
                        principalTable: "Tenders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TenderPersonnels",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenderId = table.Column<int>(type: "int", nullable: false),
                    Role = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Count = table.Column<int>(type: "int", nullable: false),
                    Education = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FeeDescription = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UnitFee = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenderPersonnels", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TenderPersonnels_Tenders_TenderId",
                        column: x => x.TenderId,
                        principalTable: "Tenders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TenderTests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenderId = table.Column<int>(type: "int", nullable: false),
                    OrderNo = table.Column<int>(type: "int", nullable: false),
                    SutCode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SutPoint = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Multiplier = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenderTests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TenderTests_Tenders_TenderId",
                        column: x => x.TenderId,
                        principalTable: "Tenders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TenderExtraCosts_TenderId",
                table: "TenderExtraCosts",
                column: "TenderId");

            migrationBuilder.CreateIndex(
                name: "IX_TenderPersonnels_TenderId",
                table: "TenderPersonnels",
                column: "TenderId");

            migrationBuilder.CreateIndex(
                name: "IX_TenderTests_TenderId",
                table: "TenderTests",
                column: "TenderId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TenderExtraCosts");

            migrationBuilder.DropTable(
                name: "TenderPersonnels");

            migrationBuilder.DropTable(
                name: "TenderTests");

            migrationBuilder.DropTable(
                name: "Tenders");
        }
    }
}
