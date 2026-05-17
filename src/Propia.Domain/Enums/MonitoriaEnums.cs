namespace Propia.Domain.Enums;

/// <summary>Severidad de un incidente. Modulo 0.3 Monitoria y Auditoria Global.</summary>
public enum SeveridadIncidente
{
    Info = 1,
    Advertencia = 2,
    Error = 3,
    Critico = 4
}

/// <summary>Estado del ciclo de vida de un incidente. Modulo 0.3.</summary>
public enum EstadoIncidente
{
    Abierto = 1,
    EnInvestigacion = 2,
    Resuelto = 3,
    Cerrado = 4,
    FalsoPositivo = 5
}

/// <summary>Tipo de evento del log centralizado. Append-only.</summary>
public enum TipoEventoSistema
{
    AccesoExitoso = 1,
    AccesoFallido = 2,
    PermisoNegado = 3,
    ConfiguracionGlobalCambiada = 4,
    TenantSuspendido = 5,
    TenantReactivado = 6,
    OrganizacionCreada = 7,
    TransferenciaCustodiaEjecutada = 8,
    JobAutomatico = 9,
    OperacionDestructiva = 10,
    ErrorInfraestructura = 11
}
