using CRMOzem.DataAccess.Context;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRMOzem.DataAccess.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260828120000_AddKotaTakipModule")]
    public partial class AddKotaTakipModule : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(name: "ozem");

            migrationBuilder.CreateTable(
                name: "Kotalar",
                schema: "ozem",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    KotaKodu = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    KotaAdi = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    ProformaUsd = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    BaslangicTarihi = table.Column<DateTime>(type: "datetime2", nullable: true),
                    BitisTarihi = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Aktif = table.Column<bool>(type: "bit", nullable: false),
                    OlusturmaTarihi = table.Column<DateTime>(type: "datetime2", nullable: false),
                    GuncellenmeTarihi = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Kotalar", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "KotaCariler",
                schema: "ozem",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    KotaId = table.Column<int>(type: "int", nullable: false),
                    CariId = table.Column<int>(type: "int", nullable: false),
                    BaslangicTarihi = table.Column<DateTime>(type: "datetime2", nullable: true),
                    BitisTarihi = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Aktif = table.Column<bool>(type: "bit", nullable: false),
                    OlusturmaTarihi = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KotaCariler", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KotaCariler_Carilers_CariId",
                        column: x => x.CariId,
                        principalTable: "Carilers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_KotaCariler_Kotalar_KotaId",
                        column: x => x.KotaId,
                        principalSchema: "ozem",
                        principalTable: "Kotalar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "KotaFinansKayitlari",
                schema: "ozem",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    KotaId = table.Column<int>(type: "int", nullable: false),
                    DonemYil = table.Column<int>(type: "int", nullable: false),
                    DonemAy = table.Column<int>(type: "int", nullable: false),
                    KayitTipi = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    TedarikciGrubu = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    GiderKategorisi = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ParaBirimi = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    KayitKaynagi = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    FaturaNo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    FaturaTarihi = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Tutar = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DovizKuru = table.Column<decimal>(type: "decimal(18,6)", nullable: false),
                    TutarTry = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Aciklama = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    BelgeLinki = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    KaynakFaturaId = table.Column<int>(type: "int", nullable: true),
                    OlusturanKullaniciId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    OlusturmaTarihi = table.Column<DateTime>(type: "datetime2", nullable: false),
                    GuncellenmeTarihi = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KotaFinansKayitlari", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KotaFinansKayitlari_Invoices_KaynakFaturaId",
                        column: x => x.KaynakFaturaId,
                        principalTable: "Invoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_KotaFinansKayitlari_Kotalar_KotaId",
                        column: x => x.KotaId,
                        principalSchema: "ozem",
                        principalTable: "Kotalar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_KotaCariler_CariId_Aktif",
                schema: "ozem",
                table: "KotaCariler",
                columns: new[] { "CariId", "Aktif" });

            migrationBuilder.CreateIndex(
                name: "IX_KotaCariler_KotaId_CariId_Aktif",
                schema: "ozem",
                table: "KotaCariler",
                columns: new[] { "KotaId", "CariId", "Aktif" });

            migrationBuilder.CreateIndex(
                name: "IX_KotaFinansKayitlari_KaynakFaturaId",
                schema: "ozem",
                table: "KotaFinansKayitlari",
                column: "KaynakFaturaId",
                unique: true,
                filter: "[KaynakFaturaId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_KotaFinansKayitlari_KotaId_DonemYil_DonemAy",
                schema: "ozem",
                table: "KotaFinansKayitlari",
                columns: new[] { "KotaId", "DonemYil", "DonemAy" });

            migrationBuilder.CreateIndex(
                name: "IX_Kotalar_KotaKodu",
                schema: "ozem",
                table: "Kotalar",
                column: "KotaKodu",
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "KotaCariler", schema: "ozem");
            migrationBuilder.DropTable(name: "KotaFinansKayitlari", schema: "ozem");
            migrationBuilder.DropTable(name: "Kotalar", schema: "ozem");
        }
    }
}
