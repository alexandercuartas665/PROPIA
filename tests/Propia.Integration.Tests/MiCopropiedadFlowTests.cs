using Microsoft.EntityFrameworkCore;
using Propia.Application.MiCopropiedad;
using Propia.Domain.Entities;
using Propia.Domain.Enums;
using Propia.Infrastructure.MiCopropiedad;
using Propia.Infrastructure.Persistence;
using Xunit;

namespace Propia.Integration.Tests;

/// <summary>
/// Tests del modulo 2.3 Mi Copropiedad (MVP).
///
/// Cubre cada seccion del service:
///  1. Identidad  - update + completitud
///  2. Distribucion - torres + unidades + suma coeficientes
///  3. Gobierno  - alta y desactivacion de miembros del consejo
///  5. Servicios - alta y baja de contratos
///  6. Zonas Comunes
///  7. Equipos / Activos
///  + Resumen      - heuristicas de completitud por seccion
///  + RLS cross-tenant - tenant A no ve torres de tenant B
///
/// Estos tests validan que el TenantConnectionInterceptor + auto-asignacion de
/// TenantId en SaveChangesAsync trabajan juntos para que las operaciones de Mi
/// Copropiedad respeten RLS en escritura y lectura.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class MiCopropiedadFlowTests
{
    private readonly PostgresFixture _fx;

    public MiCopropiedadFlowTests(PostgresFixture fx) => _fx = fx;

    [Fact]
    public async Task Actualizar_identidad_persiste_campos_y_marca_seccion_completa()
    {
        var tenantId = await SeedTenantAsync("CP Identidad");
        var (svc, _, _) = BuildService(tenantId);

        var dto = await svc.ActualizarIdentidadAsync(tenantId, new ActualizarIdentidadRequest(
            "CP Identidad Renamed", "900111222", "5",
            "Cra. 1 #2-3", "Cali", "Valle",
            TipoCopropiedad.Residencial, Estrato.Cuatro,
            null, null, "Conjunto urbano renombrado",
            null, null, null, null, null,
            null, null), CancellationToken.None);

        Assert.NotNull(dto);
        Assert.Equal("CP Identidad Renamed", dto!.Nombre);
        Assert.Equal("900111222", dto.Nit);
        Assert.Equal(TipoCopropiedad.Residencial, dto.Tipo);

        var resumen = await svc.GetResumenAsync(tenantId, CancellationToken.None);
        Assert.NotNull(resumen);
        Assert.True(resumen!.SeccionesCompletas["Identidad"], "Identidad debe quedar marcada como completa");

        await CleanupTenantAsync(tenantId);
    }

    [Fact]
    public async Task Crear_torre_asigna_tenant_id_y_aparece_en_lista()
    {
        var tenantId = await SeedTenantAsync("CP Torres");
        var (svc, _, _) = BuildService(tenantId);

        var torre = await svc.CrearTorreAsync(new CrearTorreRequest("Torre A", 12, null), CancellationToken.None);
        Assert.Equal("Torre A", torre.Nombre);
        Assert.Equal(12, torre.CantidadPisos);

        var lista = await svc.ListTorresAsync(CancellationToken.None);
        Assert.Single(lista);
        Assert.Equal("Torre A", lista[0].Nombre);

        // Verificacion directa en BD: la fila lleva tenant_id correcto
        var tenantIdEnBd = await GetTorreTenantIdAsync(torre.Id);
        Assert.Equal(tenantId, tenantIdEnBd);

        await CleanupTenantAsync(tenantId);
    }

    [Fact]
    public async Task Crear_unidad_calcula_suma_coeficientes_en_resumen()
    {
        var tenantId = await SeedTenantAsync("CP Unidades");
        var (svc, _, _) = BuildService(tenantId);

        var torre = await svc.CrearTorreAsync(new CrearTorreRequest("Torre B", 5, null), CancellationToken.None);
        await svc.CrearUnidadAsync(new CrearUnidadRequest("101", TipoUnidad.Apartamento, torre.Id, 1, 25.0m, 60m, 2, 1, 1, null, null), CancellationToken.None);
        await svc.CrearUnidadAsync(new CrearUnidadRequest("102", TipoUnidad.Apartamento, torre.Id, 1, 25.0m, 60m, 2, 1, 1, null, null), CancellationToken.None);
        await svc.CrearUnidadAsync(new CrearUnidadRequest("201", TipoUnidad.Apartamento, torre.Id, 2, 25.0m, 70m, 3, 2, 1, null, null), CancellationToken.None);
        await svc.CrearUnidadAsync(new CrearUnidadRequest("202", TipoUnidad.Apartamento, torre.Id, 2, 25.0m, 70m, 3, 2, 1, null, null), CancellationToken.None);

        var resumen = await svc.GetResumenAsync(tenantId, CancellationToken.None);
        Assert.NotNull(resumen);
        Assert.Equal(4, resumen!.CantidadUnidades);
        Assert.Equal(100.0m, resumen.CoeficientesTotalPct);
        Assert.True(resumen.SeccionesCompletas["Distribucion"], "Distribucion debe estar completa con suma de 100%");

        await CleanupTenantAsync(tenantId);
    }

    [Fact]
    public async Task Eliminar_torre_la_quita_del_listado()
    {
        var tenantId = await SeedTenantAsync("CP DelTorre");
        var (svc, _, _) = BuildService(tenantId);

        var torre = await svc.CrearTorreAsync(new CrearTorreRequest("Torre Bye", null, null), CancellationToken.None);
        var ok = await svc.EliminarTorreAsync(torre.Id, CancellationToken.None);
        Assert.True(ok);
        Assert.Empty(await svc.ListTorresAsync(CancellationToken.None));

        await CleanupTenantAsync(tenantId);
    }

    [Fact]
    public async Task Crear_contrato_servicio_aparece_en_listado()
    {
        var tenantId = await SeedTenantAsync("CP Contratos");
        var (svc, _, _) = BuildService(tenantId);

        var contrato = await svc.CrearContratoAsync(new CrearContratoServicioRequest(
            TipoServicio.Aseo, "Aseo Total SAS", "900111333", null,
            new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), 1_500_000m, null), CancellationToken.None);
        Assert.Equal(TipoServicio.Aseo, contrato.Tipo);
        Assert.Equal("Aseo Total SAS", contrato.Proveedor);

        var lista = await svc.ListContratosAsync(CancellationToken.None);
        Assert.Single(lista);

        var resumen = await svc.GetResumenAsync(tenantId, CancellationToken.None);
        Assert.True(resumen!.SeccionesCompletas["Servicios"], "Servicios debe estar completa con >= 1 contrato");

        await CleanupTenantAsync(tenantId);
    }

    [Fact]
    public async Task Crear_zona_comun_y_equipo_completan_secciones_correspondientes()
    {
        var tenantId = await SeedTenantAsync("CP Zonas");
        var (svc, _, _) = BuildService(tenantId);

        await svc.CrearZonaComunAsync(new CrearZonaComunRequest(
            "Piscina", CategoriaZonaComun.Deportiva, null, true, 50_000m, 20, null, null), CancellationToken.None);
        await svc.CrearEquipoAsync(new CrearEquipoActivoRequest(
            "Bomba Hidroflo", CategoriaEquipo.Bombeo, TipoElemento.Equipo, 1, false), CancellationToken.None);

        var resumen = await svc.GetResumenAsync(tenantId, CancellationToken.None);
        Assert.NotNull(resumen);
        Assert.Equal(1, resumen!.CantidadZonasComunes);
        Assert.Equal(1, resumen.CantidadEquipos);
        Assert.True(resumen.SeccionesCompletas["ZonasComunes"]);
        Assert.True(resumen.SeccionesCompletas["Equipos"]);

        await CleanupTenantAsync(tenantId);
    }

    [Fact]
    public async Task Agregar_miembros_consejo_marca_gobierno_completo_con_3_o_mas()
    {
        var tenantId = await SeedTenantAsync("CP Gobierno");
        var (svc, db, _) = BuildService(tenantId);

        // El service requiere PersonaId existente. Las creamos directo en BD.
        var p1 = new Persona { TipoDocumento = TipoDocumento.CC, Documento = $"C{Guid.NewGuid():N}".Substring(0, 18), Nombres = "Pres", Apellidos = "Idente" };
        var p2 = new Persona { TipoDocumento = TipoDocumento.CC, Documento = $"C{Guid.NewGuid():N}".Substring(0, 18), Nombres = "Voc", Apellidos = "Al1" };
        var p3 = new Persona { TipoDocumento = TipoDocumento.CC, Documento = $"C{Guid.NewGuid():N}".Substring(0, 18), Nombres = "Voc", Apellidos = "Al2" };
        db.Personas.AddRange(p1, p2, p3);
        await db.SaveChangesAsync();

        await svc.AgregarMiembroConsejoAsync(new AgregarMiembroConsejoRequest(
            p1.Id, CargoConsejo.Presidente, new DateOnly(2026, 1, 1), null), CancellationToken.None);
        await svc.AgregarMiembroConsejoAsync(new AgregarMiembroConsejoRequest(
            p2.Id, CargoConsejo.Vocal, new DateOnly(2026, 1, 1), null), CancellationToken.None);
        await svc.AgregarMiembroConsejoAsync(new AgregarMiembroConsejoRequest(
            p3.Id, CargoConsejo.Vocal, new DateOnly(2026, 1, 1), null), CancellationToken.None);

        // La nueva regla Gobierno tambien exige al menos un comite (Ley 675 - Convivencia obligatorio en residencial)
        await svc.CrearComiteAsync(new CrearComiteRequest("Convivencia", "Comite obligatorio Ley 675", new DateOnly(2026, 1, 1)), CancellationToken.None);

        var resumen = await svc.GetResumenAsync(tenantId, CancellationToken.None);
        Assert.Equal(3, resumen!.CantidadMiembrosConsejo);
        Assert.True(resumen.SeccionesCompletas["Gobierno"], "Gobierno debe estar completa con consejo>=3 + comite (PH con <=30 unidades no requiere revisor fiscal)");

        await CleanupTenantAsync(tenantId, personaIds: new[] { p1.Id, p2.Id, p3.Id });
    }

    [Fact]
    public async Task Generador_inteligente_crea_torres_y_unidades_en_transaccion()
    {
        var tenantId = await SeedTenantAsync("CP Generador");
        var (svc, _, _) = BuildService(tenantId);

        var req = new GenerarUnidadesRequest(
            new[]
            {
                new GeneradorTorreDto("Torre A", 3, 4),
                new GeneradorTorreDto("Torre B", 2, 4)
            },
            PatronNumeracion.PisoNumero,
            TipoUnidad.Apartamento,
            5.0m);

        var resp = await svc.GenerarUnidadesAsync(req, CancellationToken.None);

        Assert.Equal(2, resp.TorresCreadas);
        Assert.Equal(20, resp.UnidadesCreadas);  // (3*4) + (2*4)

        var unidades = await svc.ListUnidadesAsync(CancellationToken.None);
        Assert.Equal(20, unidades.Count);

        // Patron PisoNumero con multiples torres prefija con index: 1101..1304 (Torre A) y 2101..2204 (Torre B)
        Assert.Contains(unidades, u => u.Numero == "1101");
        Assert.Contains(unidades, u => u.Numero == "1304");
        Assert.Contains(unidades, u => u.Numero == "2101");
        Assert.Contains(unidades, u => u.Numero == "2204");

        var resumen = await svc.GetResumenAsync(tenantId, CancellationToken.None);
        Assert.Equal(100m, resumen!.CoeficientesTotalPct);
        Assert.True(resumen.SeccionesCompletas["Distribucion"]);

        await CleanupTenantAsync(tenantId);
    }

    [Fact]
    public async Task Generador_con_nombre_torre_duplicado_aborta_todo()
    {
        var tenantId = await SeedTenantAsync("CP Gen Dup");
        var (svc, _, _) = BuildService(tenantId);

        await svc.CrearTorreAsync(new CrearTorreRequest("Torre A", 3, null), CancellationToken.None);

        var req = new GenerarUnidadesRequest(
            new[] { new GeneradorTorreDto("Torre A", 2, 2) },
            PatronNumeracion.Corrido,
            TipoUnidad.Apartamento,
            1m);

        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.GenerarUnidadesAsync(req, CancellationToken.None));

        // La torre previa sigue ahi, pero no se crearon unidades de la torre duplicada
        var torres = await svc.ListTorresAsync(CancellationToken.None);
        Assert.Single(torres);
        var unidades = await svc.ListUnidadesAsync(CancellationToken.None);
        Assert.Empty(unidades);

        await CleanupTenantAsync(tenantId);
    }

    [Fact]
    public async Task Importar_csv_valido_crea_unidades_y_torre_nueva()
    {
        var tenantId = await SeedTenantAsync("CP CSV");
        var (svc, _, _) = BuildService(tenantId);

        var csv = "identificador,tipo_unidad,agrupacion,piso,coeficiente,area_m2\n" +
                  "101,Apartamento,Torre Este,1,25.0,60\n" +
                  "102,Apartamento,Torre Este,1,25.0,60\n" +
                  "201,Apartamento,Torre Este,2,25.0,60\n" +
                  "202,Apartamento,Torre Este,2,25.0,60";

        var resp = await svc.ImportarUnidadesCsvAsync(new ImportarUnidadesRequest(csv), CancellationToken.None);

        Assert.True(resp.Aceptado, $"Importacion rechazada: {string.Join(';', resp.Errores.Select(e => e.Mensaje))}");
        Assert.Equal(4, resp.UnidadesCreadas);
        Assert.Equal(100m, resp.SumaCoeficientes);

        var torres = await svc.ListTorresAsync(CancellationToken.None);
        Assert.Single(torres);
        Assert.Equal("Torre Este", torres[0].Nombre);

        await CleanupTenantAsync(tenantId);
    }

    [Fact]
    public async Task Importar_csv_con_duplicados_y_tipo_invalido_lista_errores_y_no_crea_nada()
    {
        var tenantId = await SeedTenantAsync("CP CSV Errs");
        var (svc, _, _) = BuildService(tenantId);

        var csv = "identificador,tipo_unidad,coeficiente\n" +
                  "101,Apartamento,25\n" +
                  "101,Apartamento,25\n" +      // duplicado en archivo
                  "102,Duplex,25\n" +           // tipo invalido
                  ",Apartamento,25";            // identificador vacio

        var resp = await svc.ImportarUnidadesCsvAsync(new ImportarUnidadesRequest(csv), CancellationToken.None);

        Assert.False(resp.Aceptado);
        Assert.Equal(0, resp.UnidadesCreadas);
        Assert.True(resp.Errores.Count >= 3, $"Esperaba 3+ errores, hubo {resp.Errores.Count}");
        Assert.Contains(resp.Errores, e => e.Mensaje.Contains("duplicado", StringComparison.OrdinalIgnoreCase));

        // Ninguna unidad fue creada (transaccional)
        Assert.Empty(await svc.ListUnidadesAsync(CancellationToken.None));

        await CleanupTenantAsync(tenantId);
    }

    [Fact]
    public async Task Crear_tipo_unidad_custom_se_lista_y_no_permite_duplicados()
    {
        var tenantId = await SeedTenantAsync("CP Tipos");
        var (svc, _, _) = BuildService(tenantId);

        var t1 = await svc.CrearTipoUnidadCustomAsync(new CrearTipoUnidadCustomRequest("Duplex", true, "Apartamento de dos niveles"), CancellationToken.None);
        Assert.Equal("Duplex", t1.Nombre);

        var lista = await svc.ListTiposUnidadCustomAsync(CancellationToken.None);
        Assert.Single(lista);

        // Duplicado por nombre debe fallar
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.CrearTipoUnidadCustomAsync(new CrearTipoUnidadCustomRequest("Duplex", true, null), CancellationToken.None));

        // Eliminar lo deja sin tipos
        var ok = await svc.EliminarTipoUnidadCustomAsync(t1.Id, CancellationToken.None);
        Assert.True(ok);
        Assert.Empty(await svc.ListTiposUnidadCustomAsync(CancellationToken.None));

        await CleanupTenantAsync(tenantId);
    }

    [Fact]
    public async Task Identidad_registral_y_labels_personalizables_persisten()
    {
        var tenantId = await SeedTenantAsync("CP Registral");
        var (svc, _, _) = BuildService(tenantId);

        var fecha = new DateOnly(2020, 6, 15);
        var dto = await svc.ActualizarIdentidadAsync(tenantId, new ActualizarIdentidadRequest(
            "CP Registral", null, null, null, null, null,
            null, null,
            null, null, null,
            "Escritura 1234", "Notaria 5 de Cali", "370-9988", "LIC-2020-001", fecha,
            "Bloque", "Nivel"), CancellationToken.None);

        Assert.NotNull(dto);
        Assert.Equal("Escritura 1234", dto!.NumeroReglamentoPh);
        Assert.Equal("Notaria 5 de Cali", dto.NotariaRegistro);
        Assert.Equal("370-9988", dto.MatriculaInmobiliaria);
        Assert.Equal(fecha, dto.FechaConstitucion);
        Assert.Equal("Bloque", dto.LabelAgrupacion);
        Assert.Equal("Nivel", dto.LabelPiso);

        await CleanupTenantAsync(tenantId);
    }

    [Fact]
    public async Task Tipos_coeficiente_lazy_seed_crea_tipo_Propiedad_principal()
    {
        var tenantId = await SeedTenantAsync("CP Coef Seed");
        var (svc, _, _) = BuildService(tenantId);

        // Primera consulta crea automaticamente el tipo principal "Propiedad"
        var lista = await svc.ListTiposCoeficienteAsync(CancellationToken.None);
        Assert.Single(lista);
        Assert.Equal("Propiedad", lista[0].Nombre);
        Assert.True(lista[0].EsPrincipal);

        // Segunda consulta no crea duplicado
        var lista2 = await svc.ListTiposCoeficienteAsync(CancellationToken.None);
        Assert.Single(lista2);

        await CleanupTenantAsync(tenantId);
    }

    [Fact]
    public async Task Crear_tipo_coeficiente_adicional_y_setear_valor_por_unidad()
    {
        var tenantId = await SeedTenantAsync("CP Coef Multi");
        var (svc, _, _) = BuildService(tenantId);

        // Crea unidad con un coef. Esto siembra el tipo "Propiedad" via ListTiposCoeficienteAsync.
        var torre = await svc.CrearTorreAsync(new CrearTorreRequest("Torre A", 5, null), CancellationToken.None);
        var unidad = await svc.CrearUnidadAsync(new CrearUnidadRequest("101", TipoUnidad.Apartamento, torre.Id, 1, 50m, null, 2, 1, 1, null, null), CancellationToken.None);

        // Lista tipos: sembrara "Propiedad" como principal
        var tipos = await svc.ListTiposCoeficienteAsync(CancellationToken.None);
        Assert.Contains(tipos, t => t.Nombre == "Propiedad" && t.EsPrincipal);

        // Crea un segundo tipo "Administracion"
        var tipoAdmin = await svc.CrearTipoCoeficienteAsync(new CrearTipoCoeficienteRequest("Administracion", "Para calculo de cuotas"), CancellationToken.None);
        Assert.False(tipoAdmin.EsPrincipal);

        // Asigna valor 60 al coef Administracion de la unidad
        var asig = await svc.SetCoeficienteUnidadAsync(unidad.Id, new SetCoeficienteUnidadRequest(tipoAdmin.Id, 60m), CancellationToken.None);
        Assert.Equal(60m, asig.Valor);

        // Lista coeficientes de la unidad: deberia tener 1 entrada (Administracion)
        var coefs = await svc.ListCoeficientesUnidadAsync(unidad.Id, CancellationToken.None);
        Assert.Contains(coefs, c => c.TipoNombre == "Administracion" && c.Valor == 60m);

        await CleanupTenantAsync(tenantId);
    }

    [Fact]
    public async Task No_se_puede_eliminar_tipo_coeficiente_principal()
    {
        var tenantId = await SeedTenantAsync("CP Coef Principal");
        var (svc, _, _) = BuildService(tenantId);

        var tipos = await svc.ListTiposCoeficienteAsync(CancellationToken.None);  // crea el seed
        var principal = tipos.Single(t => t.EsPrincipal);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.EliminarTipoCoeficienteAsync(principal.Id, CancellationToken.None));

        await CleanupTenantAsync(tenantId);
    }

    [Fact]
    public async Task Crear_comite_y_agregar_miembro_funciona()
    {
        var tenantId = await SeedTenantAsync("CP Comites");
        var (svc, db, _) = BuildService(tenantId);

        // Personas necesarias (creadas via owner, no via service)
        var p1 = new Persona { TipoDocumento = TipoDocumento.CC, Documento = $"C{Guid.NewGuid():N}".Substring(0, 18), Nombres = "Ana", Apellidos = "Comite" };
        db.Personas.Add(p1);
        await db.SaveChangesAsync();

        var comite = await svc.CrearComiteAsync(new CrearComiteRequest("Convivencia", "Comite legal Ley 675", new DateOnly(2026, 1, 1)), CancellationToken.None);
        Assert.Equal("Convivencia", comite.Nombre);

        var miembro = await svc.AgregarMiembroComiteAsync(new AgregarComiteMiembroRequest(comite.Id, p1.Id, "Coordinador"), CancellationToken.None);
        Assert.Equal(p1.Id, miembro.PersonaId);

        var lista = await svc.ListComitesAsync(CancellationToken.None);
        Assert.Single(lista);
        Assert.Equal(1, lista[0].CantidadMiembros);

        // Duplicado
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.AgregarMiembroComiteAsync(new AgregarComiteMiembroRequest(comite.Id, p1.Id, "Otro"), CancellationToken.None));

        await CleanupTenantAsync(tenantId, personaIds: new[] { p1.Id });
    }

    [Fact]
    public async Task Designar_nuevo_revisor_fiscal_retira_al_anterior()
    {
        var tenantId = await SeedTenantAsync("CP Revisor");
        var (svc, db, _) = BuildService(tenantId);

        var p1 = new Persona { TipoDocumento = TipoDocumento.CC, Documento = $"R{Guid.NewGuid():N}".Substring(0, 18), Nombres = "Rev1", Apellidos = "Fiscal" };
        var p2 = new Persona { TipoDocumento = TipoDocumento.CC, Documento = $"R{Guid.NewGuid():N}".Substring(0, 18), Nombres = "Rev2", Apellidos = "Fiscal" };
        db.Personas.AddRange(p1, p2);
        await db.SaveChangesAsync();

        var r1 = await svc.DesignarRevisorFiscalAsync(new DesignarRevisorFiscalRequest(p1.Id, "TP-12345", new DateOnly(2026, 1, 1)), CancellationToken.None);
        Assert.True(r1.Activo);

        // Designar uno nuevo - el primero se retira automaticamente
        var r2 = await svc.DesignarRevisorFiscalAsync(new DesignarRevisorFiscalRequest(p2.Id, "TP-67890", new DateOnly(2026, 6, 1)), CancellationToken.None);
        Assert.True(r2.Activo);

        var actual = await svc.GetRevisorFiscalActivoAsync(CancellationToken.None);
        Assert.NotNull(actual);
        Assert.Equal(p2.Id, actual!.PersonaId);

        await CleanupTenantAsync(tenantId, personaIds: new[] { p1.Id, p2.Id });
    }

    [Fact]
    public async Task Agregar_miembro_equipo_y_buscar_persona_por_documento()
    {
        var tenantId = await SeedTenantAsync("CP Equipo");
        var (svc, _, _) = BuildService(tenantId);

        // Vincula persona NUEVA por documento
        var docPersona = $"E{Guid.NewGuid():N}".Substring(0, 18);
        var personaId = await svc.VincularPersonaPorDocumentoAsync(
            new VincularPersonaPorDocumentoRequest(docPersona, "Carlos", "Coordinador", "carlos@coord.test", null),
            CancellationToken.None);
        Assert.NotEqual(Guid.Empty, personaId);

        // Llamar de nuevo con el mismo doc retorna el mismo Id (no duplica)
        var personaId2 = await svc.VincularPersonaPorDocumentoAsync(
            new VincularPersonaPorDocumentoRequest(docPersona, "Ignorado", "Ignorado", null, null),
            CancellationToken.None);
        Assert.Equal(personaId, personaId2);

        // Agrega al equipo
        var miembro = await svc.AgregarMiembroEquipoAsync(new AgregarMiembroEquipoRequest(
            personaId, RolEquipo.CoordinadorOperativo, null, TipoVinculacion.Interno,
            new DateOnly(2026, 3, 1), "300-1234567", "carlos@coord.test", null),
            CancellationToken.None);

        Assert.Equal(RolEquipo.CoordinadorOperativo, miembro.Rol);
        Assert.True(miembro.Activo);

        var lista = await svc.ListEquipoAsync(CancellationToken.None);
        Assert.Single(lista);

        // Desactivar
        var ok = await svc.DesactivarMiembroEquipoAsync(miembro.Id, CancellationToken.None);
        Assert.True(ok);
        var lista2 = await svc.ListEquipoAsync(CancellationToken.None);
        Assert.False(lista2[0].Activo);

        await CleanupTenantAsync(tenantId, personaIds: new[] { personaId });
    }

    [Fact]
    public async Task Tenant_no_ve_torres_de_otro_tenant_via_RLS()
    {
        var tA = await SeedTenantAsync("CP A");
        var tB = await SeedTenantAsync("CP B");

        var (svcA, _, _) = BuildService(tA);
        var (svcB, _, _) = BuildService(tB);

        // A crea una torre, B crea otra.
        await svcA.CrearTorreAsync(new CrearTorreRequest("Torre Solo A", 3, null), CancellationToken.None);
        await svcB.CrearTorreAsync(new CrearTorreRequest("Torre Solo B", 4, null), CancellationToken.None);

        var listaA = await svcA.ListTorresAsync(CancellationToken.None);
        var listaB = await svcB.ListTorresAsync(CancellationToken.None);

        Assert.Single(listaA);
        Assert.Single(listaB);
        Assert.Equal("Torre Solo A", listaA[0].Nombre);
        Assert.Equal("Torre Solo B", listaB[0].Nombre);

        await CleanupTenantAsync(tA);
        await CleanupTenantAsync(tB);
    }

    // ----------------- Helpers -----------------

    /// <summary>
    /// Construye un MiCopropiedadService con DbContext apuntado al rol propia_app
    /// + TenantConnectionInterceptor activo. Asi el test es fiel al runtime real.
    /// </summary>
    private (IMiCopropiedadService svc, PropiaDbContext db, TenantContext tenantCtx) BuildService(Guid tenantId)
    {
        var tenantCtx = new TenantContext();
        tenantCtx.SetTenant(tenantId);
        var interceptor = new TenantConnectionInterceptor(tenantCtx);

        var options = new DbContextOptionsBuilder<PropiaDbContext>()
            .UseNpgsql(_fx.AppConnectionString)
            .AddInterceptors(interceptor)
            .Options;

        var db = new PropiaDbContext(options, tenantCtx);
        return (new MiCopropiedadService(db, tenantCtx), db, tenantCtx);
    }

    private async Task<Guid> SeedTenantAsync(string nombre)
    {
        var options = new DbContextOptionsBuilder<PropiaDbContext>()
            .UseNpgsql(_fx.OwnerConnectionString)
            .Options;
        await using var ctx = new PropiaDbContext(options, new TenantContext());
        var t = new Tenant
        {
            Nombre = nombre,
            Estado = EstadoCopropiedad.Activa,
            EstadoCustodia = EstadoCustodia.SinAdmin
        };
        ctx.Tenants.Add(t);
        await ctx.SaveChangesAsync();
        return t.Id;
    }

    private async Task<Guid> GetTorreTenantIdAsync(Guid torreId)
    {
        var options = new DbContextOptionsBuilder<PropiaDbContext>()
            .UseNpgsql(_fx.OwnerConnectionString)
            .Options;
        await using var ctx = new PropiaDbContext(options, new TenantContext());
        var t = await ctx.Torres.IgnoreQueryFilters().FirstAsync(x => x.Id == torreId);
        return t.TenantId;
    }

    private async Task CleanupTenantAsync(Guid tenantId, Guid[]? personaIds = null)
    {
        var options = new DbContextOptionsBuilder<PropiaDbContext>()
            .UseNpgsql(_fx.OwnerConnectionString)
            .Options;
        await using var ctx = new PropiaDbContext(options, new TenantContext());

        // Cleanup en orden FK
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM unidad_coeficientes WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM tipos_coeficiente WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM unidades_privadas WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM torres WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM miembros_consejo WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM comite_miembros WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM comites WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM revisores_fiscales WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM miembros_equipo WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM contratos_servicio WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM zonas_comunes WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM equipos_activos WHERE tenant_id = {tenantId}");
        await ctx.Database.ExecuteSqlAsync($"DELETE FROM tenants WHERE id = {tenantId}");

        if (personaIds is not null)
        {
            foreach (var pid in personaIds)
                await ctx.Database.ExecuteSqlAsync($"DELETE FROM personas WHERE id = {pid}");
        }
    }
}
