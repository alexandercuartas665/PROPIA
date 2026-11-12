using Microsoft.EntityFrameworkCore;
using Propia.Application.SuperAdmin;
using Propia.Domain.Entities;
using Propia.Domain.Enums;
using Propia.Infrastructure.Persistence;
using Propia.Infrastructure.SuperAdmin;
using Xunit;

namespace Propia.Integration.Tests;

/// <summary>
/// Semillas globales de la Consola A&amp;D (servicio, con el rol de runtime propia_app SIN copropiedad: RLS solo
/// deja ver las filas globales): roles base (crear extendido, permisos por defecto, Administrador protegido, no se
/// elimina un rol en uso), plantillas de comunicados, etiquetas del Directorio y de documentos (PUT MERGE).
/// </summary>
[Collection(nameof(PostgresCollection))]
public class SemillasAdminFlowTests
{
    private readonly PostgresFixture _fx;
    public SemillasAdminFlowTests(PostgresFixture fx) => _fx = fx;

    private ISemillasAdminService Build()
    {
        var tenantCtx = new TenantContext();
        var options = new DbContextOptionsBuilder<PropiaDbContext>()
            .UseNpgsql(_fx.AppConnectionString)
            .AddInterceptors(new TenantConnectionInterceptor(tenantCtx))
            .Options;
        return new SemillasAdminService(new PropiaDbContext(options, tenantCtx), tenantCtx);
    }

    private PropiaDbContext OwnerDb()
        => new(new DbContextOptionsBuilder<PropiaDbContext>().UseNpgsql(_fx.OwnerConnectionString).Options, new TenantContext());

    [Fact]
    public async Task Roles_base_crear_permisos_proteccion_y_borrado()
    {
        var svc = Build();
        var ct = CancellationToken.None;
        var nombre = $"[TEST] Rol {Guid.NewGuid():N}"[..40];

        var rol = await svc.CrearRolAsync(new GuardarRolSemillaRequest(nombre, "Rol de prueba", CategoriaRol.Administrativo), ct);
        Assert.Equal(TipoRol.Extendido, rol.Tipo);
        Assert.True(rol.EsEliminable);
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.CrearRolAsync(new GuardarRolSemillaRequest(nombre.ToUpperInvariant()), ct));

