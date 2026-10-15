using Propia.Domain.Common;

namespace Propia.Domain.Entities;

/// <summary>
/// Comentario sobre una PERSONA en el contexto de una copropiedad (ficha de Residente/Directorio).
/// Tenant-scoped (una copropiedad no ve los comentarios de otra). El autor es el ApplicationUser actual;
/// el nombre se resuelve al leer. Mismo patron que TareaComentario / PqrsdComentario.
/// </summary>
public class PersonaComentario : TenantEntity
{
    public Guid PersonaId { get; set; }
    public Guid AutorUsuarioId { get; set; }
    public string Texto { get; set; } = string.Empty;
}
