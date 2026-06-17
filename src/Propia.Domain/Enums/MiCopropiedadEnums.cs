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

/// <summary>Estado de un contrato de servicio (spec 2.3 seccion 5). Vencido se auto-deriva por fecha.</summary>
public enum EstadoContrato
{
    Vigente = 1,
    EnRenovacion = 2,
    Vencido = 3
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

/// <summary>Tipo de cuenta bancaria de la copropiedad (RN-finanzas: cuentas para recaudo y factura).</summary>
public enum TipoCuentaBancaria
{
    Ahorros = 1,
    Corriente = 2,
    EncargoFiduciario = 3
}

/// <summary>Formas de pago habilitadas para residentes (flags - se pueden combinar).</summary>
[System.Flags]
public enum TiposPagoPermitidos
{
    Ninguno = 0,
    Cheque = 1 << 0,
    Consignacion = 1 << 1,
    Efectivo = 1 << 2,
    NotaCredito = 1 << 3,
    Pse = 1 << 4,
    RecaudoEnLinea = 1 << 5,
    TarjetaCredito = 1 << 6,
    Transferencia = 1 << 7,
    PseWenjoy = 1 << 8
}

/// <summary>Multiplo al que se redondean los valores (RN: 1=sin redondeo, 100, 1000).</summary>
public enum MultiploRedondeo
{
    NoRedondear = 1,
    Cien = 100,
    Quinientos = 500,
    Mil = 1000
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

/// <summary>Tipo de vinculacion del equipo de trabajo (spec 2.3 - seccion 3).</summary>
public enum TipoVinculacion
{
    /// <summary>Empleado directo de la copropiedad o la organizacion administradora.</summary>
    Interno = 1,
    /// <summary>Proveedor o contratista de servicios.</summary>
    Externo = 2
}

/// <summary>Rol de una persona vinculada a una unidad. Spec 2.3 tabla unidad_persona.</summary>
public enum RolUnidadPersona
{
    Propietario = 1,
    Residente = 2,
    Familiar = 3,
    Arrendatario = 4,
    Apoderado = 5
}

/// <summary>Rol operativo del equipo de trabajo (catalogo base).</summary>
public enum RolEquipo
{
    AdministradorDelegado = 1,
    Contador = 2,
    AuxiliarCartera = 3,
    CoordinadorOperativo = 4,
    SupervisorVigilancia = 5,
    PersonalAseo = 6,
    Mantenimiento = 7,
    Recepcion = 8,
    Otro = 99
}
