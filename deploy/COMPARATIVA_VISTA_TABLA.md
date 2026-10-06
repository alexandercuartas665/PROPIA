# Comparativa vista tabla — Tareas / Contratos / Seguros / PQRSD vs canon Unidades

> Auditoría de cumplimiento de cada módulo contra el canon de Unidades Privadas
> ([[Inventario vista tabla - Unidades Privadas (topicos)]]). Fecha: 2026-10-06 (sesión ALEX).
> Leyenda: ✅ cumple · 🟡 parcial (existe pero distinto) · ❌ falta · ⚪ N/A (no aplica por diseño).

## TAREAS EXTRA — "listas en la config del campo" (pedido Alex 2026-10-06)

> Frente de trabajo: que TODAS las listas (select) se editen DENTRO de la config del campo, no por
> fuera. Se actualiza tras CADA ronda. Estado: [x] hecho · [ ] pendiente · (🔄) en curso · (⛔) descartado.

- [x] **Contratos · Tipo de contrato** → editable inline (migración enum→string). `fd631db` · dev OK, prod en deploy.
- [x] **Contratos · Categoría** → editable inline (migración enum→string). `fd631db`.
- [x] (⛔) **PQRSD · Tipo** → read-only: determina el plazo legal (Ley 1755). Descartado por diseño.
- [x] (⛔) **PQRSD · Estado** → read-only: flujo del kanban/SLA legal. Descartado por diseño.
- [x] **Unidades · Tipo** → **YA CUMPLÍA**: sus opciones se editan en el modal "Editar campo"
      (`EditarCampoSistemaModal`: Opciones de la lista + Añadir), que ES la config del campo. Sin trabajo.
- [x] **PQRSD · Categoría** → ver+agregar en la config del campo (modal "Editar campo", lee/crea
      `pqrsd_categorias`); patrón de Unidades Tipo. La gestión completa (renombrar/activar/eliminar) sigue
      en la pestaña Categorías. Validado en Chrome + BD.

**Mecanismo correcto (no hace falta refactor compartido):** el modal "Editar campo"
(`EditarCampoSistemaModal`) ya muestra "Opciones de la lista" dentro de la config del campo. Para listas
con fuente externa (tabla propia) se cablea `OnGestionarLista`+`OpcionesVistaDe`+`OnAgregarOpcion`.

**Ya cumplían (sin trabajo):** Estado/Categoría/Tipo editables inline en Unidades/Zonas/Equipos/Vehículos/
Mascotas; y Unidades Tipo (en "Editar campo").

### Bitácora por ronda
- 2026-10-06 R1: Contratos tipo+categoria enum→string editable inline. Validado en Chrome + BD. `fd631db`.
- 2026-10-06 R2: decisión PQRSD — tipo/estado read-only (legal).
- 2026-10-06 R3: descubrimiento — Unidades Tipo YA cumplía (opciones en "Editar campo"). Se revirtió un
  refactor de `ConfigCamposEntidad` que apuntaba a un gestor enterrado (innecesario). PQRSD categoría se
  hará con el patrón "Editar campo" (no refactor compartido).
- 2026-10-06 R5: **Contratos** — reordenar (arrastre header) + redimensionar columnas (handle, doble-clic
  autoajusta), canon Unidades (interop propiaTablaColReorder/Resize; orden+ancho en unidades-config,
  ancho en Formato "w"). Validado en Chrome (reordenó + redimensionó + persistió) + BD; test limpiado.
  **Matriz: filas 7 y 8 → ✅ en Contratos.** Falta PQRSD, Seguros, Tareas.
- 2026-10-06 R4: PQRSD categoría cableada al modal "Editar campo" (ver+agregar, lee/crea pqrsd_categorias).
  Validado en Chrome (agregó ZZCATTEST -> persistió) + BD; dato borrado. **Frente "listas en config del
  campo" CERRADO** (Contratos tipo/categoria, Unidades Tipo ya ok, PQRSD categoria; tipo/estado read-only).
- 2026-10-06 R7: **Contratos — fix del resize** (el de R5 persistía pero NO cambiaba el ancho visual: mismo
  gotcha de `table-layout:auto` + `<col width>` parcial). Aplicado el mismo fix que PQRSD: `AnchoColC`
  devuelve ancho explícito para TODAS las columnas (default por columna) + `min-width:max-content` en
  `.ctr-table` (servicios.css ?v=9). Validado en Chrome (/contratos, 21 filas): numero 130→220 cambió de
  verdad y persistió; reorder numero↔tercero persistió; reset doble-clic 220→130; orden/anchos restaurados;
  servicio de prueba borrado. **R5 quedaba a medias — ahora Contratos filas 7/8 ✅ de verdad.**
