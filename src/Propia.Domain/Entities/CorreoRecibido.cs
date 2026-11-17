using Propia.Domain.Common;

namespace Propia.Domain.Entities;

/// <summary>
/// Lector de buzon (Ola 1, 2026-11-17): un correo que llego al buzon de la copropiedad (leido por IMAP con la misma
/// cuenta y contrasena de aplicacion del correo saliente, ver <see cref="TenantEmailConfig"/>). El agente
/// "Coordinador de correo" lo clasifica por tema; en Ola 2 lo delega al agente especialista. El buzon del cliente
/// NO se modifica: se abre en solo lectura y no se marca nada como leido.
/// </summary>
public class CorreoRecibido : TenantEntity
{
    /// <summary>Message-ID del correo (o "uid:{validity}:{uid}" si no trae). Unico por copropiedad: evita reprocesar.</summary>
    public string MessageId { get; set; } = "";

    /// <summary>UID IMAP del mensaje en la bandeja de entrada (referencia; el avance real va en TenantEmailConfig).</summary>
    public long ImapUid { get; set; }

    public string? RemitenteCorreo { get; set; }
    public string? RemitenteNombre { get; set; }
    public string? Para { get; set; }
    public string? Asunto { get; set; }

    /// <summary>Fecha en que el servidor recibio el correo.</summary>
    public DateTimeOffset FechaCorreo { get; set; }

    /// <summary>Cuerpo en texto plano (si solo venia en HTML, se le quitan las etiquetas). Recortado.</summary>
    public string? Cuerpo { get; set; }

    /// <summary>Documentos adjuntos guardados en el storage: JSON [{nombre, mime, tamano, ruta}].</summary>
    public string? AdjuntosJson { get; set; }

    public TemaCorreo Tema { get; set; } = TemaCorreo.SinClasificar;

    /// <summary>Que tan seguro esta el coordinador (0 a 1).</summary>
    public decimal? Confianza { get; set; }

    /// <summary>Resumen corto del correo hecho por el coordinador.</summary>
    public string? Resumen { get; set; }

    /// <summary>Lo que el coordinador propone hacer (en Ola 1 solo se muestra; no se ejecuta).</summary>
    public string? AccionSugerida { get; set; }

    /// <summary>Datos extra que el coordinador extrajo (JSON libre: servicio, proveedor, unidad, tipo de PQRSD...).</summary>
    public string? DatosJson { get; set; }

    public EstadoCorreoRecibido Estado { get; set; } = EstadoCorreoRecibido.Nuevo;

    /// <summary>Ultimo error al clasificar (legible).</summary>
    public string? Error { get; set; }

    public DateTimeOffset? ClasificadoEn { get; set; }
}

/// <summary>Tema que le asigna el Coordinador de correo.</summary>
public enum TemaCorreo
{
    SinClasificar = 0,
    /// <summary>Recibo o factura de un servicio publico (energia, agua, gas, aseo, internet...).</summary>
    ReciboServicio = 1,
    /// <summary>Peticion, queja, reclamo, sugerencia o denuncia de un residente o tercero.</summary>
    Pqrsd = 2,
    /// <summary>Poliza de seguro o contrato (o documentos de su tramite).</summary>
    PolizaContrato = 3,
    /// <summary>Correo relevante para la administracion que no encaja en los temas anteriores.</summary>
    Otro = 4,
    /// <summary>Publicidad, boletines, notificaciones automaticas sin accion.</summary>
    Irrelevante = 5
}

public enum EstadoCorreoRecibido
{
    /// <summary>Guardado, aun sin clasificar.</summary>
    Nuevo = 0,
    Clasificado = 1,
    /// <summary>El coordinador fallo (proveedor IA caido, respuesta invalida). Se puede reclasificar.</summary>
    Error = 2,
    /// <summary>La administracion lo descarto de la bandeja.</summary>
    Descartado = 3,
    /// <summary>Ya se ejecuto la accion (Ola 2).</summary>
    Procesado = 4
}
