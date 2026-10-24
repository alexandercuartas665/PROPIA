using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFoldersDocumentos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "folders",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    parent_id = table.Column<Guid>(type: "uuid", nullable: true),
                    nombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    codigo = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    descripcion = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    orden = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_folders", x => x.id);
                    table.ForeignKey(
                        name: "FK_folders_folders_parent_id",
                        column: x => x.parent_id,
                        principalTable: "folders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "folder_documentos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    folder_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    orden = table.Column<int>(type: "integer", nullable: false),
                    obligatorio = table.Column<bool>(type: "boolean", nullable: false),
                    archivado = table.Column<bool>(type: "boolean", nullable: false),
                    archivado_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    archivo_url = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    archivo_nombre = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    archivo_mime = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    archivo_tamano = table.Column<long>(type: "bigint", nullable: false),
                    numero_versiones = table.Column<int>(type: "integer", nullable: false),
                    subido_por_usuario_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_folder_documentos", x => x.id);
                    table.ForeignKey(
                        name: "FK_folder_documentos_folders_folder_id",
                        column: x => x.folder_id,
                        principalTable: "folders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "folder_campo_definiciones",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    folder_id = table.Column<Guid>(type: "uuid", nullable: true),
                    documento_id = table.Column<Guid>(type: "uuid", nullable: true),
                    label = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    tipo = table.Column<int>(type: "integer", nullable: false),
                    opciones = table.Column<string>(type: "text", nullable: true),
                    descripcion = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    obligatorio = table.Column<bool>(type: "boolean", nullable: false),
                    orden = table.Column<int>(type: "integer", nullable: false),
                    decimales = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_folder_campo_definiciones", x => x.id);
                    table.CheckConstraint("ck_folder_campo_definiciones_dueno", "(folder_id IS NOT NULL AND documento_id IS NULL) OR (folder_id IS NULL AND documento_id IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_folder_campo_definiciones_folder_documentos_documento_id",
                        column: x => x.documento_id,
                        principalTable: "folder_documentos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_folder_campo_definiciones_folders_folder_id",
                        column: x => x.folder_id,
                        principalTable: "folders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "folder_documento_versiones",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    documento_id = table.Column<Guid>(type: "uuid", nullable: false),
                    numero = table.Column<int>(type: "integer", nullable: false),
                    archivo_url = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    archivo_nombre = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    archivo_mime = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    archivo_tamano = table.Column<long>(type: "bigint", nullable: false),
                    hash_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    notas_cambio = table.Column<string>(type: "text", nullable: true),
                    subido_por_usuario_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_folder_documento_versiones", x => x.id);
                    table.ForeignKey(
                        name: "FK_folder_documento_versiones_folder_documentos_documento_id",
                        column: x => x.documento_id,
                        principalTable: "folder_documentos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "folder_documento_campo_valores",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    documento_id = table.Column<Guid>(type: "uuid", nullable: false),
                    definicion_id = table.Column<Guid>(type: "uuid", nullable: false),
                    valor = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_folder_documento_campo_valores", x => x.id);
                    table.ForeignKey(
                        name: "FK_folder_documento_campo_valores_folder_campo_definiciones_de~",
                        column: x => x.definicion_id,
                        principalTable: "folder_campo_definiciones",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_folder_documento_campo_valores_folder_documentos_documento_~",
                        column: x => x.documento_id,
                        principalTable: "folder_documentos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_folder_campo_definiciones_documento_id",
                table: "folder_campo_definiciones",
                column: "documento_id");

            migrationBuilder.CreateIndex(
                name: "IX_folder_campo_definiciones_folder_id",
                table: "folder_campo_definiciones",
                column: "folder_id");

            migrationBuilder.CreateIndex(
                name: "IX_folder_campo_definiciones_tenant_id_documento_id_orden",
                table: "folder_campo_definiciones",
                columns: new[] { "tenant_id", "documento_id", "orden" });

            migrationBuilder.CreateIndex(
                name: "IX_folder_campo_definiciones_tenant_id_folder_id_orden",
                table: "folder_campo_definiciones",
                columns: new[] { "tenant_id", "folder_id", "orden" });

            migrationBuilder.CreateIndex(
                name: "IX_folder_documento_campo_valores_definicion_id",
                table: "folder_documento_campo_valores",
                column: "definicion_id");

            migrationBuilder.CreateIndex(
                name: "IX_folder_documento_campo_valores_documento_id_definicion_id",
                table: "folder_documento_campo_valores",
                columns: new[] { "documento_id", "definicion_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_folder_documento_campo_valores_tenant_id",
                table: "folder_documento_campo_valores",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_folder_documento_versiones_documento_id_numero",
                table: "folder_documento_versiones",
                columns: new[] { "documento_id", "numero" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_folder_documento_versiones_tenant_id",
                table: "folder_documento_versiones",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_folder_documentos_folder_id",
                table: "folder_documentos",
                column: "folder_id");

            migrationBuilder.CreateIndex(
                name: "IX_folder_documentos_tenant_id_folder_id_archivado_orden",
                table: "folder_documentos",
                columns: new[] { "tenant_id", "folder_id", "archivado", "orden" });

            migrationBuilder.CreateIndex(
                name: "IX_folders_parent_id",
                table: "folders",
                column: "parent_id");

            migrationBuilder.CreateIndex(
                name: "IX_folders_tenant_id_codigo",
                table: "folders",
                columns: new[] { "tenant_id", "codigo" });

            migrationBuilder.CreateIndex(
                name: "IX_folders_tenant_id_parent_id_orden",
                table: "folders",
                columns: new[] { "tenant_id", "parent_id", "orden" });

            // ------------------------------------------------------------------
            // Copia de datos: TRD/Expedientes (v1) -> Folders (v2), conservando Ids.
            //   serie            -> folder raiz
            //   subserie         -> subfolder de la serie
            //   expediente       -> subfolder de la subserie (match por nombre; si no, de la serie; si no, raiz)
            //   subserie_tipologia / expediente_tipologia -> documento del folder (con archivo y versiones)
            //   subserie_campo / expediente_campo -> metadato Texto del folder
            //   meta_json de la tipologia -> valores por documento
            // Las tablas origen tienen RLS FORCE: el owner tambien queda sujeto a la policy y leeria 0 filas.
            // Se quita FORCE solo durante la copia (misma transaccion) y se restaura al final.
            // Las tablas v1 NO se borran aqui (la UI vieja sigue leyendolas hasta que entre la v2).
            // ------------------------------------------------------------------
            migrationBuilder.Sql(@"
ALTER TABLE series_documentales NO FORCE ROW LEVEL SECURITY;
ALTER TABLE subseries_documentales NO FORCE ROW LEVEL SECURITY;
ALTER TABLE subserie_tipologias NO FORCE ROW LEVEL SECURITY;
ALTER TABLE subserie_campos NO FORCE ROW LEVEL SECURITY;
ALTER TABLE expedientes NO FORCE ROW LEVEL SECURITY;
ALTER TABLE expediente_tipologias NO FORCE ROW LEVEL SECURITY;
ALTER TABLE expediente_tipologia_versiones NO FORCE ROW LEVEL SECURITY;
ALTER TABLE expediente_campos NO FORCE ROW LEVEL SECURITY;

INSERT INTO folders (id, tenant_id, parent_id, nombre, codigo, descripcion, orden, created_at, created_by, updated_at, updated_by)
SELECT s.id, s.tenant_id, NULL, s.nombre, NULL, NULL, s.orden, s.created_at, s.created_by, s.updated_at, s.updated_by
FROM series_documentales s;

INSERT INTO folders (id, tenant_id, parent_id, nombre, codigo, descripcion, orden, created_at, created_by, updated_at, updated_by)
SELECT ss.id, ss.tenant_id, ss.serie_id, ss.nombre, NULL, NULL, ss.orden, ss.created_at, ss.created_by, ss.updated_at, ss.updated_by
FROM subseries_documentales ss;

INSERT INTO folders (id, tenant_id, parent_id, nombre, codigo, descripcion, orden, created_at, created_by, updated_at, updated_by)
SELECT e.id, e.tenant_id,
       COALESCE(
         (SELECT ss.id FROM subseries_documentales ss
            JOIN series_documentales s ON s.id = ss.serie_id
           WHERE ss.tenant_id = e.tenant_id AND ss.nombre = e.subserie AND s.nombre = e.serie
           ORDER BY ss.orden LIMIT 1),
         (SELECT s.id FROM series_documentales s WHERE s.tenant_id = e.tenant_id AND s.nombre = e.serie ORDER BY s.orden LIMIT 1)),
       e.nombre, e.codigo, NULL,
       1000 + ROW_NUMBER() OVER (PARTITION BY e.tenant_id, e.serie, e.subserie ORDER BY e.created_at),
       e.created_at, e.created_by, e.updated_at, e.updated_by
FROM expedientes e;

INSERT INTO folder_documentos (id, tenant_id, folder_id, nombre, orden, obligatorio, archivado, archivado_at, archivo_url, archivo_nombre, archivo_mime, archivo_tamano, numero_versiones, subido_por_usuario_id, created_at, created_by, updated_at, updated_by)
SELECT t.id, t.tenant_id, t.subserie_id, t.nombre, t.orden, (t.orden < 2), false, NULL, NULL, NULL, NULL, 0, 0, NULL, t.created_at, t.created_by, t.updated_at, t.updated_by
FROM subserie_tipologias t;

INSERT INTO folder_documentos (id, tenant_id, folder_id, nombre, orden, obligatorio, archivado, archivado_at, archivo_url, archivo_nombre, archivo_mime, archivo_tamano, numero_versiones, subido_por_usuario_id, created_at, created_by, updated_at, updated_by)
SELECT t.id, t.tenant_id, t.expediente_id, t.nombre, t.orden, t.obligatoria, false, NULL, t.archivo_url, t.archivo_nombre, t.archivo_mime, t.archivo_tamano,
       GREATEST(t.numero_versiones, CASE WHEN t.archivo_url IS NOT NULL THEN 1 ELSE 0 END), NULL, t.created_at, t.created_by, t.updated_at, t.updated_by
FROM expediente_tipologias t;

INSERT INTO folder_documento_versiones (id, tenant_id, documento_id, numero, archivo_url, archivo_nombre, archivo_mime, archivo_tamano, hash_sha256, notas_cambio, subido_por_usuario_id, created_at, created_by, updated_at, updated_by)
SELECT v.id, v.tenant_id, v.expediente_tipologia_id, v.numero, v.archivo_url, v.archivo_nombre, v.archivo_mime, v.archivo_tamano, NULL, v.notas_cambio, v.subido_por_usuario_id, v.created_at, v.created_by, v.updated_at, v.updated_by
FROM expediente_tipologia_versiones v;

-- Tipologias con archivo pero sin fila de version (cargadas antes del versionado): se crea la v1 desde el archivo actual.
INSERT INTO folder_documento_versiones (id, tenant_id, documento_id, numero, archivo_url, archivo_nombre, archivo_mime, archivo_tamano, hash_sha256, notas_cambio, subido_por_usuario_id, created_at, created_by, updated_at, updated_by)
SELECT gen_random_uuid(), t.tenant_id, t.id, 1, t.archivo_url, COALESCE(t.archivo_nombre, t.nombre), COALESCE(t.archivo_mime, 'application/octet-stream'), t.archivo_tamano, NULL, 'Migrado desde Expedientes', NULL, COALESCE(t.updated_at, t.created_at), t.created_by, NULL, NULL
FROM expediente_tipologias t
WHERE t.archivo_url IS NOT NULL
  AND NOT EXISTS (SELECT 1 FROM expediente_tipologia_versiones v WHERE v.expediente_tipologia_id = t.id);

INSERT INTO folder_campo_definiciones (id, tenant_id, folder_id, documento_id, label, tipo, opciones, descripcion, obligatorio, orden, decimales, created_at, created_by, updated_at, updated_by)
SELECT c.id, c.tenant_id, c.subserie_id, NULL, c.label, 0, NULL, NULL, false, c.orden, NULL, c.created_at, c.created_by, c.updated_at, c.updated_by
FROM subserie_campos c;

-- Campos del expediente: en v1 eran COPIA de los de la subserie. En v2 el folder del expediente ya los HEREDA
-- de su padre, asi que solo se migran los que no existen (mismo label) en el folder padre.
INSERT INTO folder_campo_definiciones (id, tenant_id, folder_id, documento_id, label, tipo, opciones, descripcion, obligatorio, orden, decimales, created_at, created_by, updated_at, updated_by)
SELECT c.id, c.tenant_id, c.expediente_id, NULL, c.label, 0, NULL, NULL, false, c.orden, NULL, c.created_at, c.created_by, c.updated_at, c.updated_by
FROM expediente_campos c
JOIN folders fe ON fe.id = c.expediente_id
WHERE NOT EXISTS (
    SELECT 1 FROM subserie_campos sc
    WHERE sc.subserie_id = fe.parent_id AND lower(sc.label) = lower(c.label));

-- Valores (meta_json clave -> valor): se cuelgan del campo heredado del padre si existe; si no, del propio.
INSERT INTO folder_documento_campo_valores (id, tenant_id, documento_id, definicion_id, valor, created_at, created_by, updated_at, updated_by)
SELECT gen_random_uuid(), t.tenant_id, t.id,
       COALESCE((SELECT sc.id FROM subserie_campos sc WHERE sc.subserie_id = fe.parent_id AND lower(sc.label) = lower(c.label) ORDER BY sc.orden LIMIT 1), c.id),
       kv.value, COALESCE(t.updated_at, t.created_at), t.created_by, NULL, NULL
FROM expediente_tipologias t
JOIN folders fe ON fe.id = t.expediente_id
JOIN LATERAL jsonb_each_text(t.meta_json::jsonb) kv ON true
JOIN expediente_campos c ON c.expediente_id = t.expediente_id AND c.clave = kv.key
WHERE t.meta_json IS NOT NULL AND t.meta_json <> '' AND kv.value IS NOT NULL AND kv.value <> ''
ON CONFLICT (documento_id, definicion_id) DO NOTHING;

-- Tildes: la TRD base v1 se sembro en ASCII y el backfill era perezoso (al entrar al modulo). Los folders y
-- documentos migrados reciben aqui los nombres con ortografia correcta (misma lista que BackfillTildesTrdAsync).
UPDATE folders SET nombre = 'Legal y constitución' WHERE nombre = 'Legal y constitucion';
UPDATE folders SET nombre = 'Pólizas y seguros' WHERE nombre = 'Polizas y seguros';
UPDATE folders SET nombre = 'Constitución' WHERE nombre = 'Constitucion';
UPDATE folder_documentos SET nombre = 'Citación' WHERE nombre = 'Citacion';
UPDATE folder_documentos SET nombre = 'Pólizas' WHERE nombre = 'Polizas';
UPDATE folder_documentos SET nombre = 'Cámara de comercio' WHERE nombre = 'Camara de comercio';
UPDATE folder_documentos SET nombre = 'Afiliación seguridad social' WHERE nombre = 'Afiliacion seguridad social';
UPDATE folder_documentos SET nombre = 'Ejecución' WHERE nombre = 'Ejecucion';
UPDATE folder_documentos SET nombre = 'Póliza áreas comunes' WHERE nombre = 'Poliza areas comunes';
UPDATE folder_documentos SET nombre = 'Póliza de manejo' WHERE nombre = 'Poliza de manejo';

ALTER TABLE series_documentales FORCE ROW LEVEL SECURITY;
ALTER TABLE subseries_documentales FORCE ROW LEVEL SECURITY;
ALTER TABLE subserie_tipologias FORCE ROW LEVEL SECURITY;
ALTER TABLE subserie_campos FORCE ROW LEVEL SECURITY;
ALTER TABLE expedientes FORCE ROW LEVEL SECURITY;
ALTER TABLE expediente_tipologias FORCE ROW LEVEL SECURITY;
ALTER TABLE expediente_tipologia_versiones FORCE ROW LEVEL SECURITY;
ALTER TABLE expediente_campos FORCE ROW LEVEL SECURITY;
");

            // RLS (aislamiento por tenant) igual que el resto de tablas tenant-scoped. Se activa DESPUES de la copia.
            foreach (var tabla in new[] { "folders", "folder_documentos", "folder_documento_versiones", "folder_campo_definiciones", "folder_documento_campo_valores" })
            {
                migrationBuilder.Sql($@"
                    ALTER TABLE {tabla} ENABLE ROW LEVEL SECURITY;
                    ALTER TABLE {tabla} FORCE ROW LEVEL SECURITY;
                    CREATE POLICY tenant_isolation ON {tabla}
                        USING (tenant_id = current_tenant_id())
                        WITH CHECK (tenant_id = current_tenant_id());
                    GRANT SELECT, INSERT, UPDATE, DELETE ON {tabla} TO propia_app;
                ");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "folder_documento_campo_valores");

            migrationBuilder.DropTable(
                name: "folder_documento_versiones");

            migrationBuilder.DropTable(
                name: "folder_campo_definiciones");

            migrationBuilder.DropTable(
                name: "folder_documentos");

            migrationBuilder.DropTable(
                name: "folders");
        }
    }
}
