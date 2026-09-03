using Microsoft.EntityFrameworkCore;
using Propia.Application.Common;
using Propia.Application.MiCopropiedad;
using Propia.Domain.Entities;
using Propia.Domain.Enums;
using Propia.Infrastructure.Persistence;

namespace Propia.Infrastructure.MiCopropiedad;

// Particion de MiCopropiedadService extraida por area tematica (mismo comportamiento,
// clase parcial: comparte _db/_tenant/_blob/_seed/_dir del constructor principal).
public partial class MiCopropiedadService
{
    // Rebalanceo de coeficientes (Ley 675 art. 26), cuota consolidada y tipos de coeficiente PH (B2).
    // ----------------------------- Rebalanceo de coeficientes (Ley 675 art. 26: suma = 100) -----------------------------

    public async Task<RebalanceoCoeficientesDto> RebalancearCoeficientesAsync(CancellationToken ct)
    {
        var unidades = await _db.UnidadesPrivadas.ToListAsync(ct);
        var sumaAnterior = unidades.Sum(u => u.CoeficientePropiedad);

        // Estrategia: solo unidades con PagaAdministracion=true reciben coeficiente > 0.
        // Las que no pagan (parqueadero, deposito) van a 0. Distribucion equitativa con
        // compensacion en la ultima para que el total sea exactamente 100.0000.
        var pagan = unidades.Where(u => u.PagaAdministracion).OrderBy(u => u.Numero).ToList();
        var noPagan = unidades.Where(u => !u.PagaAdministracion).ToList();

        if (pagan.Count == 0)
        {
            // Nada que rebalancear, dejar todas en 0
            foreach (var u in unidades) u.CoeficientePropiedad = 0m;
            await _db.SaveChangesAsync(ct);
            return new RebalanceoCoeficientesDto(unidades.Count, sumaAnterior, 0m, 0m, 0, noPagan.Count);
        }

        var coefBase = Math.Round(100m / pagan.Count, 4, MidpointRounding.ToZero);
        decimal acumulado = 0m;
        for (int i = 0; i < pagan.Count; i++)
        {
            if (i == pagan.Count - 1)
            {
                // Compensacion en la ultima para evitar floating drift y forzar suma=100
                pagan[i].CoeficientePropiedad = Math.Round(100m - acumulado, 4);
            }
            else
            {
                pagan[i].CoeficientePropiedad = coefBase;
                acumulado += coefBase;
            }
        }
        foreach (var u in noPagan) u.CoeficientePropiedad = 0m;

        await _db.SaveChangesAsync(ct);
        var sumaNueva = unidades.Sum(u => u.CoeficientePropiedad);
        await RegistrarBitacoraAsync("Distribucion",
            $"Coeficientes rebalanceados: {pagan.Count} apartamentos a {coefBase}% (ultima ajustada), {noPagan.Count} en 0%. Suma anterior {sumaAnterior:0.##}% -> nueva {sumaNueva:0.##}%.", ct);
        return new RebalanceoCoeficientesDto(unidades.Count, sumaAnterior, sumaNueva, coefBase, pagan.Count, noPagan.Count);
    }

    // ----------------------------- Cuota consolidada (principal + asociadas con factura) -----------------------------

    public async Task<CuotaConsolidadaDto?> GetCuotaConsolidadaAsync(Guid unidadId, CancellationToken ct)
    {
        var principal = await _db.UnidadesPrivadas.AsNoTracking().FirstOrDefaultAsync(u => u.Id == unidadId, ct);
        if (principal is null) return null;

        var asociadas = await _db.UnidadVinculos.AsNoTracking()
            .Include(v => v.UnidadAsociada)
            .Where(v => v.UnidadPrincipalId == unidadId)
            .ToListAsync(ct);

        var coefAsociadasFactura = asociadas
            .Where(v => v.IncluyeEnFacturacion)
            .Sum(v => v.UnidadAsociada!.CoeficientePropiedad);

        return new CuotaConsolidadaDto(
            principal.Id, principal.Numero,
            principal.CoeficientePropiedad,
            coefAsociadasFactura,
            principal.CoeficientePropiedad + coefAsociadasFactura,
            asociadas.Count,
            asociadas.Count(v => v.IncluyeEnFacturacion));
    }

