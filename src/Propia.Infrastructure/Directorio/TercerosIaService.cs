using System.Globalization;
using Propia.Application.Common;
using Propia.Application.Directorio;
using Propia.Application.Documentos;
using Propia.Domain.Enums;
using Propia.Infrastructure.Persistence;

namespace Propia.Infrastructure.Directorio;

/// <summary>
/// "Crear terceros con IA" (Directorio > Terceros): con lo que leyo la IA de un RUT o una Camara de Comercio crea el
/// tercero o actualiza el que ya existe (empresa por NIT, persona por tipo + numero de documento). Al actualizar solo
/// COMPLETA lo que falta: nunca pisa datos que alguien ya edito. Vincula el tercero a la copropiedad, vincula al
/// representante legal (creandolo si no existe) y guarda el archivo en sus Documentos con el tipo en el nombre.
/// Reusa IDirectorioService para conservar validaciones, vinculos y RLS.
/// </summary>
public sealed class TercerosIaService : ITercerosIaService
{
    private readonly IDirectorioService _dir;
    private readonly PropiaDbContext _db;
    private readonly ITenantContext _tenant;

    public TercerosIaService(IDirectorioService dir, PropiaDbContext db, ITenantContext tenant)
    {
        _dir = dir; _db = db; _tenant = tenant;
    }

    public async Task<TerceroDesdeDocumentoResultado> CrearDesdeDocumentoAsync(TerceroLeidoRequest d, string nombreArchivo, string? contentType,
        long tamanoBytes, Stream contenido, CancellationToken ct)
    {
        var etiqueta = EtiquetaDocumento(d.TipoDocumentoFuente);
        var esPersona = EsPersonaNatural(d);
        EntidadDirectorio tipo; Guid id; string nombre; bool creado; string? representante = null;

        if (!esPersona)
        {
            var nit = NormalizadorDocumental.Nit(d.Nit);
            if (string.IsNullOrWhiteSpace(nit)) throw new InvalidOperationException("La IA no encontró el NIT del tercero en el documento.");
            var (nitSinDv, dv) = PartirNit(nit);
            // El digito de verificacion protege de un NIT mal leido: si no cuadra, no se crea (se avisa para revisar).
            if (dv is not null && dv != _dir.CalcularDigitoVerificacionNit(nitSinDv))
                throw new InvalidOperationException($"El NIT leído ({nitSinDv}-{dv}) no cuadra con su dígito de verificación: revisa el documento o crea el tercero a mano.");
            var existe = await _dir.BuscarEmpresaPorNitAsync(nitSinDv, ct);
            Guid? repId = null;
            if (TieneRepresentante(d))
            {
                var rep = await PersonaAsync(Tipo(d.RepresentanteTipoIdentificacion) ?? TipoDocumento.CC, Digitos(d.RepresentanteIdentificacion)!,
                    Titulo(d.RepresentanteNombres)!, Titulo(d.RepresentanteApellidos) ?? "", null, null, ct);
                repId = rep.Id; representante = rep.Nombre;
            }
            var direccion = Direccion(d);
            if (existe is null)
            {
                var razon = Limpio(d.RazonSocial) ?? throw new InvalidOperationException("La IA no encontró la razón social del tercero.");
                var e = await _dir.CrearEmpresaAsync(new CrearEmpresaRequest(nitSinDv, dv, razon, Limpio(d.NombreComercial),
                    Limpio(d.Email), Limpio(d.Telefono), direccion, null, Limpio(d.ActividadEconomica), Limpio(d.RegimenTributario), null), ct);
                id = e.Id; nombre = e.RazonSocial; creado = true;
                if (repId is { } rid)
                    await _dir.ActualizarEmpresaAsync(id, new ActualizarEmpresaRequest(e.RazonSocial, e.NombreComercial, e.Email, e.Telefono,
                        e.Direccion, e.TipoEmpresa, e.SectorEconomico, e.RegimenTributario, e.SitioWeb, e.LogoUrl, rid), ct);
            }
            else
            {
                await VinculoDirectorio.AsegurarEmpresaAsync(_db, _tenant, existe.Id, ct);
                // Completa solo lo vacio (lo que alguien ya edito se respeta).
                await _dir.ActualizarEmpresaAsync(existe.Id, new ActualizarEmpresaRequest(
                    existe.RazonSocial, existe.NombreComercial ?? Limpio(d.NombreComercial),
                    existe.Email ?? Limpio(d.Email), existe.Telefono ?? Limpio(d.Telefono), existe.Direccion ?? direccion,
                    existe.TipoEmpresa, existe.SectorEconomico ?? Limpio(d.ActividadEconomica), existe.RegimenTributario ?? Limpio(d.RegimenTributario),
                    existe.SitioWeb, existe.LogoUrl, existe.RepresentanteLegalPersonaId ?? repId), ct);
                id = existe.Id; nombre = existe.RazonSocial; creado = false;
                if (existe.RepresentanteLegalPersonaId is not null) representante = existe.RepresentanteLegalNombre;
            }
            tipo = EntidadDirectorio.Empresa;
        }
        else
        {
            var numero = Digitos(d.NumeroIdentificacion) ?? (Tipo(d.TipoIdentificacion) == TipoDocumento.NIT ? null : Digitos(d.Nit))
                ?? throw new InvalidOperationException("La IA no encontró el número de documento del tercero.");
            var nombres = Titulo(d.Nombres) ?? throw new InvalidOperationException("La IA no encontró el nombre del tercero.");
            var p = await PersonaAsync(Tipo(d.TipoIdentificacion) is TipoDocumento.NIT or null ? TipoDocumento.CC : Tipo(d.TipoIdentificacion)!.Value,
                numero, nombres, Titulo(d.Apellidos) ?? "", Limpio(d.Email), Limpio(d.Telefono), ct);
            tipo = EntidadDirectorio.Persona; id = p.Id; nombre = p.Nombre; creado = p.Creado;
        }

        var nombreDoc = $"{etiqueta} - {nombreArchivo}";
        if (nombreDoc.Length > 250) nombreDoc = nombreDoc[..250];
        await _dir.AgregarAdjuntoAsync(tipo, id, nombreDoc, contentType, tamanoBytes, contenido, ct);
        return new TerceroDesdeDocumentoResultado(tipo, id, nombre, creado, nombreDoc, representante);
    }

