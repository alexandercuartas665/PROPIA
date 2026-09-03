using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Propia.Application.Common;
using Propia.Application.Tareas;
using Propia.Domain.Entities;
using Propia.Domain.Enums;
using Propia.Infrastructure.Persistence;

namespace Propia.Infrastructure.Tareas;

// Particion de TareasService por area (clase parcial: comparte _db/_tenantContext/_http/_noti
// y GetUsuarioActualId del archivo principal). Mismo comportamiento.
public partial class TareasService
{
    // Tableros de trabajo (creacion, miembros, configuracion).
    // ===================== Tableros de trabajo =====================

    private static string? ColorEstadoBase(string nombre) => nombre switch
    {
        EstadoTareaBase.Pendiente => "#94a3b8",
        EstadoTareaBase.EnProgreso => "#3b82f6",
        EstadoTareaBase.EnRevision => "#f59e0b",
        EstadoTareaBase.Bloqueada => "#ef4444",
        EstadoTareaBase.Completada => "#22c55e",
        EstadoTareaBase.Cancelada => "#6b7280",
        _ => "#94a3b8"
    };

    private static string Iniciales(string? nombre)
    {
        var parts = (nombre ?? "").Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return "?";
        if (parts.Length == 1) return parts[0].Substring(0, Math.Min(2, parts[0].Length)).ToUpperInvariant();
        return ("" + parts[0][0] + parts[1][0]).ToUpperInvariant();
    }

    private async Task SembrarEstadosTableroAsync(Guid tableroId, CancellationToken ct)
    {
        foreach (var (nombre, orden, esTerminal) in EstadoTareaBase.Base)
            _db.TareasEstados.Add(new TareaEstado
            {
                TableroId = tableroId,
                Nombre = nombre,
                Orden = orden,
                EsTerminal = esTerminal,
                EsBase = true,
                Activo = true,
                Color = ColorEstadoBase(nombre)
            });
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>Crea el tablero "General" si no existe y migra estados/tareas legacy (TableroId null) a el.</summary>
    private async Task<Guid> AsegurarTableroDefaultAsync(CancellationToken ct)
    {
        var existing = await _db.Tableros.OrderBy(t => t.Orden).Select(t => t.Id).FirstOrDefaultAsync(ct);
        if (existing != Guid.Empty) return existing;

        var t = new Tablero { Nombre = "General", Descripcion = "Tablero principal de tareas.", Color = "#6D4FE3", Orden = 0, Activo = true };
        _db.Tableros.Add(t);
        await _db.SaveChangesAsync(ct);

        // Migrar estados y tareas legacy (sin tablero) al tablero por defecto.
        await _db.TareasEstados.Where(e => e.TableroId == null)
            .ExecuteUpdateAsync(s => s.SetProperty(e => e.TableroId, t.Id), ct);
        await _db.Tareas.Where(x => x.TableroId == null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.TableroId, t.Id), ct);
        return t.Id;
    }

    private async Task<TableroDto> MapTableroAsync(Tablero t, CancellationToken ct)
    {
        var nCards = await _db.Tareas.AsNoTracking().CountAsync(x => x.TableroId == t.Id && x.PadreId == null && !x.Eliminada, ct);
        var usuariosIds = await _db.TableroUsuarios.AsNoTracking().Where(u => u.TableroId == t.Id).Select(u => u.PersonaId).ToListAsync(ct);
        var personas = await _db.Personas.AsNoTracking().Where(p => usuariosIds.Contains(p.Id))
            .Select(p => new { p.Id, Nombre = p.Nombres + " " + p.Apellidos }).ToListAsync(ct);
        var usuarios = personas.Select(p => new TableroUsuarioDto(p.Id, p.Nombre.Trim(), Iniciales(p.Nombre))).ToList();
        // Solo campos ACTIVOS: los archivados quedan fuera del modal, columnas y filtros (datos conservados).
        var campos = await _db.TableroCampos.AsNoTracking().Where(c => c.TableroId == t.Id && c.Activo)
            .OrderBy(c => c.Orden)
            .Select(c => new TableroCampoDto(c.Id, c.Label, c.Orden, c.Tipo, c.Opciones, c.MostrarEnFiltro, c.Columna, c.Descripcion, c.Requerido, c.ValorPorDefecto, c.PermiteVarios, c.CamposSuma, c.Activo))
            .ToListAsync(ct);
        return new TableroDto(t.Id, t.Nombre, t.Descripcion, t.Color, t.Orden, nCards, usuarios, campos);
    }