- 2026-10-06 R6: **PQRSD** — reordenar (arrastre header) + redimensionar columnas (handle `.pk-col-rz`,
  doble-clic autoajusta), canon Unidades (interop propiaTablaColReorder/Resize; orden+ancho en
  unidades-config entidad=pqrsd, ancho en Formato "w"). **Gotcha clave:** con `table-layout:auto` un
  `<col width>` suelto entre hermanos 'auto' se IGNORA; el canon `.tbl` funciona porque TODAS las `<col>`
  llevan ancho px explícito. Fix: `AnchoColP` devuelve ancho (redimensionado o default por columna) para
  todas + `min-width:max-content` en `.pk-table` (igual que `.tbl`). Validado en Chrome: resize visual
  170→250, persistió tras recarga; reorder tipo↔categoria, persistió; reset doble-clic 250→170; layout
  del demo restaurado. **Matriz: filas 7 y 8 → ✅ en PQRSD.** Falta Seguros, Tareas.

Componentes: Tareas `Shared/Tareas/TableroTareas.razor` (modo lista `.tb-lc-*`) · Contratos
`Pages/Capa2/Servicios.razor` (`.ctr-*`) · Seguros `Pages/Capa2/Seguros.razor` (`.seg-*`) · PQRSD
`Pages/Capa2/PqrsKanban.razor` (vista tabla `.pk-*`).

## Matriz comparativa

| # | Tópico | Tareas | Contratos | Seguros | PQRSD |
|---|---|:--:|:--:|:--:|:--:|
| 1 | Barra de herramientas (toolbar) | 🟡 | ✅ | ✅ | ✅ |
| 2 | Filtros (reglas dinámicas) | ✅ | 🟡 | 🟡 | 🟡 |
| 3 | Buscador | ✅ | ✅ | ✅ | ✅ |
| 4 | Agrupar (multinivel) | ✅ | ✅ | ✅ | ✅ |
| 5 | Ordenar por columna (clic header + ▲▼) | ✅ | 🟡 | 🟡 | ✅ |
| 6 | Menú de columna (chevron ˅ siempre visible) | ✅ | ✅ | ✅ | ✅ |
| 7 | Reordenar columnas (arrastre) | ❌ | ✅ | ❌ | ✅ |
| 8 | Redimensionar columnas | ❌ | ✅ | ❌ | ✅ |
| 9 | Botón "+" agregar campo (header) | 🟡 | ✅ | ✅ | 🟡 |
| 10 | Columna expander | ✅ | ✅ | ✅ | 🟡 |
| 11 | Checkbox selección múltiple | ❌ | ❌ | ❌ | ❌ |
| 12 | Barra de acciones masivas | ❌ | ❌ | ❌ | ❌ |
| 13 | Menú contextual (clic derecho) | ✅ | ❌ | ❌ | ❌ |
| 14 | CTA colapsable "Agregar registro" (.row-add--sm) | 🟡 | ❌ | ❌ | ⚪ |
| 15 | Botón flotante (FAB) | ✅ | ✅ | ✅ | 🟡 |
| 16 | Scroll infinito (por lotes) | 🟡 | 🟡 | 🟡 | 🟡 |
| 17 | Fila de alta inline (todos los campos) | ✅ | 🟡 | 🟡 | ⚪ |
| 18 | Botones confirmar/cancelar fila (✓/✗) | 🟡 | 🟡 | ❌ | ⚪ |
| 19 | Selector de persona flotante (en alta) | 🟡 | ✅ | ❌ | ⚪ |
| 20 | Fila recién creada resaltada (verde) | ❌ | ✅ | ✅ | ✅ |
| 21 | Edición inline de celdas | ✅ | ✅ | ❌ | ❌ |
| 22 | Modal crear campo | 🟡 | ✅ | 🟡 | 🟡 |
| 23 | Editar campo fijo / de sistema | ✅ | ✅ | ✅ | ✅ |
| 24 | Editar / eliminar campo dinámico | ✅ | ✅ | 🟡 | 🟡 |
| 25 | Gestor de campos (config avanzada) | ✅ | ✅ | ✅ | ✅ |
| 26 | Campos vinculados / entidades relacionadas | ⚪ | 🟡 | ⚪ | ❌ |
| 27 | Footer / paginador (TablaPager) | 🟡 | ❌ | 🟡 | 🟡 |
| 28 | Carga por Excel / plantilla | ❌ | ❌ | ❌ | ⚪ |
| 29 | Modo oscuro por tokens | 🟡 | 🟡 | ✅ | ✅ |
| 30 | Interop JS / menús compartidos | 🟡 | 🟡 | 🟡 | 🟡 |

### Puntaje aproximado (✅ sobre tópicos que aplican)

- **Tareas:** 14 ✅ · 11 🟡 · 4 ❌ · 1 ⚪ — el más completo (tabla "viva" con edición inline).
- **Contratos:** 14 ✅ · 9 🟡 · 6 ❌ · 1 ⚪.
- **Seguros:** 11 ✅ · 8 🟡 · 9 ❌ · 2 ⚪.
- **PQRSD:** 10 ✅ · 9 🟡 · 4 ❌ · 7 ⚪ — muchos ⚪ porque el alta es por **wizard legal**, no inline.

## Brechas COMUNES (a los 4 les falta — mayor retorno)

