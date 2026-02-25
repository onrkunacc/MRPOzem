using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRMOzem.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddSutCodes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SutCode",
                table: "Tests",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "HospitalTestMonthlyVolumes",
                columns: table => new
                {
                    HospitalTestMonthlyVolumeID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    HospitalID = table.Column<int>(type: "int", nullable: false),
                    TestID = table.Column<int>(type: "int", nullable: false),
                    Year = table.Column<int>(type: "int", nullable: false),
                    Month = table.Column<int>(type: "int", nullable: false),
                    Count = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HospitalTestMonthlyVolumes", x => x.HospitalTestMonthlyVolumeID);
                    table.ForeignKey(
                        name: "FK_HospitalTestMonthlyVolumes_Hospitals_HospitalID",
                        column: x => x.HospitalID,
                        principalTable: "Hospitals",
                        principalColumn: "HospitalID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_HospitalTestMonthlyVolumes_Tests_TestID",
                        column: x => x.TestID,
                        principalTable: "Tests",
                        principalColumn: "TestID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TestSutPoints",
                columns: table => new
                {
                    TestSutPointID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TestID = table.Column<int>(type: "int", nullable: false),
                    Year = table.Column<int>(type: "int", nullable: false),
                    Point = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TestSutPoints", x => x.TestSutPointID);
                    table.ForeignKey(
                        name: "FK_TestSutPoints_Tests_TestID",
                        column: x => x.TestID,
                        principalTable: "Tests",
                        principalColumn: "TestID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HospitalTestMonthlyVolumes_HospitalID_TestID_Year_Month",
                table: "HospitalTestMonthlyVolumes",
                columns: new[] { "HospitalID", "TestID", "Year", "Month" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HospitalTestMonthlyVolumes_TestID",
                table: "HospitalTestMonthlyVolumes",
                column: "TestID");

            migrationBuilder.CreateIndex(
                name: "IX_TestSutPoints_TestID_Year",
                table: "TestSutPoints",
                columns: new[] { "TestID", "Year" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HospitalTestMonthlyVolumes");

            migrationBuilder.DropTable(
                name: "TestSutPoints");

            migrationBuilder.DropColumn(
                name: "SutCode",
                table: "Tests");
        }
    }
}
