namespace Propia.Domain.Enums;

/// <summary>
/// Modulos de Capa 1 sujetos a permisos por cargo. Spec 1.3 v1.0 tabla
/// <c>org_cargo_permiso.modulo</c> y <c>org_colaborador_permiso.modulo</c>.
/// </summary>
public enum ModuloCapa1
{
    PanelConsolidado = 1,
    CalendarioMultiPh = 2,
    GestionEquipo = 3,
    ReportesConsolidados = 4,
    TransferenciaCustodia = 5
}

/// <summary>
/// Nivel de permiso por modulo de Capa 1. Spec 1.3 v1.0 seccion 4.2.
/// </summary>
public enum NivelPermisoCapa1
{
    /// <summary>El modulo no es visible en la navegacion.</summary>
    SinAcceso = 0,
    /// <summary>Puede ver, crear, editar y eliminar dentro del modulo.</summary>
    Completo = 1,
    /// <summary>Solo puede ver - sin acciones de escritura.</summary>
    Lectura = 2,
    /// <summary>Ve y opera solo sobre las copropiedades asignadas a el.</summary>
    Asignado = 3
}

/// <summary>Estado de un colaborador dentro de la organizacion. Spec 1.3 v1.0 tabla org_colaborador.estado.</summary>
public enum EstadoColaborador
{
    /// <summary>Cuenta activada y con acceso operativo.</summary>
    Activo = 1,
    /// <summary>Invitacion enviada, cuenta sin activar.</summary>
    Pendiente = 2,
    /// <summary>Desvinculado - sin acceso pero el registro se conserva en historial.</summary>
    Inactivo = 3
}

/// <summary>Tipo de evento registrado en el historial del colaborador. Spec 1.3 v1.0 tabla org_colaborador_historial.</summary>
public enum TipoEventoEquipo
{
    Vinculacion = 1,
    Desvinculacion = 2,
    CambioCargo = 3,
    PhAsignada = 4,
    PhRemovida = 5,
    PermisoAjustado = 6,
    EstadoCambiado = 7,
    RolPhCambiado = 8
}

/// <summary>
/// Catalogo base de cargos por defecto que se crean al activar una organizacion.
/// Spec 1.3 v1.0 seccion 3 - los cargos son 100% configurables pero la plataforma
/// trae estos seis al inicio para acelerar el setup.
/// </summary>
public static class CargoCatalogoBase
{
    public const string Director = "Director";
    public const string Coordinador = "Coordinador";
    public const string Recorredor = "Recorredor";
    public const string AsistenteAdministrativo = "Asistente Administrativo";
    public const string AsistenteCartera = "Asistente de Cartera";
    public const string AsistenteFacturacion = "Asistente de Facturacion";

    /// <summary>
    /// Plantilla de permisos por defecto por cargo (spec 1.3 v1.0 tabla 4.3).
    /// Indices: Director, Coordinador, Recorredor, AsistenteAdm, AsistenteCartera, AsistenteFact.
    /// </summary>
    public static readonly (string Cargo, (ModuloCapa1 Modulo, NivelPermisoCapa1 Nivel)[] Permisos)[] PermisosPorDefecto = new[]
    {
        (Director, new[]
        {
            (ModuloCapa1.PanelConsolidado, NivelPermisoCapa1.Completo),
            (ModuloCapa1.CalendarioMultiPh, NivelPermisoCapa1.Completo),
            (ModuloCapa1.GestionEquipo, NivelPermisoCapa1.Completo),
            (ModuloCapa1.ReportesConsolidados, NivelPermisoCapa1.Completo),
            (ModuloCapa1.TransferenciaCustodia, NivelPermisoCapa1.Completo)
        }),
        (Coordinador, new[]
        {
            (ModuloCapa1.PanelConsolidado, NivelPermisoCapa1.Asignado),
            (ModuloCapa1.CalendarioMultiPh, NivelPermisoCapa1.Asignado),
            (ModuloCapa1.GestionEquipo, NivelPermisoCapa1.Lectura),
            (ModuloCapa1.ReportesConsolidados, NivelPermisoCapa1.Asignado),
            (ModuloCapa1.TransferenciaCustodia, NivelPermisoCapa1.SinAcceso)
        }),
        (Recorredor, new[]
        {
            (ModuloCapa1.PanelConsolidado, NivelPermisoCapa1.Asignado),
            (ModuloCapa1.CalendarioMultiPh, NivelPermisoCapa1.Lectura),
            (ModuloCapa1.GestionEquipo, NivelPermisoCapa1.SinAcceso),
            (ModuloCapa1.ReportesConsolidados, NivelPermisoCapa1.SinAcceso),
            (ModuloCapa1.TransferenciaCustodia, NivelPermisoCapa1.SinAcceso)
        }),
        (AsistenteAdministrativo, new[]
        {
            (ModuloCapa1.PanelConsolidado, NivelPermisoCapa1.Asignado),
            (ModuloCapa1.CalendarioMultiPh, NivelPermisoCapa1.Asignado),
            (ModuloCapa1.GestionEquipo, NivelPermisoCapa1.SinAcceso),
            (ModuloCapa1.ReportesConsolidados, NivelPermisoCapa1.Lectura),
            (ModuloCapa1.TransferenciaCustodia, NivelPermisoCapa1.SinAcceso)
        }),
        (AsistenteCartera, new[]
        {
            (ModuloCapa1.PanelConsolidado, NivelPermisoCapa1.Asignado),
            (ModuloCapa1.CalendarioMultiPh, NivelPermisoCapa1.Lectura),
            (ModuloCapa1.GestionEquipo, NivelPermisoCapa1.SinAcceso),
            (ModuloCapa1.ReportesConsolidados, NivelPermisoCapa1.Asignado),
            (ModuloCapa1.TransferenciaCustodia, NivelPermisoCapa1.SinAcceso)
        }),
        (AsistenteFacturacion, new[]
        {
            (ModuloCapa1.PanelConsolidado, NivelPermisoCapa1.Asignado),
            (ModuloCapa1.CalendarioMultiPh, NivelPermisoCapa1.Lectura),
            (ModuloCapa1.GestionEquipo, NivelPermisoCapa1.SinAcceso),
            (ModuloCapa1.ReportesConsolidados, NivelPermisoCapa1.Asignado),
            (ModuloCapa1.TransferenciaCustodia, NivelPermisoCapa1.SinAcceso)
        })
    };
}
