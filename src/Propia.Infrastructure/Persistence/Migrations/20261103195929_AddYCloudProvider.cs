using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddYCloudProvider : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "y_cloud_api_key_encrypted",
                table: "whats_app_lines",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "y_cloud_from_number",
                table: "whats_app_lines",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "y_cloud_api_key_encrypted",
                table: "whats_app_lines");

            migrationBuilder.DropColumn(
                name: "y_cloud_from_number",
                table: "whats_app_lines");
        }
    }
}
