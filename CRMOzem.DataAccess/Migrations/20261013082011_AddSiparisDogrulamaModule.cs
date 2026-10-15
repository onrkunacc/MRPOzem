using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRMOzem.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddSiparisDogrulamaModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FizikiSayimSnapshot",
                schema: "ozem",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    HospitalID = table.Column<int>(type: "int", nullable: false),
                    ExternalProductId = table.Column<int>(type: "int", nullable: false),
                    ProductName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ReferenceNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ProductType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Platform = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UrunTipi = table.Column<int>(type: "int", nullable: false),
                    MappedTestID = table.Column<int>(type: "int", nullable: true),
                    SayimNo = table.Column<int>(type: "int", nullable: false),
                    SayimTarihi = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SayimAktifMi = table.Column<bool>(type: "bit", nullable: false),
                    BoxCount = table.Column<int>(type: "int", nullable: false),
                    FlaconCount = table.Column<int>(type: "int", nullable: false),
                    TestCount = table.Column<int>(type: "int", nullable: false),
                    BoxTextCount = table.Column<int>(type: "int", nullable: false),
                    FlaconCountPerBox = table.Column<int>(type: "int", nullable: false),
                    LotNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ExpiryDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastUpdatedExternal = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CekilmeTarihi = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FizikiSayimSnapshot", x => x.ID);
                    table.ForeignKey(
                        name: "FK_FizikiSayimSnapshot_Hospitals_HospitalID",
                        column: x => x.HospitalID,
                        principalSchema: "ozem",
                        principalTable: "Hospitals",
                        principalColumn: "HospitalID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FizikiSayimSnapshot_Tests_MappedTestID",
                        column: x => x.MappedTestID,
                        principalSchema: "ozem",
                        principalTable: "Tests",
                        principalColumn: "TestID");
                });

            migrationBuilder.CreateTable(
                name: "SiparisTalep",
                schema: "ozem",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    HospitalID = table.Column<int>(type: "int", nullable: false),
                    TalepTarihi = table.Column<DateTime>(type: "datetime2", nullable: false),
                    HedefDonem = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ExternalRefNo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    HastaneNotu = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Durum = table.Column<int>(type: "int", nullable: false),
                    OnayTarihi = table.Column<DateTime>(type: "datetime2", nullable: true),
                    OnaylayanKullanici = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    OnayNotu = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    OlusturmaTarihi = table.Column<DateTime>(type: "datetime2", nullable: false),
                    GuncellemeTarihi = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SiparisTalep", x => x.ID);
                    table.ForeignKey(
                        name: "FK_SiparisTalep_Hospitals_HospitalID",
                        column: x => x.HospitalID,
                        principalSchema: "ozem",
                        principalTable: "Hospitals",
                        principalColumn: "HospitalID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SiparisTalepKalem",
                schema: "ozem",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SiparisTalepID = table.Column<int>(type: "int", nullable: false),
                    ExternalProductId = table.Column<int>(type: "int", nullable: false),
                    ProductName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ReferenceNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    TestID = table.Column<int>(type: "int", nullable: true),
                    UrunTipi = table.Column<int>(type: "int", nullable: false),
                    HastaneTalepTest = table.Column<int>(type: "int", nullable: false),
                    HastaneTalepKutu = table.Column<int>(type: "int", nullable: false),
                    HesapGuncelStok = table.Column<int>(type: "int", nullable: false),
                    OrtalamaAylikKullanim = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    OnerilenTest = table.Column<int>(type: "int", nullable: false),
                    OnerilenKutu = table.Column<int>(type: "int", nullable: false),
                    SapmaYuzde = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    DogrulamaRengi = table.Column<int>(type: "int", nullable: false),
                    HesaplamaAciklamasi = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    OnaylananTest = table.Column<int>(type: "int", nullable: true),
                    KalemNotu = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    OlusturmaTarihi = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SiparisTalepKalem", x => x.ID);
                    table.ForeignKey(
                        name: "FK_SiparisTalepKalem_SiparisTalep_SiparisTalepID",
                        column: x => x.SiparisTalepID,
                        principalSchema: "ozem",
                        principalTable: "SiparisTalep",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SiparisTalepKalem_Tests_TestID",
                        column: x => x.TestID,
                        principalSchema: "ozem",
                        principalTable: "Tests",
                        principalColumn: "TestID");
                });

            migrationBuilder.CreateIndex(
                name: "IX_FizikiSayimSnapshot_HospitalID",
                schema: "ozem",
                table: "FizikiSayimSnapshot",
                column: "HospitalID");

            migrationBuilder.CreateIndex(
                name: "IX_FizikiSayimSnapshot_MappedTestID",
                schema: "ozem",
                table: "FizikiSayimSnapshot",
                column: "MappedTestID");

            migrationBuilder.CreateIndex(
                name: "IX_SiparisTalep_HospitalID",
                schema: "ozem",
                table: "SiparisTalep",
                column: "HospitalID");

            migrationBuilder.CreateIndex(
                name: "IX_SiparisTalepKalem_SiparisTalepID",
                schema: "ozem",
                table: "SiparisTalepKalem",
                column: "SiparisTalepID");

            migrationBuilder.CreateIndex(
                name: "IX_SiparisTalepKalem_TestID",
                schema: "ozem",
                table: "SiparisTalepKalem",
                column: "TestID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FizikiSayimSnapshot",
                schema: "ozem");

            migrationBuilder.DropTable(
                name: "SiparisTalepKalem",
                schema: "ozem");

            migrationBuilder.DropTable(
                name: "SiparisTalep",
                schema: "ozem");
        }
    }
}
