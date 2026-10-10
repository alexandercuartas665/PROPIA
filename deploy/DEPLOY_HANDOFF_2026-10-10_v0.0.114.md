# HAND-OFF DEPLOY - PROPIA (2026-10-10) - v0.0.114

> Sucede a `DEPLOY_HANDOFF_2026-10-06_v0.0.105.md`. Detalle historico en `deploy/HANDOFF_DEPLOY.md`
> y `deploy/DEPLOY_CHECKLIST.md`.

## Objetivo del deploy

- **Rama a mergear:** `equipo/alex-header-zonas-equipos` -> `main` (39 commits sobre main).
- **Version:** `0.0.114` (sube desde `0.0.113` del csproj; 0.0.113 nunca se desplego, su scope crecio).
- **Repo:** https://github.com/alexandercuartas665/PROPIA (rama `main`)
- **Mecanismo:** auto-deploy desde `main` (Railway).
- **CON MIGRACIONES: 3 nuevas, UNA DESTRUCTIVA (Fase 3 dropea la columna `tareas.prioridad`).**
  **=> BACKUP DE PROD OBLIGATORIO ANTES DE MIGRAR.** Ver seccion Migraciones.
- Sin cambios de config/arranque ni de variables de entorno.

## Que entra en esta tanda (39 commits; ver `COMMITS_INCLUIDOS.txt`)

Tres bloques grandes:

### A. Homogeneizacion UI vista-tabla (Olas 1-4 + Fase 1 estados/etiquetas) - ya estaba en 0.0.113 planeado
- Barra unica estilo Unidades (Filtros/Buscar ... Ordenar/Agrupar/Columnas) en Tareas y PQRSD.
- Estados y Etiquetas editables desde el chevron de SU columna (no en el modal Configurar).
- Normalizacion de 1a columna (checkbox 38px, expander, inset 0), alta estilo Tablero, quitar KPIs,
  guardado invisible + aviso en todos los modulos grid, columna clave en violeta.
- 1 migracion ADITIVA: `DirectorioEtiquetasListaCatalogo` (etiquetas del Directorio como lista de catalogo).

### B. Fase 2 - Prioridad configurable por tablero (Tareas) + Etapa 3
- Cada tablero define su lista de prioridades (nombre/color/orden), estilo estados/etiquetas.
  Editor en el chevron de la columna Prioridad; selects de creacion (composer/modal/fila) y pill del
  kanban usan la opcion del tablero; default de creacion = Normal.
- **2 migraciones:**
  - `Fase2TareaPrioridades` (ADITIVA): tabla `tarea_prioridades` + seed de las 4 de fabrica por tablero +
    backfill de `tareas.prioridad_id`.
  - `Fase3DropTareaPrioridadEnum` (**DESTRUCTIVA**): backfill final de `prioridad_id` null -> opcion base, y
    luego **DROP de la columna `tareas.prioridad`**. La prioridad de la tarea vive ahora SOLO en
    `prioridad_id` -> `tarea_prioridades`. El enum `PrioridadTarea` se conserva como tipo (catalogo base +
    entrada que el servicio resuelve a opcion), pero la COLUMNA desaparece.

### C. Homogeneizacion del margen lateral (uso del ancho del area de trabajo)
- Tareas/Tableros, PQRSD, Contratos/Servicios y Porteria usaban padding horizontal propio (28px) y/o
  `max-width:1900px; margin:0 auto` (se centraban en monitores anchos). Ahora usan el mismo margen que
  Unidades (el gutter lo da `.app-wrapper`; el wrapper de pagina no agrega padding horizontal ni max-width).
- Solo CSS + bump de `?v=` en `App.razor`. Sin backend. Inventario: Obsidian
  "Homogeneidad UI - Margen lateral de pagina (inventario)".

**Version:** bump `0.0.113 -> 0.0.114` (csproj `Propia.Web`).

## Migraciones a aplicar en prod (ORDEN IMPORTA; las aplica `ef database update`)

**1) BACKUP DE PROD ANTES DE NADA (por la migracion destructiva Fase 3):**
```bash
# Backup completo (recomendado) o al menos de la tabla tareas:
pg_dump "<CONNSTRING_PROD_OWNER>" -t tareas --data-only > tareas_prod_pre_0.0.114.sql
# o backup completo de la BD de prod segun el procedimiento habitual de Railway/Postgres.
```

**2) Aplicar migraciones (aplica SOLO las que falten vs `__EFMigrationsHistory`):**
```bash
cd src/Propia.Api
dotnet ef database update --project ../Propia.Infrastructure --startup-project .
```

