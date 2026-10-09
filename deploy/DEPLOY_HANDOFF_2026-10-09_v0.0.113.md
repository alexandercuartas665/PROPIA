# HAND-OFF DEPLOY - PROPIA (2026-10-09) - v0.0.113

> Portada de ESTE deploy. Trae **1 MIGRACION NUEVA** (`20261008030714_DirectorioEtiquetasListaCatalogo`),
> parcialmente destructiva (hace BACKFILL y luego DROP de la columna/FK `etiqueta_id` de `directorio_etiquetas`;
> la tabla `etiquetas_catalogo` se CONSERVA). El resto (homogenizacion de la vista tabla, alta estilo Tablero
> en Tareas, quitar KPIs) es **migration-free**. Leer "Migraciones" y "Orden de release" antes de aplicar nada.

## Objetivo
- **Rama / HEAD:** `equipo/alex-header-zonas-equipos` (merge a `main` en el release). `main` ya esta en 0.0.112.
- **Version:** `0.0.113` (desde `0.0.112`).
- **Mecanismo:** merge a `main` + push -> auto-deploy en Railway.
- **TRAE 1 MIGRACION** (`20261008030714_DirectorioEtiquetasListaCatalogo`). Ver "Orden de release".
- **Acumulado:** 25 commits sobre 0.0.112 (`main` HEAD `ff8e7502`).

## Que entra (25 commits, agrupado)

### 1) Directorio - etiquetas como LISTA DE CATALOGO (texto) [1 MIGRACION] (`968178a8`)
- Las etiquetas del Directorio dejan de ser una FK al catalogo rico (`etiquetas_catalogo`, con color/icono/
  grupo) y pasan a ser un **VALOR de texto** respaldado por la **lista de catalogo global `directorio.etiqueta`**
  (editable en Super Admin > Catalogos) + opciones **custom por copropiedad**. Mismo patron que el resto de
  listas (unidad.tipo, contrato.categoria, etc.).
- Toca: `DirectorioService` (257 lineas), `DirectorioController`, DTOs, entidad `DirectorioEtiqueta`,
  `ComunicacionesService` (segmentacion por etiqueta ahora por texto), y UI (`DirectorioPanel`,
  Personas/Empresas vista + ficha). `CatalogoListasRegistro` + `CatalogoListasSeeder` **siembran la lista
  `directorio.etiqueta`** en el arranque (idempotente; ver "Config / arranque").
- **Migracion `20261008030714`:** (a) agrega `directorio_etiquetas.valor varchar(100) NOT NULL default ''`;
  (b) **BACKFILL** `valor = etiquetas_catalogo.nombre` de la etiqueta ligada (ANTES de soltar nada, no se
  pierde el texto); (c) DROP FK + 2 indices sobre `etiqueta_id` + DROP columna `etiqueta_id`; crea indice
  unico `(vinculo_id, valor)`. La tabla `etiquetas_catalogo` **se conserva intacta** (su limpieza es tarea
  aparte). `directorio_etiquetas` **ya tiene `tenant_id` + RLS FORCE** (no se toca RLS; no suma tablas sin
  RLS -> `RlsCoverageTests` sin cambio).
- **Compatibilidad / breaking:** el valor de las etiquetas existentes se preserva (backfill por nombre). El
  `Down()` recrea `etiqueta_id` vacio (Guid 0) pero NO re-liga al catalogo: preferir fix-forward.

### 2) Homogenizacion de la VISTA TABLA (migration-free, ~21 commits)
Cierre de la homogenizacion de la vista tabla usando **Unidades** como referencia, replicado a todos los
modulos con vista tabla (Residentes, Mascotas, Vehiculos, Equipos, Zonas, Directorio P/E, Usuarios, Roles,
Contratos, Seguros, PQRSD, Mantenimiento, Tareas). Todo CSS/razor, **sin migracion**. Incluye:
- **Guardado invisible + aviso** (fila de alta sin botones +/x; se guarda al salir/Enter; Esc cancela) y
  **quitar el cesto por fila** (borrado por barra de seleccion multiple). Replicado por tandas a todos.
- **CTA de alta canon** `.row-add--sm` + **FAB** flotante; boton de alta indentado bajo la 1a columna.
- **Columna de seleccion + accion masiva** en Tareas y Mantenimiento.
- **Primera columna rigurosamente homogenea:** checkbox 38px exacto (`min=max`), checkbox centrado y pegado
  al borde (inset izq. 0), celda del expander 34px, **boton expander 26x26 "icono pelado"** (transparente,
  = Unidades), **padding de celdas sel/exp `0 4px`**.
- **Header de tabla homogeneo a 47px** en todo el sistema (= Unidades).
- **Unidades (piloto):** campos custom editables inline en filas existentes + guardado invisible robusto;
  select de Lista inline tintado con el color de la opcion.
- **Cosmetico:** codigo/clave de unidad en violeta de marca + subrayado en las tablas de todos los modulos.
- Se quito "Hitos de configuracion" del hub; divisor de 1a columna homogeneo.

### 3) Tareas - alta "estilo Tablero" + quitar KPIs (migration-free, `d4af5849`, `977f37d5`)
- **Vista Tabla:** se retira la fila de alta predispuesta permanente. "Agregar tarea" (CTA y FAB) ahora
  **ABRE** una fila nueva y vacia (como el composer del Tablero). Enter crea y deja la linea abierta (alta
  encadenada); clic fuera guarda si hay titulo y cierra; Esc cierra. El Tablero (Tarjetas) no cambia.
  **NOTA:** esto cambia el canon de vista-tabla (Tareas es la referencia); la replicacion del "boton abre la
  linea" al resto de modulos queda PENDIENTE (decidir en proxima tanda).
