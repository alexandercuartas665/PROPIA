using System.Globalization;
using System.Text;
using ClosedXML.Excel;

namespace Propia.Web.Components.Shared;

/// <summary>Columna de la vista exportada: titulo visible y, si es numerica, el formato numerico de Excel.</summary>
public sealed record ExportColumna(string Titulo, bool Numerica = false, string? FormatoExcel = null);

/// <summary>
/// Fila de la vista exportada. Si Grupo != null es la cabecera de un grupo (Nivel = profundidad, Conteo = filas);
/// si no, es una fila de datos: Celdas en el orden de las columnas (numero como decimal, resto como texto) y
/// Ruta = titulos de los grupos que la contienen (el CSV los pone como columnas iniciales).
/// </summary>
public sealed record ExportFila(IReadOnlyList<object?> Celdas, string? Grupo = null, int Nivel = 0, int Conteo = 0,
    IReadOnlyList<string>? Ruta = null)
{
    public bool EsGrupo => Grupo is not null;
    public static ExportFila DeGrupo(string titulo, int nivel, int conteo) => new(Array.Empty<object?>(), titulo, nivel, conteo);
}

/// <summary>
/// La vista tabla TAL COMO SE VE: columnas visibles en su orden, filas filtradas/ordenadas y, si esta agrupada,
/// las cabeceras de grupo. Criterios = lineas informativas (busqueda, filtros, orden, agrupacion).
/// </summary>
public sealed record ExportVista(string Modulo, string? Copropiedad, IReadOnlyList<ExportColumna> Columnas,
    IReadOnlyList<ExportFila> Filas, IReadOnlyList<string> Criterios, IReadOnlyList<string> NivelesGrupo);

/// <summary>
/// Generador de la descarga de la vista tabla (boton TablaExportar): Excel con los colores de la marca y CSV.
/// Los colores van como valores (son colores de un archivo, no del tema de la app): marca #6D4FE3 y sus suaves.
/// </summary>
public static class TablaExportador
{
    private static readonly XLColor Marca = XLColor.FromHtml("#6D4FE3");
    private static readonly XLColor MarcaSuave = XLColor.FromHtml("#EDE7FD");
    private static readonly XLColor MarcaSuave2 = XLColor.FromHtml("#F5F1FE");
    private static readonly XLColor Tinta = XLColor.FromHtml("#1B2A3A");
    private static readonly XLColor Gris = XLColor.FromHtml("#7C98B6");
    private static readonly XLColor Borde = XLColor.FromHtml("#E1E8EE");
    private static readonly CultureInfo Es = new("es-CO");

