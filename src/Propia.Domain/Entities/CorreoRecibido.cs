using Propia.Domain.Common;

namespace Propia.Domain.Entities;

/// <summary>
/// Lector de buzon (Ola 1, 2026-11-17): un correo que llego a un buzon de la copropiedad (<see cref="BuzonCorreo"/>,
/// leido por IMAP con contrasena de aplicacion). El agente "Coordinador de correo" lo clasifica por tema (lista por
/// copropiedad "correo.tema"); en Ola 2 lo delega al agente especialista. El buzon del cliente NO se modifica: se
/// abre en solo lectura y no se marca nada como leido.
/// </summary>
public class CorreoRecibido : TenantEntity
{
    /// <summary>Message-ID del correo (o "uid:{validity}:{uid}" si no trae). Unico por copropiedad: evita reprocesar.</summary>
    public string MessageId { get; set; } = "";

    /// <summary>Buzon por el que llego (null si el buzon se elimino).</summary>
    public Guid? BuzonId { get; set; }

    /// <summary>UID IMAP del mensaje en la bandeja de entrada (referencia; el avance real va en el buzon).</summary>
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

    /// <summary>Clave del tema que asigno el coordinador (opcion de la lista por copropiedad "correo.tema"; ej.
    /// "pqrsd", o "c:..." para un tema propio). null = sin clasificar.</summary>
    public string? TemaClave { get; set; }

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

    // ----- PQRSD por correo (Ola P1, 2026-11-18) -----

    /// <summary>Propuesta del agente "Radicador de PQRSD" (tipo, categoria, unidad, confianza) en JSON
    /// (<c>PropuestaPqrsdCorreo</c>). Se usa para radicar solo (confianza alta) o para prellenar el formulario.</summary>
    public string? PropuestaPqrsdJson { get; set; }

    /// <summary>Expediente PQRSD que se radico desde este correo (null = aun no). Evita radicar dos veces.</summary>
    public Guid? PqrsdExpedienteId { get; set; }

    /// <summary>Numero de radicado del expediente (copia para mostrarlo en la bandeja sin cruzar tablas).</summary>
    public string? PqrsdRadicado { get; set; }

    /// <summary>True si se radico solo (confianza sobre el umbral del buzon); false si lo confirmo una persona.</summary>
    public bool? PqrsdAutomatico { get; set; }

    /// <summary>Ola P2: cuando se envio la respuesta de radicado desde la cuenta del buzon (null = no se envio).</summary>
    public DateTimeOffset? AcuseEnviadoAt { get; set; }

    /// <summary>A quien se respondio (el remitente del correo).</summary>
    public string? AcuseDestino { get; set; }

    /// <summary>Por que no salio la respuesta por el buzon (en ese caso se envio la confirmacion de la plataforma).</summary>
    public string? AcuseError { get; set; }
}

/// <summary>Claves de los temas BASE del coordinador (lista por copropiedad "correo.tema"). La copropiedad puede
/// renombrarlos, ocultarlos o agregar los suyos.</summary>
public static class TemasCorreoBase
{
    public const string Lista = "correo.tema";
    public const string ReciboServicio = "recibo_servicio";
    public const string Pqrsd = "pqrsd";
    public const string PolizaContrato = "poliza_contrato";
    public const string Otro = "otro";
    public const string Irrelevante = "irrelevante";
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
