using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAiAgentCache : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ai_agent_cache_fields",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    agent_id = table.Column<Guid>(type: "uuid", nullable: false),
                    field_key = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    label = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    description = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    is_updatable = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_agent_cache_fields", x => x.id);
                    table.ForeignKey(
                        name: "FK_ai_agent_cache_fields_ai_agents_agent_id",
                        column: x => x.agent_id,
                        principalTable: "ai_agents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ai_agent_cache_values",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    agent_id = table.Column<Guid>(type: "uuid", nullable: false),
                    session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    field_key = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    value = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    source = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_agent_cache_values", x => x.id);
                    table.ForeignKey(
                        name: "FK_ai_agent_cache_values_ai_agents_agent_id",
                        column: x => x.agent_id,
                        principalTable: "ai_agents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ai_agent_cache_fields_agent_id_field_key",
                table: "ai_agent_cache_fields",
                columns: new[] { "agent_id", "field_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ai_agent_cache_fields_tenant_id_agent_id_sort_order",
                table: "ai_agent_cache_fields",
                columns: new[] { "tenant_id", "agent_id", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "IX_ai_agent_cache_values_agent_id_session_id_field_key",
                table: "ai_agent_cache_values",
                columns: new[] { "agent_id", "session_id", "field_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ai_agent_cache_values_tenant_id_agent_id_session_id",
                table: "ai_agent_cache_values",
                columns: new[] { "tenant_id", "agent_id", "session_id" });

            // RLS por tenant (mismo patron que el resto del modelo).
            migrationBuilder.Sql(@"
                ALTER TABLE ai_agent_cache_fields ENABLE ROW LEVEL SECURITY;
                ALTER TABLE ai_agent_cache_fields FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON ai_agent_cache_fields
                    USING (tenant_id = current_tenant_id())
                    WITH CHECK (tenant_id = current_tenant_id());
                GRANT SELECT, INSERT, UPDATE, DELETE ON ai_agent_cache_fields TO propia_app;

                ALTER TABLE ai_agent_cache_values ENABLE ROW LEVEL SECURITY;
                ALTER TABLE ai_agent_cache_values FORCE ROW LEVEL SECURITY;
                CREATE POLICY tenant_isolation ON ai_agent_cache_values
                    USING (tenant_id = current_tenant_id())
                    WITH CHECK (tenant_id = current_tenant_id());
                GRANT SELECT, INSERT, UPDATE, DELETE ON ai_agent_cache_values TO propia_app;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ai_agent_cache_fields");

            migrationBuilder.DropTable(
                name: "ai_agent_cache_values");
        }
    }
}