    /// <summary>Nombre de archivo seguro: "Unidades - Conjunto X - 2026-11-11.xlsx".</summary>
    public static string NombreArchivo(ExportVista v, string extension)
    {
        var baseNombre = string.IsNullOrWhiteSpace(v.Copropiedad) ? v.Modulo : $"{v.Modulo} - {v.Copropiedad}";
        var limpio = new string(baseNombre.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '-' : c).ToArray()).Trim();
        return $"{limpio} - {DateTime.Now:yyyy-MM-dd}.{extension}";
    }

    public static byte[] Xlsx(ExportVista v)
    {
        using var wb = new XLWorkbook();
        var hoja = wb.Worksheets.Add(NombreHoja(v.Modulo));
        var nCols = Math.Max(1, v.Columnas.Count);

        // Encabezado del documento: modulo, copropiedad + fecha y los criterios de la vista.
        var fila = 1;
        hoja.Cell(fila, 1).Value = v.Modulo;
        hoja.Cell(fila, 1).Style.Font.SetBold().Font.SetFontSize(15).Font.SetFontColor(Marca);
        fila++;
        var sub = (string.IsNullOrWhiteSpace(v.Copropiedad) ? "" : v.Copropiedad + " · ") + "Generado el " + DateTime.Now.ToString("d 'de' MMMM 'de' yyyy, h:mm tt", Es);
        hoja.Cell(fila, 1).Value = sub;
        hoja.Cell(fila, 1).Style.Font.SetFontColor(Gris);
        foreach (var c in v.Criterios)
        {
            fila++;
            hoja.Cell(fila, 1).Value = c;
            hoja.Cell(fila, 1).Style.Font.SetFontColor(Gris).Font.SetItalic();
        }
        fila += 2;

        // Cabecera de columnas: fondo marca, texto blanco.
        var filaCab = fila;
        for (var i = 0; i < v.Columnas.Count; i++)
        {
            var cel = hoja.Cell(filaCab, i + 1);
            cel.Value = v.Columnas[i].Titulo;
            cel.Style.Font.SetBold().Font.SetFontColor(XLColor.White).Fill.SetBackgroundColor(Marca)
                .Alignment.SetVertical(XLAlignmentVerticalValues.Center)
                .Alignment.SetHorizontal(v.Columnas[i].Numerica ? XLAlignmentHorizontalValues.Right : XLAlignmentHorizontalValues.Left);
        }
        hoja.Row(filaCab).Height = 22;

        foreach (var f in v.Filas)
        {
            fila++;
            if (f.EsGrupo)
            {
                // Cabecera de grupo: titulo + conteo, indentado por nivel, en tonos de la marca.
                var rango = hoja.Range(fila, 1, fila, nCols);
                rango.Style.Fill.SetBackgroundColor(f.Nivel == 0 ? MarcaSuave : MarcaSuave2);
                var cel = hoja.Cell(fila, 1);
                cel.Value = $"{new string(' ', f.Nivel * 4)}{f.Grupo} ({f.Conteo})";
                cel.Style.Font.SetBold().Font.SetFontColor(f.Nivel == 0 ? Marca : Tinta);
                continue;
            }
            for (var i = 0; i < v.Columnas.Count && i < f.Celdas.Count; i++)
            {
                var cel = hoja.Cell(fila, i + 1);
                var col = v.Columnas[i];
                switch (f.Celdas[i])
                {
                    case null: break;
                    case decimal d: cel.Value = d; break;
                    case int n: cel.Value = n; break;
                    case double db: cel.Value = db; break;
                    default: cel.Value = f.Celdas[i]!.ToString(); break;
                }
                if (col.Numerica && !string.IsNullOrEmpty(col.FormatoExcel)) cel.Style.NumberFormat.Format = col.FormatoExcel;
                cel.Style.Font.SetFontColor(Tinta);
            }
        }

        // Bordes finos, autofiltro sobre la cabecera, encabezado fijo y anchos ajustados.
        if (fila >= filaCab && v.Columnas.Count > 0)
        {
            var tabla = hoja.Range(filaCab, 1, fila, nCols);
            tabla.Style.Border.SetOutsideBorder(XLBorderStyleValues.Thin).Border.SetOutsideBorderColor(Borde)
                .Border.SetInsideBorder(XLBorderStyleValues.Thin).Border.SetInsideBorderColor(Borde);
            hoja.Range(filaCab, 1, fila, nCols).SetAutoFilter();
        }
        hoja.SheetView.FreezeRows(filaCab);
        for (var i = 1; i <= nCols; i++)
        {
            var col = hoja.Column(i);
            col.AdjustToContents(filaCab, fila);
            col.Width = Math.Clamp(col.Width + 2, 10, 60);
        }

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    /// <summary>
    /// CSV para Excel en espanol: UTF-8 con BOM, separador ";" y coma decimal. Agrupado = una columna inicial por
    /// nivel de agrupacion con el grupo de cada fila (formato plano, sin filas de titulo).
    /// </summary>
    public static byte[] Csv(ExportVista v)
    {
        var sb = new StringBuilder();
        var cab = v.NivelesGrupo.Concat(v.Columnas.Select(c => c.Titulo));
        sb.AppendLine(string.Join(';', cab.Select(Escapar)));
        foreach (var f in v.Filas.Where(x => !x.EsGrupo))
        {
            var ruta = Enumerable.Range(0, v.NivelesGrupo.Count).Select(i => f.Ruta is not null && i < f.Ruta.Count ? f.Ruta[i] : "");
            var celdas = f.Celdas.Select(c => c switch
            {
                null => "",
                decimal d => d.ToString(Es),
                int n => n.ToString(Es),
                double db => db.ToString(Es),
                _ => c.ToString() ?? ""
            });
            sb.AppendLine(string.Join(';', ruta.Concat(celdas).Select(Escapar)));
        }
        return new UTF8Encoding(true).GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
    }

    private static string Escapar(string s)
        => s.IndexOfAny(new[] { ';', '"', '\n', '\r' }) >= 0 ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;

    private static string NombreHoja(string modulo)
    {
        var limpio = new string(modulo.Where(c => !"[]:*?/\\".Contains(c)).ToArray()).Trim();
        if (limpio.Length == 0) limpio = "Vista";
        return limpio.Length > 31 ? limpio[..31] : limpio;
    }
}
