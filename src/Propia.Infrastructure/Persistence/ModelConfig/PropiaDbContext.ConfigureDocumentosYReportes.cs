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
    // Modulos 2.15 Documentos (Expedientes TRD + Archivo Digital) y 2.16 Reportes e Indicadores.
    private void ConfigureDocumentosYReportes(ModelBuilder modelBuilder)
    {
        // -------------------- Modulo 2.15 Documentos - vista Expedientes (TRD) --------------------

        modelBuilder.Entity<SerieDocumental>(b =>
        {
            b.ToTable("series_documentales");
            b.Property(x => x.Nombre).IsRequired().HasMaxLength(150);
            b.HasIndex(x => new { x.TenantId, x.Orden });
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<SubserieDocumental>(b =>
        {
            b.ToTable("subseries_documentales");
            b.Property(x => x.Nombre).IsRequired().HasMaxLength(150);
            b.HasOne(x => x.Serie).WithMany(s => s.Subseries).HasForeignKey(x => x.SerieId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => new { x.TenantId, x.SerieId, x.Orden });
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<SubserieTipologia>(b =>
        {
            b.ToTable("subserie_tipologias");
            b.Property(x => x.Nombre).IsRequired().HasMaxLength(150);
            b.HasOne(x => x.Subserie).WithMany(s => s.Tipologias).HasForeignKey(x => x.SubserieId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => new { x.TenantId, x.SubserieId, x.Orden });
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<SubserieCampo>(b =>
        {
            b.ToTable("subserie_campos");
            b.Property(x => x.Clave).IsRequired().HasMaxLength(80);
            b.Property(x => x.Label).IsRequired().HasMaxLength(150);
            b.HasOne(x => x.Subserie).WithMany(s => s.Campos).HasForeignKey(x => x.SubserieId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => new { x.TenantId, x.SubserieId, x.Orden });
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<Expediente>(b =>
        {
            b.ToTable("expedientes");
            b.Property(x => x.Codigo).IsRequired().HasMaxLength(40);
            b.Property(x => x.Nombre).IsRequired().HasMaxLength(200);
            b.Property(x => x.Serie).IsRequired().HasMaxLength(150);
            b.Property(x => x.Subserie).IsRequired().HasMaxLength(150);
            b.HasIndex(x => new { x.TenantId, x.Codigo });
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<ExpedienteTipologia>(b =>
        {
            b.ToTable("expediente_tipologias");
            b.Property(x => x.Nombre).IsRequired().HasMaxLength(150);
            b.Property(x => x.ArchivoUrl).HasMaxLength(1024);
            b.Property(x => x.ArchivoNombre).HasMaxLength(255);
            b.Property(x => x.ArchivoMime).HasMaxLength(150);
            b.Property(x => x.MetaJson).HasColumnType("text");
            b.HasOne(x => x.Expediente).WithMany(e => e.Tipologias).HasForeignKey(x => x.ExpedienteId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => new { x.TenantId, x.ExpedienteId, x.Orden });
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<ExpedienteTipologiaVersion>(b =>
        {
            b.ToTable("expediente_tipologia_versiones");
            b.Property(x => x.ArchivoUrl).IsRequired().HasMaxLength(1024);
            b.Property(x => x.ArchivoNombre).IsRequired().HasMaxLength(255);
            b.Property(x => x.ArchivoMime).IsRequired().HasMaxLength(150);
            b.Property(x => x.NotasCambio).HasColumnType("text");
            b.HasOne(x => x.Tipologia).WithMany(t => t.Versiones).HasForeignKey(x => x.ExpedienteTipologiaId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => new { x.ExpedienteTipologiaId, x.Numero }).IsUnique();
            b.HasIndex(x => x.TenantId);
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<ExpedienteCampo>(b =>
        {
            b.ToTable("expediente_campos");
            b.Property(x => x.Clave).IsRequired().HasMaxLength(80);
            b.Property(x => x.Label).IsRequired().HasMaxLength(150);
            b.HasOne(x => x.Expediente).WithMany(e => e.Campos).HasForeignKey(x => x.ExpedienteId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => new { x.TenantId, x.ExpedienteId, x.Orden });
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        // -------------------- Modulo 2.15 Documentos y Archivo Digital --------------------

        modelBuilder.Entity<DocumentoCategoria>(b =>
        {
            b.ToTable("documento_categorias");
            b.Property(x => x.Nombre).IsRequired().HasMaxLength(120);
            b.Property(x => x.Descripcion).HasMaxLength(500);
            b.Property(x => x.Icono).HasMaxLength(80);
            b.Property(x => x.Color).HasMaxLength(20);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.TenantId, x.Nombre });
            // Mezcla base (TenantId null) + tenant - mismo patron que ComunicadoPlantilla.
            b.HasQueryFilter(x => x.TenantId == null
                                  || _tenantContext.CurrentTenantId == null
                                  || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<DocumentoCarpeta>(b =>
        {
            b.ToTable("documento_carpetas");
            b.Property(x => x.Nombre).IsRequired().HasMaxLength(120);
            b.Property(x => x.Descripcion).HasMaxLength(500);
            b.HasOne(x => x.Categoria).WithMany().HasForeignKey(x => x.CategoriaId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.Padre).WithMany().HasForeignKey(x => x.PadreId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.TenantId, x.CategoriaId });
            b.HasIndex(x => new { x.TenantId, x.PadreId });
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<Documento>(b =>
        {
            b.ToTable("documentos");
            b.Property(x => x.Titulo).IsRequired().HasMaxLength(200);
            b.Property(x => x.Descripcion).HasMaxLength(1000);
            b.Property(x => x.NombreArchivoOriginal).IsRequired().HasMaxLength(255);
            b.Property(x => x.Visibilidad).IsRequired().HasMaxLength(20);
            b.Property(x => x.Origen).HasConversion<int>();
            b.Property(x => x.Estado).HasConversion<int>();
            b.HasOne(x => x.Categoria).WithMany().HasForeignKey(x => x.CategoriaId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.Carpeta).WithMany().HasForeignKey(x => x.CarpetaId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.VersionActual).WithMany().HasForeignKey(x => x.VersionActualId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.TenantId, x.CategoriaId });
            b.HasIndex(x => new { x.TenantId, x.CarpetaId });
            b.HasIndex(x => new { x.TenantId, x.Estado });
            b.HasIndex(x => new { x.TenantId, x.Origen, x.OrigenEntidadId });
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<DocumentoVersion>(b =>
        {
            b.ToTable("documento_versiones");
            b.Property(x => x.NombreArchivo).IsRequired().HasMaxLength(255);
            b.Property(x => x.TipoMime).IsRequired().HasMaxLength(100);
            b.Property(x => x.UrlStorage).IsRequired().HasMaxLength(1000);
            b.Property(x => x.HashSha256).IsRequired().HasMaxLength(64);
            b.Property(x => x.NotasCambio).HasMaxLength(1000);
            b.HasOne(x => x.Documento).WithMany(d => d.Versiones).HasForeignKey(x => x.DocumentoId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.DocumentoId, x.Numero }).IsUnique();
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<DocumentoEtiquetaCatalogo>(b =>
        {
            b.ToTable("documento_etiquetas_catalogo");
            b.Property(x => x.Nombre).IsRequired().HasMaxLength(80);
            b.Property(x => x.Color).HasMaxLength(20);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.TenantId, x.Nombre });
            b.HasQueryFilter(x => x.TenantId == null
                                  || _tenantContext.CurrentTenantId == null
                                  || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<DocumentoEtiqueta>(b =>
        {
            b.ToTable("documento_etiqueta_asignaciones");
            b.HasOne(x => x.Documento).WithMany(d => d.Etiquetas).HasForeignKey(x => x.DocumentoId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.EtiquetaCatalogo).WithMany().HasForeignKey(x => x.EtiquetaCatalogoId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.DocumentoId, x.EtiquetaCatalogoId }).IsUnique();
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<DocumentoDestacadoPersonal>(b =>
        {
            b.ToTable("documento_destacados_personal");
            b.HasOne(x => x.Documento).WithMany().HasForeignKey(x => x.DocumentoId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.DocumentoId, x.UsuarioId }).IsUnique();
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<DocumentoAuditoria>(b =>
        {
            b.ToTable("documento_auditoria");
            b.Property(x => x.TipoEvento).HasConversion<int>();
            b.Property(x => x.DetalleJson).HasColumnType("text");
            b.HasOne(x => x.Documento).WithMany().HasForeignKey(x => x.DocumentoId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.DocumentoId, x.OcurridoAt });
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<DocumentoConsumo>(b =>
        {
            b.ToTable("documento_consumo");
            b.Property(x => x.TipoEvento).HasConversion<int>();
            b.Property(x => x.Dispositivo).HasConversion<int>();
            b.HasOne(x => x.Documento).WithMany().HasForeignKey(x => x.DocumentoId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.Version).WithMany().HasForeignKey(x => x.VersionId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.DocumentoId, x.OcurridoAt });
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        // -------------------- Modulo 2.16 Reportes e Indicadores --------------------

        modelBuilder.Entity<ReporteCategoria>(b =>
        {
            b.ToTable("reporte_categorias");
            b.Property(x => x.Nombre).IsRequired().HasMaxLength(100);
            b.Property(x => x.Icono).HasMaxLength(80);
            b.Property(x => x.Color).HasMaxLength(20);
            b.Property(x => x.ModuloOrigen).IsRequired().HasMaxLength(20);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.TenantId, x.Nombre });
            // Mezcla base (TenantId null) + tenant.
            b.HasQueryFilter(x => x.TenantId == null
                                  || _tenantContext.CurrentTenantId == null
                                  || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<ReporteCatalogo>(b =>
        {
            b.ToTable("reporte_catalogo");
            b.Property(x => x.Nombre).IsRequired().HasMaxLength(150);
            b.Property(x => x.Descripcion).HasMaxLength(500);
            b.Property(x => x.ModuloOrigen).IsRequired().HasMaxLength(20);
            b.Property(x => x.Clave).IsRequired().HasMaxLength(120);
            b.Property(x => x.AudienciasJson).IsRequired().HasColumnType("text");
            b.Property(x => x.FiltrosConfigJson).HasColumnType("text");
            b.HasOne(x => x.Categoria).WithMany().HasForeignKey(x => x.CategoriaId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.TenantId, x.Clave });
            b.HasIndex(x => x.CategoriaId);
            b.HasQueryFilter(x => x.TenantId == null
                                  || _tenantContext.CurrentTenantId == null
                                  || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<ReporteGenerado>(b =>
        {
            b.ToTable("reporte_generados");
            b.Property(x => x.NombreReporte).IsRequired().HasMaxLength(200);
            b.Property(x => x.Categoria).IsRequired().HasMaxLength(100);
            b.Property(x => x.Origen).HasConversion<int>();
            b.Property(x => x.Estado).HasConversion<int>();
            b.Property(x => x.PromptIa).HasColumnType("text");
            b.Property(x => x.FiltrosAplicadosJson).HasColumnType("text");
            b.Property(x => x.ResultadoJson).HasColumnType("text");
            b.Property(x => x.ErrorMensaje).HasMaxLength(2000);
            b.Property(x => x.UrlPdf).HasMaxLength(1000);
            b.Property(x => x.UrlExcel).HasMaxLength(1000);
            b.HasOne(x => x.ReporteCatalogo).WithMany().HasForeignKey(x => x.ReporteCatalogoId).OnDelete(DeleteBehavior.SetNull);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.TenantId, x.Estado });
            b.HasIndex(x => new { x.TenantId, x.CompartidoConsejo });
            b.HasIndex(x => new { x.TenantId, x.PeriodoInicio, x.PeriodoFin });
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<ReporteProgramacion>(b =>
        {
            b.ToTable("reporte_programaciones");
            b.Property(x => x.Nombre).HasMaxLength(200);
            b.Property(x => x.Frecuencia).HasConversion<int>();
            b.Property(x => x.PeriodoQueCubre).HasConversion<int>();
            b.Property(x => x.Formato).HasConversion<int>();
            b.Property(x => x.Estado).HasConversion<int>();
            b.Property(x => x.CanalesJson).IsRequired().HasColumnType("text");
            b.Property(x => x.FiltrosAplicadosJson).HasColumnType("text");
            b.HasOne(x => x.ReporteCatalogo).WithMany().HasForeignKey(x => x.ReporteCatalogoId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.TenantId, x.Estado });
            b.HasIndex(x => x.ProximoEnvio);
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<ReporteProgramacionDestinatario>(b =>
        {
            b.ToTable("reporte_programacion_destinatarios");
            b.Property(x => x.EmailExterno).HasMaxLength(200);
            b.Property(x => x.WhatsappExterno).HasMaxLength(30);
            b.HasOne(x => x.Programacion).WithMany(p => p.Destinatarios)
                .HasForeignKey(x => x.ProgramacionId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.Persona).WithMany().HasForeignKey(x => x.PersonaId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => x.ProgramacionId);
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<ReporteSemaforoConfig>(b =>
        {
            b.ToTable("reporte_semaforo_config");
            b.Property(x => x.IndicadorKey).IsRequired().HasMaxLength(100);
            b.Property(x => x.UmbralAmarillo).HasColumnType("numeric(18,2)");
            b.Property(x => x.UmbralRojo).HasColumnType("numeric(18,2)");
            b.HasIndex(x => new { x.TenantId, x.IndicadorKey }).IsUnique();
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

    }
}
