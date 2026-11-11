using System.Diagnostics;

namespace Propia.Web.Components.Shared.IaIngesta;

/// <summary>Estado de la lectura con IA de UN documento (replica: d.ia de RP.iaIngesta).</summary>
public enum IaEstado { Ninguno, Cola, Leyendo, Listo, Pendiente, Error }

/// <summary>
/// Lectura con IA de un documento (Nuevo contrato / Nueva poliza / Actualizar desde un PDF). Lo comparten los dos
/// modales: el modulo hace el trabajo real (pdf.js, Agente Documental, aplicar campos) y avanza este objeto en cada
/// evento; IaPanel / IaRail / IaFicha se suscriben a <see cref="Cambio"/> y se repintan SOLOS, sin re-renderizar
/// el modal (un documento en segundo plano solo actualiza su tarjeta).
/// Pasos y porcentajes de la replica: 0 Subiendo el archivo (14) · 1 Leyendo el texto del PDF (38) · 2 Encontrando
/// los datos (40-90, un chip cada 170 ms) · 3 Llenando el formulario (96). Duraciones minimas 450 / 750 / - / 450 ms.
/// </summary>
public sealed class IaLectura
{
    public IaEstado Estado { get; private set; } = IaEstado.Ninguno;
    public int Paso { get; private set; }
    public int Pct { get; private set; }
    /// <summary>Datos encontrados mientras lee (chips que aparecen uno a uno).</summary>
    public List<string> Encontrados { get; } = new();
    /// <summary>Resumen final: etiquetas de los campos que lleno la IA y las dudosas (ambar).</summary>
    public List<string> Campos { get; private set; } = new();
    public List<string> Dudosos { get; private set; } = new();
    public int Tokens { get; private set; }
    /// <summary>Mensaje de error (tarjeta roja) y texto corto para la tarjeta del documento ("Solo PDF").</summary>
    public string? Msg { get; private set; }
    public string? Corto { get; private set; }

    /// <summary>Ultimo estado que ya mostro su entrada animada (replica: d.ia._visto): re-renders no la repiten.</summary>
    public IaEstado Visto { get; set; } = IaEstado.Ninguno;

    public event Action? Cambio;
    private void Notificar() => Cambio?.Invoke();

    private readonly Stopwatch _reloj = new();

    public void EnCola() { Estado = IaEstado.Cola; Notificar(); }
    public void Pendiente() { Estado = IaEstado.Pendiente; Notificar(); }
    public void Fallo(string msg, string? corto = null) { Estado = IaEstado.Error; Msg = msg; Corto = corto; Notificar(); }
    public void Reiniciar() { Estado = IaEstado.Ninguno; Encontrados.Clear(); Campos = new(); Dudosos = new(); Msg = null; Corto = null; Notificar(); }

    /// <summary>Empieza a leer: paso 0 (subiendo el archivo).</summary>
    public void Iniciar()
    {
        Estado = IaEstado.Leyendo; Paso = 0; Pct = 3; Encontrados.Clear(); Msg = null; Corto = null;
        Notificar();
        _reloj.Restart();
        Pct = 14; Notificar();
    }

    /// <summary>Pasa al paso indicado esperando antes la duracion minima del paso actual (para que la
    /// transicion se lea aunque el trabajo real sea instantaneo).</summary>
    public async Task PasoAsync(int paso, int pct)
    {
        var minimo = Paso switch { 0 => 450, 1 => 750, 3 => 450, _ => 0 };
        var falta = minimo - (int)_reloj.ElapsedMilliseconds;
        if (falta > 0) await Task.Delay(falta);
        Paso = paso; Pct = pct; _reloj.Restart();
        Notificar();
    }

    /// <summary>Mientras espera la respuesta del agente (paso 2) el porcentaje avanza despacio hasta 58,
    /// para que la barra no quede quieta en lecturas largas (vision: 20-60 s).</summary>
    public async Task EsperarAsync(Task trabajo)
    {
        while (!trabajo.IsCompleted)
        {
            await Task.WhenAny(trabajo, Task.Delay(900));
            if (!trabajo.IsCompleted && Pct < 58) { Pct++; Notificar(); }
        }
        await trabajo;
    }

    /// <summary>Paso 2: los datos encontrados aparecen uno a uno cada 170 ms (porcentaje 40-90).</summary>
    public async Task ChipsAsync(IReadOnlyList<string> etiquetas)
    {
        var desde = Math.Max(40, Pct);
        for (var j = 0; j < etiquetas.Count; j++)
        {
            Encontrados.Add(etiquetas[j]);
            Pct = desde + (int)Math.Round((90 - desde) * (j + 1) / (double)etiquetas.Count);
            Notificar();
            await Task.Delay(170);
        }
        await Task.Delay(120);
    }

    /// <summary>Fin (despues de PasoAsync(3, 96) y de aplicar los campos al formulario): espera lo que falte del
    /// paso "Llenando el formulario" y muestra el resumen con los campos llenados y los dudosos.</summary>
    public async Task TerminarAsync(IReadOnlyList<string> campos, IReadOnlyList<string> dudosos, int tokens)
    {
        var falta = 450 - (int)_reloj.ElapsedMilliseconds;
        if (falta > 0) await Task.Delay(falta);
        Campos = campos.ToList(); Dudosos = dudosos.ToList(); Tokens = tokens;
        Estado = IaEstado.Listo; Pct = 100;
        Notificar();
    }

    public static string FmtTok(int t) => t >= 1000 ? (Math.Round(t / 100.0) / 10).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture).Replace('.', ',') + "k tok" : t + " tok";
}
