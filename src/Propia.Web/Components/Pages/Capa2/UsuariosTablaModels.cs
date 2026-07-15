using Propia.Domain.Enums;

namespace Propia.Web.Components.Pages.Capa2;

// Datos del alta inline en la tabla de Usuarios. El componente de fila solo los recolecta;
// la pagina Usuarios encuentra/crea la persona en el Directorio y envia la invitacion.
public record NuevoUsuarioInvitar(
    TipoDocumento TipoDocumento, string Documento,
    string Nombres, string Apellidos, Guid RolId);
