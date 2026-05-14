namespace Propia.Domain.Enums;

/// <summary>Tipo de propiedad horizontal segun el uso predominante.</summary>
public enum TipoCopropiedad
{
    Residencial = 1,
    Comercial = 2,
    Mixto = 3,
    Conjunto = 4
}

/// <summary>Estrato socioeconomico colombiano (Ley 142 de 1994).</summary>
public enum Estrato
{
    Uno = 1, Dos = 2, Tres = 3, Cuatro = 4, Cinco = 5, Seis = 6
}

/// <summary>Tipo de unidad privada dentro de una copropiedad.</summary>
public enum TipoUnidad
{
    Apartamento = 1,
    Local = 2,
    Casa = 3,
    Oficina = 4,
    Bodega = 5,
    Parqueadero = 6,
    UtilCuarto = 7
}

/// <summary>Categoria operativa de una zona comun (para reservas y mantenimiento).</summary>
public enum CategoriaZonaComun
{
    Social = 1,         // Salon social, BBQ
    Deportiva = 2,      // Cancha, gimnasio, piscina
    Servicios = 3,      // Lavanderia, deposito
    Circulacion = 4,    // Pasillos, escaleras
    Recreativa = 5,     // Parque infantil
    Otros = 6
}

/// <summary>Categoria de un equipo o activo fisico de la copropiedad.</summary>
public enum CategoriaEquipo
{
    Bombeo = 1,
    Electricidad = 2,
    Ascensores = 3,
    Seguridad = 4,
    Comunicaciones = 5,
    Climatizacion = 6,
    Recreacion = 7,
    Otros = 8
}

/// <summary>Tipo de servicio externo contratado por la copropiedad.</summary>
public enum TipoServicio
{
    Aseo = 1,
    Seguridad = 2,
    Mantenimiento = 3,
    Jardineria = 4,
    PiscinaMantenimiento = 5,
    Ascensores = 6,
    Plagas = 7,
    SeguroPH = 8,
    InternetWifi = 9,
    Energia = 10,
    Agua = 11,
    Gas = 12,
    Otros = 13
}

/// <summary>Cargo del miembro del Consejo de Administracion.</summary>
public enum CargoConsejo
{
    Presidente = 1,
    Vicepresidente = 2,
    Secretario = 3,
    Tesorero = 4,
    Vocal = 5,
    Suplente = 6
}
