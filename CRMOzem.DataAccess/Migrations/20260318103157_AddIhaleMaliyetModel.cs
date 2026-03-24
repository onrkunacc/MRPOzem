using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace CRMOzem.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddIhaleMaliyetModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TenderExtraCosts");

            migrationBuilder.DropTable(
                name: "TenderPersonnels");

            migrationBuilder.DropTable(
                name: "TenderTests");

            migrationBuilder.DropTable(
                name: "Tenders");

            migrationBuilder.DropIndex(
                name: "IX_TestTemplates_HospitalID",
                table: "TestTemplates");

            migrationBuilder.CreateTable(
                name: "Birimler",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BirimKodu = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    BirimAdi = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Aktif = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Birimler", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Ihaleler",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IhaleAdi = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    IhaleKayitNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    IhaleTarihi = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IhaleSaati = table.Column<TimeSpan>(type: "time", nullable: true),
                    IhaleSekli = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IhaleSuresiGun = table.Column<int>(type: "int", nullable: false),
                    GenelGiderOran = table.Column<decimal>(type: "decimal(5,4)", nullable: false),
                    KarOran = table.Column<decimal>(type: "decimal(5,4)", nullable: false),
                    KDVOran = table.Column<decimal>(type: "decimal(5,4)", nullable: false),
                    KurUSD = table.Column<decimal>(type: "decimal(10,4)", nullable: false),
                    KurEUR = table.Column<decimal>(type: "decimal(10,4)", nullable: false),
                    OlusturmaTarihi = table.Column<DateTime>(type: "datetime2", nullable: false),
                    GuncellenmeTarihi = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Aktif = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Ihaleler", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EkGiderler",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IhaleId = table.Column<int>(type: "int", nullable: false),
                    BirimId = table.Column<int>(type: "int", nullable: true),
                    GiderAdi = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Kategori = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Miktar = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    BirimAciklama = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    BirimFiyat = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    ParaBirimi = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Kur = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    Aciklama = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EkGiderler", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EkGiderler_Birimler_BirimId",
                        column: x => x.BirimId,
                        principalTable: "Birimler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EkGiderler_Ihaleler_IhaleId",
                        column: x => x.IhaleId,
                        principalTable: "Ihaleler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IhaleKalemler",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IhaleId = table.Column<int>(type: "int", nullable: false),
                    BirimId = table.Column<int>(type: "int", nullable: true),
                    SiraNo = table.Column<int>(type: "int", nullable: false),
                    SutKodu = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    TetkikAdi = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    TestMiktari = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    SutFiyat = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    SutCarpan = table.Column<decimal>(type: "decimal(10,4)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IhaleKalemler", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IhaleKalemler_Birimler_BirimId",
                        column: x => x.BirimId,
                        principalTable: "Birimler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IhaleKalemler_Ihaleler_IhaleId",
                        column: x => x.IhaleId,
                        principalTable: "Ihaleler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PersonelGiderler",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IhaleId = table.Column<int>(type: "int", nullable: false),
                    BirimId = table.Column<int>(type: "int", nullable: false),
                    GorevUnvan = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Sayi = table.Column<int>(type: "int", nullable: false),
                    EgitimDurumu = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UcretTipi = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    BirimFiyatAylik = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Aciklama = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PersonelGiderler", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PersonelGiderler_Birimler_BirimId",
                        column: x => x.BirimId,
                        principalTable: "Birimler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PersonelGiderler_Ihaleler_IhaleId",
                        column: x => x.IhaleId,
                        principalTable: "Ihaleler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Birimler",
                columns: new[] { "Id", "Aktif", "BirimAdi", "BirimKodu" },
                values: new object[,]
                {
                    { 1, true, "Biyokimya", "BIO" },
                    { 2, true, "Makro/Mikro ELISA", "ELISA" },
                    { 3, true, "Hemogram", "HEM" },
                    { 4, true, "Hormon", "HOR" },
                    { 5, true, "Kan Sayım", "KAN" },
                    { 6, true, "Sedimentasyon", "SEDIM" },
                    { 7, true, "Hepatit", "HEP" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_TestTemplates_HospitalID_TestID",
                table: "TestTemplates",
                columns: new[] { "HospitalID", "TestID" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EkGiderler_BirimId",
                table: "EkGiderler",
                column: "BirimId");

            migrationBuilder.CreateIndex(
                name: "IX_EkGiderler_IhaleId",
                table: "EkGiderler",
                column: "IhaleId");

            migrationBuilder.CreateIndex(
                name: "IX_IhaleKalemler_BirimId",
                table: "IhaleKalemler",
                column: "BirimId");

            migrationBuilder.CreateIndex(
                name: "IX_IhaleKalemler_IhaleId",
                table: "IhaleKalemler",
                column: "IhaleId");

            migrationBuilder.CreateIndex(
                name: "IX_PersonelGiderler_BirimId",
                table: "PersonelGiderler",
                column: "BirimId");

            migrationBuilder.CreateIndex(
                name: "IX_PersonelGiderler_IhaleId",
                table: "PersonelGiderler",
                column: "IhaleId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EkGiderler");

            migrationBuilder.DropTable(
                name: "IhaleKalemler");

            migrationBuilder.DropTable(
                name: "PersonelGiderler");

            migrationBuilder.DropTable(
                name: "Birimler");

            migrationBuilder.DropTable(
                name: "Ihaleler");

            migrationBuilder.DropIndex(
                name: "IX_TestTemplates_HospitalID_TestID",
                table: "TestTemplates");

            migrationBuilder.CreateTable(
                name: "Tenders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DurationMonths = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OverallCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    RegistrationNumber = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TenderDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TotalExtraCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalPersonnelCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalTestCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(max)", nullable: false)
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
                    Currency = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ExchangeRate = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
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
                    Count = table.Column<int>(type: "int", nullable: false),
                    Education = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FeeDescription = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Role = table.Column<string>(type: "nvarchar(max)", nullable: false),
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
                    Multiplier = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OrderNo = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    SutCode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SutPoint = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
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
                name: "IX_TestTemplates_HospitalID",
                table: "TestTemplates",
                column: "HospitalID");

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
    }
}
