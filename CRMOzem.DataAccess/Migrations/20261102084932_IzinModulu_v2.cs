using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRMOzem.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class IzinModulu_v2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Aciklama",
                schema: "ozem",
                table: "UserLeaveBalances",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CarriedOverDays",
                schema: "ozem",
                table: "UserLeaveBalances",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "KidemYili",
                schema: "ozem",
                table: "UserLeaveBalances",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ManuelGirildi",
                schema: "ozem",
                table: "UserLeaveBalances",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "DecisionAt",
                schema: "ozem",
                table: "LeaveRequests",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DecisionByName",
                schema: "ozem",
                table: "LeaveRequests",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DecisionByUserId",
                schema: "ozem",
                table: "LeaveRequests",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DecisionNote",
                schema: "ozem",
                table: "LeaveRequests",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "SaturdayExcluded",
                schema: "ozem",
                table: "LeaveRequests",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "TotalDays",
                schema: "ozem",
                table: "LeaveRequests",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Departman",
                table: "AspNetUsers",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DogumTarihi",
                table: "AspNetUsers",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "IseGirisTarihi",
                table: "AspNetUsers",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "IstenCikisTarihi",
                table: "AspNetUsers",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TcKimlikNo",
                table: "AspNetUsers",
                type: "nvarchar(11)",
                maxLength: 11,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ResmiTatiller",
                schema: "ozem",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Tarih = table.Column<DateTime>(type: "date", nullable: false),
                    Ad = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    TamGun = table.Column<bool>(type: "bit", nullable: false),
                    Aciklama = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Yil = table.Column<int>(type: "int", nullable: false),
                    OlusturmaTarihi = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResmiTatiller", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ResmiTatiller_Tarih",
                schema: "ozem",
                table: "ResmiTatiller",
                column: "Tarih",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ResmiTatiller_Yil",
                schema: "ozem",
                table: "ResmiTatiller",
                column: "Yil");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ResmiTatiller",
                schema: "ozem");

            migrationBuilder.DropColumn(
                name: "Aciklama",
                schema: "ozem",
                table: "UserLeaveBalances");

            migrationBuilder.DropColumn(
                name: "CarriedOverDays",
                schema: "ozem",
                table: "UserLeaveBalances");

            migrationBuilder.DropColumn(
                name: "KidemYili",
                schema: "ozem",
                table: "UserLeaveBalances");

            migrationBuilder.DropColumn(
                name: "ManuelGirildi",
                schema: "ozem",
                table: "UserLeaveBalances");

            migrationBuilder.DropColumn(
                name: "DecisionAt",
                schema: "ozem",
                table: "LeaveRequests");

            migrationBuilder.DropColumn(
                name: "DecisionByName",
                schema: "ozem",
                table: "LeaveRequests");

            migrationBuilder.DropColumn(
                name: "DecisionByUserId",
                schema: "ozem",
                table: "LeaveRequests");

            migrationBuilder.DropColumn(
                name: "DecisionNote",
                schema: "ozem",
                table: "LeaveRequests");

            migrationBuilder.DropColumn(
                name: "SaturdayExcluded",
                schema: "ozem",
                table: "LeaveRequests");

            migrationBuilder.DropColumn(
                name: "TotalDays",
                schema: "ozem",
                table: "LeaveRequests");

            migrationBuilder.DropColumn(
                name: "Departman",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "DogumTarihi",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "IseGirisTarihi",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "IstenCikisTarihi",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "TcKimlikNo",
                table: "AspNetUsers");
        }
    }
}