- **Quitar KPIs:** se retiran los mini indicadores (tarjetas KPI) de Tareas y de los Tableros (bloques
  `.tb-kpis` de la galeria y `.tb-bkpis` del tablero), por decision de producto (vista tabla sin KPI). Se
  limpio el codigo muerto (fetch `/api/tareas/resumen`, `_resumen`, `TaskKpis()`, `KpiVm`): 1 llamada API
  menos por carga.

## Migraciones (APLICAR EN PROD, owner `propia`)
**1 migracion nueva:** `20261008030714_DirectorioEtiquetasListaCatalogo`. **Aplicada y verificada en dev**
(`directorio_etiquetas` ya tiene `valor` y no `etiqueta_id`). Comando:
```
cd src/Propia.Api
"$USERPROFILE/.dotnet/tools/dotnet-ef.exe" database update --project ../Propia.Infrastructure --startup-project .
```
`database update` aplica solo lo pendiente (compara `__EFMigrationsHistory`).

### Orden de release
La migracion es un "rename" de columna (`etiqueta_id` -> `valor`): el codigo viejo (0.0.112) lee `etiqueta_id`
y el nuevo (0.0.113) lee `valor`, asi que **la superficie de etiquetas del Directorio tiene una ventana breve
de desajuste cualquiera sea el orden**. El resto de la app NO depende de esta migracion.
1. Desplegar el codigo 0.0.113 (Railway build+release del nuevo artefacto).
2. Aplicar `database update` **inmediatamente despues**. La ventana afecta SOLO a las etiquetas del
   Directorio (lectura/segmentacion por etiqueta); el resto opera normal.
3. Aviso al equipo antes de aplicarla en cualquier BD compartida.

## Config / arranque
- **Seeder de catalogo:** `CatalogoListasSeeder` ahora siembra la lista global **`directorio.etiqueta`** en
  CADA arranque (idempotente: solo inserta lo que falta, no pisa ediciones de A&D; va en try/catch, no
  bloquea el arranque). **La migracion debe estar aplicada antes** para que el modulo lea `valor`.
- Sin variables de entorno nuevas.

## Cache-busting (ya bumpeado en `Components/App.razor` - servir estas versiones)
`tabla-base.css?v=7`, `tareas.css?v=20`, `pqrs.css?v=21`, `servicios.css?v=15`, `directorio.css?v=11`,
`usuarios.css?v=21`, `roles.css?v=14`, `porteria.css?v=3`, `propia-ui.js?v=23`.

## Verificacion hecha (dev)
- `dotnet build` del Web (+ dependencias Api/Infra/Application/Domain) -> **0 errores**, sin warnings nuevos
  (35 warnings preexistentes).
- **Runtime (Chrome, 5105, copropiedad demo):**
  - Tareas vista Tabla: "Agregar tarea" ABRE la fila -> Enter crea (T-2026-0040, conteo 16->17, confirmado en
    BD) y deja la linea abierta -> Esc cierra. Tablero (Tarjetas): "Agregar" abre el composer. Dato de prueba
    **borrado** (0 huerfanos).
  - KPIs: Tabla y Tarjetas sin fila KPI (`.tb-bkpis`/`.tb-kpis` = 0 en el DOM).
  - Primera columna (Mantenimiento, caso con width fijo): celda 38px, checkbox a 11px del borde (padding
    `0 4px` neutro con `box-sizing:border-box`).
- **Pendiente antes del release:** correr `dotnet test` en el commit exacto del deploy. Baseline conocido:
  **224 verdes / 8-9 rojos PREEXISTENTES** (Billing, RlsCoverage, SuperAdmin, Usuarios, Reservas). Cualquier
  rojo NUEVO es regresion. `DirectorioFlowTests` se modifico por el cambio de etiquetas: confirmar verde.

## Humo post-deploy
- [ ] Desplegar codigo 0.0.113, luego aplicar `database update` (inmediatamente despues).
- [ ] Pie de la app en `v0.0.113`.
- [ ] **Directorio:** las etiquetas existentes conservan su texto (backfill); el selector de etiquetas ofrece
      la lista `directorio.etiqueta` del catalogo + custom por copropiedad; crear/asignar etiqueta OK;
      segmentar una comunicacion por etiqueta OK. Verificar que el modulo carga sin 500.
- [ ] **Tareas:** `/tareas` vista Tabla -> "Agregar tarea" abre la fila (no hay fila predispuesta); Enter crea;
      Esc cierra. Tablero sin fila KPI. Vista Tabla sin fila KPI.
- [ ] **Vista tabla (muestreo):** Residentes/Mascotas/Vehiculos/Zonas/Equipos/Usuarios/Directorio/Contratos/
      Seguros/PQRSD/Mantenimiento -> 1a columna homogenea (checkbox 38px pegado al borde, expander icono pelado),
      header 47px, alta con guardado invisible, sin cesto por fila. CSS servido con los `?v` de arriba.
- [ ] **Super Admin > Catalogos:** aparece la lista `directorio.etiqueta` (editable).

## Rollback
- **Vista tabla / Tareas / KPIs:** son solo codigo (CSS/razor). Redeploy de 0.0.112 los revierte sin tocar datos.
- **Etiquetas Directorio:** la migracion es parcialmente destructiva (DROP `etiqueta_id` tras backfill).
  Preferir **fix-forward**. Si hay que volver: redeploy del artefacto 0.0.112 + `Down()` de `20261008030714`
  (recrea `etiqueta_id` vacio, NO re-liga al catalogo rico; los valores de texto ya no se usarian). No
  revertir el esquema a mano.

---
Generado 2026-10-09 (v0.0.113). Companion: `DEPLOY_HANDOFF_2026-10-07_v0.0.112.md`, `HANDOFF_DEPLOY.md`.
