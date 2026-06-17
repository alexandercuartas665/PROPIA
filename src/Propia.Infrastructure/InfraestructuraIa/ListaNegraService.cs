using Microsoft.EntityFrameworkCore;
using Propia.Application.Common;
using Propia.Application.InfraestructuraIa;
using Propia.Domain.Entities;
using Propia.Infrastructure.Persistence;

namespace Propia.Infrastructure.InfraestructuraIa;

/// <summary>
/// Lista negra global del tenant: numeros que ningun agente de IA debe atender.
/// Portado desde CUBOT.travels (BlockedNumberService).
/// </summary>
public sealed class ListaNegraService : IListaNegraService
{
    private readonly PropiaDbContext _db;
    private readonly ITenantContext _tenant;

    public ListaNegraService(PropiaDbContext db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<IReadOnlyList<NumeroEnListaNegraDto>> ListarAsync(CancellationToken ct = default)
    {
        return await _db.NumerosEnListaNegra.AsNoTracking()
            .OrderByDescending(b => b.CreatedAt)
            .Select(b => new NumeroEnListaNegraDto(b.Id, b.Telefono, b.Nota, b.CreatedAt))
            .ToListAsync(ct);
    }

    public async Task<NumeroEnListaNegraDto?> AgregarAsync(AgregarNumeroBloqueadoRequest req, CancellationToken ct = default)
    {
        if (_tenant.CurrentTenantId is not Guid tenantId) return null;
        var digits = Normalizar(req.Telefono);
        if (digits.Length < 7) return null;

        // Dedupe por digitos: si ya existe equivalente, devolverlo.
        var existing = await _db.NumerosEnListaNegra
            .FirstOrDefaultAsync(b => b.Telefono == digits, ct);
        if (existing is not null)
        {
            return new NumeroEnListaNegraDto(existing.Id, existing.Telefono, existing.Nota, existing.CreatedAt);
        }

        var row = new NumeroEnListaNegra
        {
            TenantId = tenantId,
            Telefono = digits,
            Nota = string.IsNullOrWhiteSpace(req.Nota) ? null : req.Nota.Trim()
        };
        _db.NumerosEnListaNegra.Add(row);
        await _db.SaveChangesAsync(ct);
        return new NumeroEnListaNegraDto(row.Id, row.Telefono, row.Nota, row.CreatedAt);
    }

    public async Task<bool> EliminarAsync(Guid id, CancellationToken ct = default)
    {
        var row = await _db.NumerosEnListaNegra.FirstOrDefaultAsync(b => b.Id == id, ct);
        if (row is null) return false;
        _db.NumerosEnListaNegra.Remove(row);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> EstaBloqueadoAsync(string telefono, CancellationToken ct = default)
    {
        var digits = Normalizar(telefono);
        if (digits.Length < 7) return false;
        return await _db.NumerosEnListaNegra.AnyAsync(b => b.Telefono == digits, ct);
    }

    private static string Normalizar(string? telefono) =>
        new string((telefono ?? string.Empty).Where(char.IsDigit).ToArray());
}
