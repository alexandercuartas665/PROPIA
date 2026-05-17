using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Propia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Add2_9FestivosColombianos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "festivos_colombianos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    fecha = table.Column<DateOnly>(type: "date", nullable: false),
                    descripcion = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_festivos_colombianos", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_festivos_colombianos_fecha",
                table: "festivos_colombianos",
                column: "fecha",
                unique: true);

            // ============================================================
            // SEED: Festivos colombianos 2024-2032 (Ley 51/1983 + religiosos)
            // ============================================================
            // Festivos fijos: 1 ene, 1 may, 20 jul, 7 ago, 8 dic, 25 dic.
            // Festivos trasladados al lunes siguiente (Ley Emiliani):
            //   6 ene (Reyes Magos), 19 mar (San Jose), 29 jun (San Pedro y San Pablo),
            //   12 oct (Dia de la Raza), 1 nov (Todos los Santos), 11 nov (Independencia Cartagena).
            // Festivos religiosos derivados de Pascua (Domingo de Resurreccion):
            //   Jueves Santo  = Pascua - 3 dias
            //   Viernes Santo = Pascua - 2 dias
            //   Ascension del Senor   = Pascua + 39 dias -> trasladado al lunes siguiente
            //   Corpus Christi        = Pascua + 60 dias -> trasladado al lunes siguiente
            //   Sagrado Corazon       = Pascua + 68 dias -> trasladado al lunes siguiente
            //
            // Generamos via SQL Raw porque MigrationBuilder.InsertData no soporta logica
            // condicional. Inserts idempotentes via ON CONFLICT DO NOTHING.
            var festivos = GenerarFestivos(2024, 2032);
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("INSERT INTO festivos_colombianos (id, fecha, descripcion, created_at) VALUES");
            for (int i = 0; i < festivos.Count; i++)
            {
                var (fecha, desc) = festivos[i];
                sb.Append("  (")
                    .Append("'").Append(Guid.NewGuid()).Append("', ")
                    .Append("'").Append(fecha.ToString("yyyy-MM-dd")).Append("', ")
                    .Append("'").Append(desc.Replace("'", "''")).Append("', ")
                    .Append("CURRENT_TIMESTAMP)")
                    .Append(i == festivos.Count - 1 ? string.Empty : ",")
                    .AppendLine();
            }
            sb.AppendLine("ON CONFLICT (fecha) DO NOTHING;");
            migrationBuilder.Sql(sb.ToString());
        }

        private static System.Collections.Generic.List<(DateOnly, string)> GenerarFestivos(int desde, int hasta)
        {
            var lista = new System.Collections.Generic.List<(DateOnly, string)>();
            for (int y = desde; y <= hasta; y++)
            {
                lista.Add((new DateOnly(y, 1, 1), "Ano Nuevo"));
                lista.Add((new DateOnly(y, 5, 1), "Dia del Trabajo"));
                lista.Add((new DateOnly(y, 7, 20), "Dia de la Independencia"));
                lista.Add((new DateOnly(y, 8, 7), "Batalla de Boyaca"));
                lista.Add((new DateOnly(y, 12, 8), "Inmaculada Concepcion"));
                lista.Add((new DateOnly(y, 12, 25), "Navidad"));

                // Trasladados (Emiliani)
                lista.Add((SiguienteLunes(new DateOnly(y, 1, 6)),  "Reyes Magos (trasladado)"));
                lista.Add((SiguienteLunes(new DateOnly(y, 3, 19)), "San Jose (trasladado)"));
                lista.Add((SiguienteLunes(new DateOnly(y, 6, 29)), "San Pedro y San Pablo (trasladado)"));
                lista.Add((SiguienteLunes(new DateOnly(y, 10, 12)),"Dia de la Raza (trasladado)"));
                lista.Add((SiguienteLunes(new DateOnly(y, 11, 1)), "Todos los Santos (trasladado)"));
                lista.Add((SiguienteLunes(new DateOnly(y, 11, 11)),"Independencia de Cartagena (trasladado)"));

                // Religiosos derivados de Pascua
                var pascua = CalcularPascua(y);
                lista.Add((pascua.AddDays(-3), "Jueves Santo"));
                lista.Add((pascua.AddDays(-2), "Viernes Santo"));
                lista.Add((SiguienteLunes(pascua.AddDays(39)), "Ascension del Senor (trasladado)"));
                lista.Add((SiguienteLunes(pascua.AddDays(60)), "Corpus Christi (trasladado)"));
                lista.Add((SiguienteLunes(pascua.AddDays(68)), "Sagrado Corazon (trasladado)"));
            }
            return lista;
        }

        private static DateOnly SiguienteLunes(DateOnly fecha)
        {
            // Si ya es lunes, queda el mismo. Sino, suma dias hasta llegar al proximo lunes.
            while (fecha.DayOfWeek != DayOfWeek.Monday) fecha = fecha.AddDays(1);
            return fecha;
        }

        /// <summary>Algoritmo de Butcher/Meeus para calcular Domingo de Resurreccion (calendario gregoriano).</summary>
        private static DateOnly CalcularPascua(int year)
        {
            int a = year % 19;
            int b = year / 100;
            int c = year % 100;
            int d = b / 4;
            int e = b % 4;
            int f = (b + 8) / 25;
            int g = (b - f + 1) / 3;
            int h = (19 * a + b - d - g + 15) % 30;
            int i = c / 4;
            int k = c % 4;
            int l = (32 + 2 * e + 2 * i - h - k) % 7;
            int m = (a + 11 * h + 22 * l) / 451;
            int month = (h + l - 7 * m + 114) / 31;
            int day = (h + l - 7 * m + 114) % 31 + 1;
            return new DateOnly(year, month, day);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "festivos_colombianos");
        }
    }
}