    public async Task<UnidadDto?> ObtenerUnidadAsync(Guid unidadId, CancellationToken ct)
    {
        return await _db.UnidadesPrivadas
            .AsNoTracking()
            .Include(u => u.Torre)
            .Where(u => u.Id == unidadId)
            .Select(u => new UnidadDto(
                u.Id, u.Numero, u.Tipo,
                u.TorreId, u.Torre != null ? u.Torre.Nombre : null, u.Piso,
                u.CoeficientePropiedad, u.AreaM2,
                u.Habitaciones, u.Banos, u.Parqueaderos,
                u.Estado, u.Observaciones, u.MatriculaInmobiliaria, u.PagaAdministracion, u.CuotaMensual,
                // Mismo propietario que en el listado, para que el DTO diga lo mismo venga de donde venga.
                // Nombre del primer propietario. Contempla dueno persona O empresa (juridico):
                // el join simple contra Personas dejaria fuera los apartamentos de una empresa.
                (from up in _db.UnidadPersonas
                 where up.UnidadId == u.Id && up.Rol == RolUnidadPersona.Propietario
                 orderby up.EntidadTipo, up.Id
                 select up.EntidadTipo == EntidadDirectorio.Empresa
                     ? _db.Empresas.Where(e => e.Id == up.EmpresaId).Select(e => e.RazonSocial).FirstOrDefault()
                     : _db.Personas.Where(p => p.Id == up.PersonaId).Select(p => (p.Nombres + " " + p.Apellidos).Trim()).FirstOrDefault()
                ).FirstOrDefault(),
                _db.UnidadPersonas.Count(up => up.UnidadId == u.Id && up.Rol == RolUnidadPersona.Propietario),
                (from v in _db.UnidadVinculos where v.UnidadAsociadaId == u.Id select (Guid?)v.UnidadPrincipalId).FirstOrDefault(),
                u.ReferenciaPago))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<TipoUnidadCustomDto>> ListTiposUnidadCustomAsync(CancellationToken ct)
    {
        return await _db.TiposUnidadCustom
            .AsNoTracking()
            .OrderBy(t => t.Nombre)
            .Select(t => new TipoUnidadCustomDto(t.Id, t.Nombre, t.PagaAdministracionPorDefecto, t.Descripcion, t.Activo))
            .ToListAsync(ct);
    }

    public async Task<TipoUnidadCustomDto> CrearTipoUnidadCustomAsync(CrearTipoUnidadCustomRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Nombre))
            throw new InvalidOperationException("Nombre del tipo es obligatorio.");
        var nombre = req.Nombre.Trim();
        if (await _db.TiposUnidadCustom.AnyAsync(t => t.Nombre == nombre, ct))
            throw new InvalidOperationException($"Ya existe un tipo de unidad llamado '{nombre}' en esta copropiedad.");

