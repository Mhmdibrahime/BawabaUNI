using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BawabaUNI.Migrations
{
    /// <inheritdoc />
    public partial class Visits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ConsultationRequests_AspNetUsers_AssignedToUserId",
                table: "ConsultationRequests");

            migrationBuilder.DropIndex(
                name: "IX_ConsultationRequests_AssignedToUserId",
                table: "ConsultationRequests");

            migrationBuilder.DropColumn(
                name: "AssignedAt",
                table: "ConsultationRequests");

            migrationBuilder.DropColumn(
                name: "AssignedToUserId",
                table: "ConsultationRequests");

            migrationBuilder.DropColumn(
                name: "CompletedAt",
                table: "ConsultationRequests");

            migrationBuilder.CreateTable(
                name: "Visits",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    NumberOfVisits = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Visits", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Visits");

            migrationBuilder.AddColumn<DateTime>(
                name: "AssignedAt",
                table: "ConsultationRequests",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AssignedToUserId",
                table: "ConsultationRequests",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CompletedAt",
                table: "ConsultationRequests",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConsultationRequests_AssignedToUserId",
                table: "ConsultationRequests",
                column: "AssignedToUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_ConsultationRequests_AspNetUsers_AssignedToUserId",
                table: "ConsultationRequests",
                column: "AssignedToUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");
        }
    }
}
