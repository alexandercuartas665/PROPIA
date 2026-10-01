---
name: homogenizar-vista-tabla
description: Patron CANONICO unico de la "vista tabla" de PROPIA (toolbar, filtros, agrupar, campos, orden, header fijo, KPIs, expander, alta inline, edicion inline, bordes/colores). USA SIEMPRE que se cree o ajuste la vista tabla / modo tabla de cualquier modulo (Contratos, Seguros, Directorio, Unidades, Zonas, Equipos, Usuarios, PQRSD, etc.) o cuando el usuario hable de homogenizar tablas, boton "+ Agregar", header que se ve raro, columnas, o inline crear/editar desde la tabla. NOTA: los modulos ya NO usan tarjetas KPI/indicadores (ver seccion 7). El objetivo es que TODOS los modulos queden IDENTICOS en tamano, comportamiento y estilo. Referencia viva: modulo Tareas (barra/header/alta) y Zonas Comunes (alta inline).
---

# Homogenizar la vista tabla de PROPIA

## Por que existe

El unico proposito es **homogenizar**: que cada modulo con vista tabla se vea y se comporte IDENTICO.
Cada vez que se toca un modulo "a mano" queda distinto y se genera ir-y-venir. Esta skill es la fuente
unica del patron. Antes de tocar CUALQUIER tabla, seguir esto al pie de la letra. Si algo no esta aqui,
mirar como quedo en **Tareas** (referencia) y NO inventar variaciones.

## Reglas de oro (leer siempre)

- **NO inventar.** Copiar exactamente el patron de Tareas / Zonas. Mismos px, mismos hex, mismas clases.
- **Auditar por MCP Chrome antes de decir "listo"**: medir con `getComputedStyle`/`getBoundingClientRect`,
  y para creacion hacer clic REAL y verificar VISIBILIDAD en viewport (no solo que exista el elemento).
- **No agregar cosas no pedidas** (contadores "X de Y", textos, botones extra). Homogenizar != agregar.
- **Preguntar antes de cambios ambiguos.** Confirmar el ajuste exacto si hay duda.
- Reglas del proyecto: solo ASCII en codigo/comentarios; espanol en UI; respeta tenant_id; build sin
  warnings nuevos. Local: API 7113, Web 5105; matar Propia.Api.exe Y Propia.Web.exe antes de recompilar
  (Web referencia Api); lanzar con ASPNETCORE_ENVIRONMENT=Development. `propia-tokens.css` cachea: subir
  `?v=N` en App.razor al cambiarlo. En claude-in-chrome el javascript_tool devuelve {} en funciones
  async: usar codigo sincrono; para inputs Blazor setear value con el setter nativo + `new Event('input',{bubbles:true})`;
  y OJO: `@bind` por defecto es onchange -> en alta usar `@bind:event="oninput"`. Los `<input type=date>`
  con `@bind` sobre string fallan: usar `value="@x" @oninput=...`.

## 1. Barra de herramientas (toolbar)

Fila flex: IZQUIERDA `[Filtros] [Buscar]`; spacer `flex:1`; DERECHA `[Agrupar] [Campos]`.
Botones ~36px alto, padding 8px 12px, radius 9, borde #E1E8EE, texto 13px/600 color #516F90, gap 6 con icono.
Iconos (SVG 16, stroke 2):
- Filtros: `M3 5h18M6 12h12M10 19h4`
- Agrupar: 4 rects `x=3/14 y=3/14 w=7 h=7 rx=1.5`. Activo (hay agrupacion): fondo #F1ECFD, violeta #6D4FE3.
- Campos: 2 columnas `M3 3h7v18H3zM14 3h7v18h-7z`.
Prohibido: selects sueltos de "Agrupar" o filtros tipo "Todo tipo/Toda torre" fuera del boton Filtros.
Orden de botones a la derecha: **Filtros ... Agrupar, Campos** (Campos siempre al final, mismo lugar).

## 2. Filtros (boton "Filtros")

Reglas dinamicas campo+operador+valor con logica AND/OR. Operadores por tipo:
texto (contiene/no contiene/es/no es/vacio/no vacio), numero (=,!=,>,>=,<,<=,vacio,no vacio),
fecha (es/antes/despues/vacio/no vacio), seleccion (es/no es/vacio/no vacio). Editor de valor segun tipo.
Persistir en localStorage `propia_<modulo>_filtros`. Referencia: `FiltroDinamico` / PqrsKanban.

