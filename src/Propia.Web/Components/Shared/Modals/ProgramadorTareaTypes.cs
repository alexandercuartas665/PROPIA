using System;

namespace Propia.Web.Components.Shared.Modals;

/// <summary>Opcion de tablero para el dropdown del ProgramadorTareaModal.</summary>
public class TableroOpcion
{
    public Guid Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
}

/// <summary>Opcion de persona (usuario afectado) para el ProgramadorTareaModal.</summary>
public class PersonaOpcion
{
    public Guid Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
}
