using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Propia.Application.Common;
using Propia.Domain.Common;
using Propia.Domain.Entities;
using Propia.Domain.Enums;

namespace Propia.Infrastructure.Persistence;

// Configuracion de modelo EF Core extraida de PropiaDbContext.OnModelCreating.
// Clase parcial: conserva acceso a _tenantContext (los HasQueryFilter siguen
// capturando la misma instancia). El orden de invocacion lo fija OnModelCreating.
public partial class PropiaDbContext
{
    // Modulos 2.7 Cartera y Estado de Cuenta y 2.9 PQRSD y Convivencia.
    private void ConfigureCarteraYPqrsd(ModelBuilder modelBuilder)
    {
        // -------------------- Modulo 2.7 Cartera y Estado de Cuenta --------------------

        modelBuilder.Entity<EstadoCarteraConfig>(b =>
        {
            b.ToTable("estados_cartera_config");
            b.Property(x => x.Nombre).IsRequired().HasMaxLength(100);
            b.Property(x => x.Color).HasMaxLength(20);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.TenantId, x.Nombre }).IsUnique();
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<CarteraConfig>(b =>
        {
            b.ToTable("cartera_config");
            b.Property(x => x.MensajePazSalvo).HasMaxLength(1000);
            b.Property(x => x.TasaMoraMensual).HasPrecision(8, 4);
            b.HasIndex(x => x.TenantId).IsUnique();
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<CarteraUnidad>(b =>
        {
            b.ToTable("cartera_unidades");
            b.Property(x => x.SaldoCapital).HasPrecision(18, 2);
            b.Property(x => x.SaldoIntereses).HasPrecision(18, 2);
            b.Ignore(x => x.SaldoTotal);  // calculado
            b.HasOne(x => x.UnidadPrivada).WithMany().HasForeignKey(x => x.UnidadPrivadaId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.EstadoGestion).WithMany().HasForeignKey(x => x.EstadoGestionId).OnDelete(DeleteBehavior.SetNull);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.TenantId, x.UnidadPrivadaId }).IsUnique();
            b.HasIndex(x => x.EstadoGestionId);
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<DeudaDetalle>(b =>
        {
            b.ToTable("deuda_detalle");
            b.Property(x => x.Concepto).IsRequired().HasMaxLength(100);
            b.Property(x => x.CapitalOriginal).HasPrecision(18, 2);
            b.Property(x => x.CapitalPendiente).HasPrecision(18, 2);
            b.Property(x => x.InteresAcumulado).HasPrecision(18, 2);
            b.HasOne(x => x.UnidadPrivada).WithMany().HasForeignKey(x => x.UnidadPrivadaId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.LiquidacionUnidad).WithMany().HasForeignKey(x => x.LiquidacionUnidadId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.TenantId, x.UnidadPrivadaId });
            b.HasIndex(x => x.LiquidacionUnidadId).IsUnique();
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<AcuerdoPago>(b =>
        {
            b.ToTable("acuerdos_pago");
            b.Property(x => x.MontoTotal).HasPrecision(18, 2);
            b.Property(x => x.CapitalIncluido).HasPrecision(18, 2);
            b.Property(x => x.InteresesIncluidos).HasPrecision(18, 2);
            b.Property(x => x.InteresesCondonados).HasPrecision(18, 2);
            b.Property(x => x.NotasAdmin).HasMaxLength(1000);
            b.Property(x => x.AceptacionHash).HasMaxLength(255);
            b.Property(x => x.AceptacionIp).HasMaxLength(50);
            b.Property(x => x.AceptacionMetodo).HasMaxLength(50);
            b.HasOne(x => x.UnidadPrivada).WithMany().HasForeignKey(x => x.UnidadPrivadaId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.AcuerdoPadre).WithMany().HasForeignKey(x => x.AcuerdoPadreId).OnDelete(DeleteBehavior.SetNull);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.TenantId, x.UnidadPrivadaId });
            b.HasIndex(x => new { x.TenantId, x.Estado });
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<AcuerdoCuota>(b =>
        {
            b.ToTable("acuerdo_cuotas");
            b.Property(x => x.Monto).HasPrecision(18, 2);
            b.Property(x => x.Capital).HasPrecision(18, 2);
            b.Property(x => x.Intereses).HasPrecision(18, 2);
            b.HasOne(x => x.Acuerdo).WithMany(a => a.Cuotas).HasForeignKey(x => x.AcuerdoId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => x.AcuerdoId);
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<PazSalvoEmitido>(b =>
        {
            b.ToTable("paz_salvos_emitidos");
            b.Property(x => x.Condiciones).HasMaxLength(2000);
            b.Property(x => x.DocumentoUrl).HasMaxLength(500);
            b.Property(x => x.CodigoVerificacion).IsRequired().HasMaxLength(100);
            b.Property(x => x.MotivoAnulacion).HasMaxLength(500);
            b.HasOne(x => x.UnidadPrivada).WithMany().HasForeignKey(x => x.UnidadPrivadaId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => x.CodigoVerificacion).IsUnique();
            b.HasIndex(x => new { x.TenantId, x.UnidadPrivadaId });
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<Condonacion>(b =>
        {
            b.ToTable("condonaciones");
            b.Property(x => x.MontoCondonado).HasPrecision(18, 2);
            b.Property(x => x.Motivo).IsRequired().HasMaxLength(1000);
            b.Property(x => x.DocumentoSoporteUrl).HasMaxLength(500);
            b.Property(x => x.SaldoAntes).HasColumnType("text");
            b.Property(x => x.SaldoDespues).HasColumnType("text");
            b.HasOne(x => x.UnidadPrivada).WithMany().HasForeignKey(x => x.UnidadPrivadaId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.TenantId, x.UnidadPrivadaId });
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<CarteraHistorial>(b =>
        {
            b.ToTable("cartera_historial");
            b.Property(x => x.Descripcion).IsRequired().HasMaxLength(500);
            b.Property(x => x.DatosAdicionales).HasColumnType("text");
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.TenantId, x.UnidadPrivadaId, x.OcurridoAt });
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        // -------------------- Modulo 2.9 PQRSD y Convivencia --------------------

        modelBuilder.Entity<PqrsdCategoria>(b =>
        {
            b.ToTable("pqrsd_categorias");
            b.Property(x => x.Nombre).IsRequired().HasMaxLength(100);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.TenantId, x.Nombre }).IsUnique();
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<PqrsdConfiguracionPlazo>(b =>
        {
            b.ToTable("pqrsd_configuracion_plazos");
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.TenantId, x.Tipo }).IsUnique();
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<PqrsdExpediente>(b =>
        {
            b.ToTable("pqrsd_expedientes");
            b.Property(x => x.NumeroRadicado).IsRequired().HasMaxLength(30);
            b.Property(x => x.Descripcion).IsRequired().HasMaxLength(2000);
            b.Property(x => x.RespuestaAdmin).HasMaxLength(4000);
            b.Property(x => x.InconformidadTexto).HasMaxLength(2000);
            b.Property(x => x.RespuestaDefinitiva).HasMaxLength(4000);
            b.Property(x => x.Seccional).HasMaxLength(120);
            b.Property(x => x.Administrador).HasMaxLength(160);
            b.HasOne(x => x.Categoria).WithMany().HasForeignKey(x => x.CategoriaId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.RadicadorPersona).WithMany().HasForeignKey(x => x.RadicadorPersonaId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.EstadoColumna).WithMany().HasForeignKey(x => x.EstadoId).OnDelete(DeleteBehavior.SetNull);
            b.HasOne(x => x.TipoConfig).WithMany().HasForeignKey(x => x.TipoId).OnDelete(DeleteBehavior.SetNull);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.TenantId, x.NumeroRadicado }).IsUnique();
            b.HasIndex(x => new { x.TenantId, x.Estado });
            b.HasIndex(x => new { x.TenantId, x.Tipo });
            b.HasIndex(x => new { x.TenantId, x.EstadoId });
            b.HasIndex(x => new { x.TenantId, x.Archivado });
            b.HasIndex(x => x.RadicadorPersonaId);
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<PqrsdEstado>(b =>
        {
            b.ToTable("pqrsd_estados");
            b.Property(x => x.Nombre).IsRequired().HasMaxLength(80);
            b.Property(x => x.Color).HasMaxLength(20);
            b.Property(x => x.Activo).HasDefaultValue(true);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.TenantId, x.Nombre }).IsUnique();
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<PqrsdTipo>(b =>
        {
            b.ToTable("pqrsd_tipos");
            b.Property(x => x.Nombre).IsRequired().HasMaxLength(100);
            b.Property(x => x.Activo).HasDefaultValue(true);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.TenantId, x.Nombre }).IsUnique();
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<MotivoCierre>(b =>
        {
            b.ToTable("motivos_cierre");
            b.Property(x => x.Modulo).IsRequired().HasMaxLength(20);
            b.Property(x => x.Nombre).IsRequired().HasMaxLength(120);
            b.Property(x => x.Activo).HasDefaultValue(true);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.TenantId, x.Modulo, x.Nombre }).IsUnique();
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<PqrsdCampo>(b =>
        {
            b.ToTable("pqrsd_campos");
            b.Property(x => x.Label).IsRequired().HasMaxLength(120);
            b.Property(x => x.Activo).HasDefaultValue(true);
            b.HasIndex(x => x.TenantId);
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<PqrsdCampoValor>(b =>
        {
            b.ToTable("pqrsd_campo_valores");
            b.Property(x => x.Valor).HasColumnType("text");
            b.HasOne(x => x.Expediente).WithMany(e => e.CamposValores).HasForeignKey(x => x.ExpedienteId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.ExpedienteId, x.PqrsdCampoId }).IsUnique();
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<PqrsdComentario>(b =>
        {
            b.ToTable("pqrsd_comentarios");
            b.Property(x => x.Texto).IsRequired().HasColumnType("text");
            b.Property(x => x.AutorNombre).HasMaxLength(200);
            b.HasOne(x => x.Expediente).WithMany(e => e.Comentarios).HasForeignKey(x => x.PqrsdExpedienteId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => x.PqrsdExpedienteId);
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<PqrsdAdjunto>(b =>
        {
            b.ToTable("pqrsd_adjuntos");
            b.Property(x => x.NombreArchivo).IsRequired().HasMaxLength(255);
            b.Property(x => x.TipoMime).IsRequired().HasMaxLength(100);
            b.Property(x => x.UrlStorage).IsRequired().HasMaxLength(1000);
            b.HasOne(x => x.Expediente).WithMany(e => e.Adjuntos).HasForeignKey(x => x.ExpedienteId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.Respuesta).WithMany(r => r.Adjuntos).HasForeignKey(x => x.RespuestaId).OnDelete(DeleteBehavior.SetNull);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => x.ExpedienteId);
            b.HasIndex(x => x.RespuestaId);
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<PqrsdRespuesta>(b =>
        {
            b.ToTable("pqrsd_respuestas");
            b.Property(x => x.CuerpoHtml).IsRequired().HasColumnType("text");
            b.Property(x => x.Asunto).HasMaxLength(300);
            b.Property(x => x.AutorNombre).HasMaxLength(200);
            b.HasOne(x => x.Expediente).WithMany(e => e.Respuestas).HasForeignKey(x => x.ExpedienteId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => x.ExpedienteId);
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<PqrsdRespuestaDestinatario>(b =>
        {
            b.ToTable("pqrsd_respuesta_destinatarios");
            b.Property(x => x.Email).IsRequired().HasMaxLength(320);
            b.Property(x => x.Nombre).HasMaxLength(200);
            b.HasOne(x => x.Respuesta).WithMany(r => r.Destinatarios).HasForeignKey(x => x.RespuestaId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => x.RespuestaId);
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<PqrsdRespuestaVersion>(b =>
        {
            b.ToTable("pqrsd_respuesta_versiones");
            b.Property(x => x.CuerpoHtml).IsRequired().HasColumnType("text");
            b.Property(x => x.Asunto).HasMaxLength(300);
            b.Property(x => x.AutorNombre).HasMaxLength(200);
            b.HasOne(x => x.Respuesta).WithMany(r => r.Versiones).HasForeignKey(x => x.RespuestaId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.RespuestaId, x.Numero });
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<PqrsdPlantillaRespuesta>(b =>
        {
            b.ToTable("pqrsd_plantillas_respuesta");
            b.Property(x => x.Nombre).IsRequired().HasMaxLength(200);
            b.Property(x => x.CuerpoHtml).IsRequired().HasColumnType("text");
            b.HasIndex(x => x.TenantId);
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        // Catalogo GLOBAL de plantillas semilla (operado por Super Admin). Sin tenant_id, sin RLS.
        modelBuilder.Entity<PqrsdPlantillaSemilla>(b =>
        {
            b.ToTable("pqrsd_plantillas_semilla");
            b.Property(x => x.Nombre).IsRequired().HasMaxLength(200);
            b.Property(x => x.CuerpoHtml).IsRequired().HasColumnType("text");
        });

        modelBuilder.Entity<PqrsdHistorialEstado>(b =>
        {
            b.ToTable("pqrsd_historial_estados");
            b.Property(x => x.Nota).HasMaxLength(500);
            b.HasOne(x => x.Expediente).WithMany(e => e.Historial).HasForeignKey(x => x.ExpedienteId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.ExpedienteId, x.CreatedAt });
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<PqrsdComiteSesion>(b =>
        {
            b.ToTable("pqrsd_comite_sesiones");
            b.Property(x => x.EnlaceReunion).HasMaxLength(500);
            b.Property(x => x.BorradorActa).HasColumnType("text");
            b.Property(x => x.ActaFinal).HasColumnType("text");
            b.HasOne(x => x.Expediente).WithMany().HasForeignKey(x => x.ExpedienteId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => x.ExpedienteId);
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<PqrsdComiteMiembroSesion>(b =>
        {
            b.ToTable("pqrsd_comite_miembros");
            b.HasOne(x => x.Sesion).WithMany(s => s.Miembros).HasForeignKey(x => x.SesionId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.Persona).WithMany().HasForeignKey(x => x.PersonaId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.SesionId, x.PersonaId }).IsUnique();
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

    }
}
