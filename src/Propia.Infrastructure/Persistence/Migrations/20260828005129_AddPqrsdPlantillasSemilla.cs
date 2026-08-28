using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPqrsdPlantillasSemilla : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "pqrsd_plantillas_semilla",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    cuerpo_html = table.Column<string>(type: "text", nullable: false),
                    activa = table.Column<bool>(type: "boolean", nullable: false),
                    orden = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pqrsd_plantillas_semilla", x => x.id);
                });

            // Siembra el catalogo global con las 5 plantillas base (idempotente por nombre).
            migrationBuilder.Sql(@"
INSERT INTO pqrsd_plantillas_semilla (id, nombre, cuerpo_html, activa, orden, created_at)
SELECT gen_random_uuid(), x.nombre, x.cuerpo_html, true, x.orden, now()
FROM jsonb_to_recordset('[{""nombre"": ""Acuse de recibo"", ""orden"": 0, ""cuerpo_html"": ""<p><strong>{copropiedad.nombre}</strong></p><p>{fecha.hoy}</p><p>Senor(a) {solicitante.nombre}, identificado(a) con {solicitante.identificacion}.</p><p>Reciba un cordial saludo. Confirmamos la recepcion de su solicitud radicada bajo el numero <strong>{radicado.numero}</strong> del {radicado.fecha}, correspondiente a {radicado.tipo} - {radicado.categoria}. Su solicitud sera atendida dentro de los terminos de ley.</p><p>Atentamente,<br>{gestor.nombre}<br>Administracion {copropiedad.nombre}</p>""}, {""nombre"": ""Respuesta a peticion"", ""orden"": 1, ""cuerpo_html"": ""<p><strong>{copropiedad.nombre}</strong> - NIT {copropiedad.nit}</p><p>{copropiedad.direccion}, {copropiedad.ciudad}</p><p>{fecha.hoy}</p><p>Senor(a) {solicitante.nombre}<br>Unidad {unidad.numero} {unidad.torre}</p><p>Asunto: Respuesta al radicado {radicado.numero}</p><p>En atencion a su {radicado.tipo} radicada el {radicado.fecha}, nos permitimos informar lo siguiente:</p><p>[Escriba aqui la respuesta]</p><p>Cordialmente,<br>{gestor.nombre}<br>Administracion</p>""}, {""nombre"": ""Solicitud de informacion adicional"", ""orden"": 2, ""cuerpo_html"": ""<p>Senor(a) {solicitante.nombre},</p><p>Con relacion a su solicitud <strong>{radicado.numero}</strong>, requerimos la siguiente informacion adicional para continuar con su tramite:</p><ul><li>[Detalle 1]</li><li>[Detalle 2]</li></ul><p>Agradecemos enviar la informacion al correo {solicitante.correo} o comunicarse con la administracion.</p><p>Atentamente,<br>{gestor.nombre}</p>""}, {""nombre"": ""Respuesta a queja o reclamo"", ""orden"": 3, ""cuerpo_html"": ""<p><strong>{copropiedad.nombre}</strong></p><p>{fecha.hoy}</p><p>Senor(a) {solicitante.nombre}, de la unidad {unidad.numero}.</p><p>Hemos revisado su {radicado.tipo} radicada bajo el numero <strong>{radicado.numero}</strong>. Al respecto le informamos las acciones adoptadas:</p><p>[Descripcion de las acciones]</p><p>Quedamos atentos. Cordialmente,<br>{gestor.nombre}<br>Administracion {copropiedad.nombre}</p>""}, {""nombre"": ""Cierre de PQRSD"", ""orden"": 4, ""cuerpo_html"": ""<p>Senor(a) {solicitante.nombre},</p><p>Damos respuesta definitiva y cierre a su solicitud <strong>{radicado.numero}</strong> del {radicado.fecha}. Consideramos atendido su requerimiento; si persiste alguna inquietud puede radicar una nueva solicitud.</p><p>Agradecemos su participacion.<br>{gestor.nombre}<br>Administracion {copropiedad.nombre}</p>""}]'::jsonb) AS x(nombre text, orden int, cuerpo_html text)
WHERE NOT EXISTS (SELECT 1 FROM pqrsd_plantillas_semilla s WHERE s.nombre = x.nombre);
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "pqrsd_plantillas_semilla");
        }
    }
}
