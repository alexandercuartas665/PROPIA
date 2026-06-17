using Propia.Domain.Common;
using Propia.Domain.Enums;

namespace Propia.Domain.Entities;

/// <summary>
/// Tenant == Copropiedad (Propiedad Horizontal). Es la entidad raiz de Capa 2.
/// Toda informacion operativa pertenece a un Tenant - el Tenant es duenio de la data.
/// El campo TenantId de todas las TenantEntity apunta al Id de un Tenant.
/// Spec: modulo 2.3 Mi Copropiedad + modulo 0.1 Super Admin Console (estados y custodia).
/// </summary>
public class Tenant : BaseEntity
{
    public string Nombre { get; set; } = string.Empty;
    public string? Nit { get; set; }
    public string? DigitoVerificacion { get; set; }
    public string? Direccion { get; set; }
    public string? Ciudad { get; set; }
    public string? Departamento { get; set; }
    public string? CodigoPropia { get; set; }  // Codigo legible asignado por la plataforma

    // Datos para el modulo 2.3 Mi Copropiedad - seccion Identidad
    public TipoCopropiedad? TipoCopropiedad { get; set; }
    public Estrato? Estrato { get; set; }
    public string? FotoFachadaUrl { get; set; }  // se usa como imagen de portada (hero)
    public string? LogoUrl { get; set; }
    public string? Descripcion { get; set; }

    // Contacto de la copropiedad (modulo 2.3 - seccion 1 Identidad)
    public string? TelefonoContacto { get; set; }
    public string? EmailContacto { get; set; }

    // Identidad registral (modulo 2.3 - todos opcionales)
    public string? NumeroReglamentoPh { get; set; }
    public string? NotariaRegistro { get; set; }
    public string? MatriculaInmobiliaria { get; set; }
    public string? LicenciaConstruccion { get; set; }
    public DateOnly? FechaConstitucion { get; set; }

    // Labels personalizables para estructura fisica (Sector vs Torre, Planta vs Piso, etc.)
    public string? LabelAgrupacion { get; set; }  // Default "Torre"
    public string? LabelPiso { get; set; }        // Default "Piso"

    // Parametros financieros (modulo 2.3 - seccion 8 Finanzas). Consumidos por 2.6/2.7.
    public string Moneda { get; set; } = "COP";          // Codigo ISO 4217 (RN: nunca simbolo/texto libre)
    public int DiaCorte { get; set; } = 1;               // RN-17: entre 1 y 28
    public bool TasaMoraEsLegal { get; set; } = true;    // true = tasa legal vigente automatica
    public decimal? TasaMoraValor { get; set; }          // % mensual cuando es fija (RN-18: <= maximo legal)
    public int PeriodoGraciaDias { get; set; }           // 0..30
    public bool FinanzasConfiguradas { get; set; }       // true una vez el admin guarda la seccion

    // Configuracion avanzada de Finanzas (modulo 2.3 seccion 2 "Mas informacion" del modal).
    public MultiploRedondeo MultiploRedondeo { get; set; } = MultiploRedondeo.NoRedondear;
    public MultiploRedondeo MultiploRedondeoCuotaExtra { get; set; } = MultiploRedondeo.NoRedondear;
    public MultiploRedondeo MultiploRedondeoProntoPago { get; set; } = MultiploRedondeo.NoRedondear;

    // Consecutivos: ultimo numero usado. Se incrementan al emitir cada documento.
    public int ConsecutivoFactura { get; set; }
    public int ConsecutivoRC { get; set; }                // RC = Recibo de Caja
    public int ConsecutivoNotaCredito { get; set; }
    public int ConsecutivoPazYSalvo { get; set; }
    public int ConsecutivoActaConsejo { get; set; }
    public int ConsecutivoActaAsamblea { get; set; }

    public string? ConvenioRecaudo { get; set; }
    public string? Chartld { get; set; }                  // codigo CHARTLD del operador (bancario)
    public string? ComunicacionFactura { get; set; }      // texto libre (nombre/email/tel auxiliar)
    public string? WenjoyCodigoRecaudo { get; set; }

    public TiposPagoPermitidos TiposPagoPermitidos { get; set; } = TiposPagoPermitidos.Ninguno;
    public string? FormasDePago { get; set; }             // descripcion libre (texto)

    public decimal MinimoSaldoProntoPago { get; set; }
    public decimal MinimoSaldoCartera { get; set; }
    public string? CuentaContable { get; set; }
    public string? ZonaFacturacion { get; set; }          // ej. Sur, Norte (distinta a Ciudad)
    public int? EstratoFacturacion { get; set; }

    public EstadoCopropiedad Estado { get; set; } = EstadoCopropiedad.Activa;
    public EstadoCustodia EstadoCustodia { get; set; } = EstadoCustodia.SinAdmin;

    public DateTimeOffset? FechaActivacion { get; set; }
    public DateTimeOffset? FechaCancelacion { get; set; }

    // Organizacion actual a cargo (puede ser null cuando el estado es SinAdmin)
    public Guid? OrganizacionId { get; set; }
    public Organizacion? Organizacion { get; set; }

    // Navegacion inversa
    public ICollection<UsuarioTenant> Usuarios { get; set; } = new List<UsuarioTenant>();
}
