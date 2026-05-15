namespace Propia.Domain.Enums;

/// <summary>
/// Origen del documento. Spec 2.15 seccion 4.3 y RN-04.
/// MANUAL: subido directamente por usuario.
/// ASAMBLEA, PQRSD, TAREA, MANTENIMIENTO, PORTERIA, COMUNICACION: generado por otro modulo (mira IBlobStorage y guarda metadatos del origen).
/// SISTEMA: archivo plantilla u otro generado por el motor.
/// </summary>
public enum OrigenDocumento
{
    Manual = 1,
    Asamblea = 2,
    Pqrsd = 3,
    Tarea = 4,
    Mantenimiento = 5,
    Porteria = 6,
    Comunicacion = 7,
    Sistema = 8
}

/// <summary>
/// Estados del ciclo de vida documental. Spec 2.15 seccion 8.
/// Para MVP solo manejamos BORRADOR, VIGENTE y ARCHIVADO.
/// EN_FIRMA y FIRMADO se reservan para Fase 2 (firma electronica).
/// </summary>
public enum EstadoDocumento
{
    Borrador = 1,
    Vigente = 2,
    EnFirma = 3,
    Firmado = 4,
    Archivado = 5
}

/// <summary>
/// Tipo de evento en documento_auditoria - tabla append-only.
/// Spec 2.15 seccion 15.1.
/// </summary>
public enum TipoEventoDocumento
{
    Subida = 1,
    NuevaVersion = 2,
    Vista = 3,
    Descarga = 4,
    CambioMetadatos = 5,
    CambioCategoria = 6,
    CambioCarpeta = 7,
    CambioEtiquetas = 8,
    CambioVisibilidad = 9,
    CambioEstado = 10,
    InicioFirma = 11,
    FirmaIndividual = 12,
    DocumentoFirmado = 13,
    Archivado = 14,
    Reactivado = 15,
    EliminacionLogica = 16
}

/// <summary>Tipo de evento en documento_consumo. Spec 2.15 seccion 15.2.</summary>
public enum TipoEventoConsumo
{
    Vista = 1,
    Descarga = 2
}

/// <summary>Dispositivo desde el que se consumio el documento. Spec 2.15 seccion 15.2.</summary>
public enum DispositivoConsumo
{
    Mobile = 1,
    Desktop = 2,
    Unknown = 3
}
