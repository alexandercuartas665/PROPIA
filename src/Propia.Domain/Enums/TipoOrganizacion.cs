namespace Propia.Domain.Enums;

/// <summary>
/// Tipo de Organizacion que administra Copropiedades.
/// Definido en spec del modulo 0.1 Super Admin Console (Administradora / Autoadministrada).
/// </summary>
public enum TipoOrganizacion
{
    Administradora = 1,
    Autoadministrada = 2
}

/// <summary>
/// Estado operativo de una Organizacion en la consola de plataforma (0.1).
/// Activa: operativa. Inactiva: suspendida temporalmente. Archivada: fuera de uso (va a "Archivados").
/// </summary>
public enum EstadoOrganizacion
{
    Activa = 1,
    Inactiva = 2,
    Archivada = 3
}
