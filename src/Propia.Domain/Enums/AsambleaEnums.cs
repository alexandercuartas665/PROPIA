namespace Propia.Domain.Enums;

/// <summary>Tipo de sesion. Spec 2.8 v1.0 seccion 3.</summary>
public enum TipoSesion
{
    AsambleaOrdinaria = 1,
    AsambleaExtraordinaria = 2,
    Consejo = 3,
    Comite = 4
}

/// <summary>Modalidad de la sesion. Spec 2.8 v1.0 seccion 4.</summary>
public enum ModalidadSesion
{
    Presencial = 1,
    Virtual = 2,
    Mixta = 3
}

/// <summary>Estado de la sesion. Spec 2.8 v1.0 ciclo de vida.</summary>
public enum EstadoSesion
{
    Borrador = 1,
    Citada = 2,
    EnCurso = 3,
    Cerrada = 4,
    QuorumFallido = 5,
    Cancelada = 6
}

/// <summary>Estado de un punto del orden del dia.</summary>
public enum EstadoPunto
{
    Pendiente = 1,
    EnDiscusion = 2,
    EnVotacion = 3,
    Cerrado = 4
}

/// <summary>Tipo de mayoria requerida para un punto. Spec 2.8 v1.0 RN-02/03.</summary>
public enum TipoMayoria
{
    Simple = 1,
    Calificada = 2
}

/// <summary>Modalidad de votacion.</summary>
public enum ModalidadVoto
{
    Publico = 1,
    Secreto = 2
}

/// <summary>Estado de una votacion.</summary>
public enum EstadoVotacion
{
    Abierta = 1,
    Cerrada = 2
}

/// <summary>Resultado final de una votacion.</summary>
public enum ResultadoVotacion
{
    Aprobado = 1,
    Rechazado = 2,
    SinResultado = 3
}

/// <summary>Tipo de poder de representacion. Spec 2.8 v1.0 seccion 7.3.</summary>
public enum TipoPoder
{
    Digital = 1,
    Pdf = 2
}

/// <summary>Estado de un poder.</summary>
public enum EstadoPoder
{
    Pendiente = 1,
    Aprobado = 2,
    Rechazado = 3,
    Revocado = 4,
    Suspendido = 5
}

/// <summary>Evento del log de quorum (append-only). Spec 2.8 v1.0 seccion 7.4.</summary>
public enum EventoQuorum
{
    Ingreso = 1,
    Salida = 2,
    Reconexion = 3,
    GraciaInicio = 4,
    GraciaFin = 5
}

/// <summary>Tipo de firma del acta.</summary>
public enum TipoFirmaActa
{
    ElectronicaNativa = 1,
    Certificada = 2
}

/// <summary>Estado del acta.</summary>
public enum EstadoActa
{
    Borrador = 1,
    Firmada = 2
}

/// <summary>Visibilidad de un documento adjunto al expediente.</summary>
public enum VisibilidadDocumento
{
    PreAsamblea = 1,
    Durante = 2,
    PostAsamblea = 3
}

/// <summary>Calidad de un participante en la sesion.</summary>
public enum CalidadParticipante
{
    Propietario = 1,
    Apoderado = 2
}

/// <summary>Opcion de voto base. Cada votacion puede tener opciones custom (JSON).</summary>
public static class OpcionesVotoBase
{
    public const string Si = "Si";
    public const string No = "No";
    public const string Abstencion = "Abstencion";

    public static readonly string[] Default = new[] { Si, No, Abstencion };
}
