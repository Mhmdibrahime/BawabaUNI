using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BawabaUNI.Migrations
{
    /// <inheritdoc />
    public partial class cons2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AdminNotes",
                table: "ConsultationRequests");

            migrationBuilder.DropColumn(
                name: "RequestType",
                table: "ConsultationRequests");

            migrationBuilder.DropColumn(
                name: "Subject",
                table: "ConsultationRequests");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AdminNotes",
                table: "ConsultationRequests",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "RequestType",
                table: "ConsultationRequests",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Subject",
                table: "ConsultationRequests",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");
        }
    }
}
