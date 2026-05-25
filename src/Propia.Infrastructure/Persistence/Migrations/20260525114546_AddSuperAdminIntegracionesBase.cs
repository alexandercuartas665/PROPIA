using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSuperAdminIntegracionesBase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "email_configs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    smtp_host = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    smtp_port = table.Column<int>(type: "integer", nullable: false),
                    smtp_user = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    smtp_password_encrypted = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    use_ssl = table.Column<bool>(type: "boolean", nullable: false),
                    from_email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    from_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    last_validated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_email_configs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "platform_brandings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    platform_name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    tagline = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    login_logo_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    login_headline = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    login_subtext = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_platform_brandings", x => x.id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "email_configs");

            migrationBuilder.DropTable(
                name: "platform_brandings");
        }
    }
}