## 3. Agrupar (boton con MENU)

Boton que abre menu "por que campo agrupar" (incluye "Sin agrupar"). Por defecto NO agrupa. Al seleccionar:
el boton muestra el campo agrupado y queda violeta (persistente). Filas de grupo con contador. SIN boton "Quitar".

## 4. Campos (boton con MENU, NO modal)

Boton "Campos" (icono 2 columnas) que abre un MENU al pie del boton (nunca un modal). Permite por columna:
reordenar (flechas subir/bajar), ocultar/mostrar, y "Mostrar todo"/"Ocultar todo". Aplica a header y filas.
El "orden de campos" va ANTES de Agrupar en la barra. Mismo icono y misma ubicacion en todos los modulos.

## 5. Ordenar por columna

Click en el encabezado ordena; reclick invierte; indicador ▲/▼.

## 6. HEADER de la tabla (referencia: Unidades `.tbl thead th` / `.tbl-hdcell` en tabla-base.css)

Fijo SOLO en vertical (sticky top0) y OPACO; las filas pasan por DEBAJO. El header NO fija columnas horizontales.
La FUENTE UNICA del look vive en `wwwroot/css/modules/tabla-base.css` (frame `.tbl` + clase reutilizable
`.tbl-hdcell`). La referencia ya NO es Tareas: es Unidades (DistribucionPanel). Colores/bordes por TOKEN
(eso mata la divergencia); tipografia por los valores canonicos de abajo. (F2, cerrado 2026-10-01.)

