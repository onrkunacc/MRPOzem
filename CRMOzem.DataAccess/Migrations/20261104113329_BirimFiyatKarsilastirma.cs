using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRMOzem.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class BirimFiyatKarsilastirma : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BirimFiyatKlasorleri",
                schema: "ozem",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Ad = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    UstKlasorId = table.Column<int>(type: "int", nullable: true),
                    Aciklama = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Renk = table.Column<string>(type: "nvarchar(9)", maxLength: 9, nullable: true),
                    Sira = table.Column<int>(type: "int", nullable: false),
                    Aktif = table.Column<bool>(type: "bit", nullable: false),
                    OlusturmaTarihi = table.Column<DateTime>(type: "datetime2", nullable: false),
                    OlusturanKullanici = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    GuncellemeTarihi = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BirimFiyatKlasorleri", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BirimFiyatKlasorleri_BirimFiyatKlasorleri_UstKlasorId",
                        column: x => x.UstKlasorId,
                        principalSchema: "ozem",
                        principalTable: "BirimFiyatKlasorleri",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BirimFiyatKarsilastirmalari",
                schema: "ozem",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Ad = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    Aciklama = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    KlasorId = table.Column<int>(type: "int", nullable: true),
                    IhaleId = table.Column<int>(type: "int", nullable: true),
                    IhaleAdi = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    HospitalID = table.Column<int>(type: "int", nullable: true),
                    HastaneAdi = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    TedarikciEtiket = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    BizEtiket = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TedarikciParaBirimi = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    BizParaBirimi = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    EslestirmeKurali = table.Column<int>(type: "int", nullable: false),
                    KurKaynagi = table.Column<int>(type: "int", nullable: false),
                    SabitKur = table.Column<decimal>(type: "decimal(18,6)", nullable: true),
                    ToleransYuzde = table.Column<decimal>(type: "decimal(9,4)", nullable: false),
                    Durum = table.Column<int>(type: "int", nullable: false),
                    TedarikciDosyaAdi = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    TedarikciYuklemeTarihi = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TedarikciSatirSayisi = table.Column<int>(type: "int", nullable: false),
                    BizDosyaAdi = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    BizYuklemeTarihi = table.Column<DateTime>(type: "datetime2", nullable: true),
                    BizSatirSayisi = table.Column<int>(type: "int", nullable: false),
                    ToplamEslesen = table.Column<int>(type: "int", nullable: false),
                    ToplamSadeceTedarikci = table.Column<int>(type: "int", nullable: false),
                    ToplamSadeceBiz = table.Column<int>(type: "int", nullable: false),
                    FarkliUrunSayisi = table.Column<int>(type: "int", nullable: false),
                    ToplamFarkTl = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ToplamFarkUsd = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    OrtalamaSapmaYuzde = table.Column<decimal>(type: "decimal(9,4)", nullable: false),
                    SonHesaplamaTarihi = table.Column<DateTime>(type: "datetime2", nullable: true),
                    KuruBulunamayanSatir = table.Column<int>(type: "int", nullable: false),
                    OlusturmaTarihi = table.Column<DateTime>(type: "datetime2", nullable: false),
                    OlusturanKullanici = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    GuncellemeTarihi = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BirimFiyatKarsilastirmalari", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BirimFiyatKarsilastirmalari_BirimFiyatKlasorleri_KlasorId",
                        column: x => x.KlasorId,
                        principalSchema: "ozem",
                        principalTable: "BirimFiyatKlasorleri",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "BirimFiyatSatirlari",
                schema: "ozem",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    KarsilastirmaId = table.Column<int>(type: "int", nullable: false),
                    Taraf = table.Column<int>(type: "int", nullable: false),
                    ReferansNo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ReferansNormalize = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UrunAdi = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    FaturaTarihi = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Donem = table.Column<DateTime>(type: "date", nullable: true),
                    BirimFiyat = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    ParaBirimi = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Miktar = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    Birim = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Kur = table.Column<decimal>(type: "decimal(18,6)", nullable: true),
                    KurTarihi = table.Column<DateTime>(type: "date", nullable: true),
                    BirimFiyatTl = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    BirimFiyatUsd = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    KaynakSatirNo = table.Column<int>(type: "int", nullable: false),
                    FaturaNo = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Uyari = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    OlusturmaTarihi = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BirimFiyatSatirlari", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BirimFiyatSatirlari_BirimFiyatKarsilastirmalari_KarsilastirmaId",
                        column: x => x.KarsilastirmaId,
                        principalSchema: "ozem",
                        principalTable: "BirimFiyatKarsilastirmalari",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BirimFiyatSonuclari",
                schema: "ozem",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    KarsilastirmaId = table.Column<int>(type: "int", nullable: false),
                    ReferansNo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ReferansNormalize = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UrunAdi = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Donem = table.Column<DateTime>(type: "date", nullable: true),
                    TedarikciSatirId = table.Column<int>(type: "int", nullable: true),
                    BizSatirId = table.Column<int>(type: "int", nullable: true),
                    TedarikciTarih = table.Column<DateTime>(type: "datetime2", nullable: true),
                    BizTarih = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TedarikciSatirAdedi = table.Column<int>(type: "int", nullable: false),
                    BizSatirAdedi = table.Column<int>(type: "int", nullable: false),
                    TedarikciFiyatUsd = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    TedarikciFiyatTl = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    BizFiyatTl = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    BizFiyatUsd = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    Kur = table.Column<decimal>(type: "decimal(18,6)", nullable: true),
                    KurTarihi = table.Column<DateTime>(type: "date", nullable: true),
                    FarkTl = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    FarkUsd = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    FarkYuzde = table.Column<decimal>(type: "decimal(12,4)", nullable: true),
                    TutarEtkisiTl = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Miktar = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    Durum = table.Column<int>(type: "int", nullable: false),
                    Yon = table.Column<int>(type: "int", nullable: false),
                    DikkatGerektiriyor = table.Column<bool>(type: "bit", nullable: false),
                    Not = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BirimFiyatSonuclari", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BirimFiyatSonuclari_BirimFiyatKarsilastirmalari_KarsilastirmaId",
                        column: x => x.KarsilastirmaId,
                        principalSchema: "ozem",
                        principalTable: "BirimFiyatKarsilastirmalari",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BirimFiyatKarsilastirmalari_KlasorId",
                schema: "ozem",
                table: "BirimFiyatKarsilastirmalari",
                column: "KlasorId");

            migrationBuilder.CreateIndex(
                name: "IX_BirimFiyatKarsilastirmalari_OlusturmaTarihi",
                schema: "ozem",
                table: "BirimFiyatKarsilastirmalari",
                column: "OlusturmaTarihi");

            migrationBuilder.CreateIndex(
                name: "IX_BirimFiyatKlasorleri_UstKlasorId",
                schema: "ozem",
                table: "BirimFiyatKlasorleri",
                column: "UstKlasorId");

            migrationBuilder.CreateIndex(
                name: "IX_BirimFiyatSatirlari_KarsilastirmaId_Taraf_ReferansNormalize_Donem",
                schema: "ozem",
                table: "BirimFiyatSatirlari",
                columns: new[] { "KarsilastirmaId", "Taraf", "ReferansNormalize", "Donem" });

            migrationBuilder.CreateIndex(
                name: "IX_BirimFiyatSonuclari_KarsilastirmaId_DikkatGerektiriyor",
                schema: "ozem",
                table: "BirimFiyatSonuclari",
                columns: new[] { "KarsilastirmaId", "DikkatGerektiriyor" });

            migrationBuilder.CreateIndex(
                name: "IX_BirimFiyatSonuclari_KarsilastirmaId_Durum",
                schema: "ozem",
                table: "BirimFiyatSonuclari",
                columns: new[] { "KarsilastirmaId", "Durum" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BirimFiyatSatirlari",
                schema: "ozem");

            migrationBuilder.DropTable(
                name: "BirimFiyatSonuclari",
                schema: "ozem");

            migrationBuilder.DropTable(
                name: "BirimFiyatKarsilastirmalari",
                schema: "ozem");

            migrationBuilder.DropTable(
                name: "BirimFiyatKlasorleri",
                schema: "ozem");
        }
    }
}
