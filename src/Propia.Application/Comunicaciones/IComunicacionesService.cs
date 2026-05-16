using Propia.Domain.Enums;

namespace Propia.Application.Comunicaciones;

/// <summary>
/// Modulo 2.14 Comunicaciones - servicio de aplicacion (spec v1.0 MVP).
/// Gestiona comunicados a residentes: redaccion, segmentacion, envio simulado
/// (WhatsApp diferido a Fase 2 con T.2), acuse de recibo silencioso.
/// </summary>
public interface IComunicacionesService
{
    // ----- Plantillas -----
    Task<IReadOnlyList<PlantillaDto>> ListarPlantillasAsync(bool? soloGlobales, CancellationToken ct);
    Task<PlantillaDto?> GetPlantillaAsync(Guid id, CancellationToken ct);
    Task<PlantillaDto> CrearPlantillaTenantAsync(CrearPlantillaRequest req, CancellationToken ct);
    Task<bool> ActualizarPlantillaTenantAsync(Guid id, ActualizarPlantillaRequest req, CancellationToken ct);
    /// <summary>Soft delete (RN-11) - desactiva. Rechaza si tiene comunicados activos.</summary>
    Task<bool> DesactivarPlantillaTenantAsync(Guid id, CancellationToken ct);

    // ----- Comunicado: ciclo de vida -----
    Task<IReadOnlyList<ComunicadoListaDto>> ListarComunicadosAsync(
        EstadoComunicado? estado,
        TipoComunicadoBase? tipo,
        string? query,
        CancellationToken ct);

    Task<ComunicadoDetalleDto?> GetComunicadoAsync(Guid id, CancellationToken ct);

    Task<ComunicadoDetalleDto> CrearBorradorAsync(CrearBorradorRequest req, CancellationToken ct);
    Task<bool> ActualizarBorradorAsync(Guid id, ActualizarBorradorRequest req, CancellationToken ct);

    // ----- Segmentos -----
    Task<SegmentoDto> AgregarSegmentoAsync(Guid comunicadoId, AgregarSegmentoRequest req, CancellationToken ct);
    Task<bool> RemoverSegmentoAsync(Guid comunicadoId, Guid segmentoId, CancellationToken ct);

    /// <summary>Calcula destinatarios sin enviar - permite ver conteo antes de confirmar.</summary>
    Task<PreviewDestinatariosDto> PreviewDestinatariosAsync(Guid comunicadoId, CancellationToken ct);

    // ----- Adjuntos -----
    Task<AdjuntoDto> AgregarAdjuntoAsync(Guid comunicadoId, CrearAdjuntoRequest req, CancellationToken ct);
    Task<bool> RemoverAdjuntoAsync(Guid comunicadoId, Guid adjuntoId, CancellationToken ct);

    // ----- Envio -----
    /// <summary>
    /// Confirma envio: si FechaProgramada es null, envia inmediato; si tiene valor,
    /// queda en Programado y se enviara cuando el job programado lo dispare.
    /// El envio real por WhatsApp (T.2) esta diferido a Fase 2 - el MVP simula
    /// la entrega marcando los destinatarios como Entregado.
    /// </summary>
    Task<bool> EnviarAsync(Guid id, EnviarRequest req, CancellationToken ct);

    /// <summary>Solo aplica a Borrador o Programado.</summary>
    Task<bool> CancelarAsync(Guid id, CancelarRequest req, CancellationToken ct);

    /// <summary>
    /// Reenvio a pendientes (RN-07) - encola nuevamente para los destinatarios
    /// que aun no abrieron el comunicado. NO crea un nuevo comunicado.
    /// </summary>
    Task<int> ReenviarAPendientesAsync(Guid id, CancellationToken ct);

    // ----- Acuses -----
    Task<AcuseListaDto> ListarAcusesAsync(Guid comunicadoId, CancellationToken ct);

    /// <summary>
    /// Endpoint publico: registra el acuse y devuelve el contenido del comunicado.
    /// Sin auth - se invoca cuando el residente abre el enlace de WhatsApp.
    /// </summary>
    Task<VistaPublicaComunicadoDto?> AbrirVistaPublicaAsync(Guid token, DispositivoAcuse dispositivo, CancellationToken ct);

    // ----- Resumen para bandeja/KPIs -----
    Task<ResumenComunicacionesDto> GetResumenAsync(CancellationToken ct);
}
