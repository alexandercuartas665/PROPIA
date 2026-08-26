---
name: homogenizar-vista-tabla
description: Patron CANONICO unico de la "vista tabla" de PROPIA (toolbar, filtros, agrupar, campos, orden, header fijo, KPIs, expander, alta inline, edicion inline, bordes/colores). USA SIEMPRE que se cree o ajuste la vista tabla / modo tabla de cualquier modulo (Contratos, Seguros, Directorio, Unidades, Zonas, Equipos, Usuarios, PQRSD, etc.) o cuando el usuario hable de homogenizar tablas, boton "+ Agregar", header que se ve raro, KPIs de indicadores, columnas, o inline crear/editar desde la tabla. El objetivo es que TODOS los modulos queden IDENTICOS en tamano, comportamiento y estilo. Referencia viva: modulo Tareas (barra/header/alta) y Zonas Comunes (alta inline).
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

## 6. HEADER de la tabla (referencia: Tareas `.tb-list-hdr`)

Fijo SOLO en vertical (sticky top) y OPACO; las filas pasan por DEBAJO. El header NO fija columnas horizontales.
- Contenedor scroll = la lista (`overflow:auto; max-height: calc(100vh - 190px)`). Scroll dentro de la tabla.
  Tareas ofrece barra horizontal espejo ARRIBA (`.tb-topscroll`, sincronizada) para alcanzar columnas.
- Header row: `display:flex; align-items:center; gap:12px; padding:10px 18px; border-bottom:1px solid #EEF3F8;
  background:#FAFBFC; position:sticky; top:0; z-index:8; min-width:max-content`. Fondo SOLIDO (nunca transparente).
- Celdas header: 10.5px, peso 700, letter-spacing .4px, color #A6B7C8, uppercase.
- CASO `<table>` real (PQRSD/Contratos): `<th> position:sticky; top:0; background solido; z-index alto` +
  **`border-collapse: separate; border-spacing:0`** (NUNCA `collapse`: el borde colapsado se filtra bajo el
  header y parece que los datos pasan por detras). Separadores de fila en el `td`, no en el `tr`. Sombra
  opcional `box-shadow: 0 2px 4px -2px rgba(27,42,58,.12)`.

## 7. KPIs (indicadores) - tamano de Tareas (tb-bkpi)

Clase compartida `.mod-stat*` (propia-tokens.css) ya alineada a tb-bkpi: tarjeta ~51px alto, icono 30x30
radius 8, valor 17px, label 10.5px color #A6B7C8 peso 600, padding 7px 11px, radius 11.
Grid: `repeat(auto-fit, minmax(128px,1fr))`, gap 8, margin-bottom 10. NO tarjeta grande, NO override local.

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
