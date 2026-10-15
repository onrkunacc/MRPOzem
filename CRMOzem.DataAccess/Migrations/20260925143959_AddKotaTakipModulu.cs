using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRMOzem.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddKotaTakipModulu : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Kotalar",
                schema: "ozem",
                columns: table => new
                {
                    KotaID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    KotaKodu = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    IsAdi = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Bolge = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ProformaTarihi = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ProformaTutarUsd = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    BaslamaTarihi = table.Column<DateTime>(type: "datetime2", nullable: true),
                    BitisTarihi = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AySayisi = table.Column<int>(type: "int", nullable: true),
                    Durum = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Sorumlu = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Aciklama = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    OlusturmaTarihi = table.Column<DateTime>(type: "datetime2", nullable: false),
                    GuncellemeTarihi = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EkleyenKullaniciId = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Kotalar", x => x.KotaID);
                });

            migrationBuilder.CreateTable(
                name: "TcmbKurlari",
                schema: "ozem",
                columns: table => new
                {
                    TcmbKurID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Tarih = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UsdAlis = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    UsdSatis = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    EurAlis = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: true),
                    EurSatis = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: true),
                    UsdEfektifAlis = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: true),
                    UsdEfektifSatis = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: true),
                    CekilmeZamani = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ManuelGirildi = table.Column<bool>(type: "bit", nullable: false),
                    Not = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TcmbKurlari", x => x.TcmbKurID);
                });

            migrationBuilder.CreateTable(
                name: "Tedarikciler",
                schema: "ozem",
                columns: table => new
                {
                    TedarikciID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Ad = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Grup = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Aciklama = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Aktif = table.Column<bool>(type: "bit", nullable: false),
                    Sira = table.Column<int>(type: "int", nullable: false),
                    OlusturmaTarihi = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tedarikciler", x => x.TedarikciID);
                });

            migrationBuilder.CreateTable(
                name: "KotaHareketleri",
                schema: "ozem",
                columns: table => new
                {
                    KotaHareketID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    KotaID = table.Column<int>(type: "int", nullable: false),
                    TedarikciID = table.Column<int>(type: "int", nullable: false),
                    HareketTuru = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Yil = table.Column<int>(type: "int", nullable: false),
                    Ay = table.Column<int>(type: "int", nullable: false),
                    FaturaNo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    FaturaTarihi = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Aciklama = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Tutar = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ParaBirimi = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    Kur = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    TLKarsiligi = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    UsdKarsiligi = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    EkleyenKullaniciId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OlusturmaTarihi = table.Column<DateTime>(type: "datetime2", nullable: false),
                    GuncellemeTarihi = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KotaHareketleri", x => x.KotaHareketID);
                    table.ForeignKey(
                        name: "FK_KotaHareketleri_Kotalar_KotaID",
                        column: x => x.KotaID,
                        principalSchema: "ozem",
                        principalTable: "Kotalar",
                        principalColumn: "KotaID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_KotaHareketleri_Tedarikciler_TedarikciID",
                        column: x => x.TedarikciID,
                        principalSchema: "ozem",
                        principalTable: "Tedarikciler",
                        principalColumn: "TedarikciID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_KotaHareketleri_KotaID_Yil_Ay",
                schema: "ozem",
                table: "KotaHareketleri",
                columns: new[] { "KotaID", "Yil", "Ay" });

            migrationBuilder.CreateIndex(
                name: "IX_KotaHareketleri_TedarikciID",
                schema: "ozem",
                table: "KotaHareketleri",
                column: "TedarikciID");

            migrationBuilder.CreateIndex(
                name: "IX_Kotalar_Durum",
                schema: "ozem",
                table: "Kotalar",
                column: "Durum");

            migrationBuilder.CreateIndex(
                name: "IX_Kotalar_KotaKodu",
                schema: "ozem",
                table: "Kotalar",
                column: "KotaKodu");

            migrationBuilder.CreateIndex(
                name: "IX_TcmbKurlari_Tarih",
                schema: "ozem",
                table: "TcmbKurlari",
                column: "Tarih",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tedarikciler_Ad",
                schema: "ozem",
                table: "Tedarikciler",
                column: "Ad");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "KotaHareketleri",
                schema: "ozem");

            migrationBuilder.DropTable(
                name: "TcmbKurlari",
                schema: "ozem");

            migrationBuilder.DropTable(
                name: "Kotalar",
                schema: "ozem");

            migrationBuilder.DropTable(
                name: "Tedarikciler",
                schema: "ozem");
        }
    }
}
