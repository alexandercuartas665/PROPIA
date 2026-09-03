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
    // Modulos 2.8 Asambleas, 2.11 Mantenimiento y Activos y 2.14 Comunicaciones.
    private void ConfigureAsambleasMantenimientoComunicaciones(ModelBuilder modelBuilder)
    {
        // -------------------- Modulo 2.8 Asambleas y Organos de Gobierno --------------------

        modelBuilder.Entity<AsambleaConfig>(b =>
        {
            b.ToTable("asamblea_config");
            b.HasIndex(x => x.TenantId).IsUnique();
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<Sesion>(b =>
        {
            b.ToTable("sesiones");
            b.Property(x => x.Titulo).IsRequired().HasMaxLength(255);
            b.Property(x => x.LugarFisico).HasMaxLength(500);
            b.Property(x => x.EnlaceVideo).HasMaxLength(500);
            b.Property(x => x.QuorumRequeridoPct).HasPrecision(5, 2);
            b.HasOne(x => x.SesionPadre).WithMany().HasForeignKey(x => x.SesionPadreId).OnDelete(DeleteBehavior.SetNull);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.TenantId, x.Estado });
            b.HasIndex(x => new { x.TenantId, x.FechaSesion });
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<SesionPunto>(b =>
        {
            b.ToTable("sesion_puntos");
            b.Property(x => x.Titulo).IsRequired().HasMaxLength(255);
            b.Property(x => x.Descripcion).HasMaxLength(2000);
            b.Property(x => x.MayoriaPct).HasPrecision(5, 2);
            b.Property(x => x.OpcionesVoto).HasColumnType("text");
            b.Property(x => x.NarrativaSecretario).HasColumnType("text");
            b.HasOne(x => x.Sesion).WithMany(s => s.Puntos).HasForeignKey(x => x.SesionId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => x.SesionId);
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<SesionDocumento>(b =>
        {
            b.ToTable("sesion_documentos");
            b.Property(x => x.Nombre).IsRequired().HasMaxLength(255);
            b.Property(x => x.Descripcion).HasMaxLength(500);
            b.Property(x => x.UrlStorage).IsRequired().HasMaxLength(500);
            b.Property(x => x.TipoArchivo).HasMaxLength(50);
            b.HasOne(x => x.Sesion).WithMany(s => s.Documentos).HasForeignKey(x => x.SesionId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.Punto).WithMany().HasForeignKey(x => x.PuntoId).OnDelete(DeleteBehavior.SetNull);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => x.SesionId);
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<SesionParticipante>(b =>
        {
            b.ToTable("sesion_participantes");
            b.Property(x => x.Coeficiente).HasPrecision(10, 6);
            b.HasOne(x => x.Sesion).WithMany(s => s.Participantes).HasForeignKey(x => x.SesionId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.Persona).WithMany().HasForeignKey(x => x.PersonaId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.UnidadPrivada).WithMany().HasForeignKey(x => x.UnidadPrivadaId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.SesionId, x.UnidadPrivadaId }).IsUnique();
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<SesionPoder>(b =>
        {
            b.ToTable("sesion_poderes");
            b.Property(x => x.DocumentoUrl).HasMaxLength(500);
            b.Property(x => x.HashPoder).HasMaxLength(255);
            b.Property(x => x.FirmanteIp).HasMaxLength(50);
            b.Property(x => x.NotaRechazo).HasMaxLength(500);
            b.HasOne(x => x.Sesion).WithMany(s => s.Poderes).HasForeignKey(x => x.SesionId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.ApoderadoPersona).WithMany().HasForeignKey(x => x.ApoderadoPersonaId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.SesionId, x.OtorganteUnidadId });
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<SesionQuorumLog>(b =>
        {
            b.ToTable("sesion_quorum_log");
            b.Property(x => x.Coeficiente).HasPrecision(10, 6);
            b.Property(x => x.QuorumAcumuladoPct).HasPrecision(5, 2);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.SesionId, x.CreatedAt });
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<Votacion>(b =>
        {
            b.ToTable("votaciones");
            b.Property(x => x.QuorumAlAbrirPct).HasPrecision(5, 2);
            b.Property(x => x.CoeficienteTotalSala).HasPrecision(10, 6);
            b.Property(x => x.ResultadoOpcion).HasMaxLength(100);
            b.Property(x => x.ResultadoPct).HasPrecision(5, 2);
            b.HasOne(x => x.Sesion).WithMany().HasForeignKey(x => x.SesionId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.Punto).WithMany().HasForeignKey(x => x.PuntoId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => x.PuntoId);
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<Voto>(b =>
        {
            b.ToTable("votos");
            b.Property(x => x.CoeficienteAportado).HasPrecision(10, 6);
            b.Property(x => x.Opcion).IsRequired().HasMaxLength(100);
            b.HasOne(x => x.Votacion).WithMany(v => v.Votos).HasForeignKey(x => x.VotacionId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.VotacionId, x.UnidadPrivadaId }).IsUnique();  // RN-09: un voto por unidad
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<Acta>(b =>
        {
            b.ToTable("actas");
            b.Property(x => x.ContenidoGenerado).HasColumnType("text");
            b.Property(x => x.NarrativaSecretario).HasColumnType("text");
            b.Property(x => x.DocumentoUrl).HasMaxLength(500);
            b.Property(x => x.HashDocumento).HasMaxLength(255);
            b.Property(x => x.FirmanteIp).HasMaxLength(50);
            b.HasOne(x => x.Sesion).WithOne(s => s.Acta).HasForeignKey<Acta>(x => x.SesionId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => x.SesionId).IsUnique();
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<EleccionConsejo>(b =>
        {
            b.ToTable("elecciones_consejo");
            b.Property(x => x.Estado).HasMaxLength(20);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => x.SesionId);
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<EleccionCandidato>(b =>
        {
            b.ToTable("eleccion_candidatos");
            b.Property(x => x.Cargo).HasMaxLength(100);
            b.Property(x => x.VotosCoeficiente).HasPrecision(10, 6);
            b.HasOne(x => x.Eleccion).WithMany(e => e.Candidatos).HasForeignKey(x => x.EleccionId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.Persona).WithMany().HasForeignKey(x => x.PersonaId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.EleccionId, x.PersonaId });
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        // -------------------- Modulo 2.11 Mantenimiento y Activos --------------------

        modelBuilder.Entity<MantenimientoPlan>(b =>
        {
            b.ToTable("mantenimiento_planes");
            b.Property(x => x.Nombre).IsRequired().HasMaxLength(200);
            b.Property(x => x.Descripcion).HasColumnType("text");
            b.Property(x => x.ActivoTipo).HasConversion<int>();
            b.Property(x => x.Frecuencia).HasConversion<int>();
            b.Property(x => x.Disparo).HasConversion<int>();
            b.HasOne(x => x.ProveedorPreferido).WithMany().HasForeignKey(x => x.ProveedorPreferidoId).OnDelete(DeleteBehavior.SetNull);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.TenantId, x.ActivoTipo, x.ActivoId });
            b.HasIndex(x => new { x.TenantId, x.ProximaEjecucion });
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<MantenimientoIntervencion>(b =>
        {
            b.ToTable("mantenimiento_intervenciones");
            b.Property(x => x.Codigo).IsRequired().HasMaxLength(20);
            b.Property(x => x.Titulo).IsRequired().HasMaxLength(200);
            b.Property(x => x.Descripcion).HasColumnType("text");
            b.Property(x => x.MotivoCancelacion).HasColumnType("text");
            b.Property(x => x.EstadoActivoNuevo).HasMaxLength(50);
            b.Property(x => x.Tipo).HasConversion<int>();
            b.Property(x => x.ActivoTipo).HasConversion<int>();
            b.Property(x => x.Origen).HasConversion<int>();
            b.Property(x => x.Estado).HasConversion<int>();
            b.Property(x => x.Prioridad).HasConversion<int>();
            b.HasOne(x => x.Plan).WithMany(p => p.Intervenciones).HasForeignKey(x => x.PlanId).OnDelete(DeleteBehavior.SetNull);
            b.HasOne(x => x.Proveedor).WithMany().HasForeignKey(x => x.ProveedorId).OnDelete(DeleteBehavior.SetNull);
            b.HasOne(x => x.ResponsableInterno).WithMany().HasForeignKey(x => x.ResponsableInternoId).OnDelete(DeleteBehavior.SetNull);
            b.HasOne(x => x.Tarea).WithMany().HasForeignKey(x => x.TareaId).OnDelete(DeleteBehavior.SetNull);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.TenantId, x.Codigo }).IsUnique();  // RN-15
            b.HasIndex(x => new { x.TenantId, x.ActivoTipo, x.ActivoId });
            b.HasIndex(x => new { x.TenantId, x.Estado });
            b.HasIndex(x => new { x.TenantId, x.FechaProgramada });
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<MantenimientoBitacora>(b =>
        {
            b.ToTable("mantenimiento_bitacora");
            b.Property(x => x.Contenido).IsRequired().HasColumnType("text");
            b.Property(x => x.TipoAutor).HasConversion<int>();
            b.HasOne(x => x.Intervencion).WithMany(i => i.Bitacora).HasForeignKey(x => x.IntervencionId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.TenantId, x.IntervencionId, x.CreatedAt });
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<MantenimientoAdjunto>(b =>
        {
            b.ToTable("mantenimiento_adjuntos");
            b.Property(x => x.NombreArchivo).IsRequired().HasMaxLength(500);
            b.Property(x => x.TipoMime).IsRequired().HasMaxLength(100);
            b.Property(x => x.UrlStorage).IsRequired().HasMaxLength(1000);
            b.HasOne(x => x.Bitacora).WithMany(bi => bi.Adjuntos).HasForeignKey(x => x.BitacoraId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => x.BitacoraId);
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<MantenimientoHistorialEstado>(b =>
        {
            b.ToTable("mantenimiento_historial_estado");
            b.Property(x => x.EstadoAnterior).IsRequired().HasMaxLength(50);
            b.Property(x => x.EstadoNuevo).IsRequired().HasMaxLength(50);
            b.Property(x => x.Motivo).HasColumnType("text");
            b.Property(x => x.ActivoTipo).HasConversion<int>();
            b.HasOne(x => x.Intervencion).WithMany().HasForeignKey(x => x.IntervencionId).OnDelete(DeleteBehavior.SetNull);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.TenantId, x.ActivoTipo, x.ActivoId, x.CreatedAt });
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        // -------------------- Modulo 2.14 Comunicaciones --------------------

        modelBuilder.Entity<ComunicadoPlantilla>(b =>
        {
            b.ToTable("comunicado_plantillas");
            b.Property(x => x.Nombre).IsRequired().HasMaxLength(150);
            b.Property(x => x.AsuntoModelo).IsRequired().HasMaxLength(150);
            b.Property(x => x.CuerpoModelo).IsRequired().HasColumnType("text");
            b.Property(x => x.TipoComunicado).HasConversion<int>();
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.TenantId, x.Nombre });
            // Query filter: globales (TenantId null) son visibles para todos; tenant solo ve los suyos.
            b.HasQueryFilter(x => x.TenantId == null
                                  || _tenantContext.CurrentTenantId == null
                                  || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<Comunicado>(b =>
        {
            b.ToTable("comunicados");
            b.Property(x => x.Asunto).IsRequired().HasMaxLength(150);
            b.Property(x => x.CuerpoHtml).IsRequired().HasColumnType("text");
            b.Property(x => x.CuerpoTextoPlano).IsRequired().HasColumnType("text");
            b.Property(x => x.TipoComunicado).HasConversion<int>();
            b.Property(x => x.Estado).HasConversion<int>();
            b.HasOne(x => x.Plantilla).WithMany().HasForeignKey(x => x.PlantillaId).OnDelete(DeleteBehavior.SetNull);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.TenantId, x.Estado });
            b.HasIndex(x => new { x.TenantId, x.FechaProgramada });
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<ComunicadoSegmento>(b =>
        {
            b.ToTable("comunicado_segmentos");
            b.Property(x => x.TipoSegmento).HasConversion<int>();
            b.Property(x => x.ValorJson).IsRequired().HasColumnType("text");
            b.HasOne(x => x.Comunicado).WithMany(c => c.Segmentos).HasForeignKey(x => x.ComunicadoId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => x.ComunicadoId);
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<ComunicadoAdjunto>(b =>
        {
            b.ToTable("comunicado_adjuntos");
            b.Property(x => x.NombreArchivo).IsRequired().HasMaxLength(255);
            b.Property(x => x.TipoMime).IsRequired().HasMaxLength(100);
            b.Property(x => x.UrlStorage).IsRequired().HasMaxLength(1000);
            b.HasOne(x => x.Comunicado).WithMany(c => c.Adjuntos).HasForeignKey(x => x.ComunicadoId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => x.ComunicadoId);
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<ComunicadoDestinatario>(b =>
        {
            b.ToTable("comunicado_destinatarios");
            b.Property(x => x.EstadoEntrega).HasConversion<int>();
            b.HasOne(x => x.Comunicado).WithMany(c => c.Destinatarios).HasForeignKey(x => x.ComunicadoId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.Persona).WithMany().HasForeignKey(x => x.PersonaId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => x.Token).IsUnique();  // RN-08
            b.HasIndex(x => new { x.ComunicadoId, x.PersonaId }).IsUnique();  // RN-03 deduplicacion
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<ComunicadoAcuse>(b =>
        {
            b.ToTable("comunicado_acuses");
            b.Property(x => x.Dispositivo).HasConversion<int>();
            b.HasOne(x => x.Comunicado).WithMany().HasForeignKey(x => x.ComunicadoId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.Destinatario).WithMany(d => d.Acuses).HasForeignKey(x => x.DestinatarioId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.ComunicadoId, x.PersonaId });
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

    }
}
