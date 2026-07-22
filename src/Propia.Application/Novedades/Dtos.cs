using Propia.Domain.Enums;

namespace Propia.Application.Novedades;

public record NovedadComentarioDto(Guid Id, string AutorNombre, string AutorIniciales, string Texto, string FechaTexto);

public record NovedadDto(
    Guid Id, string Titulo, string? Texto, string? ImagenUrl,
    string AutorNombre, string AutorIniciales, string FechaTexto,
    int Likes, bool YoDiLike,
    IReadOnlyList<NovedadComentarioDto> Comentarios);

public record PublicarNovedadRequest(string Titulo, string? Texto, string? ImagenUrl);

public record ComentarNovedadRequest(string Texto);