    private static int ClampColumna(int c) => c < 1 ? 1 : (c > 2 ? 2 : c);

    public async Task<TableroCampoDto> AgregarCampoAsync(Guid tableroId, GuardarCampoRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Label) || req.Label.Trim().Length < 2)
            throw new InvalidOperationException("Etiqueta minimo 2 caracteres.");
        var lab = req.Label.Trim();
        if (!await _db.Tableros.AnyAsync(t => t.Id == tableroId, ct))
            throw new InvalidOperationException("Tablero no encontrado.");
        if (await _db.TableroCampos.AnyAsync(c => c.TableroId == tableroId && c.Label == lab && c.Activo, ct))
            throw new InvalidOperationException("Ya existe un campo con esa etiqueta.");
        var orden = (await _db.TableroCampos.Where(c => c.TableroId == tableroId).Select(c => (int?)c.Orden).MaxAsync(ct) ?? 0) + 1;
        var c2 = new TableroCampo
        {
            TableroId = tableroId,
            Label = lab,
            Orden = orden,
            Tipo = req.Tipo,
            Opciones = string.IsNullOrWhiteSpace(req.Opciones) ? null : req.Opciones.Trim(),
            MostrarEnFiltro = req.MostrarEnFiltro,
            Columna = ClampColumna(req.Columna),
            Descripcion = string.IsNullOrWhiteSpace(req.Descripcion) ? null : req.Descripcion.Trim(),
            Requerido = req.Requerido,
            ValorPorDefecto = string.IsNullOrWhiteSpace(req.ValorPorDefecto) ? null : req.ValorPorDefecto.Trim(),
            PermiteVarios = req.PermiteVarios,
            CamposSuma = string.IsNullOrWhiteSpace(req.CamposSuma) ? null : req.CamposSuma.Trim()
        };
        _db.TableroCampos.Add(c2);
        await _db.SaveChangesAsync(ct);
        return new TableroCampoDto(c2.Id, c2.Label, c2.Orden, c2.Tipo, c2.Opciones, c2.MostrarEnFiltro, c2.Columna, c2.Descripcion, c2.Requerido, c2.ValorPorDefecto, c2.PermiteVarios, c2.CamposSuma, c2.Activo);
    }

    public async Task<bool> ActualizarCampoAsync(Guid tableroId, Guid campoId, GuardarCampoRequest req, CancellationToken ct)
    {
        var c = await _db.TableroCampos.FirstOrDefaultAsync(x => x.Id == campoId && x.TableroId == tableroId, ct);
        if (c is null) return false;
        if (string.IsNullOrWhiteSpace(req.Label) || req.Label.Trim().Length < 2)
            throw new InvalidOperationException("Etiqueta minimo 2 caracteres.");
        var lab = req.Label.Trim();
        if (await _db.TableroCampos.AnyAsync(x => x.TableroId == tableroId && x.Label == lab && x.Id != campoId && x.Activo, ct))
            throw new InvalidOperationException("Ya existe un campo con esa etiqueta.");
        c.Label = lab;
        c.Tipo = req.Tipo;
        c.Opciones = string.IsNullOrWhiteSpace(req.Opciones) ? null : req.Opciones.Trim();
        c.MostrarEnFiltro = req.MostrarEnFiltro;
        c.Columna = ClampColumna(req.Columna);
        c.Descripcion = string.IsNullOrWhiteSpace(req.Descripcion) ? null : req.Descripcion.Trim();
        c.Requerido = req.Requerido;
        c.ValorPorDefecto = string.IsNullOrWhiteSpace(req.ValorPorDefecto) ? null : req.ValorPorDefecto.Trim();
        c.PermiteVarios = req.PermiteVarios;
        c.CamposSuma = string.IsNullOrWhiteSpace(req.CamposSuma) ? null : req.CamposSuma.Trim();
        c.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>Inserta/actualiza los valores de campos personalizados de una tarjeta (solo campos validos del tablero).</summary>
    private async Task ReemplazarCamposValoresAsync(Guid tareaId, IReadOnlyList<TareaCampoValorDto>? valores, CancellationToken ct)
    {
        if (valores is null) return;
        var tableroId = await _db.Tareas.Where(t => t.Id == tareaId).Select(t => t.TableroId).FirstOrDefaultAsync(ct);
        var validos = (await _db.TableroCampos.Where(c => c.TableroId == tableroId).Select(c => c.Id).ToListAsync(ct)).ToHashSet();
        var existentes = await _db.TareaCampoValores.Where(v => v.TareaId == tareaId).ToListAsync(ct);
        foreach (var v in valores)
        {
            if (!validos.Contains(v.CampoId)) continue;
            var val = string.IsNullOrWhiteSpace(v.Valor) ? null : v.Valor.Trim();
            var ex = existentes.FirstOrDefault(x => x.TableroCampoId == v.CampoId);
            if (ex is null)
            {
                if (val is not null)
                    _db.TareaCampoValores.Add(new TareaCampoValor { TareaId = tareaId, TableroCampoId = v.CampoId, Valor = val });
            }
            else
            {
                ex.Valor = val;
                ex.UpdatedAt = DateTimeOffset.UtcNow;
            }
        }
        await _db.SaveChangesAsync(ct);
    }

    public async Task<bool> EliminarCampoAsync(Guid tableroId, Guid campoId, CancellationToken ct)
    {
        var c = await _db.TableroCampos.FirstOrDefaultAsync(x => x.Id == campoId && x.TableroId == tableroId, ct);
        if (c is null) return false;
        await _db.TareaCampoValores.Where(v => v.TableroCampoId == campoId).ExecuteDeleteAsync(ct);
        _db.TableroCampos.Remove(c);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>Archiva (activo=false) o restaura (activo=true) un campo. Los valores capturados se
    /// conservan; el campo solo desaparece/aparece del modal, columnas y filtros (distinto de eliminar).</summary>
    public async Task<bool> SetCampoActivoAsync(Guid tableroId, Guid campoId, bool activo, CancellationToken ct)
    {
        var c = await _db.TableroCampos.FirstOrDefaultAsync(x => x.Id == campoId && x.TableroId == tableroId, ct);
        if (c is null) return false;
        if (activo && await _db.TableroCampos.AnyAsync(x => x.TableroId == tableroId && x.Id != campoId && x.Label == c.Label && x.Activo, ct))
            throw new InvalidOperationException($"Ya existe un campo activo llamado '{c.Label}'. Renombra uno antes de restaurar.");
        c.Activo = activo;
        c.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>Lista los campos ARCHIVADOS (activo=false) de un tablero, para poder restaurarlos.</summary>
    public async Task<IReadOnlyList<TableroCampoDto>> ListarCamposArchivadosAsync(Guid tableroId, CancellationToken ct) =>
        await _db.TableroCampos.AsNoTracking().Where(c => c.TableroId == tableroId && !c.Activo)
            .OrderBy(c => c.Label)
            .Select(c => new TableroCampoDto(c.Id, c.Label, c.Orden, c.Tipo, c.Opciones, c.MostrarEnFiltro, c.Columna, c.Descripcion, c.Requerido, c.ValorPorDefecto, c.PermiteVarios, c.CamposSuma, c.Activo))
            .ToListAsync(ct);

    /// <summary>Sube (direccion &lt; 0) o baja (direccion &gt;= 0) un campo, intercambiando el
    /// Orden con el campo vecino. Normaliza los ordenes a 0..n-1 para tolerar huecos/empates.</summary>
    public async Task<bool> ReordenarCampoAsync(Guid tableroId, Guid campoId, int direccion, CancellationToken ct)
    {
        var campos = await _db.TableroCampos.Where(c => c.TableroId == tableroId)
            .OrderBy(c => c.Orden).ThenBy(c => c.Id).ToListAsync(ct);
        for (int i = 0; i < campos.Count; i++) campos[i].Orden = i;
        var idx = campos.FindIndex(c => c.Id == campoId);
        if (idx < 0) return false;
        var swap = idx + (direccion < 0 ? -1 : 1);
        if (swap < 0 || swap >= campos.Count) return false;   // ya esta en el extremo
        (campos[idx].Orden, campos[swap].Orden) = (campos[swap].Orden, campos[idx].Orden);
        campos[idx].UpdatedAt = campos[swap].UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<IReadOnlyList<TableroDto>> ListarTablerosAsync(CancellationToken ct)
    {
        await AsegurarEstadosBaseAsync(ct);
        await AsegurarTableroDefaultAsync(ct);
        var tableros = await _db.Tableros.AsNoTracking().Where(t => t.Activo).OrderBy(t => t.Orden).ToListAsync(ct);
        var result = new List<TableroDto>(tableros.Count);
        foreach (var t in tableros) result.Add(await MapTableroAsync(t, ct));
        return result;
    }

    public async Task<TableroDto?> GetTableroAsync(Guid id, CancellationToken ct)
    {
        var t = await _db.Tableros.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        return t is null ? null : await MapTableroAsync(t, ct);
    }

    private async Task SetTableroUsuariosAsync(Guid tableroId, IReadOnlyList<Guid>? personaIds, CancellationToken ct)
    {
        await _db.TableroUsuarios.Where(u => u.TableroId == tableroId).ExecuteDeleteAsync(ct);
        foreach (var pid in (personaIds ?? Array.Empty<Guid>()).Distinct())
            _db.TableroUsuarios.Add(new TableroUsuario { TableroId = tableroId, PersonaId = pid });
        await _db.SaveChangesAsync(ct);
    }

    public async Task<TableroDto> CrearTableroAsync(GuardarTableroRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Nombre)) throw new InvalidOperationException("Nombre del tablero requerido.");
        var maxOrden = await _db.Tableros.AnyAsync(ct) ? await _db.Tableros.MaxAsync(t => t.Orden, ct) : -1;
        var t = new Tablero
        {
            Nombre = req.Nombre.Trim(),
            Descripcion = string.IsNullOrWhiteSpace(req.Descripcion) ? null : req.Descripcion.Trim(),
            Color = string.IsNullOrWhiteSpace(req.Color) ? "#6D4FE3" : req.Color.Trim(),
            Orden = maxOrden + 1,
            Activo = true
        };
        _db.Tableros.Add(t);
        await _db.SaveChangesAsync(ct);
        await SetTableroUsuariosAsync(t.Id, req.UsuarioPersonaIds, ct);
        await SembrarEstadosTableroAsync(t.Id, ct);
        return (await GetTableroAsync(t.Id, ct))!;
    }

    public async Task<bool> ActualizarTableroAsync(Guid id, GuardarTableroRequest req, CancellationToken ct)
    {
        var t = await _db.Tableros.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (t is null) return false;
        if (string.IsNullOrWhiteSpace(req.Nombre)) throw new InvalidOperationException("Nombre del tablero requerido.");
        t.Nombre = req.Nombre.Trim();
        t.Descripcion = string.IsNullOrWhiteSpace(req.Descripcion) ? null : req.Descripcion.Trim();
        t.Color = string.IsNullOrWhiteSpace(req.Color) ? t.Color : req.Color.Trim();
        t.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        await SetTableroUsuariosAsync(t.Id, req.UsuarioPersonaIds, ct);
        return true;
    }

    // Enlazar/desenlazar una persona a un tablero (2.5.D: gestion de tableros desde el usuario).
    public async Task<bool> AgregarUsuarioTableroAsync(Guid tableroId, Guid personaId, CancellationToken ct)
    {
        var existe = await _db.Tableros.AnyAsync(t => t.Id == tableroId, ct);
        if (!existe) return false;
        var ya = await _db.TableroUsuarios.AnyAsync(u => u.TableroId == tableroId && u.PersonaId == personaId, ct);
        if (!ya)
        {
            _db.TableroUsuarios.Add(new TableroUsuario { TableroId = tableroId, PersonaId = personaId });
            await _db.SaveChangesAsync(ct);
        }
        return true;
    }

    public async Task<AgregarPorCorreoResultado> AgregarUsuarioTableroPorCorreoAsync(Guid tableroId, string email, CancellationToken ct)
    {
        email = (email ?? "").Trim();
        if (email.Length == 0 || !email.Contains('@'))
            return new AgregarPorCorreoResultado(false, "Escribe un correo valido.", null, false);
        if (!await _db.Tableros.AnyAsync(t => t.Id == tableroId, ct))
            return new AgregarPorCorreoResultado(false, "Tablero no encontrado.", null, false);

        // Usuario del sistema = cuenta con login (asp_net_users) con persona asociada. Es GLOBAL
        // (sin tenant): se busca por el correo normalizado exacto, aunque sea de otro cliente.
        var norm = email.ToUpperInvariant();
        var personaId = await _db.Users.IgnoreQueryFilters()
            .Where(u => u.NormalizedEmail == norm && u.PersonaId != null)
            .Select(u => u.PersonaId)
            .FirstOrDefaultAsync(ct);
        if (personaId is not Guid pid)
            return new AgregarPorCorreoResultado(false,
                "No hay un usuario del sistema con ese correo (debe tener cuenta activa en la plataforma).", null, false);

        var nombre = await _db.Personas.IgnoreQueryFilters()
            .Where(p => p.Id == pid)
            .Select(p => (p.Nombres + " " + p.Apellidos).Trim())
            .FirstOrDefaultAsync(ct);

        var ya = await _db.TableroUsuarios.AnyAsync(u => u.TableroId == tableroId && u.PersonaId == pid, ct);
        if (!ya)
        {
            _db.TableroUsuarios.Add(new TableroUsuario { TableroId = tableroId, PersonaId = pid });
            await _db.SaveChangesAsync(ct);
        }
        return new AgregarPorCorreoResultado(true, null, string.IsNullOrWhiteSpace(nombre) ? email : nombre, ya);
    }

    public async Task<bool> QuitarUsuarioTableroAsync(Guid tableroId, Guid personaId, CancellationToken ct)
    {
        var n = await _db.TableroUsuarios.Where(u => u.TableroId == tableroId && u.PersonaId == personaId).ExecuteDeleteAsync(ct);
        return n > 0;
    }

    public async Task<bool> EliminarTableroAsync(Guid id, CancellationToken ct)
    {
        var t = await _db.Tableros.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (t is null) return false;
        // Soft-delete: ocultamos el tablero pero conservamos sus tarjetas/estados.
        t.Activo = false;
        t.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<TableroBoardDto?> GetTableroBoardAsync(Guid tableroId, CancellationToken ct, bool verCerradas = false)
    {
        await AsegurarTableroDefaultAsync(ct);
        var t = await _db.Tableros.AsNoTracking().FirstOrDefaultAsync(x => x.Id == tableroId && x.Activo, ct);
        if (t is null) return null;
        var dto = await MapTableroAsync(t, ct);
        var estados = await _db.TareasEstados.AsNoTracking().Where(e => e.TableroId == tableroId)
            .OrderBy(e => e.Orden).ThenBy(e => e.Nombre)
            .Select(e => new EstadoTareaDto(e.Id, e.Nombre, e.Color, e.Orden, e.EsTerminal, e.EsBase, e.Activo))
            .ToListAsync(ct);
        var tareas = await ListarTareasAsync(null, null, null, null, null, null, ct, tableroId, verCerradas);
        return new TableroBoardDto(dto, estados, tareas);
    }

    public async Task<bool> ActualizarProgresoAsync(Guid tareaId, int progreso, CancellationToken ct)
    {
        var t = await _db.Tareas.FirstOrDefaultAsync(x => x.Id == tareaId, ct);
        if (t is null) return false;
        // El progreso de una tarea PADRE se deriva de sus hijas; solo las hojas se editan directo.
        var tieneHijos = await _db.Tareas.AnyAsync(x => x.PadreId == tareaId && !x.Eliminada, ct);
        if (!tieneHijos)
        {
            t.Progreso = Math.Clamp(progreso, 0, 100);
            t.UpdatedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(ct);
        }
        await RecomputarProgresoAncestrosAsync(t.PadreId, ct);
        return true;
    }

    /// <summary>Recalcula el progreso de los ancestros (padre, abuelo...) como promedio del progreso
    /// efectivo de sus hijas directas. Una hija en estado Completada cuenta como 100%.</summary>
    private async Task RecomputarProgresoAncestrosAsync(Guid? padreId, CancellationToken ct)
    {
        var changed = false;
        while (padreId is { } pid)
        {
            var padre = await _db.Tareas.FirstOrDefaultAsync(x => x.Id == pid && !x.Eliminada, ct);
            if (padre is null) break;
            var hijos = await _db.Tareas.Where(x => x.PadreId == pid && !x.Eliminada)
                .Select(x => new { x.Progreso, Nombre = x.Estado!.Nombre }).ToListAsync(ct);
            if (hijos.Count > 0)
            {
                var prom = (int)Math.Round(hijos.Average(h => h.Nombre == EstadoTareaBase.Completada ? 100.0 : h.Progreso));
                if (padre.Progreso != prom) { padre.Progreso = prom; padre.UpdatedAt = DateTimeOffset.UtcNow; changed = true; }
            }
            padreId = padre.PadreId;
        }
        if (changed) await _db.SaveChangesAsync(ct);
    }
}
