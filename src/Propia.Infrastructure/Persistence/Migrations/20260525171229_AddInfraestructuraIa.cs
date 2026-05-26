using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInfraestructuraIa : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ai_agents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    role = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    provider = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    model = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    system_prompt = table.Column<string>(type: "text", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_agents", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ai_usage_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    agent_id = table.Column<Guid>(type: "uuid", nullable: true),
                    provider = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    model = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    input_tokens = table.Column<int>(type: "integer", nullable: false),
                    output_tokens = table.Column<int>(type: "integer", nullable: false),
                    total_tokens = table.Column<int>(type: "integer", nullable: false),
                    estimated_cost_usd = table.Column<decimal>(type: "numeric(12,6)", precision: 12, scale: 6, nullable: false),
                    source = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    success = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_usage_logs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "whats_app_lines",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    instance_name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    phone_number = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    assigned_to_usuario_tenant_id = table.Column<Guid>(type: "uuid", nullable: true),
                    last_connected_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_status_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_whats_app_lines", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ai_agent_prompts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    agent_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    rule = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    body = table.Column<string>(type: "text", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_agent_prompts", x => x.id);
                    table.ForeignKey(
                        name: "FK_ai_agent_prompts_ai_agents_agent_id",
                        column: x => x.agent_id,
                        principalTable: "ai_agents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ai_agent_resources",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    agent_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    resource_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    detail = table.Column<string>(type: "text", nullable: true),
                    file_url = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    file_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_agent_resources", x => x.id);
                    table.ForeignKey(
                        name: "FK_ai_agent_resources_ai_agents_agent_id",
                        column: x => x.agent_id,
                        principalTable: "ai_agents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "conversations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    contact_phone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    contact_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    whats_app_line_id = table.Column<Guid>(type: "uuid", nullable: true),
                    persona_id = table.Column<Guid>(type: "uuid", nullable: true),
                    last_message_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_conversations", x => x.id);
                    table.ForeignKey(
                        name: "FK_conversations_personas_persona_id",
                        column: x => x.persona_id,
                        principalTable: "personas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_conversations_whats_app_lines_whats_app_line_id",
                        column: x => x.whats_app_line_id,
                        principalTable: "whats_app_lines",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "messages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    conversation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    direction = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    external_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    body = table.Column<string>(type: "text", nullable: false),
                    message_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    sent_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    sent_by_usuario_tenant_id = table.Column<Guid>(type: "uuid", nullable: true),
                    sent_by_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    media_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    media_url = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    media_mime_type = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_messages", x => x.id);
                    table.ForeignKey(
                        name: "FK_messages_conversations_conversation_id",
                        column: x => x.conversation_id,
                        principalTable: "conversations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ai_agent_prompts_agent_id",
                table: "ai_agent_prompts",
                column: "agent_id");

            migrationBuilder.CreateIndex(
                name: "IX_ai_agent_prompts_tenant_id_agent_id",
                table: "ai_agent_prompts",
                columns: new[] { "tenant_id", "agent_id" });

            migrationBuilder.CreateIndex(
                name: "IX_ai_agent_resources_agent_id",
                table: "ai_agent_resources",
                column: "agent_id");

            migrationBuilder.CreateIndex(
                name: "IX_ai_agent_resources_tenant_id_agent_id",
                table: "ai_agent_resources",
                columns: new[] { "tenant_id", "agent_id" });

            migrationBuilder.CreateIndex(
                name: "IX_ai_agents_tenant_id_is_active",
                table: "ai_agents",
                columns: new[] { "tenant_id", "is_active" });

            migrationBuilder.CreateIndex(
                name: "IX_ai_usage_logs_tenant_id_agent_id",
                table: "ai_usage_logs",
                columns: new[] { "tenant_id", "agent_id" });

            migrationBuilder.CreateIndex(
                name: "IX_ai_usage_logs_tenant_id_created_at",
                table: "ai_usage_logs",
                columns: new[] { "tenant_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_conversations_persona_id",
                table: "conversations",
                column: "persona_id");

            migrationBuilder.CreateIndex(
                name: "IX_conversations_tenant_id_contact_phone",
                table: "conversations",
                columns: new[] { "tenant_id", "contact_phone" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_conversations_tenant_id_last_message_at",
                table: "conversations",
                columns: new[] { "tenant_id", "last_message_at" });

            migrationBuilder.CreateIndex(
                name: "IX_conversations_whats_app_line_id",
                table: "conversations",
                column: "whats_app_line_id");

            migrationBuilder.CreateIndex(
                name: "IX_messages_conversation_id_sent_at",
                table: "messages",
                columns: new[] { "conversation_id", "sent_at" });

            migrationBuilder.CreateIndex(
                name: "IX_messages_tenant_id_external_id",
                table: "messages",
                columns: new[] { "tenant_id", "external_id" },
                unique: true,
                filter: "external_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_whats_app_lines_tenant_id_instance_name",
                table: "whats_app_lines",
                columns: new[] { "tenant_id", "instance_name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_whats_app_lines_tenant_id_status",
                table: "whats_app_lines",
                columns: new[] { "tenant_id", "status" });

            // RLS: las 7 tablas son TenantEntity. Mismo patron que el resto de Capa 2
            // (ENABLE + FORCE + POLICY tenant_isolation via current_tenant_id()).
            var tablas = new[]
            {
                "whats_app_lines",
                "conversations",
                "messages",
                "ai_agents",
                "ai_agent_prompts",
                "ai_agent_resources",
                "ai_usage_logs"
            };
            foreach (var tabla in tablas)
            {
                migrationBuilder.Sql($@"
ALTER TABLE {tabla} ENABLE ROW LEVEL SECURITY;
ALTER TABLE {tabla} FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON {tabla}
    USING (tenant_id = current_tenant_id())
    WITH CHECK (tenant_id = current_tenant_id());
");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ai_agent_prompts");

            migrationBuilder.DropTable(
                name: "ai_agent_resources");

            migrationBuilder.DropTable(
                name: "ai_usage_logs");

            migrationBuilder.DropTable(
                name: "messages");

            migrationBuilder.DropTable(
                name: "ai_agents");

            migrationBuilder.DropTable(
                name: "conversations");

            migrationBuilder.DropTable(
                name: "whats_app_lines");
        }
    }
}
