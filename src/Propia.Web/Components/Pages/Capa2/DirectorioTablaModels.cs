using Propia.Domain.Enums;

namespace Propia.Web.Components.Pages.Capa2;

// Datos del alta inline de la vista Tabla del Directorio. El componente de vista solo
// recolecta estos datos y los emite; la pagina Directorio hace el POST con su propio Auth().
public record NuevaPersonaDir(
    TipoDocumento TipoDocumento, string Documento,
    string Nombres, string Apellidos, string? Email, string? Telefono);

public record NuevaEmpresaDir(
    string Nit, string? Dv, string RazonSocial,
    string? NombreComercial, string? Email, string? Telefono);