1. **Reordenar columnas por arrastre (❌ Seguros, Tareas).** Hecho en Contratos (`R5`) y PQRSD (`R6`). Falta Seguros y Tareas: cablear `propiaTablaColReorder` al header.
2. **Redimensionar columnas (❌ Seguros, Tareas).** Hecho en Contratos (`R5`) y PQRSD (`R6`). Falta Seguros y Tareas: handle `.*-col-rz` + `propiaTablaResize`. **Ojo:** para que el ancho se respete, TODAS las `<col>` deben llevar ancho px explícito (no solo las redimensionadas) — ver gotcha R6.
3. **Checkbox selección múltiple (❌ x4).** Ninguno permite seleccionar filas en la tabla.
4. **Barra de acciones masivas (❌ x4).** Sin borrado/acción en lote (eliminar es fila a fila o desde modal).
5. **Scroll infinito real (🟡 x4).** Todos son render-all: `CargarMas()` no-op, `dstInfinite` solo alterna el FAB. (Unidades sí carga por lotes.) — Prioridad baja si los volúmenes son chicos.
6. **Footer `TablaPager` (🟡/❌).** Ninguno usa el componente compartido; todos tienen `.tbl-foot` de conteo plano (Contratos ni eso: migración declarada pero no hecha).

## Brechas por módulo (lo más importante para acercarlo al canon)

**Tareas** (ya es el más alineado):
- ❌ Reordenar/redimensionar columnas · ❌ selección+acciones masivas · ❌ fila verde.
- 🟡 Footer→TablaPager · 🟡 CTA colapsable + ✓/✗ (hoy alta siempre abierta; guarda al blur/Enter — divergencia "A4" ¿intencional?) · 🟡 selector persona flotante (usa `<select>` plano) · 🟡 crear campo usa popover propio, no `CrearCampoModal` · 🟡 **hex hardcodeados** en dark mode (tareas.css + .razor) — viola tokens.

**Contratos:**
- ❌ **Footer/paginador** (solo conteo; el comentario del CSS da por hecha una migración a TablaPager que NO ocurrió) · ❌ selección+acciones masivas · ❌ menú contextual · ❌ reordenar/redimensionar · ❌ CTA `.row-add--sm` (alta siempre abierta, clase propia `.ctr-row-add`).
- 🟡 Filtros propios (no `FiltroDinamico`) · 🟡 ordenar solo por menú (no clic en header) · 🟡 alta no captura dinámicos · 🟡 **hex hardcodeado + tokens rotos** (`var(--propia-bg-card)5F5`) en servicios.css.

**Seguros** (el más lejos del canon de "tabla viva"):
- ❌ **Edición inline de celdas** (solo lectura; todo se edita en el modal) · ❌ CTA `.row-add--sm` + ✓/✗ (alta siempre expandida, solo el expander crea) · ❌ selector persona flotante en alta (texto plano) · ❌ selección+acciones masivas · ❌ menú contextual · ❌ reordenar/redimensionar.
- 🟡 Alta solo 6 campos fijos (dinámicos → "(auto)") · 🟡 ordenar sin clic en header · 🟡 footer no TablaPager · 🟡 filtros sin persistencia.

**PQRSD** (muchos ⚪ por diseño = alta por wizard legal):
- ✅ Reordenar/redimensionar columnas (R6). · ❌ selección+acciones masivas · ❌ menú contextual · ❌ edición inline de celdas (la fila navega a la ficha).
- 🟡 Footer→TablaPager · 🟡 filtros con popover propio (aunque persiste) · 🟡 expander solo abre ficha.
- ⚪ Alta inline / ✓-✗ / selector persona / CTA / Excel: N/A (radicación por wizard).

## Recomendación (orden para "cumplir lo más posible")

1. **Lote transversal de columnas** (toca los 4): cablear **reordenar** (`propiaTablaColReorder`) y **redimensionar** (`propiaTablaResize` + handle) en cada header. Es el interop ya probado en Unidades; alto impacto, bajo riesgo.
2. **Selección + acciones masivas** (toca los 4): columna checkbox + "seleccionar todo" + `.dst-selbar` con borrado en lote. Reusa el patrón de Unidades/Roles.
3. **Footer `TablaPager`** en Contratos (falta del todo), luego Seguros/PQRSD/Tareas.
4. **Menú contextual** en Contratos, Seguros, PQRSD (Tareas ya lo tiene).
5. **Seguros: edición inline de celdas** + alta canónica (CTA `.row-add--sm` + ✓/✗ + selector persona flotante + dinámicos) — es el que más se aparta.
6. **Limpieza de tokens**: quitar hex hardcodeados/tokens rotos en `servicios.css` y `tareas.css`.
7. **Unificar filtros** al componente `FiltroDinamico` en Contratos/Seguros/PQRSD.

> Nada de esto se ha implementado todavía: es el mapa de trabajo. Al construir, Unidades es la
> referencia 1:1 y se valida en runtime (Chrome) módulo por módulo.
