# HAND-OFF DEPLOY - PROPIA (2026-10-06) - v0.0.104

> Portada de ESTE deploy (sucede a `DEPLOY_HANDOFF_2026-10-06.md` / v0.0.103, que se deja por historia).
> Detalle historico en `HANDOFF_DEPLOY.md`; paso-a-paso en `DEPLOY_CHECKLIST.md`.

## Objetivo del deploy

- **Rama / HEAD:** `main` (tras merge de `equipo/alex-header-zonas-equipos`)
- **Version:** `0.0.104` (sube desde `0.0.103`)
- **Repo:** https://github.com/alexandercuartas665/PROPIA (rama `main`)
- **Mecanismo:** auto-deploy desde `main` (Railway). Prod toma ese commit.
- **SIN MIGRACIONES. SIN cambios de config/arranque.** Cambios 100% de UI (frontend) + 1 linea en un
  servicio (whitelist de `entidad` para la config de columnas de Roles). Nada que aplicar en BD.

## Que entra en esta tanda (5 commits sobre v0.0.103)

**Roles (bandeja): igualar la tabla a la maqueta `roles.html` usando el canon .tbl**
- `edeaac4` - Roles: reordenar + redimensionar columnas (canon vista-tabla) + borde simetrico.
- `6f754dc` - Roles: separadores verticales de columna + expander (primer paso).
- `d8a641a` - Roles: **usar el canon .tbl de la maqueta en vez de pelearlo**. RolesPanel pasa a las clases
  canonicas (`tbl-th--menu`, `tbl-th-lbl`, `tbl-col-rz`, `tbl-exp-btn`, chip de categoria transparente +
  punto de color, `propia-badge--neutral` para "Sistema"); se borran los overrides de `roles.css` que
  invertian el canon (header SIN separadores + cuerpo CON separadores). Se conservan la descripcion del rol
  y la columna de acciones (kebab), ausentes en la maqueta pero consistentes con el resto del app.

**Canon vista-tabla compartido (afecta TODAS las tablas, calca de la maqueta `assets/app.css`)**
- `d41db37` - `tabla-base.css`: header `font-weight` 700 -> 600 y `color` secondary -> text-h1
  (rgb(27,42,58)); expander `.tbl-exp-btn` 22x22 r6 -> 26x26 r7. Es un ajuste cosmetico uniforme; verificado
  sin regresion en Unidades (`/distribucion`).

**Ficha de Unidad Privada (modal)**
- `0d415d9` - `GestionarUnidadesModal.razor` + `unidades-modal.css`: modal de ficha alineado a la maqueta
  `editor-ficha.html` (pestana "Unidades anexas" con alta/edicion/mover/eliminar; 4 pestanas; historial =
  bitacora; se quito "Estado de cuenta").

**Version:** bump `0.0.103 -> 0.0.104` (csproj `Propia.Web`).

## Archivos tocados (7 + csproj)

```
src/Propia.Web/Propia.Web.csproj                                    (Version 0.0.104)
src/Propia.Web/Components/App.razor                                 (cache-bust: roles.css?v=12, tabla-base.css?v=3)
src/Propia.Web/Components/Pages/Capa2/RolesPanel.razor             (canon .tbl)
src/Propia.Web/wwwroot/css/modules/roles.css                       (quita overrides)
src/Propia.Web/wwwroot/css/modules/tabla-base.css                  (header + expander al pixel)
src/Propia.Web/Components/Shared/Modals/GestionarUnidadesModal.razor (ficha de unidad)
src/Propia.Web/wwwroot/css/modules/unidades-modal.css             (estilos ficha)
src/Propia.Infrastructure/MiCopropiedad/MiCopropiedadService.Unidades.cs (whitelist entidad "rol")
```

## Migraciones

**NINGUNA en esta tanda.** (Si prod viniera por detras de v0.0.103, aplicar antes las 2 de esa tanda:
`ContratoTipoCategoriaAString` y `AddCatalogoOpcion` - ver `DEPLOY_HANDOFF_2026-10-06.md`. El
`database update` las aplica en orden.)

## Verificacion ya hecha (dev, tenant demo "Conjunto Altos del Bosque")

- Roles (`/roles`) medido con getComputedStyle contra la maqueta servida por HTTP: header padding
  14px 30px 14px 16px, peso 600, color rgb(27,42,58), separadores solo en header (border-right +
  box-shadow inset rgb(225,232,238)); cuerpo sin border-right; expander 26x26 r7 transparente -> violeta
  al hover; chip de categoria transparente+borde; badge "Sistema" bg rgb(238,243,248); sin scroll horizontal.
- Drag/resize de columnas de Roles intactos (4 handles, cursor grab). Config de columnas de prueba
  reseteada al orden natural.
- Unidades (`/distribucion`): sin regresion con el nuevo canon.

## Verificacion post-deploy (humo)

- [ ] `/roles`: header con separadores verticales (solo header), cuerpo limpio; "Sistema" como badge gris;
      categorias como chip transparente con punto de color; expander violeta al pasar el mouse por la fila.
- [ ] `/roles`: arrastrar un encabezado reordena; el tirador del borde redimensiona; doble-clic autoajusta;
      persiste al recargar.
- [ ] `/distribucion` y demas vistas-tabla: el header se ve consistente (texto oscuro en negrita) sin romper
      alineacion.
- [ ] Ficha de unidad: abrir una unidad -> 4 pestanas; "Unidades anexas" permite agregar/editar/mover/eliminar.
- [ ] Pie de la app muestra `v0.0.104`.

## Rollback

- Redeploy del artefacto anterior (v0.0.103). Sin migraciones nuevas, el rollback es limpio (solo UI).

---
Generado 2026-10-06. Companions: `HANDOFF_DEPLOY.md`, `DEPLOY_CHECKLIST.md`, `DEPLOY_HANDOFF_2026-10-06.md`.
