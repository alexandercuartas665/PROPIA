using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWhatsAppCloudProvider : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "cloud_access_token_encrypted",
                table: "whats_app_lines",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "cloud_business_account_id",
                table: "whats_app_lines",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "cloud_phone_number_id",
                table: "whats_app_lines",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "cloud_webhook_verify_token_encrypted",
                table: "whats_app_lines",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "provider",
                table: "whats_app_lines",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "cloud_access_token_encrypted",
                table: "whats_app_lines");

            migrationBuilder.DropColumn(
                name: "cloud_business_account_id",
                table: "whats_app_lines");

            migrationBuilder.DropColumn(
                name: "cloud_phone_number_id",
                table: "whats_app_lines");

            migrationBuilder.DropColumn(
                name: "cloud_webhook_verify_token_encrypted",
                table: "whats_app_lines");

            migrationBuilder.DropColumn(
                name: "provider",
                table: "whats_app_lines");
        }
    }
}