    // Busca la persona por documento (la vincula a la copropiedad) o la crea; completa correo/telefono si le faltan.
    private async Task<(Guid Id, string Nombre, bool Creado)> PersonaAsync(TipoDocumento tipo, string numero, string nombres, string apellidos,
        string? email, string? telefono, CancellationToken ct)
    {
        var existe = await _dir.BuscarPersonaPorDocumentoAsync(new BuscarPorDocumentoRequest(tipo, numero), ct);
        if (existe is not null)
        {
            await VinculoDirectorio.AsegurarPersonaAsync(_db, _tenant, existe.Id, ct);
            var nuevoEmail = string.IsNullOrWhiteSpace(existe.Email) ? email : existe.Email;
            var nuevoTel = string.IsNullOrWhiteSpace(existe.Telefono) ? telefono : existe.Telefono;
            if (nuevoEmail != existe.Email || nuevoTel != existe.Telefono)
                await _dir.ActualizarPersonaAsync(existe.Id, new ActualizarPersonaRequest(existe.Nombres, existe.Apellidos, nuevoEmail, nuevoTel,
                    existe.FotoUrl, existe.FechaNacimiento, existe.Genero), ct);
            return (existe.Id, $"{existe.Nombres} {existe.Apellidos}".Trim(), false);
        }
        if (apellidos.Length == 0)
        {
            // Nombre completo en una sola cadena: nombres = primera mitad, apellidos = el resto.
            var partes = nombres.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (partes.Length >= 2) { var k = partes.Length >= 4 ? 2 : 1; nombres = string.Join(' ', partes[..k]); apellidos = string.Join(' ', partes[k..]); }
            else apellidos = "-";
        }
        var p = await _dir.CrearPersonaAsync(new CrearPersonaRequest(tipo, numero, nombres, apellidos, email, telefono, null, null), ct);
        return (p.Id, $"{p.Nombres} {p.Apellidos}".Trim(), true);
    }

    private static bool EsPersonaNatural(TerceroLeidoRequest d)
    {
        var tp = (d.TipoPersona ?? "").Trim().ToLowerInvariant();
        if (tp.StartsWith("jur")) return false;
        if (tp.StartsWith("nat")) return true;
        // Sin tipo: empresa si trae razon social o NIT sin nombres de persona.
        if (!string.IsNullOrWhiteSpace(d.RazonSocial)) return false;
        return !string.IsNullOrWhiteSpace(d.Nombres);
    }

    private static bool TieneRepresentante(TerceroLeidoRequest d)
        => Digitos(d.RepresentanteIdentificacion) is not null && !string.IsNullOrWhiteSpace(d.RepresentanteNombres);

    internal static string EtiquetaDocumento(string? fuente) => (fuente ?? "").Trim().ToLowerInvariant() switch
    {
        var f when f.Contains("rut") => "RUT",
        var f when f.Contains("camara") || f.Contains("cámara") => "Cámara de Comercio",
        var f when f.Contains("banc") => "Certificación bancaria",
        _ => "Documento"
    };

    private static (string NitSinDv, string? Dv) PartirNit(string nit)
    {
        var i = nit.IndexOf('-');
        return i > 0 ? (nit[..i], nit[(i + 1)..]) : (nit, null);
    }

    private static TipoDocumento? Tipo(string? s)
    {
        var t = (s ?? "").Trim().ToUpperInvariant().Replace(".", "");
        if (t.Length == 0) return null;
        if (t.StartsWith("CC") || t.Contains("CIUDADAN")) return TipoDocumento.CC;
        if (t.StartsWith("CE") || t.Contains("EXTRANJER")) return TipoDocumento.CE;
        if (t.StartsWith("PA") || t.Contains("PASAPORTE")) return TipoDocumento.PA;
        if (t.StartsWith("TI") || t.Contains("IDENTIDAD")) return TipoDocumento.TI;
        if (t.StartsWith("NIT")) return TipoDocumento.NIT;
        return null;
    }

    private static string? Digitos(string? s)
    {
        var d = new string((s ?? "").Where(char.IsAsciiDigit).ToArray());
        return d.Length == 0 ? null : d;
    }

    private static string? Limpio(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    // "JUAN CARLOS" -> "Juan Carlos" (los certificados vienen en mayusculas).
    private static string? Titulo(string? s)
    {
        var v = Limpio(s);
        if (v is null) return null;
        return v.Any(char.IsLower) ? v : CultureInfo.GetCultureInfo("es-CO").TextInfo.ToTitleCase(v.ToLowerInvariant());
    }

    private static string? Direccion(TerceroLeidoRequest d)
    {
        var dir = Limpio(d.Direccion); var ciudad = Titulo(d.Ciudad);
        if (dir is null) return ciudad;
        return ciudad is null || dir.Contains(ciudad, StringComparison.OrdinalIgnoreCase) ? dir : $"{dir}, {ciudad}";
    }
}
