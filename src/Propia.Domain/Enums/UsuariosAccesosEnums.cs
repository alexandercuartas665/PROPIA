namespace Propia.Domain.Enums;

/// <summary>Estado de la credencial de acceso de un usuario. Spec 2.5 v1.0 tabla usuario.estado.</summary>
public enum EstadoUsuario
{
    Activo = 1,
    Inactivo = 2,
    Pendiente = 3,
    Bloqueado = 4
}

/// <summary>Tipo de metodo de autenticacion vinculado al usuario. Spec 2.5 v1.0 seccion 11.</summary>
public enum TipoAuthMetodo
{
    EmailPassword = 1,
    MagicLink = 2,
    WhatsappOtp = 3,
    GoogleSso = 4,
    MicrosoftSso = 5
}

/// <summary>Tipo de rol. Spec 2.5 v1.0 tabla rol.tipo.</summary>
public enum TipoRol
{
    /// <summary>Rol de sistema PROPIA (Super Admin) - global, no editable.</summary>
    Sistema = 1,
    /// <summary>Rol base no eliminable (Administrador, Consejero, Propietario, Residente, Operario).</summary>
    Base = 2,
    /// <summary>Rol extendido predefinido por PROPIA (Coordinador, Contador, etc) - desactivable.</summary>
    Extendido = 3,
    /// <summary>Rol personalizado creado por la copropiedad.</summary>
    Personalizado = 4
}

/// <summary>Accion sobre un modulo. Spec 2.5 v1.0 tabla rol_permiso.accion.</summary>
public enum AccionPermiso
{
    Ver = 1,
    Crear = 2,
    Editar = 3,
    Eliminar = 4,
    Aprobar = 5,
    Exportar = 6
}

/// <summary>Nivel de dato al que aplica el permiso. Spec 2.5 v1.0 tabla rol_permiso.nivel_dato.</summary>
public enum NivelDato
{
    /// <summary>Sin acceso al modulo en este nivel.</summary>
    SinAcceso = 0,
    /// <summary>Solo datos propios del usuario (su unidad, sus PQRS).</summary>
    Propio = 1,
    /// <summary>Toda la copropiedad.</summary>
    Copropiedad = 2
}

/// <summary>Estado del vinculo usuario-copropiedad. Spec 2.5 v1.0 tabla usuario_copropiedad_rol.estado.</summary>
public enum EstadoVinculoUsuario
{
    Activo = 1,
    Inactivo = 2
}

/// <summary>Estado de una invitacion al sistema. Spec 2.5 v1.0 tabla usuario_invitacion.estado.</summary>
public enum EstadoInvitacion
{
    Pendiente = 1,
    Aceptada = 2,
    Expirada = 3,
    Cancelada = 4
}

/// <summary>Canal por donde se envia la invitacion al usuario.</summary>
public enum CanalEnvioInvitacion
{
    Email = 1,
    Whatsapp = 2,
    Ambos = 3
}

/// <summary>Tipo de evento auditado. Spec 2.5 v1.0 tabla acceso_auditoria.tipo_evento.</summary>
public enum TipoEventoAuditoria
{
    LoginExitoso = 1,
    LoginFallido = 2,
    Logout = 3,
    InvitacionEnviada = 10,
    InvitacionAceptada = 11,
    InvitacionExpirada = 12,
    InvitacionCancelada = 13,
    AccesoOtorgado = 20,
    AccesoRevocado = 21,
    RolCambiado = 30,
    RolCreado = 31,
    RolEditado = 32,
    RolEliminado = 33,
    RolCopiado = 34,
    PermisoModificado = 35,
    SesionCerradaRemota = 40,
    BloqueoPorIntentos = 41,
    Desbloqueo = 42,
    ExportacionUsuarios = 50
}

/// <summary>Canal por el que ocurrio una accion auditada.</summary>
public enum CanalAcceso
{
    Web = 1,
    Whatsapp = 2,
    Api = 3,
    Sistema = 4
}

/// <summary>
/// Codigos de modulo que se usan en rol_permiso.modulo_codigo. Centralizamos aqui para
/// evitar typos y para que la matriz de permisos se mantenga consistente con la spec.
/// </summary>
public static class ModuloCodigo
{
    public const string Dashboard = "DASHBOARD";
    public const string MiCopropiedad = "MI_COPROPIEDAD";
    public const string Directorio = "DIRECTORIO";
    public const string UsuariosAccesos = "USUARIOS_ACCESOS";
    public const string Presupuesto = "PRESUPUESTO";
    public const string Cartera = "CARTERA";
    public const string Asambleas = "ASAMBLEAS";
    public const string Pqrs = "PQRS";
    public const string Tareas = "TAREAS";
    public const string Mantenimiento = "MANTENIMIENTO";
    public const string Porteria = "PORTERIA";
    public const string Reservas = "RESERVAS";
    public const string Comunicaciones = "COMUNICACIONES";
    public const string Documentos = "DOCUMENTOS";
    public const string Reportes = "REPORTES";

    public static readonly string[] Todos = new[]
    {
        Dashboard, MiCopropiedad, Directorio, UsuariosAccesos,
        Presupuesto, Cartera, Asambleas, Pqrs, Tareas, Mantenimiento,
        Porteria, Reservas, Comunicaciones, Documentos, Reportes
    };
}
