using System.Text;
using ClosedXML.Excel;
using ExcelDataReader;

namespace Propia.Infrastructure.MiCopropiedad;

/// <summary>El archivo subido no es un libro de Excel legible. El mensaje es apto para mostrar al usuario.</summary>
public sealed class ExcelEntradaException : Exception
{
    public ExcelEntradaException(string mensaje) : base(mensaje) { }
}

/// <summary>
/// Abre el archivo que sube el usuario en cualquiera de los dos formatos de Excel y lo entrega como
/// XLWorkbook, que es lo que leen todos los importadores (ClosedXML solo entiende .xlsx):
///  - .xlsx (OOXML, firma ZIP "PK"): se abre directo con ClosedXML.
///  - .xls (Excel 97-2003, BIFF, firma OLE2 D0 CF 11 E0): se lee con ExcelDataReader y se vuelca
///    hoja por hoja y celda por celda a un XLWorkbook en memoria. Solo viajan VALORES (numero, texto,
///    fecha, booleano) y la visibilidad de la hoja; listas desplegables, estilos y nombres definidos no
///    hacen falta para importar.
/// Se decide por la firma binaria, no por la extension: un .xls renombrado a .xlsx (o al reves) se
/// abre igual. Cualquier otro contenido (CSV, PDF, archivo vacio) lanza ExcelEntradaException con un
/// mensaje claro en vez del generico "verifica el formato".
/// </summary>
public static class ExcelEntrada
{
    private static bool _codePagesRegistradas;

    public static XLWorkbook Abrir(Stream contenido)
    {
        var ms = new MemoryStream();
        contenido.CopyTo(ms);
        if (ms.Length < 8) throw new ExcelEntradaException("El archivo esta vacio o no es un libro de Excel.");
        ms.Position = 0;
        var firma = new byte[8];
        _ = ms.Read(firma, 0, firma.Length);
        ms.Position = 0;

        if (firma[0] == 0x50 && firma[1] == 0x4B)          // "PK": zip -> .xlsx
            return new XLWorkbook(ms);
        if (EsOle2(firma))                                  // compound file -> .xls
            return ConvertirXls(ms);
        throw new ExcelEntradaException(
            "El archivo no es un libro de Excel (.xlsx ni .xls). Si es un CSV u otro formato, abrelo en Excel y guardalo como Libro de Excel.");
    }

    private static bool EsOle2(byte[] f)
        => f[0] == 0xD0 && f[1] == 0xCF && f[2] == 0x11 && f[3] == 0xE0
        && f[4] == 0xA1 && f[5] == 0xB1 && f[6] == 0x1A && f[7] == 0xE1;

    private static XLWorkbook ConvertirXls(MemoryStream ms)
    {
        // Los .xls viejos guardan texto en code pages (p.ej. 1252). En .NET Core esos encodings no
        // vienen registrados por defecto; sin esto ExcelDataReader lanza NotSupportedException.
        if (!_codePagesRegistradas)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            _codePagesRegistradas = true;
        }

        IExcelDataReader reader;
        try { reader = ExcelReaderFactory.CreateBinaryReader(ms); }
        catch (Exception ex)
        {
            throw new ExcelEntradaException("El archivo parece Excel 97-2003 (.xls) pero no se pudo leer: " + ex.Message);
        }

        var wb = new XLWorkbook();
        using (reader)
        {
            do
            {
                var nombre = string.IsNullOrWhiteSpace(reader.Name) ? $"Hoja{wb.Worksheets.Count + 1}" : reader.Name;
                var ws = wb.AddWorksheet(nombre);
                if (!string.Equals(reader.VisibleState, "visible", StringComparison.OrdinalIgnoreCase))
                    ws.Visibility = XLWorksheetVisibility.Hidden;
                var fila = 0;
                while (reader.Read())
                {
                    fila++;
                    for (var i = 0; i < reader.FieldCount; i++)
                    {
                        var v = reader.GetValue(i);
                        if (v is null) continue;
                        ws.Cell(fila, i + 1).Value = AValor(v);
                    }
                }
            } while (reader.NextResult());
        }
        return wb;
    }

    private static XLCellValue AValor(object v) => v switch
    {
        double d => d,
        float f => (double)f,
        int n => n,
        long l => l,
        decimal m => (double)m,
        bool b => b,
        DateTime dt => dt,
        TimeSpan ts => ts,
        string s => s,
        _ => v.ToString() ?? string.Empty
    };
}
