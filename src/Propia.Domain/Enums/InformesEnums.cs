namespace Propia.Domain.Enums;

/// <summary>Estado de un informe de gestion generado (modulo Informes de gestion, Capa 2).</summary>
public enum EstadoInforme
{
    /// <summary>Creado desde una plantilla pero aun sin generar contenido.</summary>
    Borrador = 0,
    /// <summary>El sistema ya genero (o el usuario edito) el contenido de las secciones.</summary>
    Generado = 1,
    /// <summary>Marcado como definitivo por el administrador.</summary>
    Finalizado = 2
}