Las 3 nuevas de esta tanda (en orden):
- `20261008030714_DirectorioEtiquetasListaCatalogo`  -- ADITIVA (Directorio: etiquetas como catalogo)
- `20261010101256_Fase2TareaPrioridades`  -- ADITIVA: crea `tarea_prioridades` (RLS FORCE + policy tenant +
  GRANT propia_app en la propia migracion), siembra 4 prioridades de fabrica por tablero y backfillea
  `tareas.prioridad_id`.
- `20261010205117_Fase3DropTareaPrioridadEnum`  -- **DESTRUCTIVA**: 2 UPDATE de backfill (prioridad_id null ->
  opcion base por `base_valor`; remanentes -> Normal) y luego **`DROP COLUMN tareas.prioridad`**.
  El SQL exacto esta en `SQL_MIGRACIONES_v0.0.114.sql` y en el archivo de la migracion.

> El backfill de Fase 3 corre DENTRO de la migracion, ANTES del drop: ninguna tarea CON tablero pierde su
> prioridad. Tareas huerfanas sin `tablero_id` (si existieran en prod) quedan con `prioridad_id` null (el
> DTO deriva "Normal"); no bloquea.

## Verificacion ya hecha (dev, tenant demo, build 0.0.113->0.0.114)

- Fase 2/2b runtime: crear por modal/fila persiste `prioridad_id`; default = Normal; pills del kanban con
  color/nombre de la opcion; inline/reorder/rename/color/agrupar/ordenar/filtrar por prioridad verificados
  en BD y revertidos.
- Etapa 3 en dev: migracion aplicada, columna `tareas.prioridad` eliminada, `prioridad_id` backfilleado
  (quedo 1 huerfana sin tablero, correcto). Suite de integracion: **376 verdes / 5 rojos preexistentes**
  (4 Billing + 1 SuperAdmin), **0 regresiones**. Build sin errores.
- Margenes: Tareas, PQRSD, Contratos, Porteria medidos a 1680px con EXTRA_INSET=0 (left 311, igual que
  Unidades `dst-*`); a 2560px ya no se centran (max-width none).

## Verificacion post-deploy (humo)

- [ ] Pie de la app muestra `v0.0.114`.
- [ ] `/tareas`: el board carga; crear una tarea y elegir prioridad persiste; el pill del kanban muestra la
      opcion; cambiar prioridad inline en la tabla funciona. (La columna `tareas.prioridad` ya NO existe.)
- [ ] `/tareas`: editor de prioridades (chevron de la columna Prioridad) permite renombrar/color/reordenar.
- [ ] En prod, confirmar `select count(*) from tareas where prioridad_id is null;` -> deberia ser 0
      (salvo huerfanas sin tablero, que son legacy).
- [ ] Margenes: `/tareas`, `/pqrs`, `/contratos`, `/porteria` arrancan pegados al sidebar y llenan el ancho,
      igual que `/distribucion`.
- [ ] Un flujo que crea tareas desde otro modulo sigue bien: generar una intervencion en `/mantenimiento`
      crea su tarea con prioridad (Alta/Normal) en el tablero General.

## Rollback

- **App:** redeploy del artefacto anterior (v0.0.113/0.0.105 segun lo que estuviera en prod).
- **BD:** la migracion Fase 3 tiene `Down` que re-crea la columna `prioridad` y la backfillea desde
  `base_valor` de la opcion (best-effort; las prioridades PERSONALIZADAS no se recuperan con fidelidad, por
  diseno). Para revertir: `dotnet ef database update <migracion_anterior_a_Fase3>` o restaurar el backup del
  paso 1. **Si se hace rollback de BD, la app debe volver tambien al artefacto que aun lee la columna.**

## Archivos del paquete (esta carpeta)

- `DEPLOY_HANDOFF_2026-10-10_v0.0.114.md`  (este documento)
- `COMMITS_INCLUIDOS.txt`  (los 39 commits de la rama vs main)
- `ARCHIVOS_CAMBIADOS.txt`  (git diff --stat main..HEAD; 78 archivos)
- `SQL_MIGRACIONES_v0.0.114.sql`  (el SQL que corren las 3 migraciones, para revisar/rollback)
- `migraciones/`  (copias de los 3 archivos de migracion .cs)

> El deploy NO es por copia de archivos: es `merge de la rama a main` -> Railway auto-deploy. Esta carpeta
> es el paquete de HANDOFF (revision + pasos + referencia de migraciones), no el artefacto desplegable.

---
Generado 2026-10-10. Companions: `deploy/HANDOFF_DEPLOY.md`, `deploy/DEPLOY_CHECKLIST.md`,
`DEPLOY_HANDOFF_2026-10-06_v0.0.105.md`.
