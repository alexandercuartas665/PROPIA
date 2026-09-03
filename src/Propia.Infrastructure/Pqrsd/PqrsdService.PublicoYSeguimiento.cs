using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Propia.Application.Common;
using Propia.Application.Pqrsd;
using Propia.Domain.Entities;
using Propia.Domain.Enums;
using Propia.Infrastructure.Persistence;

namespace Propia.Infrastructure.Pqrsd;

// Particion de PqrsdService por area (clase parcial: comparte _db/_tenantContext/_http/_noti/_tareas/_membrete
// y los helpers transversales del archivo principal). Mismo comportamiento.
public partial class PqrsdService
{
    // Formulario publico sin login y seguimiento publico (link compartible).
    // ===================== Formulario publico (sin login) =====================

    /// <summary>Fija el tenant en el contexto y reabre la conexion para que el interceptor aplique app.tenant_id (patron de escrituras publicas).</summary>
    private async Task ActivarTenantPublicoAsync(Guid tenantId)
    {
        _tenantContext.SetTenant(tenantId);
        await _db.Database.CloseConnectionAsync();
    }

    public async Task<PqrsdPublicoConfigDto?> GetConfigPublicoAsync(Guid tenantId, CancellationToken ct)
    {
        // Tenants es entidad global: se puede leer el branding sin tenant en sesion.
        var tenant = await _db.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Id == tenantId, ct);
        if (tenant is null || tenant.Estado != EstadoCopropiedad.Activa) return null;

        await ActivarTenantPublicoAsync(tenantId);
        await AsegurarCatalogoBaseAsync(ct);   // siembra tipos/categorias si el modulo nunca se abrio en esta copropiedad

        var tipos = await _db.PqrsdTipos.AsNoTracking()
            .Where(t => t.Activo)
            .OrderBy(t => t.Orden).ThenBy(t => t.Nombre)
            .Select(t => new PqrsdTipoPublicoDto(t.Id, t.Nombre, t.DiasHabiles))
            .ToListAsync(ct);

        var cats = await _db.PqrsdCategorias.AsNoTracking()
            .Where(c => c.Activa)
            .OrderBy(c => c.Orden).ThenBy(c => c.Nombre)
            .Select(c => new PqrsdCategoriaPublicaDto(c.Id, c.Nombre))
            .ToListAsync(ct);

        // Toggles de campos opcionales del formulario + textos de encabezado/pie (default: todo visible).
        var fcfg = await _db.PqrsdFormularioPublicoConfigs.AsNoTracking().FirstOrDefaultAsync(ct);

        // Campos dinamicos marcados para pedirse en el formulario publico (en su orden).
        var camposPub = await _db.PqrsdCampos.AsNoTracking()
            .Where(c => c.Activo && c.MostrarEnPublico)
            .OrderBy(c => c.Orden).ThenBy(c => c.Label)
            .Select(c => new PqrsdCampoPublicoDto(c.Id, c.Label, c.Tipo, c.Opciones, c.Requerido, c.Descripcion))
            .ToListAsync(ct);

        // LogoUrl se guarda RELATIVA al mismo origen (convencion host unificado): la pagina publica la usa tal cual.
        return new PqrsdPublicoConfigDto(tenant.Nombre, tenant.LogoUrl, tipos, cats,
            fcfg?.MostrarTorre ?? true, fcfg?.MostrarCorreo ?? true, fcfg?.MostrarTelefono ?? true,
            fcfg?.EncabezadoTexto, fcfg?.PieTexto, camposPub,
            ParseOrdenCamposFijos(fcfg?.OrdenCamposFijosJson));
    }

    // ===================== Seguimiento publico (link compartible con el radicador) =====================

    public async Task<bool> SetAdjuntoCompartidoAsync(Guid expedienteId, Guid adjuntoId, bool compartido, CancellationToken ct)
    {
        var adj = await _db.PqrsdAdjuntos
            .FirstOrDefaultAsync(a => a.Id == adjuntoId && a.ExpedienteId == expedienteId, ct);
        if (adj is null) return false;
        adj.Compartido = compartido;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<Guid?> ObtenerOCrearShareTokenAsync(Guid expedienteId, CancellationToken ct)
    {
        var exp = await _db.PqrsdExpedientes.FirstOrDefaultAsync(e => e.Id == expedienteId, ct);
        if (exp is null) return null;
        if (exp.ShareToken is null)
        {
            exp.ShareToken = Guid.NewGuid();
            await _db.SaveChangesAsync(ct);
        }
        return exp.ShareToken;
    }

    public async Task<PqrsdSeguimientoPublicoDto?> GetSeguimientoPublicoAsync(Guid tenantId, Guid token, CancellationToken ct)
    {
        // Tenants es entidad global: se lee el branding sin tenant en sesion.
        var tenant = await _db.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Id == tenantId, ct);
        if (tenant is null || tenant.Estado != EstadoCopropiedad.Activa) return null;

        await ActivarTenantPublicoAsync(tenantId);

        var exp = await _db.PqrsdExpedientes.AsNoTracking()
            .Include(e => e.Categoria)
            .Include(e => e.TipoConfig)
            .Include(e => e.EstadoColumna)
            .Include(e => e.Adjuntos)
            .FirstOrDefaultAsync(e => e.ShareToken == token, ct);
        if (exp is null) return null;

        var tipoNombre = exp.TipoConfig?.Nombre ?? exp.Tipo.ToString();
        var estadoNombre = exp.EstadoColumna?.Nombre ?? exp.Estado.ToString();

        var adjuntos = exp.Adjuntos
            .Where(a => a.Compartido)
            .OrderBy(a => a.CreatedAt)
            .Select(a => new PqrsdSeguimientoAdjuntoDto(a.Id, a.NombreArchivo, a.TipoMime, a.TamanioBytes, a.UrlStorage, a.CreatedAt))
            .ToList();

        return new PqrsdSeguimientoPublicoDto(
            tenant.Nombre, tenant.LogoUrl,
            exp.NumeroRadicado, tipoNombre, exp.Categoria?.Nombre ?? "-", estadoNombre,
            exp.CreatedAt, exp.RespuestaAdmin, exp.RespuestaAdminAt, adjuntos);
    }

}