VALORES CANONICOS del header (medir computed, deben coincidir 1:1 con Unidades):
- position:sticky; top:0; z-index:6; background:var(--propia-bg-table-head) (#FAFBFC / rgb(250,251,252)).
- font-size:11.5px; font-weight:700; letter-spacing:.4px; text-transform:uppercase; white-space:nowrap.
- color:var(--propia-text-secondary) (#516F90 / rgb(81,111,144)).
- border-bottom:1px solid var(--propia-border-card) (#E1E8EE / rgb(225,232,238)).
- SEPARADORES de columna: border-right:1px solid var(--propia-border-card) entre celdas de DATOS, full-height;
  excepciones sin separador: seleccion, expander, +columna/acciones y la ultima.
- El PADDING horizontal NO es libre: cada modulo conserva el suyo para que la celda de header quede ALINEADA
  con su columna de cuerpo (medir 0px de drift header<->cuerpo). Forzar el padding de Unidades donde el cuerpo
  tiene otra geometria corre el texto = BUG. Regla: ALINEACION > igualar el numero de padding.

DOS CAMINOS segun la estructura (ambos miden computed == Unidades):
- `<table>` real (PQRSD, Contratos): dar la clase `.tbl-hdcell` (+ `--plain` en expander/acciones) a los `<th>`;
  el look sale de tabla-base.css. `border-collapse: separate; border-spacing:0` (NUNCA `collapse`: el borde
  colapsado se filtra bajo el header sticky y se ven separadores "huerfanos"). El padding queda en el modulo,
  alineado a su `td`.
- Pseudo-tabla CSS-grid (Zonas, Equipos, Residentes, Usuarios, Directorio P+E, Mascotas, Vehiculos):
  `.tbl-hdcell` NO aplica (en grid el sticky+bg+border-bottom van en la FILA, no en la celda; ponerlos por
  celda duplica el sticky y hace el border-bottom un stub bajo cada label, ademas .tbl-hdcell no trae el
  stretch). Se llevan los VALORES canonicos IN-PLACE por token: bg/sticky/border-bottom/tipografia en la FILA
  (`*-head`), separador full-height en las CELDAS. Separador full-height = `align-self:stretch` + mover el
  padding VERTICAL de la fila a la celda (fila `padding:0 <h>`, celda `padding:<v> 0`) + `display:flex;
  align-items:center` para centrar; el padding HORIZONTAL no se toca (alineacion). Alinea sin drift si el grid
  resuelve el MISMO ancho en header y cuerpo (min-width fijo, o celdas con `min-width:0`). Si el grid usa
  `min-width:max-content`, header y cuerpo toman anchos distintos y el separador queda huerfano (ver excepcion).

EXCEPCION Mascotas / Vehiculos (decision de Alex, 2026-10-01): su grid usa `min-width:max-content`, con lo que
header y cuerpo se dimensionan a su propio contenido (anchos distintos) y un separador quedaria huerfano (medido
~94px de drift). Alex eligio NO tocar la geometria del cuerpo: quedan con TODO el tratamiento canonico pero SIN
separadores verticales. (Seguros SI acepto el refactor de geometria -Seguros-B-: `min-width:0` en celdas de
header y cuerpo -> los tracks fr resuelven por peso -> separadores alineados; esto toca el cuerpo y requiere OK
explicito de Alex por modulo.)

DRAG de columnas (F1, comportamiento aparte del DISEÑO del header): el "feel" pro (columna fantasma + drop-line)
vive en `propiaTablaColReorder` (propia-ui.js) + clases `.tbl-col-*` (tabla-base.css). Solo las celdas con
`data-clave` son arrastrables (`[data-clave]{cursor:grab}` global). NO poner `data-clave` en un header que no
cablee `OnColReorder`: dispararia el cursor grab sin drag real. El drag NO es parte del "diseño del header";
no se agrega en modulos que no lo tengan ya.

## 7. Sin KPIs / indicadores (decision de producto)

Los modulos de vista tabla **NO** llevan tarjetas de indicadores (KPI). Se eliminaron por decision
de producto: el header pasa directo del titulo/subtitulo a la barra de filtros, sin bloque `.mod-stats`.
NO agregar tarjetas `.mod-stat`. Si un modulo usaba esas tarjetas como TABS (Directorio: Personas/Empresas,
Usuarios: Activos/Pendientes/Inactivos), se reemplazan por **tabs de texto simples** (`.dir-tab` / `.usr-tab`:
boton plano con subrayado inferior en el activo), no por tarjetas KPI.

## 8. Columna expander (primera) + ALTA INLINE

- 1a columna del EXPANDER: **ancho fijo 34px** (ref PQRS `.pk-th-exp,.pk-td-exp{width:34px;text-align:center;padding:0 4px}`;
  Tareas `.tb-row-exp{width:34px;flex:0 0 34px}`). Icono expander `M15 3h6v6M9 21H3v-6M21 3l-7 7M3 21l7-7`; en
  filas de datos abre la ficha al hover.
- NO poner un "+" decorativo dentro de la celda del titulo. El unico control de alta es el boton "+ Agregar".
- Fila de alta: captura TODOS los campos (texto->input; lista/enum->select; fecha->`input type=date` con
  value/@oninput; numero/moneda->number; persona->`SelectorPersona Flotante="true"`; unidad->`SelectorUnidadCascada`;
  dinamicos por tipo). Automaticos (consecutivos, semaforos, fechas calc) muestran "(auto)".
- Boton **"+ Agregar"** AL FINAL de la fila (columna de acciones), estilo `zc-row-add` (ver 9-D). SIEMPRE
  habilitado (sin `disabled`); validar minimos al clic con toast.
- **La columna de acciones / "+ Agregar" NO es una columna FIJA (nada de position:sticky right).** Va al
  final y se alcanza con el scroll horizontal (barra superior espejo tipo Tareas). NO fijarla a la derecha.

## 9. Bordes/colores del inline (referencia Tareas)

- A. INPUTS/SELECTS de la FILA DE ALTA (`.tb-nf-inp`): reposo `1px dashed #D7E0EA` (punteado gris; NUNCA
  violeta fijo). radius 7; padding 5px 8px; font 12.5px; color #33475B; background #fff; box-sizing border-box.
  FOCUS: `border-style:solid; border-color:#6D4FE3; box-shadow:0 0 0 2px rgba(109,79,227,.12)`. Numeros: text-align right.
  Dark: bg #232342; border #3a3a5a; color #D5D5E0.
  - **Aplica a TODAS las clases de alta de cada modulo** (mismos valores exactos), no solo a `.tb-nf-inp`:
    `.dst-nf-inp` (Unidades), `.dtab-inp` (Directorio), `.utb-inp` (Usuarios), `.zc-row-inp` (Zonas),
    `.ea-row-inp` (Equipos), etc. Un alta con borde **SOLIDO** en reposo (o sin el glow al focus) es un BUG
    de homogeneidad: se ve como caja de formulario maciza en vez del input "fantasma" punteado del canon.
- B. EDICION de celda existente (`.tb-cell-inp`): `1px solid #6D4FE3` + `box-shadow 0 0 0 2px rgba(109,79,227,.12)`
  (violeta = edicion activa). radius 6; padding 4px 7px; font 12.5px. Solo mientras la celda esta en edicion.
- C. SELECTS: mismas reglas. `<optgroup>` cuando se agrupan opciones (ej "Asociado a" -> Equipos/Zonas).
- D. BOTON "+ Agregar" (`zc-row-add`, violeta SUAVE): background #F1ECFD; color #6D4FE3; border 1px solid #DCD2F8;
  radius 7; font 11.5/700; padding 6px 10px. Hover #E4DBFB. Disabled opacity .5. AL FINAL, NO fijo.
- E. FILA RECIEN CREADA: background #ECFBF2 + `box-shadow: inset 3px 0 0 #34C759` en la 1a celda; hasta refrescar.
- F. El violeta SOLIDO es exclusivo de edicion activa; la alta va punteada gris y solo violeta al focus.

## 10. Al AGREGAR

Crear por API; limpiar la fila; toast breve. Dejar el registro recien creado RESALTADO (verde, punto E) hasta
el proximo refresco (rastrear ids en `_recienCreados` y empujarlos al final con OrderBy ESTABLE si el orden
natural los moveria; al recargar vuelve al orden natural). Comportamiento de abrir/no abrir modal segun pida
el modulo (por defecto NO abrir modal salvo que se indique).

## 11. Edicion inline de filas existentes

Celdas editables in situ; listas como desplegables; guardar por API (PUT MERGE, no pisar lo no enviado);
solo las celdas permitidas por las reglas del modulo.

## 12. Buscador de persona/tercero flotante

`SelectorPersona Flotante="true"` cuando vive en una fila (contenedor con overflow que lo recorta): panel
`position:fixed` anclado al input, con flip arriba si no cabe, clamp al viewport, max-height, z-index ~4000,
tarjeta con sombra. Solo pasar el parametro.

## 13. Anchos minimos de columnas

Dar `min-width` a columnas que cortan el dato (ref PQRSD: Radicado 140, Asunto 280/resumen 380, Tipo 120,
Estado 155, Unidad 170, Persona 240). Medir en Chrome que no se corte.

## 14. Footer / paginador de la tabla (referencia: Unidades `.dst-pager`)

Barra inferior FIJA al pie de la tabla, con el conteo a la izquierda y los controles a la derecha.
Referencia canonica: Distribucion.razor (Unidades) y ZonasComunes.razor.
- **UBICACION (critico):** el footer va DENTRO del MISMO contenedor con borde/radio/`overflow:hidden`
  que envuelve la tabla (NO como hermano/pegado despues del contenedor, que se ve "flotando"). Debe ser
  el ultimo hijo de esa caja, despues de la zona scrolleable/filas y de la fila de alta. Asi comparte el
  borde, el radio inferior y el `overflow:hidden` de la caja y se integra (su `border-top` lo separa de
  las filas). Estructura canonica: `caja(border+radius+overflow:hidden) > [scroll/filas] + [fila alta] + <TablaPager/>`.
  - Modulos cuyo contenedor de tabla ES el scroll (overflow:auto con borde/radio, ej. PQRSD `.pk-table-wrap`,
    Contratos `.ctr-table-wrap`): envolver `[scroll][footer]` en una caja externa `overflow:hidden` con el
    borde/radio, y quitarselos al scroll interno (para que el footer no scrollee en horizontal con la tabla).
  - Modulos con sub-vista (Directorio/Usuarios): renderizar el `<TablaPager>` DENTRO de la sub-vista (pasarle
    Total/Pagina/PorPagina + callbacks), para que quede dentro de su propio contenedor de tabla.
- Usar SIEMPRE el componente compartido `TablaPager` (fuente unica del diseno).
- Contenedor: `display:flex; align-items:center; justify-content:space-between; gap:12px; flex-wrap:wrap;
  padding:10px 16px; background:#FAFBFC; border-top:1px solid #EEF2F6`.
- IZQUIERDA `.dst-pager-info` (12.5px, color #6B7C8F): `Mostrando {desde}-{hasta} de {total} {entidad}`
  (ej. "Mostrando 1-25 de 213 unidades"). Con 0 registros -> "Mostrando 0 de 0".
- DERECHA `.dst-pager-ctrl` (flex, gap 8):
  - `<select>` tamano de pagina con opciones **25 / 50 / 100** ("N por pagina"). Borde #E1E8EE, radius 7,
    12.5px; focus violeta. SIEMPRE visible.
  - Anterior / posicion / Siguiente SOLO si `TotalPaginas > 1`: botones 28x28 (borde #E1E8EE, radius 7,
    hover violeta, disabled opacity .4) con chevrons `M15 6l-6 6 6 6` / `M9 6l6 6-6 6`, y en medio
    `.dst-pager-pos` "{pagina} / {total}" (12.5px/700, min-width 52px, centrado).
- Estado/計算 en el @code: `_porPagina` (default 25), `TamanosPagina = {25,50,100}`, `_pagina`,
  `TotalPaginas = ceil(total/porPagina)`, `Desde=(pagina-1)*porPagina`, `Hasta=min(Desde+porPagina,total)`,
  `Pagina = Filtradas.Skip(Desde).Take(porPagina)`. La paginacion cuenta sobre la lista YA FILTRADA.
- **El footer se muestra SIEMPRE que la tabla este visible, incluso con 0 filas** (NO gatear con
  `Total > 0`). Con la tabla vacia el footer sale igual: "Mostrando 0 de 0 {entidad}" + selector "25 por
  pagina" (sin anterior/siguiente). El conteo + el selector "por pagina" se ven siempre; anterior/siguiente
  solo con >1 pagina. Regla de vista vacia (decision de producto): un modulo de vista tabla, al quedar
  **sin filas, NO muestra una tarjeta de estado vacio en lugar de la tabla**: renderiza la MISMA tabla
  (barra + cabecera + fila de alta + footer "Mostrando 0 de 0") para poder empezar a llenar en la fila
  inline desde cero. Referencia: Unidades, Directorio (Tabla) y Usuarios (Tabla).
- **Unica excepcion al footer:** con **agrupacion activa** NO se pagina (se muestran los grupos completos,
  sin footer). Por eso el gate correcto es `@if (Grupos is null)` (o el equivalente "sin agrupar"), NUNCA
  `Total > 0`.
- La vista Tarjetas/Lista comparte la misma paginacion y footer (mismo _pagina/_porPagina). En Tarjetas/Lista
  con 0 filas si es valido un mensaje breve (no la tabla), pero la vista Tabla siempre lleva la tabla + footer.
- Modulos con footer canonico: Unidades (Distribucion) = referencia. **Directorio (Personas/Empresas),
  Usuarios, Zonas Comunes y Equipos y Activos ya tienen el footer canonico via `<TablaPager>`, visible
  tambien con 0 filas** (en su vista Tabla). En Zonas/Equipos el cartel de vacio (`zc-empty`/`ea-empty`)
  se muestra SOLO en la vista Tarjetas (`_view == "cards"`); en la vista Tabla el vacio lo comunica la
  propia tabla (cabecera + fila de alta + footer "Mostrando 0 de 0"). Contratos tiene un paginador VIEJO
  distinto (centrado, "Pagina X de Y", sin conteo ni "por pagina") que debe migrarse a este.
  Tareas/PQRSD/Seguros: al homogenizar, agregar este footer (aunque la lista sea corta o vacia: mostrara
  "Mostrando N de N" + "25 por pagina" sin anterior/siguiente).

## Protocolo de auditoria (obligatorio por cada punto)

1. Medir con getComputedStyle/getBoundingClientRect y comparar contra Tareas/Zonas (KPI vs tb-bkpi; boton
   bg rgb(241,236,253) color rgb(109,79,227); input alta borderStyle dashed color rgb(215,224,234)).
2. Alta: escribir (evento input real), confirmar boton habilitado, hacer CLIC REAL y verificar que el
   registro se crea, aparece resaltado y (si aplica) toast. VALIDAR VISIBILIDAD en viewport, no solo existencia.
3. Persistencia: confirmar en BD/recarga que se guardaron listas/persona/unidad.
4. Flotante: con la fila al fondo, verificar position:fixed y flip.
5. Header: con scroll, el header queda arriba OPACO y las filas pasan por debajo (no por detras).
6. Limpieza: eliminar SIEMPRE los registros de prueba. Si hay triggers append-only, en una transaccion
   `SET LOCAL session_replication_role='replica'` antes de los DELETE (solo dev).
7. Entregar compilando y con resumen de lo medido por punto. NO marcar listo sin auditar en Chrome.

## Estado de homogenizacion (mantener en el inventario)

El detalle y el avance por modulo viven en el vault:
`D:\Obsidian\Propia\03. NOTAS DE DESARROLLO\Homogeneidad UI - Barra de tabla (inventario).md`.
Actualizar ahi (icono verde por observacion resuelta) tras auditar cada punto.
