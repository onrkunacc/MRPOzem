using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CRMOzem.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class IkiAsamaliIzin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserSupervisors_UserId",
                schema: "ozem",
                table: "UserSupervisors");

            migrationBuilder.DropIndex(
                name: "IX_LeaveRequests_StatusId",
                schema: "ozem",
                table: "LeaveRequests");

            migrationBuilder.DropIndex(
                name: "IX_LeaveRequests_UserId",
                schema: "ozem",
                table: "LeaveRequests");

            migrationBuilder.AddColumn<DateTime>(
                name: "AtandiTarih",
                schema: "ozem",
                table: "UserSupervisors",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "AtayanAd",
                schema: "ozem",
                table: "UserSupervisors",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AtayanUserId",
                schema: "ozem",
                table: "UserSupervisors",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SupervisorDecisionAt",
                schema: "ozem",
                table: "LeaveRequests",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SupervisorName",
                schema: "ozem",
                table: "LeaveRequests",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SupervisorNote",
                schema: "ozem",
                table: "LeaveRequests",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "SupervisorOnayladi",
                schema: "ozem",
                table: "LeaveRequests",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SupervisorUserId",
                schema: "ozem",
                table: "LeaveRequests",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "YoneticisizGecildi",
                schema: "ozem",
                table: "LeaveRequests",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_UserSupervisors_UserId",
                schema: "ozem",
                table: "UserSupervisors",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LeaveRequests_StatusId_StartDate",
                schema: "ozem",
                table: "LeaveRequests",
                columns: new[] { "StatusId", "StartDate" });

            migrationBuilder.CreateIndex(
                name: "IX_LeaveRequests_SupervisorUserId",
                schema: "ozem",
                table: "LeaveRequests",
                column: "SupervisorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_LeaveRequests_UserId_StartDate",
                schema: "ozem",
                table: "LeaveRequests",
                columns: new[] { "UserId", "StartDate" });

            migrationBuilder.Sql(@"
                DECLARE @yon int = (SELECT Id FROM ozem.LeaveStatuses WHERE Code = 'Pending');
                DECLARE @ik  int = (SELECT Id FROM ozem.LeaveStatuses WHERE Code = 'SupervisorApproved');

                IF @ik IS NULL
                BEGIN
                    INSERT INTO ozem.LeaveStatuses (Name, Code, Description)
                    VALUES (N'İK Onayında', 'SupervisorApproved',
                            N'Yönetici onayladı, İnsan Kaynakları''nın nihai onayı bekleniyor.');
                    SET @ik = SCOPE_IDENTITY();
                END

                UPDATE ozem.LeaveStatuses
                   SET Name = N'Yönetici Onayında'
                 WHERE Code = 'Pending';

                IF @yon IS NOT NULL
                    UPDATE ozem.LeaveRequests
                       SET StatusId = @ik, YoneticisizGecildi = 1
                     WHERE StatusId = @yon AND SupervisorUserId IS NULL;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserSupervisors_UserId",
                schema: "ozem",
                table: "UserSupervisors");

            migrationBuilder.DropIndex(
                name: "IX_LeaveRequests_StatusId_StartDate",
                schema: "ozem",
                table: "LeaveRequests");

            migrationBuilder.DropIndex(
                name: "IX_LeaveRequests_SupervisorUserId",
                schema: "ozem",
                table: "LeaveRequests");

            migrationBuilder.DropIndex(
                name: "IX_LeaveRequests_UserId_StartDate",
                schema: "ozem",
                table: "LeaveRequests");

            migrationBuilder.DropColumn(
                name: "AtandiTarih",
                schema: "ozem",
                table: "UserSupervisors");

            migrationBuilder.DropColumn(
                name: "AtayanAd",
                schema: "ozem",
                table: "UserSupervisors");

            migrationBuilder.DropColumn(
                name: "AtayanUserId",
                schema: "ozem",
                table: "UserSupervisors");

            migrationBuilder.DropColumn(
                name: "SupervisorDecisionAt",
                schema: "ozem",
                table: "LeaveRequests");

            migrationBuilder.DropColumn(
                name: "SupervisorName",
                schema: "ozem",
                table: "LeaveRequests");

            migrationBuilder.DropColumn(
                name: "SupervisorNote",
                schema: "ozem",
                table: "LeaveRequests");

            migrationBuilder.DropColumn(
                name: "SupervisorOnayladi",
                schema: "ozem",
                table: "LeaveRequests");

            migrationBuilder.DropColumn(
                name: "SupervisorUserId",
                schema: "ozem",
                table: "LeaveRequests");

            migrationBuilder.DropColumn(
                name: "YoneticisizGecildi",
                schema: "ozem",
                table: "LeaveRequests");

            migrationBuilder.CreateIndex(
                name: "IX_UserSupervisors_UserId",
                schema: "ozem",
                table: "UserSupervisors",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_LeaveRequests_StatusId",
                schema: "ozem",
                table: "LeaveRequests",
                column: "StatusId");

            migrationBuilder.CreateIndex(
                name: "IX_LeaveRequests_UserId",
                schema: "ozem",
                table: "LeaveRequests",
                column: "UserId");
        }
    }
}
