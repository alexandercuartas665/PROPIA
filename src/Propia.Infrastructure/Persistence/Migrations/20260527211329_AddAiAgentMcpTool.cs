using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAiAgentMcpTool : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ai_agent_mcp_tools",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    agent_id = table.Column<Guid>(type: "uuid", nullable: false),
                    connection_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    tool_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_agent_mcp_tools", x => x.id);
                    table.ForeignKey(
                        name: "FK_ai_agent_mcp_tools_ai_agents_agent_id",
                        column: x => x.agent_id,
                        principalTable: "ai_agents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ai_agent_mcp_tools_agent_id_connection_code_tool_name",
                table: "ai_agent_mcp_tools",
                columns: new[] { "agent_id", "connection_code", "tool_name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ai_agent_mcp_tools_tenant_id_agent_id",
                table: "ai_agent_mcp_tools",
                columns: new[] { "tenant_id", "agent_id" });

            migrationBuilder.Sql(@"
                ALTER TABLE ai_agent_mcp_tools ENABLE ROW LEVEL SECURITY;
                ALTER TABLE ai_agent_mcp_tools FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON ai_agent_mcp_tools
                    USING (tenant_id = current_tenant_id())
                    WITH CHECK (tenant_id = current_tenant_id());
                GRANT SELECT, INSERT, UPDATE, DELETE ON ai_agent_mcp_tools TO propia_app;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ai_agent_mcp_tools");
        }
    }
}
