using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Propia.Application.Common;
using Propia.Application.Tareas;
using Propia.Domain.Entities;
using Propia.Domain.Enums;
using Propia.Infrastructure.Persistence;
using Propia.Infrastructure.Tareas;
using System.Security.Claims;
using Xunit;

namespace Propia.Integration.Tests;

/// <summary>Tests del modulo 2.10 Tareas y Proyectos (spec v1.0 MVP).</summary>
[Collection(nameof(PostgresCollection))]
public class TareasFlowTests : IAsyncLifetime
{
    private readonly PostgresFixture _fx;
    private IServiceProvider _services = null!;

    public TareasFlowTests(PostgresFixture fx) => _fx = fx;

    public Task InitializeAsync()
    {
        var sc = new ServiceCollection();
        sc.AddLogging();
        sc.AddDataProtection();
        sc.AddSingleton<ITenantContext, TenantContext>();
        sc.AddSingleton<IHttpContextAccessor>(new HttpContextAccessor { HttpContext = BuildFakeHttpContext() });
        sc.AddScoped<TenantConnectionInterceptor>();
        sc.AddDbContext<PropiaDbContext>((sp, opts) =>
            opts.UseNpgsql(_fx.AppConnectionString)
                .AddInterceptors(sp.GetRequiredService<TenantConnectionInterceptor>()));
        sc.AddIdentityCore<ApplicationUser>()
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<PropiaDbContext>()
            .AddDefaultTokenProviders();
        sc.AddSingleton<Propia.Application.Notificaciones.INotificacionDispatcher, FakeNotificacionDispatcher>();
        _services = sc.BuildServiceProvider();
        return Task.CompletedTask;
    }
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Seed_lazy_de_6_estados_base_incluye_Pendiente_Completada_Cancelada()
    {
        var tenantId = await SeedTenantAsync("Tareas Seed");
        var (svc, _, _) = Build(tenantId);

        var estados = await svc.ListarEstadosAsync(CancellationToken.None);

        Assert.Equal(6, estados.Count);
        Assert.Contains(estados, e => e.Nombre == EstadoTareaBase.Pendiente && !e.EsTerminal);
        Assert.Contains(estados, e => e.Nombre == EstadoTareaBase.Completada && e.EsTerminal);
        Assert.Contains(estados, e => e.Nombre == EstadoTareaBase.Cancelada && e.EsTerminal);

        await CleanTenant(tenantId);
    }

