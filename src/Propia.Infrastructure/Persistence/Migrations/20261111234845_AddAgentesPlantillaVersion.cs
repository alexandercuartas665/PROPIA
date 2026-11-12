using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAgentesPlantillaVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "template_id",
                table: "ai_agents",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "template_prompt_hash",
                table: "ai_agents",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "template_version",
                table: "ai_agents",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "clave",
                table: "ai_agent_templates",
                type: "character varying(60)",
                maxLength: 60,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "version",
                table: "ai_agent_templates",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateIndex(
                name: "IX_ai_agents_template_id",
                table: "ai_agents",
                column: "template_id");

            migrationBuilder.CreateIndex(
                name: "IX_ai_agent_templates_clave",
                table: "ai_agent_templates",
                column: "clave",
                unique: true,
                filter: "clave IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_ai_agents_ai_agent_templates_template_id",
                table: "ai_agents",
                column: "template_id",
                principalTable: "ai_agent_templates",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            // Agentes del sistema: clave estable para que el codigo no dependa del nombre (una plantilla por clave).
            migrationBuilder.Sql(@"
UPDATE ai_agent_templates SET clave = 'agente-documental'
 WHERE id = (SELECT id FROM ai_agent_templates WHERE name = 'Agente Documental' AND platform_key IS NULL ORDER BY created_at LIMIT 1);
UPDATE ai_agent_templates SET clave = 'auxiliar-administrativo'
 WHERE id = (SELECT id FROM ai_agent_templates WHERE name = 'Auxiliar Administrativo' AND platform_key IS NULL ORDER BY created_at LIMIT 1);");

            // Backfill: enlaza cada agente de copropiedad con la plantilla de la que salio (por nombre, con los
            // placeholders reemplazados) y guarda la huella del prompt que la plantilla le habria puesto. Si el
            // prompt actual difiere, el agente cuenta como EDITADO por la copropiedad y la actualizacion lo respeta.
            // ai_agents tiene RLS FORCE: el owner la suspende solo durante el UPDATE.
            migrationBuilder.Sql(@"
ALTER TABLE ai_agents NO FORCE ROW LEVEL SECURITY;
UPDATE ai_agents a
   SET template_id = x.template_id,
       template_version = 1,
       template_prompt_hash = encode(sha256(convert_to(x.prompt, 'UTF8')), 'hex')
  FROM (
        SELECT DISTINCT ON (ag.id) ag.id AS agent_id, t.id AS template_id,
               replace(replace(t.system_prompt, '{COPROPIEDAD_NOMBRE}', n.nombre), '{ORGANIZACION_NOMBRE}', coalesce(o.nombre, n.nombre)) AS prompt
          FROM ai_agents ag
          JOIN tenants n ON n.id = ag.tenant_id
          LEFT JOIN organizaciones o ON o.id = n.organizacion_id
          JOIN ai_agent_templates t ON t.platform_key IS NULL
               AND (ag.name = t.name
                    OR ag.name = replace(replace(t.name, '{COPROPIEDAD_NOMBRE}', n.nombre), '{ORGANIZACION_NOMBRE}', coalesce(o.nombre, n.nombre)))
         WHERE ag.template_id IS NULL
         ORDER BY ag.id, t.created_at
       ) x
 WHERE a.id = x.agent_id;
ALTER TABLE ai_agents FORCE ROW LEVEL SECURITY;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ai_agents_ai_agent_templates_template_id",
                table: "ai_agents");

            migrationBuilder.DropIndex(
                name: "IX_ai_agents_template_id",
                table: "ai_agents");

            migrationBuilder.DropIndex(
                name: "IX_ai_agent_templates_clave",
                table: "ai_agent_templates");

            migrationBuilder.DropColumn(
                name: "template_id",
                table: "ai_agents");

            migrationBuilder.DropColumn(
                name: "template_prompt_hash",
                table: "ai_agents");

            migrationBuilder.DropColumn(
                name: "template_version",
                table: "ai_agents");

            migrationBuilder.DropColumn(
                name: "clave",
                table: "ai_agent_templates");

            migrationBuilder.DropColumn(
                name: "version",
                table: "ai_agent_templates");
        }
    }
}
