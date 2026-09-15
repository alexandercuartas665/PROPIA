using Microsoft.EntityFrameworkCore;
using Propia.Domain.Enums;
using Propia.Infrastructure.Persistence;

namespace Propia.Infrastructure.MiCopropiedad;

/// <summary>
/// Helpers COMPARTIDOS de los tipos de campo avanzados (Fase 2): Formula, Usuario y Directorio. La logica
/// es identica en las 8 superficies del Selector de Campos, asi que vive aqui para que cada servicio solo
/// la invoque (validacion del valor) y solo aporte lo que ES propio de su superficie (el resolver de campos
/// de sistema para las Formulas). El computo de la Formula en si es <see cref="Propia.Application.MiCopropiedad.CampoFormulaConfig.ComputarTexto"/>.
/// </summary>
public static class CamposAvanzados
{
    /// <summary>
    /// Valida el valor que se intenta guardar en un campo segun su tipo:
    /// - Formula: es calculado y de SOLO LECTURA -> no admite valor.
    /// - Usuario: el valor es el Guid de una persona que debe ser USUARIO del tenant.
    /// - Directorio: el valor es el Guid de una persona que debe estar en el DIRECTORIO del tenant.
    /// Las dos consultas van acotadas por RLS al tenant activo (usuarios_tenant / directorio_vinculos son
    /// TenantEntity), asi que un id de OTRO tenant NO valida -> rechazo CROSS-TENANT con mensaje explicito
    /// (no un "0 filas" mudo). Celda vacia = no se valida (permite limpiar el campo).
    /// </summary>
    public static async Task ValidarValorAsync(PropiaDbContext db, TipoCampoTablero tipo, string? valor, CancellationToken ct)
    {
        if (tipo == TipoCampoTablero.Formula)
            throw new InvalidOperationException("Un campo Formula es calculado y de solo lectura; no admite valor.");
        if (string.IsNullOrWhiteSpace(valor)) return;

        if (tipo == TipoCampoTablero.Usuario)
        {
            if (!Guid.TryParse(valor, out var personaId))
                throw new InvalidOperationException("El valor de un campo Usuario debe ser el id de un usuario del tenant.");
            if (!await db.UsuariosTenant.AnyAsync(u => u.PersonaId == personaId, ct))
                throw new InvalidOperationException("El usuario seleccionado no pertenece a esta copropiedad.");
        }
        else if (tipo == TipoCampoTablero.Directorio)
        {
            if (!Guid.TryParse(valor, out var entidadId))
                throw new InvalidOperationException("El valor de un campo Directorio debe ser el id de una persona del directorio.");
            if (!await db.DirectorioVinculos.AnyAsync(v =>
                    v.EntidadTipo == EntidadDirectorio.Persona && v.EntidadId == entidadId && v.Estado == EstadoVinculo.Activo, ct))
                throw new InvalidOperationException("La persona seleccionada no esta en el directorio de esta copropiedad.");
        }
    }
}
