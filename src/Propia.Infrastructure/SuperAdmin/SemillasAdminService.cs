using Microsoft.EntityFrameworkCore;
using Propia.Application.Common;
using Propia.Application.SuperAdmin;
using Propia.Domain.Entities;
using Propia.Domain.Enums;
using Propia.Infrastructure.Persistence;

namespace Propia.Infrastructure.SuperAdmin;

/// <summary>
/// Semillas globales de la Consola A&amp;D (filas con tenant_id NULL): indice de todo lo que el Super Admin siembra y
/// API de las que no tenian endpoint (roles base, plantillas de comunicados, etiquetas del Directorio, etiquetas
/// de documentos, catalogo de reportes). Corre SIN copropiedad activa: RLS solo deja ver las filas globales y aqui
/// ademas se filtra por TenantId == null. Lo unico que recorre copropiedades (uso de un rol antes de eliminarlo) lo
/// hace con SetTenant + CloseConnectionAsync.
/// </summary>
public sealed class SemillasAdminService : ISemillasAdminService
{
    private readonly PropiaDbContext _db;
    private readonly ITenantContext _tenant;

    public SemillasAdminService(PropiaDbContext db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    // =====================================================================
    // Indice
    // =====================================================================

    public async Task<IReadOnlyList<SemillaTipoDto>> IndiceAsync(CancellationToken ct)
    {
        var listas = await _db.CatalogoOpciones.AsNoTracking().Select(o => o.Lista).Distinct().CountAsync(ct);
        var agentes = await _db.AiAgentTemplates.AsNoTracking().CountAsync(ct);
        var pqrsd = await _db.PqrsdPlantillasSemilla.AsNoTracking().CountAsync(ct);
        var planes = await _db.Planes.AsNoTracking().CountAsync(ct);
        var roles = await _db.RolesCopropiedad.AsNoTracking().CountAsync(r => r.TenantId == null, ct);
        var comunicados = await _db.ComunicadoPlantillas.AsNoTracking().CountAsync(p => p.TenantId == null, ct);
        var etqDir = await _db.EtiquetasCatalogo.AsNoTracking().CountAsync(e => e.TenantId == null, ct);
        var etqDoc = await _db.DocumentoEtiquetasCatalogo.AsNoTracking().CountAsync(e => e.TenantId == null, ct);
        var reportes = await _db.ReporteCatalogo.AsNoTracking().CountAsync(r => r.TenantId == null, ct);

        const string S = "/api/admin/semillas";
        return new List<SemillaTipoDto>
        {
            new("listas", "Catálogo de listas", "Opciones de fábrica de las listas del sistema (cada copropiedad copia la suya).", listas,
                new[] { "GET /api/admin/catalogos", "GET /api/admin/catalogos/opciones?lista={lista}", "POST /api/admin/catalogos/opciones?lista={lista}",
                        "PUT /api/admin/catalogos/opcion/{id}", "POST /api/admin/catalogos/reordenar?lista={lista}", "POST /api/admin/catalogos/resembrar" }),
            new("agentes", "Plantillas de agentes IA", "Agentes que nacen en cada copropiedad; versionadas, con \"Actualizar copropiedades\".", agentes,
                new[] { "GET /admin/ai-agent-templates", "GET /admin/ai-agent-templates/{id}", "POST /admin/ai-agent-templates", "PUT /admin/ai-agent-templates/{id}",
                        "DELETE /admin/ai-agent-templates/{id}", "GET /admin/ai-agent-templates/{id}/propagacion", "POST /admin/ai-agent-templates/{id}/propagar",
                        "GET /admin/ai-agent-templates/mcp-catalog" }),
            new("plantillas-pqrsd", "Plantillas PQRSD por defecto", "Plantillas de respuesta con las que nace cada copropiedad.", pqrsd,
                new[] { "GET /admin/pqrsd-plantillas-semilla", "POST /admin/pqrsd-plantillas-semilla", "PUT /admin/pqrsd-plantillas-semilla/{id}",
                        "DELETE /admin/pqrsd-plantillas-semilla/{id}" }),
            new("menu", "Menú del sistema", "Orden, nombres y visibilidad del menú de las copropiedades.", 1,
                new[] { "GET /admin/menu", "PUT /admin/menu", "POST /admin/menu/reset" }),
            new("planes", "Planes", "Planes de suscripción de la plataforma.", planes,
                new[] { "GET /api/admin/billing/planes", "POST /api/admin/billing/planes", "PUT /api/admin/billing/planes/{id}", "DELETE /api/admin/billing/planes/{id}" }),
            new("roles", "Roles base", "Roles globales y sus permisos por defecto (cada copropiedad puede sobrescribirlos).", roles,
                new[] { $"GET {S}/roles", $"POST {S}/roles", $"PUT {S}/roles/{{id}}", $"PUT {S}/roles/{{id}}/permisos", $"DELETE {S}/roles/{{id}}" }),
            new("comunicados", "Plantillas de comunicados", "Plantillas globales de PROPIA que las copropiedades clonan.", comunicados,
                new[] { $"GET {S}/comunicados", $"POST {S}/comunicados", $"PUT {S}/comunicados/{{id}}" }),
            new("etiquetas-directorio", "Etiquetas del Directorio", "Etiquetas base del Directorio (personas y empresas).", etqDir,
                new[] { $"GET {S}/etiquetas-directorio", $"POST {S}/etiquetas-directorio", $"PUT {S}/etiquetas-directorio/{{id}}" }),
            new("etiquetas-documentos", "Etiquetas de documentos", "Etiquetas base del Archivo central.", etqDoc,
                new[] { $"GET {S}/etiquetas-documentos", $"POST {S}/etiquetas-documentos", $"PUT {S}/etiquetas-documentos/{{id}}" }),
            new("reportes", "Catálogo de reportes", "Categorías y reportes prediseñados (se editan y activan; los crea cada módulo).", reportes,
                new[] { $"GET {S}/reportes", $"PUT {S}/reportes/categorias/{{id}}", $"PUT {S}/reportes/{{id}}" }),
        };
    }

    // =====================================================================
    // Roles base globales
    // =====================================================================

    private const string RolAdministrador = "Administrador";

    public async Task<IReadOnlyList<RolSemillaDto>> ListarRolesAsync(CancellationToken ct)
    {
        var roles = await _db.RolesCopropiedad.AsNoTracking().Where(r => r.TenantId == null)
            .OrderBy(r => r.Tipo).ThenBy(r => r.Nombre).ToListAsync(ct);
        var ids = roles.Select(r => r.Id).ToList();
        var permisos = await _db.RolPermisos.AsNoTracking().Where(p => ids.Contains(p.RolId)).ToListAsync(ct);
        return roles.Select(r => ToDto(r, permisos.Where(p => p.RolId == r.Id))).ToList();
    }

    public async Task<RolSemillaDto> CrearRolAsync(GuardarRolSemillaRequest req, CancellationToken ct)
    {
        var nombre = Requerido(req.Nombre, "Ponle un nombre al rol.", 100);
        if (await _db.RolesCopropiedad.AnyAsync(r => r.TenantId == null && r.Nombre.ToLower() == nombre.ToLower(), ct))
            throw new InvalidOperationException($"Ya existe un rol base llamado \"{nombre}\".");
        var r = new Rol
        {
            TenantId = null,
            Nombre = nombre,
            Descripcion = Opcional(req.Descripcion, 500),
            Tipo = TipoRol.Extendido,
            Categoria = req.Categoria ?? CategoriaRol.Administrativo,
            EsEliminable = true,
            Activo = req.Activo ?? true,
        };
        _db.RolesCopropiedad.Add(r);
        await _db.SaveChangesAsync(ct);
        return ToDto(r, Array.Empty<RolPermiso>());
    }

    public async Task<RolSemillaDto?> ActualizarRolAsync(Guid id, GuardarRolSemillaRequest req, CancellationToken ct)
    {
        var r = await _db.RolesCopropiedad.FirstOrDefaultAsync(x => x.Id == id && x.TenantId == null, ct);
        if (r is null) return null;
        var esAdmin = string.Equals(r.Nombre, RolAdministrador, StringComparison.OrdinalIgnoreCase);
        if (req.Nombre is not null)
        {
            var nombre = Requerido(req.Nombre, "El nombre no puede quedar vacío.", 100);
            if (esAdmin && !string.Equals(nombre, r.Nombre, StringComparison.Ordinal))
                throw new InvalidOperationException("El rol Administrador no se renombra: el sistema lo asigna por nombre al crear cada copropiedad.");
            if (await _db.RolesCopropiedad.AnyAsync(x => x.TenantId == null && x.Id != id && x.Nombre.ToLower() == nombre.ToLower(), ct))
                throw new InvalidOperationException($"Ya existe un rol base llamado \"{nombre}\".");
            r.Nombre = nombre;
        }
        if (req.Descripcion is not null) r.Descripcion = Opcional(req.Descripcion, 500);
        if (req.Categoria is { } c) r.Categoria = c;
        if (req.Activo is { } a)
        {
            if (esAdmin && !a) throw new InvalidOperationException("El rol Administrador no se desactiva.");
            r.Activo = a;
        }
        await _db.SaveChangesAsync(ct);
        return ToDto(r, await _db.RolPermisos.AsNoTracking().Where(p => p.RolId == id).ToListAsync(ct));
    }

    public async Task<RolSemillaDto?> GuardarPermisosRolAsync(Guid id, GuardarPermisosRolSemillaRequest req, CancellationToken ct)
    {
        var r = await _db.RolesCopropiedad.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.TenantId == null, ct);
        if (r is null) return null;
        var actuales = await _db.RolPermisos.Where(p => p.RolId == id).ToListAsync(ct);
        foreach (var p in req.Permisos ?? Array.Empty<RolPermisoSemillaDto>())
        {
            var modulo = (p.ModuloCodigo ?? "").Trim().ToUpperInvariant();
            if (!ModuloCodigo.Todos.Contains(modulo)) throw new InvalidOperationException($"El módulo \"{p.ModuloCodigo}\" no existe.");
            if (!Enum.IsDefined(p.Accion)) throw new InvalidOperationException($"La acción {p.Accion} no existe.");
            var fila = actuales.FirstOrDefault(x => x.ModuloCodigo == modulo && x.Accion == p.Accion);
            if (fila is null)
            {
                fila = new RolPermiso { RolId = id, ModuloCodigo = modulo, Accion = p.Accion };
                _db.RolPermisos.Add(fila);
                actuales.Add(fila);
            }
            fila.Habilitado = p.Habilitado;
            fila.NivelDato = p.NivelDato;
        }
        await _db.SaveChangesAsync(ct);
        return ToDto(r, actuales);
    }