    [Fact]
    public async Task Crear_tarea_genera_numero_y_arranca_Pendiente_con_historial()
    {
        var tenantId = await SeedTenantAsync("Tareas Crear");
        var (svc, _, _) = Build(tenantId);

        var t = await svc.CrearTareaAsync(new CrearTareaRequest(
            "Limpiar tanque", "Mantenimiento mensual del tanque", PrioridadTarea.Alta,
            null, null, null, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7)), null, null), CancellationToken.None);

        Assert.StartsWith($"T-{DateTime.UtcNow.Year}-", t.NumeroTarea);
        Assert.Equal(EstadoTareaBase.Pendiente, t.Estado.Nombre);
        Assert.Equal(PrioridadTarea.Alta, t.Prioridad);
        Assert.Single(t.Historial);
        Assert.Equal(TipoEventoTarea.Creada, t.Historial[0].TipoEvento);

        await CleanTenant(tenantId);
    }

    [Fact]
    public async Task Cambiar_estado_a_Completada_marca_fecha_completada_y_registra_historial()
    {
        var tenantId = await SeedTenantAsync("Tareas Completar");
        var (svc, db, _) = Build(tenantId);

        var t = await svc.CrearTareaAsync(new CrearTareaRequest(
            "Tarea T", null, PrioridadTarea.Normal, null, null, null, null, null, null), CancellationToken.None);
        var estados = await svc.ListarEstadosAsync(CancellationToken.None);
        var completada = estados.First(e => e.Nombre == EstadoTareaBase.Completada);

        // Completada es estado terminal -> requiere motivo de cierre.
        var motivoId = await CrearMotivoTareasAsync(db);
        await svc.CambiarEstadoAsync(t.Id, new CambiarEstadoRequest(completada.Id, null, motivoId), CancellationToken.None);

        var d = await svc.GetTareaAsync(t.Id, CancellationToken.None);
        Assert.Equal(EstadoTareaBase.Completada, d!.Estado.Nombre);
        Assert.NotNull(d.FechaCompletada);
        Assert.Contains(d.Historial, h => h.TipoEvento == TipoEventoTarea.EstadoCambiado);

        await CleanTenant(tenantId);
    }

    [Fact]
    public async Task Cancelar_requiere_motivo_explicito()
    {
        var tenantId = await SeedTenantAsync("Tareas Cancelar");
        var (svc, db, _) = Build(tenantId);

        var t = await svc.CrearTareaAsync(new CrearTareaRequest(
            "Tarea T", null, PrioridadTarea.Normal, null, null, null, null, null, null), CancellationToken.None);
        var cancelada = (await svc.ListarEstadosAsync(CancellationToken.None)).First(e => e.Nombre == EstadoTareaBase.Cancelada);

        // Sin motivo de cierre -> error
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.CambiarEstadoAsync(t.Id, new CambiarEstadoRequest(cancelada.Id, null), CancellationToken.None));

        // Con motivo de cierre -> ok
        var motivoId = await CrearMotivoTareasAsync(db);
        await svc.CambiarEstadoAsync(t.Id, new CambiarEstadoRequest(cancelada.Id, "No es necesario", motivoId), CancellationToken.None);
        var d = await svc.GetTareaAsync(t.Id, CancellationToken.None);
        Assert.Equal(EstadoTareaBase.Cancelada, d!.Estado.Nombre);

        await CleanTenant(tenantId);
    }

    [Fact]
    public async Task Numero_es_unico_y_secuencial_dentro_del_tenant()
    {
        var tenantId = await SeedTenantAsync("Tareas Numero");
        var (svc, _, _) = Build(tenantId);

        var t1 = await svc.CrearTareaAsync(new CrearTareaRequest("Tarea A", null, PrioridadTarea.Normal, null, null, null, null, null, null), CancellationToken.None);
        var t2 = await svc.CrearTareaAsync(new CrearTareaRequest("Tarea B", null, PrioridadTarea.Normal, null, null, null, null, null, null), CancellationToken.None);
        var t3 = await svc.CrearTareaAsync(new CrearTareaRequest("Tarea C", null, PrioridadTarea.Normal, null, null, null, null, null, null), CancellationToken.None);

        Assert.Equal(3, new[] { t1.NumeroTarea, t2.NumeroTarea, t3.NumeroTarea }.Distinct().Count());
        Assert.EndsWith("0003", t3.NumeroTarea);

        await CleanTenant(tenantId);
    }

    [Fact]
    public async Task Tarea_padre_e_hijos_se_relacionan_y_la_ficha_lista_subtareas()
    {
        var tenantId = await SeedTenantAsync("Tareas Padre");
        var (svc, _, _) = Build(tenantId);

        var padre = await svc.CrearTareaAsync(new CrearTareaRequest(
            "Proyecto pintura", null, PrioridadTarea.Alta, null, null, null, null, null, null), CancellationToken.None);
        var h1 = await svc.CrearTareaAsync(new CrearTareaRequest(
            "Comprar pintura", null, PrioridadTarea.Normal, null, null, null, null, padre.Id, null), CancellationToken.None);
        var h2 = await svc.CrearTareaAsync(new CrearTareaRequest(
            "Pintar fachada", null, PrioridadTarea.Normal, null, null, null, null, padre.Id, null), CancellationToken.None);

        var detalle = await svc.GetTareaAsync(padre.Id, CancellationToken.None);
        Assert.Equal(2, detalle!.Subtareas.Count);
        Assert.Contains(detalle.Subtareas, s => s.Id == h1.Id);
        Assert.Contains(detalle.Subtareas, s => s.Id == h2.Id);

        await CleanTenant(tenantId);
    }

    [Fact]
    public async Task Historial_es_append_only_trigger_bloquea_update_y_delete()
    {
        var tenantId = await SeedTenantAsync("Tareas Audit");
        var (svc, db, _) = Build(tenantId);

        var t = await svc.CrearTareaAsync(new CrearTareaRequest(
            "Tarea T", null, PrioridadTarea.Normal, null, null, null, null, null, null), CancellationToken.None);
        var h = await db.TareaHistorial.AsNoTracking().FirstAsync(x => x.TareaId == t.Id);

        await Assert.ThrowsAnyAsync<Exception>(() =>
            db.Database.ExecuteSqlAsync($"UPDATE tarea_historial SET descripcion = 'alterado' WHERE id = {h.Id}"));
        await Assert.ThrowsAnyAsync<Exception>(() =>
            db.Database.ExecuteSqlAsync($"DELETE FROM tarea_historial WHERE id = {h.Id}"));

        await CleanTenant(tenantId);
    }

    // ===================== Dependencias (Fase 2) =====================

    [Fact]
    public async Task Dependencias_agregar_listar_y_remover()
    {
        var tenantId = await SeedTenantAsync("Tareas Deps");
        var (svc, db, _) = Build(tenantId);

        var pre = await svc.CrearTareaAsync(new CrearTareaRequest(
            "Predecesora", null, PrioridadTarea.Normal, null, null, null, null, null, null), CancellationToken.None);
        var suc = await svc.CrearTareaAsync(new CrearTareaRequest(
            "Sucesora", null, PrioridadTarea.Normal, null, null, null, null, null, null), CancellationToken.None);

        var dep = await svc.AgregarDependenciaAsync(suc.Id,
            new AgregarDependenciaRequest(pre.Id, TipoDependenciaTarea.Bloqueante),
            CancellationToken.None);

        Assert.Equal(pre.Id, dep.DependeDeTareaId);
        Assert.Equal(TipoDependenciaTarea.Bloqueante, dep.Tipo);

        var lista = await svc.ListarDependenciasAsync(suc.Id, CancellationToken.None);
        Assert.Single(lista);

        var ok = await svc.RemoverDependenciaAsync(suc.Id, dep.Id, CancellationToken.None);
        Assert.True(ok);
        var lista2 = await svc.ListarDependenciasAsync(suc.Id, CancellationToken.None);
        Assert.Empty(lista2);

        await CleanTenant(tenantId);
    }

    [Fact]
    public async Task Dependencias_evita_ciclo()
    {
        var tenantId = await SeedTenantAsync("Tareas Ciclo");
        var (svc, _, _) = Build(tenantId);

        var a = await svc.CrearTareaAsync(new CrearTareaRequest(
            "Tarea A", null, PrioridadTarea.Normal, null, null, null, null, null, null), CancellationToken.None);
        var b = await svc.CrearTareaAsync(new CrearTareaRequest(
            "Tarea B", null, PrioridadTarea.Normal, null, null, null, null, null, null), CancellationToken.None);

        // A depende de B
        await svc.AgregarDependenciaAsync(a.Id, new AgregarDependenciaRequest(b.Id), CancellationToken.None);
        // Ahora intentar B depende de A debe fallar (ciclo)
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.AgregarDependenciaAsync(b.Id, new AgregarDependenciaRequest(a.Id), CancellationToken.None));

        await CleanTenant(tenantId);
    }

    [Fact]
    public async Task Dependencias_misma_tarea_falla()
    {
        var tenantId = await SeedTenantAsync("Tareas Self");
        var (svc, _, _) = Build(tenantId);
        var t = await svc.CrearTareaAsync(new CrearTareaRequest(
            "Tarea T", null, PrioridadTarea.Normal, null, null, null, null, null, null), CancellationToken.None);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.AgregarDependenciaAsync(t.Id, new AgregarDependenciaRequest(t.Id), CancellationToken.None));
        await CleanTenant(tenantId);
    }

    /// <summary>
    /// T-03b. Ningun camino de creacion puede dejar una tarea "nacida cerrada": en una columna terminal
    /// pero con Cerrada = false, sin motivo y sin fecha de cierre. Esa tarea se ve en el tablero activo
    /// como si estuviera hecha y no aparece nunca en la pestana "Cerrados".
    /// </summary>
    [Fact]
    public async Task Ningun_camino_de_creacion_deja_una_tarea_nacida_cerrada()
    {
        var tenantId = await SeedTenantAsync("Tareas Nace Cerrada");
        var (svc, db, _) = Build(tenantId);
        var estados = await svc.ListarEstadosAsync(CancellationToken.None);
        var completada = estados.First(e => e.EsTerminal && e.Nombre == "Completada");
        var motivoId = await CrearMotivoTareasAsync(db);

        // 1) Crear directamente en un estado terminal se rechaza.
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.CrearTareaAsync(
            new CrearTareaRequest("Nace cerrada", null, PrioridadTarea.Normal, completada.Id,
                null, null, null, null, null),
            CancellationToken.None));

        // 2) Duplicar una tarea CERRADA da una copia ABIERTA (antes heredaba el estado terminal).
        var t = await svc.CrearTareaAsync(new CrearTareaRequest(
            "Para cerrar", null, PrioridadTarea.Normal, null, null, null, null, null, null),
            CancellationToken.None);
        await svc.CambiarEstadoAsync(t.Id, new CambiarEstadoRequest(completada.Id, null, motivoId),
            CancellationToken.None);

        var duplicada = await svc.DuplicarTareaAsync(t.Id, CancellationToken.None);
        Assert.NotNull(duplicada);
        var duplicadaDb = await db.Tareas.AsNoTracking().FirstAsync(x => x.Id == duplicada!.Id);
        Assert.False(duplicadaDb.Cerrada);
        Assert.NotEqual(completada.Id, duplicadaDb.EstadoId);

        // 3) Copiar hereda igual -> tambien arranca abierta...
        var copias = await svc.CopiarTareaAsync(t.Id, new CopiarTareaRequest(), CancellationToken.None);
        var copiaDb = await db.Tareas.AsNoTracking().FirstAsync(x => x.Id == copias[0].Id);
        Assert.False(copiaDb.Cerrada);
        Assert.NotEqual(completada.Id, copiaDb.EstadoId);

        // ...pero ELEGIR el estado terminal en la copia se rechaza, igual que al crear.
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.CopiarTareaAsync(
            t.Id, new CopiarTareaRequest(EstadoId: completada.Id), CancellationToken.None));

        // Y no quedo ninguna tarea en estado terminal sin cerrar.
        var fantasmas = await db.Tareas.AsNoTracking()
            .CountAsync(x => x.EstadoId == completada.Id && !x.Cerrada);
        Assert.Equal(0, fantasmas);

        await CleanTenant(tenantId);
    }

    // ===================== Bulk actions (Fase 2) =====================

    /// <summary>
    /// T-04. El lote tiene que cerrar EXACTAMENTE igual que el cambio de estado de a una: pidiendo
    /// motivo, marcando la tarea como cerrada y distinguiendo "completada" de "cancelada". Antes
    /// movia la columna y ya: dejaba Cerrada en false, sin motivo, y le ponia fecha de completada
    /// hasta a las canceladas.
    /// </summary>
    [Fact]
    public async Task Bulk_cambiar_estado_cierra_con_las_mismas_reglas_que_el_cambio_individual()
    {
        var tenantId = await SeedTenantAsync("Tareas Bulk");
        var (svc, db, _) = Build(tenantId);

        var estados = await svc.ListarEstadosAsync(CancellationToken.None);
        var completada = estados.First(e => e.EsTerminal && e.Nombre == "Completada");
        var cancelada = estados.First(e => e.EsTerminal && e.Nombre == "Cancelada");
        var motivoId = await CrearMotivoTareasAsync(db);
        var t1 = await svc.CrearTareaAsync(new CrearTareaRequest(
            "Tarea T1", null, PrioridadTarea.Normal, null, null, null, null, null, null), CancellationToken.None);
        var t2 = await svc.CrearTareaAsync(new CrearTareaRequest(
            "Tarea T2", null, PrioridadTarea.Normal, null, null, null, null, null, null), CancellationToken.None);

        // Sin motivo el lote se rechaza entero...
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.BulkCambiarEstadoAsync(
            new BulkCambiarEstadoRequest(new[] { t1.Id, t2.Id }, completada.Id, "Bulk MCP"),
            CancellationToken.None));
        // ...y no toca ninguna tarea.
        var intactas = await db.Tareas.AsNoTracking()
            .Where(t => t.Id == t1.Id || t.Id == t2.Id).ToListAsync();
        Assert.All(intactas, t => Assert.False(t.Cerrada));
        Assert.All(intactas, t => Assert.NotEqual(completada.Id, t.EstadoId));

        // Con motivo cierra de verdad.
        var res = await svc.BulkCambiarEstadoAsync(
            new BulkCambiarEstadoRequest(new[] { t1.Id, t2.Id }, completada.Id, "Bulk MCP", motivoId),
            CancellationToken.None);
        Assert.Equal(2, res.Solicitados);
        Assert.Equal(2, res.Aplicados);
        Assert.Empty(res.Errores);

        var actualizadas = await db.Tareas.AsNoTracking()
            .Where(t => t.Id == t1.Id || t.Id == t2.Id).ToListAsync();
        Assert.All(actualizadas, t =>
        {
            Assert.True(t.Cerrada);
            Assert.NotNull(t.CerradaAt);
            Assert.Equal(motivoId, t.MotivoCierreId);
            Assert.NotNull(t.FechaCompletada);
            Assert.Equal(100, t.Progreso);
        });

        // Cancelar no es completar: queda cerrada, pero SIN fecha de completada.
        var t3 = await svc.CrearTareaAsync(new CrearTareaRequest(
            "Tarea T3", null, PrioridadTarea.Normal, null, null, null, null, null, null), CancellationToken.None);
        await svc.BulkCambiarEstadoAsync(
            new BulkCambiarEstadoRequest(new[] { t3.Id }, cancelada.Id, null, motivoId), CancellationToken.None);
        var t3Cerrada = await db.Tareas.AsNoTracking().FirstAsync(t => t.Id == t3.Id);
        Assert.True(t3Cerrada.Cerrada);
        Assert.Null(t3Cerrada.FechaCompletada);

        // Un id que no existe se reporta: la lista de errores estaba declarada y nunca se llenaba.
        var resFantasma = await svc.BulkCambiarEstadoAsync(
            new BulkCambiarEstadoRequest(new[] { Guid.NewGuid() }, completada.Id, null, motivoId),
            CancellationToken.None);
        Assert.Single(resFantasma.Errores);

        await CleanTenant(tenantId);
    }

    [Fact]
    public async Task Bulk_cambiar_prioridad_aplica_en_lote()
    {
        var tenantId = await SeedTenantAsync("Tareas Bulk Prio");
        var (svc, db, _) = Build(tenantId);
        var t1 = await svc.CrearTareaAsync(new CrearTareaRequest(
            "Tarea T1", null, PrioridadTarea.Normal, null, null, null, null, null, null), CancellationToken.None);
        var t2 = await svc.CrearTareaAsync(new CrearTareaRequest(
            "Tarea T2", null, PrioridadTarea.Baja, null, null, null, null, null, null), CancellationToken.None);

        var res = await svc.BulkCambiarPrioridadAsync(
            new BulkCambiarPrioridadRequest(new[] { t1.Id, t2.Id }, PrioridadTarea.Urgente),
            CancellationToken.None);
        Assert.Equal(2, res.Aplicados);
        var actualizadas = await db.Tareas.AsNoTracking()
            .Where(t => t.Id == t1.Id || t.Id == t2.Id).ToListAsync();
        Assert.All(actualizadas, t => Assert.Equal(PrioridadTarea.Urgente, t.Prioridad));
        await CleanTenant(tenantId);
    }

    // ===================== Origen de modulo (PQRSD hospeda el tablero) =====================

    // Criterio 9 del handoff "Tareas dentro de PQRSD": crear via el unico contrato de tareas
    // (CrearTareaAsync/POST /api/tareas) con origen PQRSD y leer por el board con el filtro por origen.
    // Reemplaza al viejo par ListTareasDePqrAsync/CrearTareaDePqrAsync (ya retirado).
    [Fact]
    public async Task Crear_tarea_con_origen_PQRSD_persiste_vinculo_y_board_filtra_por_entidad()
    {
        var tenantId = await SeedTenantAsync("Tareas Origen PQRSD");
        var (svc, db, _) = Build(tenantId);

        var tablero = await svc.CrearTableroAsync(
            new GuardarTableroRequest("PQRSD", "Tareas generadas desde PQRSD", "#7C5CFA", new List<Guid>()),
            CancellationToken.None);

        var expedienteA = Guid.NewGuid();
        var expedienteB = Guid.NewGuid();

        var tA = await svc.CrearTareaAsync(new CrearTareaRequest(
            "Visita tecnica", null, PrioridadTarea.Normal, null, null, null, null, null, null,
            TableroId: tablero.Id, ModuloOrigenCodigo: "PQRSD", ModuloOrigenEntidadId: expedienteA), CancellationToken.None);
        await svc.CrearTareaAsync(new CrearTareaRequest(
            "Cotizar reparacion", null, PrioridadTarea.Normal, null, null, null, null, null, null,
            TableroId: tablero.Id, ModuloOrigenCodigo: "PQRSD", ModuloOrigenEntidadId: expedienteB), CancellationToken.None);
        await svc.CrearTareaAsync(new CrearTareaRequest(
            "Tarea manual del tablero", null, PrioridadTarea.Normal, null, null, null, null, null, null,
            TableroId: tablero.Id), CancellationToken.None);

        // El vinculo de modulo se persiste y la tarea nace como ModuloExterno (no Manual).
        var dbTA = await db.Tareas.AsNoTracking().FirstAsync(x => x.Id == tA.Id);
        Assert.Equal(OrigenTarea.ModuloExterno, dbTA.Origen);
        Assert.Equal("PQRSD", dbTA.ModuloOrigenCodigo);
        Assert.Equal(expedienteA, dbTA.ModuloOrigenEntidadId);

        // El board filtrado por (origen PQRSD, expediente A) solo trae la tarea de ese expediente.
        var boardA = await svc.GetTableroBoardAsync(tablero.Id, CancellationToken.None, false, "PQRSD", expedienteA);
        Assert.NotNull(boardA);
        Assert.Single(boardA!.Tareas);
        Assert.Equal(tA.Id, boardA.Tareas[0].Id);

        // Otro expediente no ve esa tarea (aislamiento por OrigenEntidadId).
        var boardB = await svc.GetTableroBoardAsync(tablero.Id, CancellationToken.None, false, "PQRSD", expedienteB);
        Assert.NotNull(boardB);
        Assert.Single(boardB!.Tareas);
        Assert.NotEqual(tA.Id, boardB.Tareas[0].Id);

        // Sin filtro, el board trae las 3 tareas del tablero (regresion cero para /tareas).
        var boardTodo = await svc.GetTableroBoardAsync(tablero.Id, CancellationToken.None);
        Assert.NotNull(boardTodo);
        Assert.Equal(3, boardTodo!.Tareas.Count);

        await CleanTenant(tenantId);
    }

    /// <summary>
    /// H-1 (T-02): el modal de tarjeta manda los responsables por ResponsablePersonaIds (con
    /// AsignadoPersonaId=null). Ese camino NO pasaba por la validacion de tenant, asi que se podia
    /// asignar/colaborar con personas de otra copropiedad. Debe rechazarse en crear y en actualizar;
    /// con un responsable vinculado, debe crear y quedar de asignado.
    /// </summary>
    [Fact]
    public async Task Crear_y_actualizar_validan_los_responsables_contra_el_tenant()
    {
        var tenantId = await SeedTenantAsync("Tareas Responsables");
        var (svc, db, _) = Build(tenantId);

        var ajeno = Guid.NewGuid();                              // persona NO vinculada a esta copropiedad
        var propio = await SeedPersonaVinculadaAsync(tenantId);  // persona vinculada en el Directorio

        // Crear con un responsable ajeno -> rechazado, y no queda ninguna tarea (transaccion).
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.CrearTareaAsync(new CrearTareaRequest(
            "T resp ajeno", null, PrioridadTarea.Normal, null, null, null, null, null, null,
            ResponsablePersonaIds: new[] { propio, ajeno }), CancellationToken.None));
        Assert.Empty(await db.Tareas.AsNoTracking().Where(t => t.Titulo == "T resp ajeno").ToListAsync());

        // Crear con responsable vinculado -> ok; el primero queda de asignado.
        var ok = await svc.CrearTareaAsync(new CrearTareaRequest(
            "T resp propio", null, PrioridadTarea.Normal, null, null, null, null, null, null,
            ResponsablePersonaIds: new[] { propio }), CancellationToken.None);
        var creada = await db.Tareas.AsNoTracking().FirstAsync(t => t.Id == ok.Id);
        Assert.Equal(propio, creada.AsignadoPersonaId);

        // Actualizar con un responsable ajeno -> rechazado (mismo hueco por el otro camino).
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.ActualizarTareaAsync(ok.Id,
            new ActualizarTareaRequest("T resp propio", null, PrioridadTarea.Normal, null, null, null,
                ResponsablePersonaIds: new[] { ajeno }), CancellationToken.None));

        await CleanTenant(tenantId);
    }

    /// <summary>
    /// T-06 (spec 2.10 seccion 21): validaciones de la tarea. Titulo 3..200, descripcion &lt;= 4000, fecha de
    /// vencimiento &gt;= inicio, prioridad valida. Antes solo se validaba titulo no vacio y lo demas reventaba
    /// en 500 (DbUpdateException) o pasaba silencioso.
    /// </summary>
    [Fact]
    public async Task Crear_valida_titulo_descripcion_fechas_y_prioridad()
    {
        var tenantId = await SeedTenantAsync("Tareas Validaciones");
        var (svc, db, _) = Build(tenantId);

        CrearTareaRequest Req(string titulo, string? desc = null, DateOnly? ini = null, DateOnly? ven = null,
            PrioridadTarea prio = PrioridadTarea.Normal)
            => new(titulo, desc, prio, null, null, ini, ven, null, null);

        // Titulo muy corto (< 3).
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.CrearTareaAsync(Req("ab"), CancellationToken.None));
        // Titulo muy largo (> 200).
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.CrearTareaAsync(Req(new string('x', 201)), CancellationToken.None));
        // Descripcion > 4000.
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.CrearTareaAsync(Req("Titulo ok", new string('d', 4001)), CancellationToken.None));
        // Fecha de vencimiento anterior a la de inicio.
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.CrearTareaAsync(
            Req("Titulo ok", ini: new DateOnly(2026, 1, 10), ven: new DateOnly(2026, 1, 5)), CancellationToken.None));
        // Prioridad fuera de rango del enum.
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.CrearTareaAsync(Req("Titulo ok", prio: (PrioridadTarea)99), CancellationToken.None));

        // Ninguna de las anteriores dejo tarea (crear va en transaccion).
        Assert.Empty(await db.Tareas.AsNoTracking().Where(t => t.TableroId != null && t.Titulo.StartsWith("Titulo ok")).ToListAsync());

        // Caso valido: titulo 3..200, fechas coherentes, prioridad valida -> crea.
        var ok = await svc.CrearTareaAsync(
            Req("Tarea valida", "descripcion corta", new DateOnly(2026, 1, 5), new DateOnly(2026, 1, 10), PrioridadTarea.Alta),
            CancellationToken.None);
        Assert.NotEqual(Guid.Empty, ok.Id);

        // Actualizar con fecha invertida tambien se rechaza.
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.ActualizarTareaAsync(ok.Id,
            new ActualizarTareaRequest("Tarea valida", null, PrioridadTarea.Normal, null,
                new DateOnly(2026, 2, 10), new DateOnly(2026, 2, 1)), CancellationToken.None));

        await CleanTenant(tenantId);
    }

    [Fact]
    public async Task T10_crear_en_paralelo_da_numeros_unicos_y_consecutivos_sin_excepcion()
    {
        // T-10 (RN-01): 10 creaciones concurrentes contra el MISMO tenant, cada una con su propio
        // DbContext/servicio (DbContext no es thread-safe). Sin el advisory lock dos calcularian el mismo
        // T-{anio}-NNNN y la segunda reventaria contra el UNIQUE (tenant_id, numero_tarea). Con el lock:
        // numeros unicos, consecutivos sin huecos y CERO excepciones (ni por UNIQUE ni por deadlock).
        var tenantId = await SeedTenantAsync("Tareas Concurrencia");
        const int n = 10;

        // Pre-calienta el seed perezoso (tablero por defecto + 6 estados) con una creacion previa, para
        // que las concurrentes solo compitan por el consecutivo, no por ese seed compartido.
        var (warm, _, warmScope) = Build(tenantId);
        await warm.CrearTareaAsync(new CrearTareaRequest(
            "Warmup", null, PrioridadTarea.Normal, null, null, null, null, null, null), CancellationToken.None);
        warmScope.Dispose();

        var builds = Enumerable.Range(0, n).Select(_ => Build(tenantId)).ToList();
        try
        {
            var tareas = builds.Select((b, i) => b.svc.CrearTareaAsync(new CrearTareaRequest(
                $"Concurrente {i + 1}", null, PrioridadTarea.Normal, null, null, null, null, null, null),
                CancellationToken.None)).ToList();

            // Task.WhenAll propaga la primera excepcion: si alguna revienta, el test falla aqui.
            var creadas = await Task.WhenAll(tareas);

            var prefijo = $"T-{DateTime.UtcNow.Year}-";
            var numeros = creadas.Select(t => t.NumeroTarea).ToList();
            Assert.All(numeros, x => Assert.StartsWith(prefijo, x));
            var seq = numeros.Select(x => int.Parse(x[prefijo.Length..])).OrderBy(x => x).ToList();
            Assert.Equal(n, seq.Distinct().Count());                          // unicos
            Assert.Equal(Enumerable.Range(seq[0], n).ToList(), seq);          // consecutivos sin huecos
            Assert.Equal(2, seq[0]);                                          // arrancan justo tras el warmup (0001)
        }
        finally
        {
            foreach (var b in builds) b.scope.Dispose();
            await CleanTenant(tenantId);
        }
    }

    [Fact]
    public async Task H2_enlazar_persona_ajena_al_tablero_se_rechaza_pero_miembro_previo_sobrevive()
    {
        // H-2: enlazar a un tablero una persona que no pertenece a esta copropiedad debe fallar
        // (AgregarUsuario y crear/actualizar tablero), pero un miembro que YA estaba (p.ej. invitado
        // externo por correo, cross-tenant deliberado) debe sobrevivir al reenviar la lista en un
        // ActualizarTablero (se valida solo el delta nuevo).
        var tenantId = await SeedTenantAsync("Tareas H-2");
        var (svc, db, _) = Build(tenantId);

        var vinculada = await SeedPersonaVinculadaAsync(tenantId);   // persona de esta copropiedad
        var ajena = await SeedPersonaGlobalSinVinculoAsync();        // persona real, NO vinculada aqui

        // Crear tablero con una persona vinculada -> ok.
        var tab = await svc.CrearTableroAsync(
            new GuardarTableroRequest("Tablero H2", null, "#6D4FE3", new[] { vinculada }), CancellationToken.None);
        Assert.NotNull(tab);

        // Enlazar directo una persona ajena -> excepcion y sin fila.
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.AgregarUsuarioTableroAsync(tab.Id, ajena, CancellationToken.None));
        Assert.False(await db.TableroUsuarios.AsNoTracking().AnyAsync(u => u.TableroId == tab.Id && u.PersonaId == ajena));

        // Actualizar el tablero metiendo a la ajena como miembro NUEVO -> excepcion.
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.ActualizarTableroAsync(tab.Id,
            new GuardarTableroRequest("Tablero H2", null, "#6D4FE3", new[] { vinculada, ajena }), CancellationToken.None));

        // La ajena ya es miembro (simula el invite por-correo cross-tenant: se inserta directo).
        db.TableroUsuarios.Add(new TableroUsuario { TableroId = tab.Id, PersonaId = ajena });
        await db.SaveChangesAsync(CancellationToken.None);

        // Reenviar la lista completa con la ajena ya-miembro -> NO lanza y NO la expulsa (delta vacio para ella).
        var ok = await svc.ActualizarTableroAsync(tab.Id,
            new GuardarTableroRequest("Tablero H2b", null, "#6D4FE3", new[] { vinculada, ajena }), CancellationToken.None);
        Assert.True(ok);
        Assert.True(await db.TableroUsuarios.AsNoTracking().AnyAsync(u => u.TableroId == tab.Id && u.PersonaId == ajena));

        await CleanTenant(tenantId);
        await BorrarPersonaGlobalAsync(ajena);
    }

    // ===================== Helpers =====================

    private (ITareasService svc, PropiaDbContext db, IServiceScope scope) Build(Guid tenantId)
    {
        var scope = _services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        ctx.SetTenant(tenantId);
        var db = scope.ServiceProvider.GetRequiredService<PropiaDbContext>();
        var http = scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>();
        var noti = scope.ServiceProvider.GetRequiredService<Propia.Application.Notificaciones.INotificacionDispatcher>();
        return (new TareasService(db, ctx, http, noti), db, scope);
    }

    // Crea un motivo de cierre del modulo "tareas" (requerido al pasar a un estado terminal).
    private static async Task<Guid> CrearMotivoTareasAsync(PropiaDbContext db)
    {
        var m = new Propia.Domain.Entities.MotivoCierre { Modulo = "tareas", Nombre = "Resuelto", Activo = true };
        db.MotivosCierre.Add(m);
        await db.SaveChangesAsync(CancellationToken.None);
        return m.Id;
    }

    private static HttpContext BuildFakeHttpContext()
    {
        var ctx = new DefaultHttpContext();
        var uid = Guid.NewGuid().ToString();
        ctx.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim("user_id", uid), new Claim("persona_id", uid)
        }, "test"));
        return ctx;
    }

    private async Task<Guid> SeedTenantAsync(string nombre)
    {
        var opts = new DbContextOptionsBuilder<PropiaDbContext>().UseNpgsql(_fx.OwnerConnectionString).Options;
        await using var ctx = new PropiaDbContext(opts, new TenantContext());
        var t = new Tenant { Nombre = nombre, Estado = EstadoCopropiedad.Activa, EstadoCustodia = EstadoCustodia.ConAdmin };
        ctx.Tenants.Add(t);
        await ctx.SaveChangesAsync();
        return t.Id;
    }

    // Crea una Persona (global) y su vinculo con la copropiedad, como exige ValidarPersonaDelTenantAsync.
    private async Task<Guid> SeedPersonaVinculadaAsync(Guid tenantId)
    {
        var opts = new DbContextOptionsBuilder<PropiaDbContext>().UseNpgsql(_fx.OwnerConnectionString).Options;
        await using var ctx = new PropiaDbContext(opts, new TenantContext());
        var p = new Persona
        {
            TipoDocumento = TipoDocumento.CC,
            Documento = $"D{Guid.NewGuid():N}".Substring(0, 18),
            Nombres = "Resp",
            Apellidos = "Test"
        };
        ctx.Personas.Add(p);
        // TenantId explicito: el vinculo es TenantEntity y este contexto no tiene tenant activo.
        ctx.DirectorioVinculos.Add(new DirectorioVinculo
        {
            TenantId = tenantId,
            EntidadTipo = EntidadDirectorio.Persona,
            EntidadId = p.Id,
            FechaDesde = DateOnly.FromDateTime(DateTime.UtcNow),
            Estado = EstadoVinculo.Activo
        });
        await ctx.SaveChangesAsync();
        return p.Id;
    }

    // Persona GLOBAL sin vinculo en ningun tenant de prueba: simula "de otra copropiedad" (no vinculada aqui).
    private async Task<Guid> SeedPersonaGlobalSinVinculoAsync()
    {
        var opts = new DbContextOptionsBuilder<PropiaDbContext>().UseNpgsql(_fx.OwnerConnectionString).Options;
        await using var ctx = new PropiaDbContext(opts, new TenantContext());
        var p = new Persona
        {
            TipoDocumento = TipoDocumento.CC,
            Documento = $"D{Guid.NewGuid():N}".Substring(0, 18),
            Nombres = "Ajena",
            Apellidos = "Test"
        };
        ctx.Personas.Add(p);
        await ctx.SaveChangesAsync();
        return p.Id;
    }

    private async Task BorrarPersonaGlobalAsync(Guid personaId)
    {
        var opts = new DbContextOptionsBuilder<PropiaDbContext>().UseNpgsql(_fx.OwnerConnectionString).Options;
        await using var ctx = new PropiaDbContext(opts, new TenantContext());
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM personas WHERE id = {personaId}");
    }

    private async Task CleanTenant(Guid tenantId)
    {
        var opts = new DbContextOptionsBuilder<PropiaDbContext>().UseNpgsql(_fx.OwnerConnectionString).Options;
        await using var ctx = new PropiaDbContext(opts, new TenantContext());
        await ctx.Database.ExecuteSqlAsync($"ALTER TABLE tarea_historial DISABLE TRIGGER ALL");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM tarea_historial WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"ALTER TABLE tarea_historial ENABLE TRIGGER ALL");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM tarea_comentarios WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM tarea_etiqueta_asignaciones WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM tareas WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM personas WHERE id IN (SELECT entidad_id FROM directorio_vinculos WHERE tenant_id = {tenantId})");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM directorio_vinculos WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM motivos_cierre WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM tarea_etiquetas WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM tarea_estados WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM tenants WHERE id = {tenantId}");
    }
}
