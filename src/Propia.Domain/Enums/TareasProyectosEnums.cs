namespace Propia.Domain.Enums;

/// <summary>
/// Prioridades fijas de plataforma (no configurables). Spec 2.10 v1.0 seccion 5.
/// </summary>
public enum PrioridadTarea
{
    Urgente = 1,
    Alta = 2,
    Normal = 3,
    Baja = 4
}

/// <summary>
/// Origen de la creacion de la tarea. Spec 2.10 v1.0 seccion 6.1 (campo `origen`).
/// </summary>
public enum OrigenTarea
{
    Manual = 1,
    WhatsappIa = 2,
    ModuloExterno = 3
}

/// <summary>
/// Estados base predefinidos. La copropiedad puede renombrar/agregar, pero estos vienen
/// sembrados al activar el modulo. Spec 2.10 v1.0 seccion 4.
/// </summary>
public static class EstadoTareaBase
{
    public const string Pendiente = "Pendiente";
    public const string EnProgreso = "En progreso";
    public const string EnRevision = "En revision";
    public const string Bloqueada = "Bloqueada";
    public const string Completada = "Completada";
    public const string Cancelada = "Cancelada";

    /// <summary>Estados terminales no eliminables ni renombrables.</summary>
    public static readonly string[] Terminales = new[] { Completada, Cancelada };

    /// <summary>Catalogo base con (Nombre, Orden, EsTerminal).</summary>
    public static readonly (string Nombre, int Orden, bool EsTerminal)[] Base = new[]
    {
        (Pendiente, 1, false),
        (EnProgreso, 2, false),
        (EnRevision, 3, false),
        (Bloqueada, 4, false),
        (Completada, 5, true),
        (Cancelada, 6, true)
    };
}

/// <summary>Tipo de evento en el historial de la tarea (append-only). Spec 2.10 v1.0.</summary>
public enum TipoEventoTarea
{
    Creada = 1,
    EstadoCambiado = 2,
    AsignacionCambiada = 3,
    FechaCambiada = 4,
    PrioridadCambiada = 5,
    EtiquetaAsignada = 6,
    EtiquetaRemovida = 7,
    ColaboradorAgregado = 8,
    ColaboradorRemovido = 9,
    ComentarioAgregado = 10,
    AdjuntoAgregado = 11,
    Cancelada = 12,
    Reabierta = 13,
    DependenciaAgregada = 14,
    DependenciaRemovida = 15,
    PersonaMencionada = 16,
    Actualizada = 17,
    Eliminada = 18
}

/// <summary>
/// Tipo de un campo personalizado de un tablero (modulo 2.10). Define como se captura y renderiza
/// en el modal de la tarjeta. Portado del patron de campos dinamicos de CUBOT.travels.
/// </summary>
public enum TipoCampoTablero
{
    Texto = 0,
    AreaTexto = 1,
    Numero = 2,
    Moneda = 3,
    Fecha = 4,
    Hora = 5,
    Telefono = 6,
    Seleccion = 7,
    Booleano = 8,
    Url = 9,
    Email = 10,
    /// <summary>Divisoria visual con titulo; NO captura valor.</summary>
    Separador = 11,
    /// <summary>Calculado y de solo lectura: suma otros campos Numero/Moneda (ver CamposSuma).</summary>
    Total = 12,
    /// <summary>Numero mostrado como porcentaje (sufijo %). Se agrega al FINAL para no renumerar.</summary>
    Porcentaje = 13,
    /// <summary>Fecha + hora combinadas (un solo valor). Se agrega al FINAL para no renumerar.</summary>
    FechaHora = 14,
    /// <summary>Calculado y de solo lectura (Fase 2, Unidades): aplica una <see cref="OperacionFormula"/>
    /// (Suma/Promedio/Conteo/Minimo/Maximo) sobre otros campos Numero/Moneda de la misma fila. Generaliza
    /// a Total=12 (que se conserva para Tareas/PQRSD). La config (operacion + campos fuente) va como JSON
    /// en la columna polimorfica Opciones. Se computa en LECTURA; NO persiste resultado. Se agrega al FINAL.</summary>
    Formula = 15,
    /// <summary>Vincula a UNA cuenta de usuario del tenant (Fase 2, Unidades). El valor guarda el Guid del
    /// usuario; se valida pertenencia al tenant. Se agrega al FINAL para no renumerar.</summary>
    Usuario = 16,
    /// <summary>Vincula a UNA persona/empresa del Directorio (Fase 2, Unidades). El valor guarda el Guid;
    /// personas es GLOBAL, se valida pertenencia al tenant. Se agrega al FINAL para no renumerar.</summary>
    Directorio = 17
}

/// <summary>
/// Operacion de un campo <see cref="TipoCampoTablero.Formula"/> (Fase 2). Se aplica sobre los valores no
/// vacios de los campos fuente (Numero/Moneda) de la misma fila. Conteo = numero de campos fuente CON
/// valor; el resto opera sobre los valores numericos de esos campos.
/// </summary>
public enum OperacionFormula
{
    Suma = 0,
    Promedio = 1,
    Conteo = 2,
    Minimo = 3,
    Maximo = 4
}

/// <summary>Tipo de dependencia entre dos tareas. Spec 2.10 v1.0 Fase 2.</summary>
public enum TipoDependenciaTarea
{
    /// <summary>La tarea A no puede pasar a EnProgreso hasta que B este Completada.</summary>
    Bloqueante = 1,
    /// <summary>Relacion informativa (predecesora natural pero no bloquea).</summary>
    Sugerida = 2
}