    public async Task<bool> EliminarRolAsync(Guid id, CancellationToken ct)
    {
        var r = await _db.RolesCopropiedad.FirstOrDefaultAsync(x => x.Id == id && x.TenantId == null, ct);
        if (r is null) return false;
        if (!r.EsEliminable || r.Tipo != TipoRol.Extendido)
            throw new InvalidOperationException("Solo se eliminan roles extendidos eliminables; los base se desactivan.");
        if (await RolEnUsoAsync(id, ct))
            throw new InvalidOperationException("El rol está asignado a usuarios en alguna copropiedad: desactívalo en vez de eliminarlo.");
        _db.RolPermisos.RemoveRange(await _db.RolPermisos.Where(p => p.RolId == id).ToListAsync(ct));
        _db.RolesCopropiedad.Remove(r);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // usuario_tenant_roles tiene RLS por copropiedad: se recorre cada una (SetTenant + CloseConnection) y se
    // vuelve al contexto sin copropiedad.
    private async Task<bool> RolEnUsoAsync(Guid rolId, CancellationToken ct)
    {
        var previo = _tenant.CurrentTenantId;
        var tenants = await _db.Tenants.AsNoTracking().Select(t => t.Id).ToListAsync(ct);
        try
        {
            foreach (var tid in tenants)
            {
                _tenant.SetTenant(tid);
                await _db.Database.CloseConnectionAsync();
                if (await _db.UsuarioTenantRoles.AsNoTracking().AnyAsync(x => x.RolId == rolId, ct)) return true;
            }
            return false;
        }
        finally
        {
            if (previo is { } p) _tenant.SetTenant(p); else _tenant.Clear();
            await _db.Database.CloseConnectionAsync();
        }
    }

    private static RolSemillaDto ToDto(Rol r, IEnumerable<RolPermiso> permisos)
        => new(r.Id, r.Nombre, r.Descripcion, r.Tipo, r.Categoria, r.EsEliminable, r.Activo,
            permisos.OrderBy(p => p.ModuloCodigo).ThenBy(p => p.Accion)
                .Select(p => new RolPermisoSemillaDto(p.ModuloCodigo, p.Accion, p.Habilitado, p.NivelDato)).ToList());

    // =====================================================================
    // Plantillas globales de comunicados
    // =====================================================================

    public async Task<IReadOnlyList<ComunicadoPlantillaSemillaDto>> ListarPlantillasComunicadoAsync(CancellationToken ct)
        => (await _db.ComunicadoPlantillas.AsNoTracking().Where(p => p.TenantId == null).OrderBy(p => p.Nombre).ToListAsync(ct))
            .Select(ToDto).ToList();

    public async Task<ComunicadoPlantillaSemillaDto> CrearPlantillaComunicadoAsync(GuardarComunicadoPlantillaSemillaRequest req, CancellationToken ct)
    {
        var p = new ComunicadoPlantilla
        {
            TenantId = null,
            EsGlobal = true,
            Nombre = Requerido(req.Nombre, "Ponle un nombre a la plantilla.", 200),
            TipoComunicado = req.TipoComunicado ?? TipoComunicadoBase.Circular,
            AsuntoModelo = Requerido(req.AsuntoModelo, "Escribe el asunto de la plantilla.", 300),
            CuerpoModelo = Common.HtmlSanitization.Clean(req.CuerpoModelo) ?? "",   // S-19
            AcusePorDefecto = req.AcusePorDefecto ?? false,
            Activa = req.Activa ?? true,
        };
        _db.ComunicadoPlantillas.Add(p);
        await _db.SaveChangesAsync(ct);
        return ToDto(p);
    }

    public async Task<ComunicadoPlantillaSemillaDto?> ActualizarPlantillaComunicadoAsync(Guid id, GuardarComunicadoPlantillaSemillaRequest req, CancellationToken ct)
    {
        var p = await _db.ComunicadoPlantillas.FirstOrDefaultAsync(x => x.Id == id && x.TenantId == null, ct);
        if (p is null) return null;
        if (req.Nombre is not null) p.Nombre = Requerido(req.Nombre, "El nombre no puede quedar vacío.", 200);
        if (req.TipoComunicado is { } t) p.TipoComunicado = t;
        if (req.AsuntoModelo is not null) p.AsuntoModelo = Requerido(req.AsuntoModelo, "El asunto no puede quedar vacío.", 300);
        if (req.CuerpoModelo is not null) p.CuerpoModelo = Common.HtmlSanitization.Clean(req.CuerpoModelo) ?? "";   // S-19
        if (req.AcusePorDefecto is { } a) p.AcusePorDefecto = a;
        if (req.Activa is { } act) p.Activa = act;
        await _db.SaveChangesAsync(ct);
        return ToDto(p);
    }

    private static ComunicadoPlantillaSemillaDto ToDto(ComunicadoPlantilla p)
        => new(p.Id, p.Nombre, p.TipoComunicado, p.AsuntoModelo, p.CuerpoModelo, p.AcusePorDefecto, p.Activa);

    // =====================================================================
    // Etiquetas base del Directorio
    // =====================================================================

    public async Task<IReadOnlyList<EtiquetaDirectorioSemillaDto>> ListarEtiquetasDirectorioAsync(CancellationToken ct)
        => (await _db.EtiquetasCatalogo.AsNoTracking().Where(e => e.TenantId == null).OrderBy(e => e.Orden).ThenBy(e => e.Nombre).ToListAsync(ct))
            .Select(ToDto).ToList();

    public async Task<EtiquetaDirectorioSemillaDto> CrearEtiquetaDirectorioAsync(GuardarEtiquetaDirectorioSemillaRequest req, CancellationToken ct)
    {
        var nombre = Requerido(req.Nombre, "Ponle un nombre a la etiqueta.", 100);
        var codigo = string.IsNullOrWhiteSpace(req.Codigo) ? CodigoDe(nombre) : req.Codigo.Trim().ToUpperInvariant();
        if (await _db.EtiquetasCatalogo.AnyAsync(e => e.TenantId == null && e.Codigo == codigo, ct))
            throw new InvalidOperationException($"Ya existe una etiqueta base con el código {codigo}.");
        var orden = req.Orden ?? (await _db.EtiquetasCatalogo.Where(e => e.TenantId == null).Select(e => (int?)e.Orden).MaxAsync(ct) ?? 0) + 1;
        var e = new EtiquetaCatalogo
        {
            TenantId = null,
            EsBase = true,
            Codigo = codigo,
            Nombre = nombre,
            Grupo = req.Grupo ?? GrupoEtiqueta.Identidad,
            AplicaA = req.AplicaA ?? AplicaEtiqueta.Persona,
            Icono = Opcional(req.Icono, 20),
            Color = Color(req.Color),
            Orden = orden,
            Activo = req.Activo ?? true,
        };
        _db.EtiquetasCatalogo.Add(e);
        await _db.SaveChangesAsync(ct);
        return ToDto(e);
    }

    public async Task<EtiquetaDirectorioSemillaDto?> ActualizarEtiquetaDirectorioAsync(Guid id, GuardarEtiquetaDirectorioSemillaRequest req, CancellationToken ct)
    {
        var e = await _db.EtiquetasCatalogo.FirstOrDefaultAsync(x => x.Id == id && x.TenantId == null, ct);
        if (e is null) return null;
        // El codigo no se cambia: los modulos con logica especial lo leen por codigo.
        if (req.Nombre is not null) e.Nombre = Requerido(req.Nombre, "El nombre no puede quedar vacío.", 100);
        if (req.Grupo is { } g) e.Grupo = g;
        if (req.AplicaA is { } ap) e.AplicaA = ap;
        if (req.Icono is not null) e.Icono = Opcional(req.Icono, 20);
        if (req.Color is not null) e.Color = Color(req.Color);
        if (req.Orden is { } o) e.Orden = o;
        if (req.Activo is { } a) e.Activo = a;
        await _db.SaveChangesAsync(ct);
        return ToDto(e);
    }

    private static EtiquetaDirectorioSemillaDto ToDto(EtiquetaCatalogo e)
        => new(e.Id, e.Codigo, e.Nombre, e.Grupo, e.AplicaA, e.TieneLogicaEspecial, e.Icono, e.Color, e.Orden, e.Activo);

    // =====================================================================
    // Etiquetas base de documentos
    // =====================================================================

    public async Task<IReadOnlyList<EtiquetaDocumentoSemillaDto>> ListarEtiquetasDocumentoAsync(CancellationToken ct)
        => (await _db.DocumentoEtiquetasCatalogo.AsNoTracking().Where(e => e.TenantId == null).OrderBy(e => e.Nombre).ToListAsync(ct))
            .Select(ToDto).ToList();

    public async Task<EtiquetaDocumentoSemillaDto> CrearEtiquetaDocumentoAsync(GuardarEtiquetaDocumentoSemillaRequest req, CancellationToken ct)
    {
        var nombre = Requerido(req.Nombre, "Ponle un nombre a la etiqueta.", 100);
        if (await _db.DocumentoEtiquetasCatalogo.AnyAsync(e => e.TenantId == null && e.Nombre.ToLower() == nombre.ToLower(), ct))
            throw new InvalidOperationException($"Ya existe una etiqueta base llamada \"{nombre}\".");
        var e = new DocumentoEtiquetaCatalogo { TenantId = null, EsBase = true, Nombre = nombre, Color = Color(req.Color), Activa = req.Activa ?? true };
        _db.DocumentoEtiquetasCatalogo.Add(e);
        await _db.SaveChangesAsync(ct);
        return ToDto(e);
    }

    public async Task<EtiquetaDocumentoSemillaDto?> ActualizarEtiquetaDocumentoAsync(Guid id, GuardarEtiquetaDocumentoSemillaRequest req, CancellationToken ct)
    {
        var e = await _db.DocumentoEtiquetasCatalogo.FirstOrDefaultAsync(x => x.Id == id && x.TenantId == null, ct);
        if (e is null) return null;
        if (req.Nombre is not null) e.Nombre = Requerido(req.Nombre, "El nombre no puede quedar vacío.", 100);
        if (req.Color is not null) e.Color = Color(req.Color);
        if (req.Activa is { } a) e.Activa = a;
        await _db.SaveChangesAsync(ct);
        return ToDto(e);
    }

    private static EtiquetaDocumentoSemillaDto ToDto(DocumentoEtiquetaCatalogo e) => new(e.Id, e.Nombre, e.Color, e.Activa);

    // =====================================================================
    // Catalogo de reportes (editar / activar; no se crean)
    // =====================================================================

    public async Task<IReadOnlyList<ReporteCategoriaSemillaDto>> ListarReportesAsync(CancellationToken ct)
    {
        var cats = await _db.ReporteCategorias.AsNoTracking().Where(c => c.TenantId == null).OrderBy(c => c.Orden).ToListAsync(ct);
        var reps = await _db.ReporteCatalogo.AsNoTracking().Where(r => r.TenantId == null).OrderBy(r => r.Orden).ToListAsync(ct);
        return cats.Select(c => ToDto(c, reps.Where(r => r.CategoriaId == c.Id))).ToList();
    }

    public async Task<ReporteCategoriaSemillaDto?> ActualizarReporteCategoriaAsync(Guid id, GuardarReporteCategoriaSemillaRequest req, CancellationToken ct)
    {
        var c = await _db.ReporteCategorias.FirstOrDefaultAsync(x => x.Id == id && x.TenantId == null, ct);
        if (c is null) return null;
        if (req.Nombre is not null) c.Nombre = Requerido(req.Nombre, "El nombre no puede quedar vacío.", 150);
        if (req.Icono is not null) c.Icono = Opcional(req.Icono, 60);
        if (req.Color is not null) c.Color = Color(req.Color);
        if (req.Orden is { } o) c.Orden = o;
        if (req.Activa is { } a) c.EsActiva = a;
        await _db.SaveChangesAsync(ct);
        var reps = await _db.ReporteCatalogo.AsNoTracking().Where(r => r.TenantId == null && r.CategoriaId == id).OrderBy(r => r.Orden).ToListAsync(ct);
        return ToDto(c, reps);
    }

    public async Task<ReporteSemillaDto?> ActualizarReporteAsync(Guid id, GuardarReporteSemillaRequest req, CancellationToken ct)
    {
        var r = await _db.ReporteCatalogo.FirstOrDefaultAsync(x => x.Id == id && x.TenantId == null, ct);
        if (r is null) return null;
        if (req.Nombre is not null) r.Nombre = Requerido(req.Nombre, "El nombre no puede quedar vacío.", 200);
        if (req.Descripcion is not null) r.Descripcion = Opcional(req.Descripcion, 1000);
        if (req.AudienciasJson is not null)
        {
            try
            {
                var arr = System.Text.Json.JsonSerializer.Deserialize<string[]>(req.AudienciasJson);
                if (arr is null || arr.Length == 0) throw new InvalidOperationException("Indica al menos una audiencia.");
                r.AudienciasJson = System.Text.Json.JsonSerializer.Serialize(arr.Select(a => a.Trim().ToUpperInvariant()).Distinct());
            }
            catch (System.Text.Json.JsonException) { throw new InvalidOperationException("Audiencias debe ser un arreglo JSON, ej.: [\"ADMINISTRADOR\",\"CONSEJO\"]."); }
        }
        if (req.Orden is { } o) r.Orden = o;
        if (req.Activo is { } a) r.EsActivo = a;
        await _db.SaveChangesAsync(ct);
        return ToDto(r);
    }

    private static ReporteCategoriaSemillaDto ToDto(ReporteCategoria c, IEnumerable<ReporteCatalogo> reps)
        => new(c.Id, c.Nombre, c.Icono, c.Color, c.ModuloOrigen, c.Orden, c.EsActiva, reps.Select(ToDto).ToList());

    private static ReporteSemillaDto ToDto(ReporteCatalogo r)
        => new(r.Id, r.CategoriaId, r.Clave, r.Nombre, r.Descripcion, r.ModuloOrigen, r.AudienciasJson, r.Orden, r.EsActivo);

    // =====================================================================
    // Helpers
    // =====================================================================

    private static string Requerido(string? v, string error, int max)
    {
        var s = (v ?? "").Trim();
        if (s.Length == 0) throw new InvalidOperationException(error);
        return s.Length > max ? s[..max] : s;
    }

    private static string? Opcional(string? v, int max)
    {
        var s = (v ?? "").Trim();
        return s.Length == 0 ? null : s.Length > max ? s[..max] : s;
    }

    // Color hex (#RGB / #RRGGBB) o vacio. Otro formato = error (se pinta inline en los modulos).
    private static string? Color(string? c)
    {
        var s = (c ?? "").Trim();
        if (s.Length == 0) return null;
        if (s.Length is not (4 or 7) || s[0] != '#' || !s.Skip(1).All(Uri.IsHexDigit))
            throw new InvalidOperationException("El color va en hexadecimal, ej.: #6D4FE3.");
        return s.ToUpperInvariant();
    }

    private static string CodigoDe(string nombre)
    {
        var sinTildes = new string(nombre.Normalize(System.Text.NormalizationForm.FormD)
            .Where(ch => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch) != System.Globalization.UnicodeCategory.NonSpacingMark).ToArray());
        var cod = System.Text.RegularExpressions.Regex.Replace(sinTildes.ToUpperInvariant(), "[^A-Z0-9]+", "_").Trim('_');
        return cod.Length == 0 ? "ETIQUETA" : cod.Length > 50 ? cod[..50] : cod;
    }
}
