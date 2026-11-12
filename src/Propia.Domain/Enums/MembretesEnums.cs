namespace Propia.Domain.Enums;

/// <summary>Tipo de membrete de documento: encabezado (arriba de la hoja) o pie de pagina.</summary>
public enum TipoMembrete
{
    Encabezado = 1,
    Pie = 2
}

/// <summary>
/// Como elige una plantilla su encabezado / pie: el predeterminado vigente de la copropiedad, ninguno, o uno
/// concreto (Fijo, con su Id). Cambiar el predeterminado cambia de una vez las plantillas en Predeterminado.
/// </summary>
public enum ModoMembrete
{
    Predeterminado = 0,
    Ninguno = 1,
    Fijo = 2
}
