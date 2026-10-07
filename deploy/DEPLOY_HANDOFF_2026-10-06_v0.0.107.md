# HAND-OFF DEPLOY - PROPIA (2026-10-06) - v0.0.107

> Sucede a `DEPLOY_HANDOFF_2026-10-06_v0.0.106.md`. Trabajo nocturno (sesión ALEX): homogeneizacion vista-tabla.

## Objetivo
- **Version:** `0.0.107` (desde `0.0.106`). Auto-deploy desde `main` (Railway).
- **SIN MIGRACIONES. SIN cambios de config.** Solo UI (CSS + menu contextual).

## Que entra (2 commits sobre v0.0.106)
- `7425a30` - **Usuarios Activos/Inactivos:** header de la tabla (grid `.utb-*`) alineado al canon `.tbl`
  (peso 600 + color text-h1 + separador con sombra inset). Estructura grid se mantiene (columnas
  configurables, campos dinamicos, reorder/resize, alta inline). bump usuarios.css ?v=16.
- `3d467ed` - **Comparativa vista-tabla (fila 13, menu contextual):** clic derecho con `TablaCtxMenu`
  en **Contratos** (Abrir ficha / Copiar enlace / Eliminar) y **Seguros** (Abrir ficha / Copiar enlace).

## Archivos (4 + csproj)
```
src/Propia.Web/Propia.Web.csproj                               (Version 0.0.107)
src/Propia.Web/wwwroot/css/modules/usuarios.css               (header .utb al canon)
src/Propia.Web/Components/App.razor                           (usuarios.css ?v=16)
src/Propia.Web/Components/Pages/Capa2/Servicios.razor        (ctx menu Contratos)
src/Propia.Web/Components/Pages/Capa2/Seguros.razor          (ctx menu Seguros)
```

## Migraciones: NINGUNA.

## Verificacion hecha (Chrome, dev, tenant demo)
- Activos/Inactivos: header utb 600 / rgb(27,42,58) / separador rgb(225,232,238)+sombra inset (= canon).
- Contratos: clic derecho en fila -> menu Abrir ficha / Copiar enlace / Eliminar.
- Seguros: clic derecho -> Abrir ficha / Copiar enlace.

## Humo post-deploy
- [ ] /usuarios: header de la tabla igual a Pendientes/Unidades.
- [ ] /contratos y /seguros: clic derecho en una fila abre el menu contextual.
- [ ] Pie v0.0.107.

## Rollback: redeploy v0.0.106 (sin migraciones).
