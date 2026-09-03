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
    // Modulo 2.3 Mi Copropiedad + Seguros + Informes + Servicios/Contratos + Programador + 2.17 Servicios publicos + tipos/comites/equipo de trabajo.
    private void ConfigureMiCopropiedad(ModelBuilder modelBuilder)
    {
        // -------------------- Modulo 2.3 Mi Copropiedad --------------------

        modelBuilder.Entity<Tenant>(b =>
        {
            b.Property(x => x.Ciudad).HasMaxLength(100);
            b.Property(x => x.Departamento).HasMaxLength(100);
            b.Property(x => x.DigitoVerificacion).HasMaxLength(2);
            b.Property(x => x.FotoFachadaUrl).HasMaxLength(500);
            b.Property(x => x.LogoUrl).HasMaxLength(500);
            b.Property(x => x.Descripcion).HasMaxLength(2000);
            b.Property(x => x.TelefonoContacto).HasMaxLength(50);
            b.Property(x => x.EmailContacto).HasMaxLength(200);
            // Identidad registral (modulo 2.3 spec v1.0)
            b.Property(x => x.NumeroReglamentoPh).HasMaxLength(100);
            b.Property(x => x.NotariaRegistro).HasMaxLength(200);
            b.Property(x => x.MatriculaInmobiliaria).HasMaxLength(50);
            b.Property(x => x.LicenciaConstruccion).HasMaxLength(50);
            b.Property(x => x.CertificadoMayorExtension).HasMaxLength(100);
            // Labels personalizables (spec v1.0 - "Sector"/"Planta")
            b.Property(x => x.LabelAgrupacion).HasMaxLength(30);
            b.Property(x => x.LabelPiso).HasMaxLength(30);
            // Parametros financieros (seccion 8)
            b.Property(x => x.Moneda).HasMaxLength(3).HasDefaultValue("COP");
            b.Property(x => x.DiaCorte).HasDefaultValue(1);
            b.Property(x => x.TasaMoraEsLegal).HasDefaultValue(true);
            b.Property(x => x.TasaMoraValor).HasPrecision(6, 4);
        });

        modelBuilder.Entity<Torre>(b =>
        {
            b.Property(x => x.Nombre).IsRequired().HasMaxLength(100);
            b.Property(x => x.Descripcion).HasMaxLength(500);
            b.HasIndex(x => x.TenantId);
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<UnidadPrivada>(b =>
        {
            b.Property(x => x.Numero).IsRequired().HasMaxLength(20);
            b.Property(x => x.Estado).HasMaxLength(50);
            b.Property(x => x.Observaciones).HasMaxLength(1000);
            b.Property(x => x.MatriculaInmobiliaria).HasMaxLength(50);
            b.Property(x => x.CoeficientePropiedad).HasPrecision(7, 4);
            b.Property(x => x.AreaM2).HasPrecision(10, 2);
            b.Property(x => x.CuotaMensual).HasPrecision(14, 2);
            b.HasOne(x => x.Torre).WithMany().HasForeignKey(x => x.TorreId).OnDelete(DeleteBehavior.SetNull);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.TenantId, x.Numero }).IsUnique();
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<BitacoraMiCopropiedad>(b =>
        {
            b.Property(x => x.Categoria).IsRequired().HasMaxLength(50);
            b.Property(x => x.Descripcion).IsRequired().HasMaxLength(1000);
            b.Property(x => x.Autor).HasMaxLength(200);
            b.HasIndex(x => x.TenantId);
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<UnidadVinculo>(b =>
        {
            b.HasOne(x => x.UnidadPrincipal).WithMany().HasForeignKey(x => x.UnidadPrincipalId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.UnidadAsociada).WithMany().HasForeignKey(x => x.UnidadAsociadaId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.TenantId, x.UnidadAsociadaId }).IsUnique();  // una asociada tiene un solo principal
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<UnidadPersona>(b =>
        {
            b.HasOne(x => x.Unidad).WithMany().HasForeignKey(x => x.UnidadId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => x.TenantId);
            // Antiduplicado por tipo de entidad: se implementan como indices PARCIALES en la migracion
            // (uno WHERE persona_id IS NOT NULL, otro WHERE empresa_id IS NOT NULL). No se declara
            // aqui el unique compuesto porque con persona_id/empresa_id nullable Postgres trata los
            // NULL como distintos y no protegeria las filas de empresa.
            b.HasIndex(x => new { x.TenantId, x.UnidadId, x.EmpresaId, x.Rol });
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<UnidadCampoDefinicion>(b =>
        {
            b.Property(x => x.Label).IsRequired().HasMaxLength(80);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.TenantId, x.Label }).IsUnique();
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<UnidadCampoValor>(b =>
        {
            b.HasOne(x => x.Definicion).WithMany().HasForeignKey(x => x.DefinicionId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.Unidad).WithMany().HasForeignKey(x => x.UnidadId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.DefinicionId, x.UnidadId }).IsUnique();
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<UnidadDocumento>(b =>
        {
            b.Property(x => x.Nombre).IsRequired().HasMaxLength(255);
            b.Property(x => x.Url).IsRequired();
            b.HasOne(x => x.Unidad).WithMany().HasForeignKey(x => x.UnidadId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => x.UnidadId);
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        // Prototipo v3 - bloques nuevos de la ficha de inmueble (placas, arriendos, mascotas, empleadas)
        modelBuilder.Entity<UnidadPlaca>(b =>
        {
            b.Property(x => x.Placa).IsRequired().HasMaxLength(15);
            b.HasOne(x => x.Unidad).WithMany().HasForeignKey(x => x.UnidadId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => x.UnidadId);
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<UnidadArriendo>(b =>
        {
            b.Property(x => x.Concepto).IsRequired().HasMaxLength(120);
            b.Property(x => x.Referencia).HasMaxLength(120);
            b.Property(x => x.ValorMensual).HasPrecision(14, 2);
            b.HasOne(x => x.Unidad).WithMany().HasForeignKey(x => x.UnidadId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => x.UnidadId);
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<UnidadMascota>(b =>
        {
            b.Property(x => x.Nombre).IsRequired().HasMaxLength(80);
            b.Property(x => x.Raza).HasMaxLength(80);
            b.HasOne(x => x.Unidad).WithMany().HasForeignKey(x => x.UnidadId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => x.UnidadId);
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<UnidadEmpleada>(b =>
        {
            b.Property(x => x.Nombre).IsRequired().HasMaxLength(120);
            b.Property(x => x.Documento).HasMaxLength(40);
            b.Property(x => x.Celular).HasMaxLength(40);
            b.Property(x => x.Horario).HasMaxLength(120);
            b.HasOne(x => x.Unidad).WithMany().HasForeignKey(x => x.UnidadId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => x.UnidadId);
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        // Prototipo v3 Oleada 2 - historico de titularidad + campos dinamicos por persona
        modelBuilder.Entity<UnidadTitularidad>(b =>
        {
            b.Property(x => x.Nombre).IsRequired().HasMaxLength(160);
            b.Property(x => x.Rol).HasMaxLength(40);
            b.HasOne(x => x.Unidad).WithMany().HasForeignKey(x => x.UnidadId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => x.UnidadId);
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<UnidadPersonaCampo>(b =>
        {
            b.Property(x => x.Label).IsRequired().HasMaxLength(80);
            b.Property(x => x.Valor).HasMaxLength(400);
            b.HasOne(x => x.UnidadPersona).WithMany().HasForeignKey(x => x.UnidadPersonaId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => x.UnidadPersonaId);
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<ZonaComun>(b =>
        {
            b.Property(x => x.Nombre).IsRequired().HasMaxLength(100);
            b.Property(x => x.Descripcion).HasMaxLength(500);
            b.Property(x => x.HorariosUso).HasMaxLength(200);
            b.Property(x => x.ReglasUso).HasMaxLength(2000);
            b.Property(x => x.TarifaReserva).HasPrecision(12, 2);
            b.HasIndex(x => x.TenantId);
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<EquipoActivo>(b =>
        {
            b.Property(x => x.Nombre).IsRequired().HasMaxLength(150);
            b.Property(x => x.Marca).HasMaxLength(100);
            b.Property(x => x.Modelo).HasMaxLength(100);
            b.Property(x => x.NumeroSerie).HasMaxLength(100);
            b.Property(x => x.CodigoBarra).HasMaxLength(100);
            b.Property(x => x.CondicionesUso).HasMaxLength(2000);
            b.Property(x => x.Ubicacion).HasMaxLength(200);
            b.Property(x => x.Observaciones).HasMaxLength(1000);
            b.HasIndex(x => x.TenantId);
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<ContratoServicio>(b =>
        {
            b.Property(x => x.Proveedor).IsRequired().HasMaxLength(200);
            b.Property(x => x.NitProveedor).HasMaxLength(20);
            b.Property(x => x.Contacto).HasMaxLength(200);
            b.Property(x => x.Observaciones).HasMaxLength(1000);
            b.Property(x => x.ValorMensual).HasPrecision(12, 2);
            b.Property(x => x.Estado).HasDefaultValue(EstadoContrato.Vigente);
            b.Property(x => x.DiasAnticipacionAlerta).HasDefaultValue(30);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => x.FechaFin);
            b.HasIndex(x => x.ServicioId);
            b.HasMany(x => x.Adjuntos).WithOne(a => a.Contrato!).HasForeignKey(a => a.ContratoId).OnDelete(DeleteBehavior.Cascade);
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        // Campos personalizados (EAV) de los contratos
        modelBuilder.Entity<ContratoCampo>(b =>
        {
            b.Property(x => x.Label).IsRequired().HasMaxLength(120);
            b.Property(x => x.Opciones).HasMaxLength(2000);
            b.Property(x => x.Descripcion).HasMaxLength(500);
            b.Property(x => x.Activo).HasDefaultValue(true);
            b.HasIndex(x => x.TenantId);
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });
        modelBuilder.Entity<ContratoCampoValor>(b =>
        {
            b.Property(x => x.Valor).HasMaxLength(4000);
            b.HasIndex(x => new { x.TenantId, x.ContratoId });
            b.HasIndex(x => new { x.ContratoId, x.ContratoCampoId }).IsUnique();
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });
        modelBuilder.Entity<ContratoEtapa>(b =>
        {
            b.Property(x => x.Nombre).IsRequired().HasMaxLength(80);
            b.Property(x => x.Color).HasMaxLength(9);
            b.HasIndex(x => x.TenantId);
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });
        modelBuilder.Entity<ContratoExpediente>(b =>
        {
            b.HasIndex(x => new { x.TenantId, x.ContratoId });
            b.HasIndex(x => new { x.ContratoId, x.ExpedienteId }).IsUnique();
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        // ----- Modulo Seguros (Ola 4): Polizas -----
        modelBuilder.Entity<Poliza>(b =>
        {
            b.Property(x => x.NumeroPoliza).HasMaxLength(80);
            b.Property(x => x.Aseguradora).IsRequired().HasMaxLength(200);
            b.Property(x => x.Corredor).HasMaxLength(200);
            b.Property(x => x.ValorPoliza).HasPrecision(14, 2);
            b.Property(x => x.Cobertura).HasMaxLength(4000);
            b.Property(x => x.ValoresAgregados).HasMaxLength(4000);
            b.Property(x => x.Observaciones).HasMaxLength(2000);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => x.FechaFin);
            b.HasMany(x => x.Reclamaciones).WithOne(r => r.Poliza!).HasForeignKey(r => r.PolizaId).OnDelete(DeleteBehavior.Cascade);
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });
        modelBuilder.Entity<PolizaCampo>(b =>
        {
            b.Property(x => x.Label).IsRequired().HasMaxLength(120);
            b.Property(x => x.Opciones).HasMaxLength(2000);
            b.Property(x => x.Descripcion).HasMaxLength(500);
            b.Property(x => x.Activo).HasDefaultValue(true);
            b.HasIndex(x => x.TenantId);
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });
        modelBuilder.Entity<PolizaCampoValor>(b =>
        {
            b.Property(x => x.Valor).HasMaxLength(4000);
            b.HasIndex(x => new { x.TenantId, x.PolizaId });
            b.HasIndex(x => new { x.PolizaId, x.PolizaCampoId }).IsUnique();
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });
        modelBuilder.Entity<PolizaReclamacion>(b =>
        {
            b.Property(x => x.Descripcion).IsRequired().HasMaxLength(2000);
            b.Property(x => x.MontoReclamado).HasPrecision(14, 2);
            b.Property(x => x.MontoReconocido).HasPrecision(14, 2);
            b.HasIndex(x => new { x.TenantId, x.PolizaId });
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        // Informes de gestion (plantillas inteligentes + generacion IA)
        modelBuilder.Entity<InformePlantilla>(b =>
        {
            b.Property(x => x.Nombre).IsRequired().HasMaxLength(160);
            b.Property(x => x.Descripcion).HasMaxLength(600);
            b.HasIndex(x => x.TenantId);
            b.HasMany(x => x.Secciones).WithOne(s => s.Plantilla!).HasForeignKey(s => s.PlantillaId).OnDelete(DeleteBehavior.Cascade);
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });
        modelBuilder.Entity<InformePlantillaSeccion>(b =>
        {
            b.Property(x => x.Titulo).IsRequired().HasMaxLength(200);
            b.Property(x => x.Prompt).HasMaxLength(4000);
            b.HasIndex(x => new { x.TenantId, x.PlantillaId });
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });
        modelBuilder.Entity<Informe>(b =>
        {
            b.Property(x => x.Titulo).IsRequired().HasMaxLength(200);
            b.Property(x => x.Periodo).HasMaxLength(120);
            b.Property(x => x.Estado).HasDefaultValue(EstadoInforme.Borrador);
            b.HasIndex(x => x.TenantId);
            b.HasMany(x => x.Secciones).WithOne(s => s.Informe!).HasForeignKey(s => s.InformeId).OnDelete(DeleteBehavior.Cascade);
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });
        modelBuilder.Entity<InformeSeccion>(b =>
        {
            b.Property(x => x.Titulo).IsRequired().HasMaxLength(200);
            b.Property(x => x.Prompt).HasMaxLength(4000);
            b.HasIndex(x => new { x.TenantId, x.InformeId });
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        // Servicios y contratos (Finanzas)
        modelBuilder.Entity<Servicio>(b =>
        {
            b.Property(x => x.Nombre).IsRequired().HasMaxLength(200);
            b.Property(x => x.Descripcion).HasMaxLength(1000);
            b.Property(x => x.EjecutorNombre).HasMaxLength(200);
            b.Property(x => x.CostoMensual).HasPrecision(14, 2);
            b.Property(x => x.CostoAnual).HasPrecision(14, 2);
            b.Property(x => x.Estado).HasDefaultValue(EstadoServicio.Activo);
            b.HasIndex(x => x.TenantId);
            b.HasMany(x => x.Contactos).WithOne(c => c.Servicio!).HasForeignKey(c => c.ServicioId).OnDelete(DeleteBehavior.Cascade);
            b.HasMany(x => x.Adjuntos).WithOne(a => a.Servicio!).HasForeignKey(a => a.ServicioId).OnDelete(DeleteBehavior.Cascade);
            b.HasMany(x => x.Contratos).WithOne(c => c.Servicio!).HasForeignKey(c => c.ServicioId).OnDelete(DeleteBehavior.SetNull);
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });
        modelBuilder.Entity<ServicioContacto>(b =>
        {
            b.Property(x => x.NombreSnapshot).IsRequired().HasMaxLength(200);
            b.Property(x => x.Rol).HasMaxLength(80);
            b.Property(x => x.Telefono).HasMaxLength(40);
            b.Property(x => x.Email).HasMaxLength(160);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => x.ServicioId);
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });
        modelBuilder.Entity<ServicioAdjunto>(b =>
        {
            b.Property(x => x.NombreArchivo).IsRequired().HasMaxLength(255);
            b.Property(x => x.TipoMime).HasMaxLength(120);
            b.Property(x => x.UrlStorage).IsRequired().HasMaxLength(500);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => x.ServicioId);
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });
        modelBuilder.Entity<ContratoAdjunto>(b =>
        {
            b.Property(x => x.NombreArchivo).IsRequired().HasMaxLength(255);
            b.Property(x => x.TipoMime).HasMaxLength(120);
            b.Property(x => x.UrlStorage).IsRequired().HasMaxLength(500);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => x.ContratoId);
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        // Muro de novedades generico (nacio en zonas comunes, hoy sirve para cualquier entidad).
        modelBuilder.Entity<Novedad>(b =>
        {
            b.HasIndex(x => new { x.EntidadTipo, x.EntidadId });
        });
        modelBuilder.Entity<NovedadComentario>(b => b.HasIndex(x => x.NovedadId));
        modelBuilder.Entity<NovedadLike>(b => b.HasIndex(x => new { x.NovedadId, x.PersonaId }));

        // Programador de tareas (2.10)
        modelBuilder.Entity<ProgramacionTarea>(b =>
        {
            b.Property(x => x.Titulo).IsRequired().HasMaxLength(200);
            b.Property(x => x.Descripcion).HasMaxLength(2000);
            b.Property(x => x.ModuloOrigenCodigo).HasMaxLength(40);
            b.Property(x => x.OrigenReferencia).HasMaxLength(200);
            b.Property(x => x.CronExpresion).HasMaxLength(120);
            b.Property(x => x.ZonaHoraria).IsRequired().HasMaxLength(60).HasDefaultValue("America/Bogota");
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.Activa, x.FechaProximaEjecucion });
            // El job barre por esta columna en modo cron; sin indice haria seq scan cada 15 min.
            b.HasIndex(x => new { x.Activa, x.ProximaEjecucionUtc });
            b.HasMany(x => x.Responsables).WithOne(r => r.ProgramacionTarea!).HasForeignKey(r => r.ProgramacionTareaId).OnDelete(DeleteBehavior.Cascade);
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });
        modelBuilder.Entity<ProgramacionTareaResponsable>(b =>
        {
            b.Property(x => x.NombreSnapshot).HasMaxLength(200);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => x.ProgramacionTareaId);
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        // Modulo 2.17 Servicios publicos
        modelBuilder.Entity<CuentaServicioPublico>(b =>
        {
            b.Property(x => x.Alias).IsRequired().HasMaxLength(120);
            b.Property(x => x.Prestador).HasMaxLength(160);
            b.Property(x => x.NumeroCuenta).HasMaxLength(80);
            b.Property(x => x.MetodoPago).HasMaxLength(80);
            b.Property(x => x.UnidadMedida).HasMaxLength(20);
            b.Property(x => x.UmbralAlertaPct).HasDefaultValue(25);
            b.HasIndex(x => x.TenantId);
            b.HasMany(x => x.Registros).WithOne(r => r.Cuenta!).HasForeignKey(r => r.CuentaServicioId).OnDelete(DeleteBehavior.Cascade);
            b.HasMany(x => x.Reclamaciones).WithOne(r => r.Cuenta!).HasForeignKey(r => r.CuentaServicioId).OnDelete(DeleteBehavior.Cascade);
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });
        modelBuilder.Entity<RegistroConsumoServicio>(b =>
        {
            b.Property(x => x.Consumo).HasPrecision(14, 2);
            b.Property(x => x.Valor).HasPrecision(14, 2);
            b.Property(x => x.NotaAdmin).HasMaxLength(500);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.CuentaServicioId, x.Anio, x.Mes });
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });
        modelBuilder.Entity<ReclamacionServicio>(b =>
        {
            b.Property(x => x.Motivo).IsRequired().HasMaxLength(160);
            b.Property(x => x.Radicado).HasMaxLength(80);
            b.Property(x => x.Descripcion).HasMaxLength(1000);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => x.CuentaServicioId);
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<MiembroConsejo>(b =>
        {
            b.HasOne(x => x.Persona).WithMany().HasForeignKey(x => x.PersonaId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.TenantId, x.Cargo });
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        // Tipo unidad custom (spec 2.3 - tipos personalizables por copropiedad)
        modelBuilder.Entity<TipoUnidadCustom>(b =>
        {
            b.Property(x => x.Nombre).IsRequired().HasMaxLength(100);
            b.Property(x => x.Descripcion).HasMaxLength(500);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.TenantId, x.Nombre }).IsUnique();
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        // Tipos de coeficiente PH (spec 2.3 - RN-02 multiples tipos por copropiedad)
        modelBuilder.Entity<TipoCoeficiente>(b =>
        {
            b.Property(x => x.Nombre).IsRequired().HasMaxLength(100);
            b.Property(x => x.Descripcion).HasMaxLength(500);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.TenantId, x.Nombre }).IsUnique();
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        // Valores de coeficientes por unidad (relacion M-N + Valor)
        modelBuilder.Entity<UnidadCoeficiente>(b =>
        {
            b.Property(x => x.Valor).HasPrecision(9, 6);
            b.HasOne(x => x.Unidad).WithMany().HasForeignKey(x => x.UnidadId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.TipoCoeficiente).WithMany().HasForeignKey(x => x.TipoCoeficienteId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.UnidadId, x.TipoCoeficienteId }).IsUnique();
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        // Comites y miembros (spec 2.3 - seccion 4)
        modelBuilder.Entity<Comite>(b =>
        {
            b.Property(x => x.Nombre).IsRequired().HasMaxLength(100);
            b.Property(x => x.Descripcion).HasMaxLength(500);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.TenantId, x.Nombre }).IsUnique();
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<ComiteMiembro>(b =>
        {
            b.Property(x => x.CargoEnComite).HasMaxLength(80);
            b.HasOne(x => x.Comite).WithMany(c => c.Miembros).HasForeignKey(x => x.ComiteId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.Persona).WithMany().HasForeignKey(x => x.PersonaId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.ComiteId, x.PersonaId }).IsUnique();
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        modelBuilder.Entity<RevisorFiscal>(b =>
        {
            b.Property(x => x.NumeroTarjetaProfesional).HasMaxLength(50);
            b.HasOne(x => x.Persona).WithMany().HasForeignKey(x => x.PersonaId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => x.TenantId);
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

        // Equipo de trabajo (spec 2.3 - seccion 3)
        modelBuilder.Entity<MiembroEquipo>(b =>
        {
            b.Property(x => x.RolPersonalizado).HasMaxLength(80);
            b.Property(x => x.Telefono).HasMaxLength(30);
            b.Property(x => x.Email).HasMaxLength(200);
            b.Property(x => x.Observaciones).HasMaxLength(1000);
            b.HasOne(x => x.Persona).WithMany().HasForeignKey(x => x.PersonaId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => new { x.TenantId, x.PersonaId, x.Rol });
            b.HasQueryFilter(x => _tenantContext.CurrentTenantId == null || x.TenantId == _tenantContext.CurrentTenantId);
        });

    }
}
