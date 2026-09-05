using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentExtractionLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "document_extraction_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: true),
                    modulo = table.Column<string>(type: "text", nullable: false),
                    provider = table.Column<string>(type: "text", nullable: false),
                    model = table.Column<string>(type: "text", nullable: true),
                    nombre_archivo = table.Column<string>(type: "text", nullable: true),
                    mime_type = table.Column<string>(type: "text", nullable: true),
                    size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    ok = table.Column<bool>(type: "boolean", nullable: false),
                    error = table.Column<string>(type: "text", nullable: true),
                    latency_ms = table.Column<int>(type: "integer", nullable: false),
                    input_tokens = table.Column<int>(type: "integer", nullable: false),
                    output_tokens = table.Column<int>(type: "integer", nullable: false),
                    campos_json = table.Column<string>(type: "text", nullable: true),
                    raw_response = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_document_extraction_logs", x => x.id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "document_extraction_logs");
        }
    }
}
