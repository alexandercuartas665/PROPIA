using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAgentDispatcher : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ia_pausada_at",
                table: "conversations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ai_agent_run_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    conversation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    agent_id = table.Column<Guid>(type: "uuid", nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    content = table.Column<string>(type: "text", nullable: true),
                    response = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_agent_run_logs", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ai_agent_run_logs_tenant_id_agent_id",
                table: "ai_agent_run_logs",
                columns: new[] { "tenant_id", "agent_id" });

            migrationBuilder.CreateIndex(
                name: "IX_ai_agent_run_logs_tenant_id_conversation_id_occurred_at",
                table: "ai_agent_run_logs",
                columns: new[] { "tenant_id", "conversation_id", "occurred_at" });

            // RLS por tenant (mismo patron que el resto del modelo). La bitacora la escribe el
            // dispatcher con el tenant fijado en contexto, asi que la policy la deja pasar.
            migrationBuilder.Sql(@"
                ALTER TABLE ai_agent_run_logs ENABLE ROW LEVEL SECURITY;
                ALTER TABLE ai_agent_run_logs FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON ai_agent_run_logs
                    USING (tenant_id = current_tenant_id())
                    WITH CHECK (tenant_id = current_tenant_id());
                GRANT SELECT, INSERT, UPDATE, DELETE ON ai_agent_run_logs TO propia_app;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ai_agent_run_logs");

            migrationBuilder.DropColumn(
                name: "ia_pausada_at",
                table: "conversations");
        }
    }
}
