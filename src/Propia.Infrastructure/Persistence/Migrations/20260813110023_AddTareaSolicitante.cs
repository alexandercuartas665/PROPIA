using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTareaSolicitante : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "solicitante_persona_id",
                table: "tareas",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_tareas_solicitante_persona_id",
                table: "tareas",
                column: "solicitante_persona_id");

            migrationBuilder.AddForeignKey(
                name: "FK_tareas_personas_solicitante_persona_id",
                table: "tareas",
                column: "solicitante_persona_id",
                principalTable: "personas",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_tareas_personas_solicitante_persona_id",
                table: "tareas");

            migrationBuilder.DropIndex(
                name: "IX_tareas_solicitante_persona_id",
                table: "tareas");

            migrationBuilder.DropColumn(
                name: "solicitante_persona_id",
                table: "tareas");
        }
    }
}