        var t = new TipoUnidadCustom
        {
            Nombre = nombre,
            PagaAdministracionPorDefecto = req.PagaAdministracionPorDefecto,
            Descripcion = req.Descripcion,
            Activo = true
        };
        _db.TiposUnidadCustom.Add(t);
        await _db.SaveChangesAsync(ct);
        return new TipoUnidadCustomDto(t.Id, t.Nombre, t.PagaAdministracionPorDefecto, t.Descripcion, t.Activo);
    }

    public async Task<bool> EliminarTipoUnidadCustomAsync(Guid tipoId, CancellationToken ct)
    {
        var t = await _db.TiposUnidadCustom.FirstOrDefaultAsync(x => x.Id == tipoId, ct);
        if (t is null) return false;
        _db.TiposUnidadCustom.Remove(t);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    // ----------------------------- Tipos de coeficiente (B2) -----------------------------

    public async Task<IReadOnlyList<TipoCoeficienteDto>> ListTiposCoeficienteAsync(CancellationToken ct)
    {
        // Si no hay ninguno, sembramos el default "Propiedad" (lazy init).
        var existen = await _db.TiposCoeficiente.AnyAsync(ct);
        if (!existen)
        {
            _db.TiposCoeficiente.Add(new TipoCoeficiente
            {
                Nombre = "Propiedad",
                Descripcion = "Coeficiente principal de la PH segun Ley 675",
                EsPrincipal = true,
                Activo = true
            });
            await _db.SaveChangesAsync(ct);
        }

        var tipos = await _db.TiposCoeficiente
            .AsNoTracking()
            .OrderByDescending(t => t.EsPrincipal).ThenBy(t => t.Nombre)
            .ToListAsync(ct);

        // Calcular suma actual por tipo (incluyendo el campo legacy CoeficientePropiedad para el tipo principal).
        var sumas = await _db.UnidadCoeficientes
            .AsNoTracking()
            .GroupBy(uc => uc.TipoCoeficienteId)
            .Select(g => new { TipoId = g.Key, Suma = g.Sum(x => x.Valor) })
            .ToDictionaryAsync(x => x.TipoId, x => x.Suma, ct);

        var sumaLegacyPropiedad = await _db.UnidadesPrivadas.SumAsync(u => (decimal?)u.CoeficientePropiedad, ct) ?? 0m;

        return tipos.Select(t =>
        {
            var suma = sumas.GetValueOrDefault(t.Id, 0m);
            if (t.EsPrincipal) suma += sumaLegacyPropiedad - (sumas.GetValueOrDefault(t.Id, 0m) > 0 ? 0m : 0m);
            // Para principal: si hay valores en UnidadCoeficientes los usamos; si no, caemos al legacy.
            // Simplificacion: para el tipo principal sumamos LO QUE HAYA en UnidadCoeficientes;
            // si no hay registros (suma == 0), exponemos el legacy.
            var sumaReal = t.EsPrincipal && sumas.GetValueOrDefault(t.Id, 0m) == 0m
                ? sumaLegacyPropiedad
                : sumas.GetValueOrDefault(t.Id, 0m);
            return new TipoCoeficienteDto(t.Id, t.Nombre, t.Descripcion, t.EsPrincipal, t.Activo, sumaReal);
        }).ToList();
    }

    public async Task<TipoCoeficienteDto> CrearTipoCoeficienteAsync(CrearTipoCoeficienteRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Nombre))
            throw new InvalidOperationException("Nombre del tipo de coeficiente es obligatorio.");
        var nombre = req.Nombre.Trim();
        if (await _db.TiposCoeficiente.AnyAsync(x => x.Nombre == nombre, ct))
            throw new InvalidOperationException($"Ya existe un tipo de coeficiente '{nombre}'.");

        var t = new TipoCoeficiente
        {
            Nombre = nombre,
            Descripcion = req.Descripcion,
            EsPrincipal = false,
            Activo = true
        };
        _db.TiposCoeficiente.Add(t);
        await _db.SaveChangesAsync(ct);
        return new TipoCoeficienteDto(t.Id, t.Nombre, t.Descripcion, t.EsPrincipal, t.Activo, 0m);
    }

    public async Task<bool> EliminarTipoCoeficienteAsync(Guid tipoId, CancellationToken ct)
    {
        var t = await _db.TiposCoeficiente.FirstOrDefaultAsync(x => x.Id == tipoId, ct);
        if (t is null) return false;
        if (t.EsPrincipal)
            throw new InvalidOperationException("No se puede eliminar el tipo principal de coeficiente.");

        // Cascade eliminara UnidadCoeficientes asociados via FK
        _db.TiposCoeficiente.Remove(t);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<IReadOnlyList<UnidadCoeficienteDto>> ListCoeficientesUnidadAsync(Guid unidadId, CancellationToken ct)
    {
        return await _db.UnidadCoeficientes
            .AsNoTracking()
            .Include(uc => uc.TipoCoeficiente)
            .Where(uc => uc.UnidadId == unidadId)
            .Select(uc => new UnidadCoeficienteDto(uc.TipoCoeficienteId, uc.TipoCoeficiente!.Nombre, uc.Valor))
            .ToListAsync(ct);
    }

    public async Task<UnidadCoeficienteDto> SetCoeficienteUnidadAsync(Guid unidadId, SetCoeficienteUnidadRequest req, CancellationToken ct)
    {
        if (req.Valor < 0)
            throw new InvalidOperationException("El coeficiente debe ser >= 0.");

        var unidad = await _db.UnidadesPrivadas.FirstOrDefaultAsync(u => u.Id == unidadId, ct)
            ?? throw new InvalidOperationException("Unidad no encontrada.");
        var tipo = await _db.TiposCoeficiente.FirstOrDefaultAsync(t => t.Id == req.TipoCoeficienteId, ct)
            ?? throw new InvalidOperationException("Tipo de coeficiente no encontrado.");

        var existente = await _db.UnidadCoeficientes
            .FirstOrDefaultAsync(uc => uc.UnidadId == unidadId && uc.TipoCoeficienteId == req.TipoCoeficienteId, ct);

        if (existente is null)
        {
            existente = new UnidadCoeficiente
            {
                UnidadId = unidadId,
                TipoCoeficienteId = req.TipoCoeficienteId,
                Valor = req.Valor
            };
            _db.UnidadCoeficientes.Add(existente);
        }
        else
        {
            existente.Valor = req.Valor;
        }

        // Si es el tipo principal, mantenemos el campo legacy de la unidad sincronizado
        if (tipo.EsPrincipal)
            unidad.CoeficientePropiedad = req.Valor;

        await _db.SaveChangesAsync(ct);
        await RegistrarBitacoraAsync("Coeficiente",
            $"Unidad '{unidad.Numero}': coeficiente '{tipo.Nombre}' = {req.Valor:0.####}%.", ct);
        return new UnidadCoeficienteDto(req.TipoCoeficienteId, tipo.Nombre, req.Valor);
    }

    public async Task<GenerarUnidadesResponse> GenerarUnidadesAsync(GenerarUnidadesRequest req, CancellationToken ct)
    {
        if (req.Torres is null || req.Torres.Count == 0)
            throw new InvalidOperationException("Debes definir al menos una torre/agrupacion.");
        if (req.CoeficientePorUnidad < 0)
            throw new InvalidOperationException("Coeficiente por unidad no puede ser negativo.");

        var torresCreadas = new List<Torre>();
        var unidadesCreadas = new List<UnidadPrivada>();
        var torresExistentes = await _db.Torres.CountAsync(ct);
        var unidadesExistentesNumeros = await _db.UnidadesPrivadas.Select(u => u.Numero).ToListAsync(ct);
        var unidadesGeneradas = new HashSet<string>(unidadesExistentesNumeros, StringComparer.OrdinalIgnoreCase);

        // Si hay torres pre-existentes o se piden multiples torres nuevas, usamos
        // prefijo de indice de torre para garantizar unicidad global de identificadores.
        var usarPrefijoIdx = req.Torres.Count > 1 || torresExistentes > 0;
        var corridoSeq = unidadesExistentesNumeros.Count + 1;
        var torreIdx = torresExistentes;

        foreach (var spec in req.Torres)
        {
            torreIdx++;
            if (string.IsNullOrWhiteSpace(spec.Nombre))
                throw new InvalidOperationException("Cada torre debe tener nombre.");
            if (spec.CantidadPisos <= 0 || spec.UnidadesPorPiso <= 0)
                throw new InvalidOperationException($"Torre '{spec.Nombre}': pisos y unidades por piso deben ser > 0.");
            if (await _db.Torres.AnyAsync(t => t.Nombre == spec.Nombre, ct))
                throw new InvalidOperationException($"Ya existe una torre llamada '{spec.Nombre}' en esta copropiedad.");

            var torre = new Torre { Nombre = spec.Nombre.Trim(), CantidadPisos = spec.CantidadPisos };
            _db.Torres.Add(torre);
            torresCreadas.Add(torre);

            for (var piso = 1; piso <= spec.CantidadPisos; piso++)
            {
                for (var n = 1; n <= spec.UnidadesPorPiso; n++)
                {
                    // PisoNumero: si ya hay torres en la copropiedad o se piden multiples,
                    // prefijamos con el indice de torre (Torre 1 piso 1 #1 -> 1101) para
                    // garantizar unicidad global. Con 1 sola torre y tenant vacio: 101, 102 (spec).
                    string numero;
                    if (req.Patron == PatronNumeracion.Corrido)
                    {
                        numero = corridoSeq.ToString();
                        corridoSeq++;
                    }
                    else
                    {
                        numero = usarPrefijoIdx ? $"{torreIdx}{piso}{n:D2}" : $"{piso}{n:D2}";
                    }
                    if (!unidadesGeneradas.Add(numero))
                        throw new InvalidOperationException(
                            $"Colision de identificador '{numero}'. Otra unidad ya tiene ese numero. " +
                            "Renombra la torre o cambia el patron de numeracion.");
                    var unidad = new UnidadPrivada
                    {
                        Numero = numero,
                        Tipo = req.TipoUnidadDefault,
                        Torre = torre,
                        Piso = piso,
                        CoeficientePropiedad = req.CoeficientePorUnidad
                    };
                    _db.UnidadesPrivadas.Add(unidad);
                    unidadesCreadas.Add(unidad);
                }
            }
        }

        await _db.SaveChangesAsync(ct);
        var nombresTorre = string.Join(", ", torresCreadas.Select(t => t.Nombre));
        await RegistrarBitacoraAsync("Distribucion",
            $"Generador automatico: {torresCreadas.Count} torre(s) [{nombresTorre}] + {unidadesCreadas.Count} unidades creadas (coef {req.CoeficientePorUnidad}% c/u).", ct);
        return new GenerarUnidadesResponse(
            torresCreadas.Count,
            unidadesCreadas.Count,
            torresCreadas.Select(t => t.Id).ToList(),
            unidadesCreadas.Select(u => u.Id).ToList());
    }

    public async Task<ImportarUnidadesResponse> ImportarUnidadesCsvAsync(ImportarUnidadesRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.CsvContent))
            return new ImportarUnidadesResponse(false, 0, 0, 0m, new List<ImportacionFilaError>
            {
                new(0, "CSV", "El archivo viene vacio.")
            });

        // Formato esperado (header en fila 1):
        //   identificador,tipo_unidad,agrupacion,piso,coeficiente,area_m2,paga_administracion
        // Solo identificador, tipo_unidad y coeficiente son obligatorios.

        var lineas = req.CsvContent.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');
        var errores = new List<ImportacionFilaError>();
        var filas = new List<(int Linea, string Numero, TipoUnidad Tipo, string? Agrupacion, int? Piso, decimal Coef, decimal? Area)>();

        if (lineas.Length < 2)
        {
            errores.Add(new(0, "CSV", "Necesitas al menos un encabezado y una fila de datos."));
            return new ImportarUnidadesResponse(false, 0, 0, 0m, errores);
        }

        var header = lineas[0].Split(',').Select(c => c.Trim().ToLowerInvariant()).ToList();
        int idxId = header.IndexOf("identificador");
        int idxTipo = header.IndexOf("tipo_unidad");
        int idxAgr = header.IndexOf("agrupacion");
        int idxPiso = header.IndexOf("piso");
        int idxCoef = header.IndexOf("coeficiente");
        int idxArea = header.IndexOf("area_m2");

        if (idxId < 0 || idxTipo < 0 || idxCoef < 0)
        {
            errores.Add(new(1, "header", "Faltan columnas obligatorias: identificador, tipo_unidad, coeficiente."));
            return new ImportarUnidadesResponse(false, 0, 0, 0m, errores);
        }

        var numerosVistos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        decimal suma = 0m;

        for (int i = 1; i < lineas.Length; i++)
        {
            var linea = lineas[i];
            if (string.IsNullOrWhiteSpace(linea)) continue;
            var celdas = linea.Split(',');
            var nroFila = i + 1;

            string numero = celdas.Length > idxId ? celdas[idxId].Trim() : "";
            string tipoTxt = celdas.Length > idxTipo ? celdas[idxTipo].Trim() : "";
            string coefTxt = celdas.Length > idxCoef ? celdas[idxCoef].Trim() : "";

            if (string.IsNullOrEmpty(numero))
            {
                errores.Add(new(nroFila, "identificador", "Campo obligatorio."));
                continue;
            }
            if (!numerosVistos.Add(numero))
            {
                errores.Add(new(nroFila, "identificador", $"Identificador '{numero}' aparece duplicado en el archivo."));
            }
            if (!Enum.TryParse<TipoUnidad>(tipoTxt, ignoreCase: true, out var tipo))
            {
                errores.Add(new(nroFila, "tipo_unidad", $"Tipo '{tipoTxt}' no esta en el catalogo base."));
                continue;
            }
            if (!decimal.TryParse(coefTxt, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var coef) || coef < 0)
            {
                errores.Add(new(nroFila, "coeficiente", $"Formato invalido. Usa decimal positivo (ej. 0.023400)."));
                continue;
            }

            string? agrupacion = idxAgr >= 0 && celdas.Length > idxAgr ? celdas[idxAgr].Trim() : null;
            if (string.IsNullOrWhiteSpace(agrupacion)) agrupacion = null;

            int? piso = null;
            if (idxPiso >= 0 && celdas.Length > idxPiso && int.TryParse(celdas[idxPiso].Trim(), out var p)) piso = p;

            decimal? area = null;
            if (idxArea >= 0 && celdas.Length > idxArea && decimal.TryParse(celdas[idxArea].Trim(), System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var a)) area = a;

            suma += coef;
            filas.Add((nroFila, numero, tipo, agrupacion, piso, coef, area));
        }

        if (errores.Count > 0)
            return new ImportarUnidadesResponse(false, filas.Count, 0, suma, errores);

        // Identificadores duplicados contra BD
        var numerosArchivo = filas.Select(f => f.Numero).ToList();
        var duplicados = await _db.UnidadesPrivadas
            .Where(u => numerosArchivo.Contains(u.Numero))
            .Select(u => u.Numero)
            .ToListAsync(ct);
        foreach (var dup in duplicados)
            errores.Add(new(0, "identificador", $"El identificador '{dup}' ya existe en la copropiedad."));
        if (errores.Count > 0)
            return new ImportarUnidadesResponse(false, filas.Count, 0, suma, errores);

        // Crear torres faltantes
        var nombresTorres = filas.Where(f => f.Agrupacion is not null).Select(f => f.Agrupacion!).Distinct().ToList();
        var torresExistentes = await _db.Torres
            .Where(t => nombresTorres.Contains(t.Nombre))
            .ToDictionaryAsync(t => t.Nombre, t => t, ct);
        foreach (var nombreTorre in nombresTorres.Where(n => !torresExistentes.ContainsKey(n)))
        {
            var nuevaTorre = new Torre { Nombre = nombreTorre };
            _db.Torres.Add(nuevaTorre);
            torresExistentes[nombreTorre] = nuevaTorre;
        }

        foreach (var f in filas)
        {
            var torreId = f.Agrupacion is not null ? torresExistentes[f.Agrupacion].Id : (Guid?)null;
            var torre = f.Agrupacion is not null ? torresExistentes[f.Agrupacion] : null;
            _db.UnidadesPrivadas.Add(new UnidadPrivada
            {
                Numero = f.Numero,
                Tipo = f.Tipo,
                Piso = f.Piso,
                CoeficientePropiedad = f.Coef,
                AreaM2 = f.Area,
                Torre = torre  // EF asigna torreId al guardar la torre nueva
            });
        }
        await _db.SaveChangesAsync(ct);
        return new ImportarUnidadesResponse(true, filas.Count, filas.Count, suma, Array.Empty<ImportacionFilaError>());
    }

    public async Task<bool> EliminarUnidadAsync(Guid unidadId, CancellationToken ct)
    {
        var u = await _db.UnidadesPrivadas.FirstOrDefaultAsync(x => x.Id == unidadId, ct);
        if (u is null) return false;
        _db.UnidadesPrivadas.Remove(u);
        try { await _db.SaveChangesAsync(ct); }
        catch (DbUpdateException)
        {
            throw new InvalidOperationException(
                "No se puede eliminar: la unidad tiene registros asociados (cartera, PQRSD, reservas, etc.). Desvinculalos primero.");
        }
        return true;
    }

}
