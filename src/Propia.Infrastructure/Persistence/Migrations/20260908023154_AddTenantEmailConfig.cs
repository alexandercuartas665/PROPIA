using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantEmailConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "tenant_email_configs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    smtp_host = table.Column<string>(type: "text", nullable: true),
                    smtp_port = table.Column<int>(type: "integer", nullable: false),
                    smtp_user = table.Column<string>(type: "text", nullable: true),
                    smtp_password_encrypted = table.Column<string>(type: "text", nullable: true),
                    use_ssl = table.Column<bool>(type: "boolean", nullable: false),
                    from_email = table.Column<string>(type: "text", nullable: true),
                    from_name = table.Column<string>(type: "text", nullable: true),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    last_validated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenant_email_configs", x => x.id);
                });

            // RLS: aislamiento por tenant (red de seguridad final en PostgreSQL). La app corre como
            // propia_app (no bypassa RLS). Mismo patron que el resto de tablas de Capa 2.
            migrationBuilder.Sql(@"
                ALTER TABLE tenant_email_configs ENABLE ROW LEVEL SECURITY;
                ALTER TABLE tenant_email_configs FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON tenant_email_configs
                    USING (tenant_id = current_tenant_id())
                    WITH CHECK (tenant_id = current_tenant_id());
                GRANT SELECT, INSERT, UPDATE, DELETE ON tenant_email_configs TO propia_app;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tenant_email_configs");
        }
    }
}
