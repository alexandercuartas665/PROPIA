using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAgentReaccionesBindings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "reaction",
                table: "messages",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "reaction_emojis",
                table: "ai_agents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "reaction_ratio_m",
                table: "ai_agents",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "reaction_ratio_n",
                table: "ai_agents",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "reactions_enabled",
                table: "ai_agents",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "ai_agent_line_bindings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    agent_id = table.Column<Guid>(type: "uuid", nullable: false),
                    whats_app_line_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_connected = table.Column<bool>(type: "boolean", nullable: false),
                    auto_confirm = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_agent_line_bindings", x => x.id);
                    table.ForeignKey(
                        name: "FK_ai_agent_line_bindings_ai_agents_agent_id",
                        column: x => x.agent_id,
                        principalTable: "ai_agents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ai_agent_line_bindings_whats_app_lines_whats_app_line_id",
                        column: x => x.whats_app_line_id,
                        principalTable: "whats_app_lines",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ai_agent_line_bindings_agent_id",
                table: "ai_agent_line_bindings",
                column: "agent_id");

            migrationBuilder.CreateIndex(
                name: "IX_ai_agent_line_bindings_tenant_id_agent_id",
                table: "ai_agent_line_bindings",
                columns: new[] { "tenant_id", "agent_id" });

            migrationBuilder.CreateIndex(
                name: "IX_ai_agent_line_bindings_tenant_id_whats_app_line_id_is_conne~",
                table: "ai_agent_line_bindings",
                columns: new[] { "tenant_id", "whats_app_line_id", "is_connected" },
                unique: true,
                filter: "is_connected");

            migrationBuilder.CreateIndex(
                name: "IX_ai_agent_line_bindings_whats_app_line_id",
                table: "ai_agent_line_bindings",
                column: "whats_app_line_id");

            // RLS por tenant (mismo patron que el resto del modelo).
            migrationBuilder.Sql(@"
                ALTER TABLE ai_agent_line_bindings ENABLE ROW LEVEL SECURITY;
                ALTER TABLE ai_agent_line_bindings FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON ai_agent_line_bindings
                    USING (tenant_id = current_tenant_id())
                    WITH CHECK (tenant_id = current_tenant_id());
                GRANT SELECT, INSERT, UPDATE, DELETE ON ai_agent_line_bindings TO propia_app;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ai_agent_line_bindings");

            migrationBuilder.DropColumn(
                name: "reaction",
                table: "messages");

            migrationBuilder.DropColumn(
                name: "reaction_emojis",
                table: "ai_agents");

            migrationBuilder.DropColumn(
                name: "reaction_ratio_m",
                table: "ai_agents");

            migrationBuilder.DropColumn(
                name: "reaction_ratio_n",
                table: "ai_agents");

            migrationBuilder.DropColumn(
                name: "reactions_enabled",
                table: "ai_agents");
        }
    }
}
