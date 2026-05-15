using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Propia.Application.Common;

namespace Propia.Infrastructure.Persistence;

/// <summary>
/// Interceptor que setea <c>app.tenant_id</c> en CADA conexion PostgreSQL que EF Core
/// abre, garantizando que RLS vea el tenant correcto sin importar de que conexion
/// del pool de Npgsql venga.
///
/// El TenantMiddleware ya setea ITenantContext.CurrentTenantId al inicio del request,
/// pero con connection pooling no podemos garantizar que SELECT/INSERT corran en la
/// misma conexion fisica donde el middleware hizo SET. Este interceptor cierra ese gap.
///
/// Si CurrentTenantId es null (request publico, login, etc.) limpiamos por si la
/// conexion del pool venia con app.tenant_id de otro request.
/// </summary>
public sealed class TenantConnectionInterceptor : DbConnectionInterceptor
{
    private readonly ITenantContext _tenantContext;

    public TenantConnectionInterceptor(ITenantContext tenantContext)
    {
        _tenantContext = tenantContext;
    }

    public override async Task ConnectionOpenedAsync(
        DbConnection connection,
        ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT set_config('app.tenant_id', @tid, false)";
        var p = cmd.CreateParameter();
        p.ParameterName = "tid";
        p.Value = _tenantContext.CurrentTenantId?.ToString() ?? string.Empty;
        cmd.Parameters.Add(p);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT set_config('app.tenant_id', @tid, false)";
        var p = cmd.CreateParameter();
        p.ParameterName = "tid";
        p.Value = _tenantContext.CurrentTenantId?.ToString() ?? string.Empty;
        cmd.Parameters.Add(p);
        cmd.ExecuteNonQuery();
    }
}
