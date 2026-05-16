namespace Propia.Domain.Enums;

/// <summary>Estados del comunicado. Spec 2.14 seccion 4. Enviado y Cancelado terminales (RN-04).</summary>
public enum EstadoComunicado
{
    Borrador = 1,
    Programado = 2,
    Enviando = 3,
    Enviado = 4,
    Cancelado = 5
}

/// <summary>Tipos base de comunicado (catalogo configurable por copropiedad). Spec seccion 5.</summary>
public enum TipoComunicadoBase
{
    Circular = 1,
    AvisoOperativo = 2,
    AnuncioGeneral = 3,
    ComunicadoUrgente = 4
}

/// <summary>Capa de segmentacion aplicable a un comunicado. Spec seccion 6.1.</summary>
public enum TipoSegmento
{
    TipoPersona = 1,
    Unidad = 2,
    GrupoUnidad = 3,
    EstadoCuenta = 4,
    Etiqueta = 5,
    /// <summary>Atajo "toda la comunidad" - anula los demas (RN seccion 6.1).</summary>
    Broadcast = 6
}

/// <summary>Filtro por tipo de persona (spec 6.1). Tipos del Directorio.</summary>
public enum TipoPersonaSegmento
{
    Propietarios = 1,
    Residentes = 2,
    Arrendatarios = 3,
    Todos = 4
}

/// <summary>Filtro por agrupacion de unidades (spec 6.1).</summary>
public enum GrupoUnidadSegmento
{
    Torre = 1,
    Piso = 2,
    Bloque = 3,
    TodasUnidades = 4
}

/// <summary>Filtro por estado de cuenta (spec 6.1 - consume 2.7 Cartera).</summary>
public enum EstadoCuentaSegmento
{
    AlDia = 1,
    EnMora = 2,
    ConAcuerdoPago = 3
}

/// <summary>Estado de entrega de un destinatario individual. Spec seccion 10.3 y tabla 21.</summary>
public enum EstadoEntregaDestinatario
{
    Pendiente = 1,
    Entregado = 2,
    Fallido = 3
}

/// <summary>Dispositivo desde el que se abrio el comunicado (acuse de recibo). Spec 11.1.</summary>
public enum DispositivoAcuse
{
    Mobile = 1,
    Desktop = 2,
    Unknown = 3
}
