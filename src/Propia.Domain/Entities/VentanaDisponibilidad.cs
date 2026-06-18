using Propia.Domain.Common;
using Propia.Domain.Enums;

namespace Propia.Domain.Entities;

/// <summary>
/// Ventana horaria en la que un EquipoActivo reservable o una ZonaComun esta disponible
/// para reservar. Ej.: Piscina lunes a domingo 08:00-19:00; Salon social viernes-domingo
/// 16:00-22:00. Es la CONFIGURACION (catalogo de horarios), no la lista de reservas.
/// El modulo 2.13 Reservas consulta estas ventanas para validar cada reserva nueva.
/// Polimorfico via TipoEntidad + EntidadId para reutilizar el modelo en equipos y zonas.
/// </summary>
public class VentanaDisponibilidad : TenantEntity
{
    public TipoEntidadDisponibilidad TipoEntidad { get; set; }
    public Guid EntidadId { get; set; }

    public DiaSemana DiaSemana { get; set; }
    public TimeOnly HoraInicio { get; set; }
    public TimeOnly HoraFin { get; set; }

    public bool Activa { get; set; } = true;
}
