namespace Propia.Domain.Enums;

/// <summary>Estado del ciclo de vida del presupuesto (spec 2.6 v1.0).</summary>
public enum EstadoPresupuesto
{
    Borrador = 1,
    EnAprobacion = 2,
    Aprobado = 3,
    EnEjecucion = 4,
    Cerrado = 5
}

/// <summary>Base de liquidacion por rubro (spec 2.6 - RN-14: se configura por rubro).</summary>
public enum BaseLiquidacion
{
    /// <summary>Proporcional al coeficiente de la unidad.</summary>
    Coeficiente = 1,
    /// <summary>Mismo valor para todas las unidades del mismo tipo.</summary>
    CuotaFijaPorTipo = 2,
    /// <summary>Combinacion segun configuracion de cada rubro.</summary>
    Mixto = 3
}

/// <summary>Estado de una liquidacion (snapshot inmutable).</summary>
public enum EstadoLiquidacion
{
    Emitida = 1,
    Reliquidada = 2,
    Anulada = 3
}

/// <summary>Estado de pago de la liquidacion por unidad.</summary>
public enum EstadoPagoLiquidacion
{
    Pendiente = 1,
    Pagado = 2,
    Vencido = 3,
    Exonerado = 4
}

/// <summary>Tipo de pago.</summary>
public enum TipoPago
{
    CuotaOrdinaria = 1,
    CuotaExtraordinaria = 2,
    Abono = 3
}

/// <summary>Canal por el que se recibio el pago.</summary>
public enum CanalPago
{
    WompiPse = 1,
    WompiTarjeta = 2,
    WompiNequi = 3,
    WompiEfectivo = 4,
    ManualConsignacion = 10,
    ManualEfectivo = 11,
    ManualCheque = 12,
    Otro = 99
}

/// <summary>Estado del pago.</summary>
public enum EstadoPago
{
    Pendiente = 1,
    Confirmado = 2,
    Fallido = 3,
    Revertido = 4
}

/// <summary>Estado de una cuota extraordinaria.</summary>
public enum EstadoCuotaExtraordinaria
{
    PendienteAprobacion = 1,
    Aprobada = 2,
    EnRecaudo = 3,
    Cerrada = 4
}

/// <summary>Forma de recaudo de una cuota extraordinaria.</summary>
public enum FormaRecaudo
{
    Unica = 1,
    Mensual = 2,
    Cuotas = 3
}

/// <summary>Tipo de aprobacion del presupuesto o cuota.</summary>
public enum TipoAprobacion
{
    Asamblea = 1,
    Manual = 2
}

/// <summary>Tipo de movimiento de gasto en la ejecucion presupuestal (2.6 tab Ejecucion).</summary>
public enum TipoGasto
{
    /// <summary>Compromiso: gasto reservado/contratado pero aun no pagado.</summary>
    Comprometido = 1,
    /// <summary>Ejecutado: gasto efectivamente pagado.</summary>
    Ejecutado = 2
}

/// <summary>Codigos estandar de rubros del catalogo base (spec 2.6 - tabla 5.2).</summary>
public static class RubroCatalogo
{
    public const string AdministracionGeneral = "ADMIN_GENERAL";
    public const string PersonalNomina = "PERSONAL_NOMINA";
    public const string SeguridadVigilancia = "SEGURIDAD";
    public const string MantenimientoZonasComunes = "MANTENIMIENTO";
    public const string ServiciosPublicosComunes = "SERVICIOS_PUBLICOS";
    public const string Seguros = "SEGUROS";
    public const string AseoJardineria = "ASEO_JARDIN";
    public const string GastosAdministrativos = "GASTOS_ADMIN";
    public const string FondoImprevistos = "FONDO_IMPREVISTOS";  // Obligatorio - no eliminable
    public const string OtrosGastos = "OTROS";

    public static readonly (string Codigo, string Nombre, bool Obligatorio)[] Base = new[]
    {
        (AdministracionGeneral,      "Administracion general",       false),
        (PersonalNomina,             "Personal y nomina",            false),
        (SeguridadVigilancia,        "Seguridad y vigilancia",       false),
        (MantenimientoZonasComunes,  "Mantenimiento de zonas comunes", false),
        (ServiciosPublicosComunes,   "Servicios publicos comunes",   false),
        (Seguros,                    "Seguros",                       false),
        (AseoJardineria,             "Aseo y jardineria",            false),
        (GastosAdministrativos,      "Gastos administrativos",        false),
        (FondoImprevistos,           "Fondo de imprevistos",          true),  // RN-02
        (OtrosGastos,                "Otros gastos",                  false),
    };
}
