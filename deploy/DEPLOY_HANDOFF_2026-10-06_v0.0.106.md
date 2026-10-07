# HAND-OFF DEPLOY - PROPIA (2026-10-06) - v0.0.106

> Sucede a `DEPLOY_HANDOFF_2026-10-06_v0.0.105.md`.

## Objetivo
- **Rama / HEAD:** `main` (merge de `equipo/alex-header-zonas-equipos`)
- **Version:** `0.0.106` (desde `0.0.105`)
- **Mecanismo:** auto-deploy desde `main` (Railway).
- **SIN MIGRACIONES. SIN cambios de config.** Cambios de UI (Usuarios) + backend menor (correo en el alta).

## Que entra (2 commits sobre v0.0.105)
- `3e5f7f6` - **Usuarios, alta inline:** correo editable (antes "Al aceptar"; si se escribe, la invitacion
  se envia a el y queda en la persona del Directorio) + rol como **pildora** (chip con "x", un rol por
  usuario). **Pendientes** pasa a vista tabla.
- `cd3b760` - **Usuarios Pendientes:** usa el frame canon `<table class="tbl">` (tabla-base.css / maqueta
  componentes.html) en vez del grid `.utb-*`: header sticky con separadores + filas canon.

## Archivos (4 + csproj)
```
src/Propia.Web/Propia.Web.csproj                                 (Version 0.0.106)
src/Propia.Web/Components/Pages/Capa2/UsuariosTablaModels.cs     (NuevoUsuarioInvitar + Email)
src/Propia.Web/Components/Pages/Capa2/UsuariosTablaVista.razor   (correo input + rol pildora)
src/Propia.Web/Components/Pages/Capa2/UsuariosPanel.razor        (correo -> CrearPersonaRequest; Pendientes .tbl)
```

## Migraciones: NINGUNA.

## Verificacion hecha (dev, Chrome, tenant demo)
- Alta inline: correo editable y guardado en la persona (prueba.pildora@example.com); rol pildora
  "Coordinador"; invitacion enviada -> aparece en Pendientes; datos de prueba borrados.
- Pendientes: frame .tbl canon (th 600/rgb(27,42,58)/separador rgb(225,232,238)+sombra inset, padding
  14px 16px), medido contra la maqueta componentes.html.

## Humo post-deploy
- [ ] /usuarios Tabla -> "Agregar registro": correo editable; elegir rol muestra pildora; invitar crea en Pendientes.
- [ ] /usuarios Pendientes: se ve como tabla canon (header con separadores).
- [ ] Pie v0.0.106.

## Rollback: redeploy v0.0.105 (sin migraciones).