        // Permisos por defecto: upsert (repetir no duplica).
        var p = await svc.GuardarPermisosRolAsync(rol.Id, new GuardarPermisosRolSemillaRequest(new[]
        {
            new RolPermisoSemillaDto(ModuloCodigo.Documentos, AccionPermiso.Ver, true, NivelDato.Copropiedad),
            new RolPermisoSemillaDto("documentos", AccionPermiso.Crear, true, NivelDato.Copropiedad),
        }), ct);
        Assert.Equal(2, p!.Permisos.Count);
        p = await svc.GuardarPermisosRolAsync(rol.Id, new GuardarPermisosRolSemillaRequest(new[]
            { new RolPermisoSemillaDto(ModuloCodigo.Documentos, AccionPermiso.Crear, false, NivelDato.SinAcceso) }), ct);
        Assert.Equal(2, p!.Permisos.Count);
        Assert.False(p.Permisos.Single(x => x.Accion == AccionPermiso.Crear).Habilitado);
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.GuardarPermisosRolAsync(rol.Id,
            new GuardarPermisosRolSemillaRequest(new[] { new RolPermisoSemillaDto("NO_EXISTE", AccionPermiso.Ver, true, NivelDato.Copropiedad) }), ct));

        // MERGE: solo la descripcion.
        var upd = await svc.ActualizarRolAsync(rol.Id, new GuardarRolSemillaRequest(Descripcion: "Otra"), ct);
        Assert.Equal(nombre, upd!.Nombre);
        Assert.Equal("Otra", upd.Descripcion);

        // Administrador: no se renombra ni se desactiva.
        var admin = (await svc.ListarRolesAsync(ct)).Single(r => r.Nombre == "Administrador");
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.ActualizarRolAsync(admin.Id, new GuardarRolSemillaRequest(Nombre: "Admin"), ct));
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.ActualizarRolAsync(admin.Id, new GuardarRolSemillaRequest(Activo: false), ct));
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.EliminarRolAsync(admin.Id, ct));

        Assert.True(await svc.EliminarRolAsync(rol.Id, ct));
        Assert.DoesNotContain(await svc.ListarRolesAsync(ct), r => r.Id == rol.Id);
    }

    [Fact]
    public async Task Comunicados_y_etiquetas_globales_crear_y_merge()
    {
        var svc = Build();
        var ct = CancellationToken.None;
        var sufijo = Guid.NewGuid().ToString("N")[..8];

        var com = await svc.CrearPlantillaComunicadoAsync(new GuardarComunicadoPlantillaSemillaRequest(
            $"[TEST] Circular {sufijo}", TipoComunicadoBase.Circular, "Asunto {{fecha}}", "<p>Hola</p><script>x()</script>", true), ct);
        Assert.DoesNotContain("<script", com.CuerpoModelo);   // S-19
        var com2 = await svc.ActualizarPlantillaComunicadoAsync(com.Id, new GuardarComunicadoPlantillaSemillaRequest(Activa: false), ct);
        Assert.False(com2!.Activa);
        Assert.Equal("Asunto {{fecha}}", com2.AsuntoModelo);
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.CrearPlantillaComunicadoAsync(new GuardarComunicadoPlantillaSemillaRequest("Sin asunto"), ct));

        var etq = await svc.CrearEtiquetaDirectorioAsync(new GuardarEtiquetaDirectorioSemillaRequest(Nombre: $"[TEST] Proveedor clave {sufijo}", Color: "#6d4fe3"), ct);
        Assert.StartsWith("TEST_PROVEEDOR_CLAVE", etq.Codigo);
        Assert.Equal("#6D4FE3", etq.Color);
        var etq2 = await svc.ActualizarEtiquetaDirectorioAsync(etq.Id, new GuardarEtiquetaDirectorioSemillaRequest(Orden: 99), ct);
        Assert.Equal(99, etq2!.Orden);
        Assert.Equal(etq.Nombre, etq2.Nombre);
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.ActualizarEtiquetaDirectorioAsync(etq.Id, new GuardarEtiquetaDirectorioSemillaRequest(Color: "violeta"), ct));

        var doc = await svc.CrearEtiquetaDocumentoAsync(new GuardarEtiquetaDocumentoSemillaRequest($"[TEST] Doc {sufijo}"), ct);
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.CrearEtiquetaDocumentoAsync(new GuardarEtiquetaDocumentoSemillaRequest(doc.Nombre.ToLowerInvariant()), ct));

        var indice = await svc.IndiceAsync(ct);
        Assert.Equal(10, indice.Count);
        Assert.All(indice, s => Assert.NotEmpty(s.Rutas));

        await using var db = OwnerDb();
        db.ComunicadoPlantillas.Remove(await db.ComunicadoPlantillas.IgnoreQueryFilters().SingleAsync(x => x.Id == com.Id));
        db.EtiquetasCatalogo.Remove(await db.EtiquetasCatalogo.IgnoreQueryFilters().SingleAsync(x => x.Id == etq.Id));
        db.DocumentoEtiquetasCatalogo.Remove(await db.DocumentoEtiquetasCatalogo.IgnoreQueryFilters().SingleAsync(x => x.Id == doc.Id));
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Reportes_se_editan_y_activan_sin_crear()
    {
        var svc = Build();
        var ct = CancellationToken.None;
        var cats = await svc.ListarReportesAsync(ct);
        var rep = cats.SelectMany(c => c.Reportes).FirstOrDefault();
        if (rep is null) return;   // el entorno de prueba no sembro reportes

        var upd = await svc.ActualizarReporteAsync(rep.Id, new GuardarReporteSemillaRequest(AudienciasJson: "[\"administrador\",\"consejo\",\"consejo\"]"), ct);
        Assert.Equal("[\"ADMINISTRADOR\",\"CONSEJO\"]", upd!.AudienciasJson);
        Assert.Equal(rep.Nombre, upd.Nombre);
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.ActualizarReporteAsync(rep.Id, new GuardarReporteSemillaRequest(AudienciasJson: "no-json"), ct));
        await svc.ActualizarReporteAsync(rep.Id, new GuardarReporteSemillaRequest(AudienciasJson: rep.AudienciasJson), ct);
    }
}
