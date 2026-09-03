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
    // Modulos 2.12 Porteria, 2.13 Reservas y globales Capa 1 (1.2 Calendario, 1.4 Reportes consolidados, 1.5 Transferencia de custodia, festivos).
    private void ConfigurePorteriaReservasGlobales(ModelBuilder modelBuilder)
    {
        // -------------------- Modulo 2.12 Porteria y Control de Acceso --------------------

        modelBuilder.Entity<TurnoPorteria>(b =>
        {
            b.ToTable("turnos_porteria");
            b.Property(x => x.PuntoAcceso).IsRequired().HasMaxLength(100);
            b.Property(x => x.Estado).HasConversion<int>();
            b.Property(x => x.NotaCierre).HasColumnType("text");
            b.HasOne(x => x.GuardaPersona).WithMany().HasForeignKey(x => x.GuardaPersonaId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.TenantId, x.GuardaPersonaId, x.PuntoAcceso, x.Estado });
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<VisitanteFrecuente>(b =>
        {
            b.ToTable("visitantes_frecuentes");
            b.Property(x => x.Nombre).IsRequired().HasMaxLength(200);
            b.Property(x => x.Documento).HasMaxLength(30);
            b.Property(x => x.TipoDocumento).HasConversion<int?>();
            b.Property(x => x.FotoUrl).HasMaxLength(500);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.TenantId, x.Documento });
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<AutorizacionPrevia>(b =>
        {
            b.ToTable("autorizaciones_previa");
            b.Property(x => x.NombreVisitante).IsRequired().HasMaxLength(200);
            b.Property(x => x.DocumentoVisitante).HasMaxLength(30);
            b.Property(x => x.NotaPortero).HasColumnType("text");
            b.Property(x => x.Origen).HasConversion<int>();
            b.Property(x => x.TipoVisitante).HasConversion<int>();
            b.Property(x => x.Estado).HasConversion<int>();
            b.HasOne(x => x.UnidadPrivada).WithMany().HasForeignKey(x => x.UnidadPrivadaId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.CreadoPorPersona).WithMany().HasForeignKey(x => x.CreadoPorPersonaId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.TenantId, x.Estado });
            b.HasIndex(x => new { x.TenantId, x.UnidadPrivadaId });
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<CodigoIngreso>(b =>
        {
            b.ToTable("codigos_ingreso");
            b.Property(x => x.CodigoNumerico).IsRequired().HasMaxLength(8);
            b.Property(x => x.QrPayload).IsRequired().HasColumnType("text");
            b.Property(x => x.Estado).HasConversion<int>();
            b.HasOne(x => x.Autorizacion).WithMany(a => a.Codigos).HasForeignKey(x => x.AutorizacionId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.TenantId, x.CodigoNumerico }).IsUnique();
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<RegistroVisita>(b =>
        {
            b.ToTable("registros_visita");
            b.Property(x => x.Nombre).IsRequired().HasMaxLength(200);
            b.Property(x => x.Documento).HasMaxLength(30);
            b.Property(x => x.Observacion).HasColumnType("text");
            b.Property(x => x.TipoEvento).HasConversion<int>();
            b.Property(x => x.TipoVisitante).HasConversion<int>();
            b.Property(x => x.TipoDocumento).HasConversion<int?>();
            b.Property(x => x.DestinoTipo).HasConversion<int?>();
            b.HasOne(x => x.Turno).WithMany().HasForeignKey(x => x.TurnoId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.VisitanteFrecuente).WithMany().HasForeignKey(x => x.VisitanteFrecuenteId).OnDelete(DeleteBehavior.SetNull);
            b.HasOne(x => x.Autorizacion).WithMany().HasForeignKey(x => x.AutorizacionId).OnDelete(DeleteBehavior.SetNull);
            b.HasOne(x => x.CodigoIngreso).WithMany().HasForeignKey(x => x.CodigoIngresoId).OnDelete(DeleteBehavior.SetNull);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.TenantId, x.Timestamp });
            b.HasIndex(x => x.TurnoId);
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<VehiculoAutorizado>(b =>
        {
            b.ToTable("vehiculos_autorizados");
            b.Property(x => x.Placa).IsRequired().HasMaxLength(10);
            b.Property(x => x.Tipo).HasConversion<int>();
            b.Property(x => x.Marca).HasMaxLength(100);
            b.Property(x => x.Modelo).HasMaxLength(100);
            b.Property(x => x.Color).HasMaxLength(50);
            b.Property(x => x.ParqueaderoAsignado).HasMaxLength(20);
            b.HasOne(x => x.UnidadPrivada).WithMany().HasForeignKey(x => x.UnidadPrivadaId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.TenantId, x.Placa });
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<RegistroVehiculo>(b =>
        {
            b.ToTable("registros_vehiculo");
            b.Property(x => x.Placa).IsRequired().HasMaxLength(10);
            b.Property(x => x.Conductor).HasMaxLength(200);
            b.Property(x => x.Observacion).HasColumnType("text");
            b.Property(x => x.TipoEvento).HasConversion<int>();
            b.Property(x => x.OrigenRegistro).HasConversion<int>();
            b.HasOne(x => x.Turno).WithMany().HasForeignKey(x => x.TurnoId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.VehiculoAutorizado).WithMany().HasForeignKey(x => x.VehiculoAutorizadoId).OnDelete(DeleteBehavior.SetNull);
            b.HasOne(x => x.UnidadPrivada).WithMany().HasForeignKey(x => x.UnidadPrivadaId).OnDelete(DeleteBehavior.SetNull);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.TenantId, x.Timestamp });
            b.HasIndex(x => x.TurnoId);
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<Correspondencia>(b =>
        {
            b.ToTable("correspondencias");
            b.Property(x => x.Remitente).HasMaxLength(200);
            b.Property(x => x.Descripcion).HasColumnType("text");
            b.Property(x => x.EntregadoA).HasMaxLength(200);
            b.Property(x => x.MotivoDevolucion).HasColumnType("text");
            b.Property(x => x.Tipo).HasConversion<int>();
            b.Property(x => x.Estado).HasConversion<int>();
            b.HasOne(x => x.Turno).WithMany().HasForeignKey(x => x.TurnoId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.UnidadPrivada).WithMany().HasForeignKey(x => x.UnidadPrivadaId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.TenantId, x.Estado });
            b.HasIndex(x => new { x.TenantId, x.UnidadPrivadaId, x.Estado });
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<NovedadTurno>(b =>
        {
            b.ToTable("novedades_turno");
            b.Property(x => x.Descripcion).IsRequired().HasColumnType("text");
            b.HasOne(x => x.Turno).WithMany().HasForeignKey(x => x.TurnoId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.Tarea).WithMany().HasForeignKey(x => x.TareaId).OnDelete(DeleteBehavior.SetNull);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => x.TurnoId);
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<PorteriaConfiguracion>(b =>
        {
            b.ToTable("porteria_configuracion");
            b.Property(x => x.CanalNotificacionPaquetes).HasConversion<int>();
            b.HasIndex(x => x.TenantId).IsUnique();
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        // -------------------- Modulo 2.13 Reservas de Zonas Comunes --------------------

        modelBuilder.Entity<ZonaConfigReserva>(b =>
        {
            b.ToTable("zona_config_reserva");
            b.Property(x => x.ValorTarifa).HasColumnType("numeric(18,2)");
            b.Property(x => x.ValorPenalidadCancelacion).HasColumnType("numeric(18,2)");
            b.Property(x => x.ModalidadCobro).HasConversion<int>();
            b.Property(x => x.PoliticaReembolso).HasConversion<int>();
            b.Property(x => x.CancelacionTardia).HasConversion<int>();
            b.Property(x => x.ReglamentoTexto).HasColumnType("text");
            b.Property(x => x.ReglamentoArchivoUrl).HasMaxLength(500);
            b.HasOne(x => x.ZonaComun).WithMany().HasForeignKey(x => x.ZonaComunId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.TenantId, x.ZonaComunId }).IsUnique();
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<ZonaFranja>(b =>
        {
            b.ToTable("zona_franjas");
            b.Property(x => x.DiaSemana).HasConversion<int>();
            b.HasOne(x => x.ZonaConfigReserva).WithMany(c => c.Franjas).HasForeignKey(x => x.ZonaConfigReservaId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.ZonaConfigReservaId, x.DiaSemana });
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<Reserva>(b =>
        {
            b.ToTable("reservas");
            b.Property(x => x.Codigo).IsRequired().HasMaxLength(30);
            b.Property(x => x.Estado).HasConversion<int>();
            b.Property(x => x.MotivoCancelacion).HasColumnType("text");
            b.HasOne(x => x.ZonaComun).WithMany().HasForeignKey(x => x.ZonaComunId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.Persona).WithMany().HasForeignKey(x => x.PersonaId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.UnidadPrivada).WithMany().HasForeignKey(x => x.UnidadPrivadaId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.ReservaRecurrente).WithMany().HasForeignKey(x => x.ReservaRecurrenteId).OnDelete(DeleteBehavior.SetNull);
            b.HasOne(x => x.ReservaPago).WithMany().HasForeignKey(x => x.ReservaPagoId).OnDelete(DeleteBehavior.SetNull);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.TenantId, x.Codigo }).IsUnique();
            b.HasIndex(x => new { x.TenantId, x.ZonaComunId, x.Fecha });
            b.HasIndex(x => new { x.TenantId, x.PersonaId, x.Estado });
            b.HasIndex(x => new { x.TenantId, x.Fecha, x.Estado });
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<ReservaRecurrente>(b =>
        {
            b.ToTable("reservas_recurrentes");
            b.Property(x => x.Frecuencia).HasConversion<int>();
            b.Property(x => x.Estado).HasConversion<int>();
            b.HasIndex(x => x.TenantId);
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<ReservaPago>(b =>
        {
            b.ToTable("reserva_pagos");
            b.Property(x => x.Monto).HasColumnType("numeric(18,2)");
            b.Property(x => x.EstadoPago).HasConversion<int>();
            b.Property(x => x.WompiTransactionId).HasMaxLength(100);
            b.Property(x => x.WompiReference).HasMaxLength(100);
            b.HasIndex(x => x.TenantId);
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<PrestamoEquipo>(b =>
        {
            b.ToTable("prestamos_equipo");
            b.Property(x => x.Codigo).IsRequired().HasMaxLength(30);
            b.Property(x => x.Estado).HasConversion<int>();
            b.Property(x => x.Observaciones).HasColumnType("text");
            b.Property(x => x.EntregaObservacion).HasColumnType("text");
            b.Property(x => x.DevolucionObservacion).HasColumnType("text");
            b.Property(x => x.MotivoCancelacion).HasColumnType("text");
            b.HasOne(x => x.EquipoActivo).WithMany().HasForeignKey(x => x.EquipoActivoId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.Persona).WithMany().HasForeignKey(x => x.PersonaId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.UnidadPrivada).WithMany().HasForeignKey(x => x.UnidadPrivadaId).OnDelete(DeleteBehavior.SetNull);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.TenantId, x.Codigo }).IsUnique();
            b.HasIndex(x => new { x.TenantId, x.EquipoActivoId, x.Fecha });
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<EntregaFoto>(b =>
        {
            b.ToTable("entrega_fotos");
            b.Property(x => x.OrigenTipo).IsRequired().HasMaxLength(20);
            b.Property(x => x.Url).IsRequired().HasMaxLength(1024);
            b.Property(x => x.Momento).HasConversion<int>();
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.TenantId, x.OrigenTipo, x.OrigenId });
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<ZonaBloqueo>(b =>
        {
            b.ToTable("zona_bloqueos");
            b.Property(x => x.Tipo).HasConversion<int>();
            b.Property(x => x.Etiqueta).HasConversion<int>();
            b.Property(x => x.Origen).HasConversion<int>();
            b.Property(x => x.MotivoPersonalizado).HasColumnType("text");
            b.HasOne(x => x.ZonaComun).WithMany().HasForeignKey(x => x.ZonaComunId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.TenantId, x.ZonaComunId, x.FechaInicio, x.FechaFin });
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        // -------------------- Modulo 1.2 Calendario Multi-Copropiedad (global) --------------------

        modelBuilder.Entity<CalendarioEvento>(b =>
        {
            b.ToTable("calendario_eventos");
            b.Property(x => x.Titulo).IsRequired().HasMaxLength(200);
            b.Property(x => x.Descripcion).HasColumnType("text");
            b.Property(x => x.Tipo).HasConversion<int>();
            b.Property(x => x.ZonaHoraria).IsRequired().HasMaxLength(50);
            b.HasOne(x => x.Organizacion).WithMany().HasForeignKey(x => x.OrganizacionId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.SetNull);
            b.HasIndex(x => x.OrganizacionId);
            b.HasIndex(x => new { x.OrganizacionId, x.FechaInicio });
            b.HasIndex(x => x.TenantId);
            // Calendario es global - NO HasQueryFilter por tenant. Se filtra por OrganizacionId en service.
        });

        modelBuilder.Entity<CalendarioConfigUsuario>(b =>
        {
            b.ToTable("calendario_config_usuarios");
            b.Property(x => x.VistaDefault).HasConversion<int>();
            b.Property(x => x.UltimaVista).HasConversion<int>();
            b.Property(x => x.FiltroCopropiedadesJson).HasColumnType("text");
            b.Property(x => x.FiltroTiposJson).HasColumnType("text");
            b.HasOne(x => x.Organizacion).WithMany().HasForeignKey(x => x.OrganizacionId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => new { x.UsuarioId, x.OrganizacionId }).IsUnique();
            b.HasIndex(x => x.IcalToken).IsUnique();
        });

        // -------------------- Modulo 1.4 Reportes Consolidados (global) --------------------

        modelBuilder.Entity<OrgReporte>(b =>
        {
            b.ToTable("org_reportes");
            b.Property(x => x.Nombre).IsRequired().HasMaxLength(200);
            b.Property(x => x.Categoria).HasConversion<int>();
            b.Property(x => x.ConfiguracionJson).IsRequired().HasColumnType("text");
            b.HasOne(x => x.Organizacion).WithMany().HasForeignKey(x => x.OrganizacionId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => x.OrganizacionId);
            b.HasIndex(x => new { x.OrganizacionId, x.Categoria });
        });

        modelBuilder.Entity<OrgReporteGeneracion>(b =>
        {
            b.ToTable("org_reporte_generaciones");
            b.Property(x => x.Origen).HasConversion<int>();
            b.Property(x => x.Estado).HasConversion<int>();
            b.Property(x => x.ResultadoJson).HasColumnType("text");
            b.Property(x => x.ErrorDetalle).HasMaxLength(2000);
            b.Property(x => x.UrlPdf).HasMaxLength(1000);
            b.Property(x => x.UrlExcel).HasMaxLength(1000);
            b.HasOne(x => x.Reporte).WithMany().HasForeignKey(x => x.ReporteId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.Organizacion).WithMany().HasForeignKey(x => x.OrganizacionId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => x.ReporteId);
            b.HasIndex(x => new { x.OrganizacionId, x.CreatedAt });
        });

        // -------------------- Modulo 1.5 Transferencia de Custodia (global) --------------------

        modelBuilder.Entity<TransferenciaCustodia>(b =>
        {
            b.ToTable("transferencias_custodia");
            b.Property(x => x.Escenario).HasConversion<int>();
            b.Property(x => x.Estado).HasConversion<int>();
            b.Property(x => x.SnapshotEstadoJson).HasColumnType("text");
            b.Property(x => x.AjusteFacturacionJson).HasColumnType("text");
            b.HasOne(x => x.Copropiedad).WithMany().HasForeignKey(x => x.CopropiedadId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.ActaEntregaDocumento).WithMany().HasForeignKey(x => x.ActaEntregaDocumentoId).OnDelete(DeleteBehavior.SetNull);
            b.HasIndex(x => x.CopropiedadId);
            b.HasIndex(x => x.OrganizacionSalienteId);
            b.HasIndex(x => x.OrganizacionEntranteId);
            b.HasIndex(x => x.Estado);
            // RN-16: solo una transferencia activa por copropiedad. Unique parcial en migracion SQL.
        });

        modelBuilder.Entity<TransferenciaDocumento>(b =>
        {
            b.ToTable("transferencia_documentos");
            b.Property(x => x.NombreArchivo).IsRequired().HasMaxLength(300);
            b.Property(x => x.TipoMime).IsRequired().HasMaxLength(100);
            b.Property(x => x.UrlStorage).IsRequired().HasMaxLength(1000);
            b.Property(x => x.HashSha256).IsRequired().HasMaxLength(64);
            b.Property(x => x.ResultadoValidacionIa).HasConversion<int>();
            b.Property(x => x.DetalleValidacionIaJson).HasColumnType("text");
            b.HasOne(x => x.Transferencia).WithMany(t => t.Documentos)
                .HasForeignKey(x => x.TransferenciaId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => x.TransferenciaId);
        });

        modelBuilder.Entity<TransferenciaEvento>(b =>
        {
            b.ToTable("transferencia_eventos");
            b.Property(x => x.TipoEvento).HasConversion<int>();
            b.Property(x => x.Canal).HasMaxLength(30);
            b.Property(x => x.DetalleJson).HasColumnType("text");
            b.HasOne(x => x.Transferencia).WithMany(t => t.Eventos)
                .HasForeignKey(x => x.TransferenciaId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => x.TransferenciaId);
            b.HasIndex(x => new { x.TransferenciaId, x.CreatedAt });
        });

        // Festivos colombianos (global, sin tenant) - cache memoria proceso via CalendarioHabilService
        modelBuilder.Entity<FestivoColombiano>(b =>
        {
            b.ToTable("festivos_colombianos");
            b.Property(x => x.Descripcion).IsRequired().HasMaxLength(200);
            b.HasIndex(x => x.Fecha).IsUnique();
        });

    }
}
