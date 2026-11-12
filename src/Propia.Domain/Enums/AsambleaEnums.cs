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
    Cerrado = 4,
    /// <summary>La sesion se cerro sin tratar el punto.</summary>
    NoTratado = 5
}

/// <summary>Tipo de mayoria requerida para un punto. Spec 2.8 v1.0 RN-02/03.</summary>
public enum TipoMayoria
{
    Simple = 1,
    Calificada = 2
}

/// <summary>
/// Como se compara el % del "Si" contra la mayoria del punto al cerrar una votacion. Lo define cada
/// copropiedad en la configuracion de Asambleas. MasDe = mayoria absoluta de la Ley 675 (un 50/50 con
/// mayoria del 50 % NO aprueba); AlMenos = basta con alcanzar el % (el 50/50 aprueba).
/// </summary>
public enum CriterioMayoria
{
    AlMenos = 1,
    MasDe = 2
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
    SinResultado = 3,
    /// <summary>Eleccion (planchas o candidatos): gano la opcion con mas coeficiente.</summary>
    Elegido = 4
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

/// <summary>Tipo de linea de la transcripcion de la sesion.</summary>
public enum TipoLineaTranscripcion
{
    Voz = 1,
    Sistema = 2
}

/// <summary>Estado de la redaccion del acta por la IA.</summary>
public enum EstadoRedaccionActa
{
    /// <summary>Acta vieja (sin borrador legible).</summary>
    Ninguna = 0,
    Redactando = 1,
    Lista = 2,
    /// <summary>La IA no pudo redactar: queda el borrador base con los datos del sistema.</summary>
    Error = 3
}

/// <summary>Como firmo el presidente o el secretario.</summary>
public enum MetodoFirmaActa
{
    /// <summary>Desde su cuenta de PROPIA (la persona del usuario es la designada).</summary>
    Cuenta = 1,
    /// <summary>Con el codigo de 6 digitos enviado a su correo.</summary>
    Codigo = 2
}

public enum RolFirmaActa
{
    Presidente = 1,
    Secretario = 2
}

/// <summary>Tipo de documento de la sesion (pestana Documentos).</summary>
public enum TipoDocumentoSesion
{
    Otro = 0,
    Convocatoria = 1,
    Informe = 2,
    Poder = 3,
    Grabacion = 4,
    Acta = 5
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
