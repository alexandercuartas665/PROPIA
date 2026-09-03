namespace Propia.Domain.Enums;

/// <summary>Tipo de PQRSD. Fijo de plataforma (RN-04) - determina plazo legal. Spec 2.9 v1.0 seccion 3.</summary>
public enum TipoPqrsd
{
    Peticion = 1,
    SolicitudDocumentos = 2,
    Consulta = 3,
    Queja = 4,
    Reclamo = 5,
    Sugerencia = 6,
    Denuncia = 7,
    Felicitacion = 8
}

/// <summary>Medio por el que se recibio la PQRSD (bitacora legal). Opcional.</summary>
public enum MedioRecepcionPqrsd
{
    CorreoElectronico = 1,
    Fisico = 2,
    Telefonico = 3,
    Presencial = 4,
    WhatsApp = 5,
    PortalWeb = 6,
    Otro = 7
}

/// <summary>Estado del expediente. Spec 2.9 v1.0 seccion 8.</summary>
public enum EstadoPqrsd
{
    Recibida = 1,
    EnGestion = 2,
    Respondida = 3,
    Cerrada = 4,
    ViaInternaAgotada = 5
}

/// <summary>Semaforo de plazo (calculado backend en dias habiles). Spec 2.9 v1.0 seccion 9.</summary>
public enum SemaforoPqrsd
{
    Verde = 1,
    Amarillo = 2,
    Rojo = 3,
    Negro = 4,
    TutelaActiva = 5
}

/// <summary>Nivel de urgencia. Spec 2.9 v1.0 seccion 11.</summary>
public enum NivelUrgenciaPqrsd
{
    Alta = 1,
    Media = 2,
    Baja = 3
}

/// <summary>Modalidad de la sesion del Comite de Convivencia.</summary>
public enum ModalidadComite
{
    Presencial = 1,
    Virtual = 2,
    Mixta = 3
}

/// <summary>Resultado de la sesion del Comite. Spec 2.9 v1.0 seccion 6.</summary>
public enum ResultadoComite
{
    Acuerdo = 1,
    SinAcuerdo = 2
}

/// <summary>Origen de un cambio de estado en el historial.</summary>
public enum OrigenCambioEstado
{
    Manual = 1,
    Automatico = 2,
    Sistema = 3,
    /// <summary>Ampliacion de plazo (prorroga legal, Ley 1755). Queda en la traza con su motivo.</summary>
    Prorroga = 4
}

/// <summary>Catalogo base de categorias y plazos por defecto. Spec 2.9 v1.0 seccion 3 y 4.</summary>
public static class PqrsdCatalogo
{
    /// <summary>Categorias base sembradas lazy: (Nombre, Orden).</summary>
    public static readonly (string Nombre, int Orden)[] CategoriasBase = new[]
    {
        ("Administración", 1),
        ("Financiero", 2),
        ("Convivencia", 3),
        ("Zonas Comunes", 4),
        ("Mantenimiento", 5),
        ("Seguridad", 6),
        ("Personal", 7),
        ("Consejo de Administración", 8),
        ("Otro", 9)
    };

    /// <summary>Plazos por defecto en dias habiles: (Tipo, DiasHabiles, DiasInconformidad, NivelUrgencia). Spec 2.9 v1.0 seccion 3.</summary>
    public static readonly (TipoPqrsd Tipo, int DiasHabiles, int DiasInconformidad, NivelUrgenciaPqrsd Urgencia)[] PlazosBase = new[]
    {
        (TipoPqrsd.Peticion, 15, 3, NivelUrgenciaPqrsd.Media),
        (TipoPqrsd.SolicitudDocumentos, 10, 3, NivelUrgenciaPqrsd.Media),
        (TipoPqrsd.Consulta, 30, 3, NivelUrgenciaPqrsd.Baja),
        (TipoPqrsd.Queja, 15, 3, NivelUrgenciaPqrsd.Media),
        (TipoPqrsd.Reclamo, 15, 3, NivelUrgenciaPqrsd.Media),
        (TipoPqrsd.Sugerencia, 15, 3, NivelUrgenciaPqrsd.Baja),
        (TipoPqrsd.Denuncia, 15, 3, NivelUrgenciaPqrsd.Alta),
        (TipoPqrsd.Felicitacion, 5, 3, NivelUrgenciaPqrsd.Baja)
    };
}
