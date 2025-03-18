using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRMOzem.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddNewFollowDocument : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CargoCompany",
                table: "FollowDocuments",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeliveryMethod",
                table: "FollowDocuments",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "IsSent",
                table: "FollowDocuments",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "FollowDocuments",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SentDate",
                table: "FollowDocuments",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CargoCompany",
                table: "FollowDocuments");

            migrationBuilder.DropColumn(
                name: "DeliveryMethod",
                table: "FollowDocuments");

            migrationBuilder.DropColumn(
                name: "IsSent",
                table: "FollowDocuments");

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "FollowDocuments");

            migrationBuilder.DropColumn(
                name: "SentDate",
                table: "FollowDocuments");
        }
    }
}
