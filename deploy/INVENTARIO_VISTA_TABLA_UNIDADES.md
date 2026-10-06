# Inventario de la vista tabla — Unidades Privadas (tópicos)

> Catálogo de TODO lo que se pidió/construyó en la vista tabla de **Unidades Privadas**, que es el
> **canon** que el resto de módulos (Usuarios, Directorio, Roles, PQRSD, Tareas, Seguros, Contratos,
> Zonas, Equipos, Vehículos, Mascotas, Residentes) deben copiar al homogenizar.
> Última revisión: 2026-10-06 (sesión ALEX).

**Componente de referencia:** `src/Propia.Web/Components/Pages/Capa2/DistribucionPanel.razor` (`@page "/distribucion"`).
**CSS canon:** `src/Propia.Web/wwwroot/css/modules/tabla-base.css` (clases `tbl`, `tbl-*`, `row-add--sm`, `tbl-fab`).
**Interop JS:** `src/Propia.Web/wwwroot/js/propia-ui.js` (`propiaUI.dstInfinite`, `dstScrollToCta`, `propiaFloatPos`) + `propiaTablaColReorder` + `propiaTablaResize`.
**Campos dinámicos (EAV):** patrón `<X>CampoDefinicion` + `<X>CampoValor` + `<X>CampoConfig` por tenant (ver CLAUDE.md).

## Barra de herramientas y organización

| Tópico | Descripción | Referencia de código |
|---|---|---|
| Barra de herramientas (toolbar) | Fila superior: `[Filtros] [Buscar] ... [Agrupar] [Campos]` + selector `Tarjetas/Tabla`. Mismo orden/altura en todos los módulos. | `DistribucionPanel.razor:342-380`; CSS `:3000` (`/* Barra de filtros */`) |
| Filtros (reglas dinámicas) | Botón "Filtros" que abre el editor de reglas campo+operador+valor con AND/OR; persiste en localStorage. Componente compartido. | `<FiltroDinamico>` `DistribucionPanel.razor:349`; `Components/Shared/FiltroDinamico.razor` |
| Buscador | Input de texto que filtra por unidad/propietario. Sin `value=` (Blazor Server lo reafirma). | `DistribucionPanel.razor:352` (`.dst-search-inp`, `@bind _busca`) |
| Agrupar (menú multi-nivel) | Botón que abre menú "agrupar por campo" (N niveles); filas de grupo con contador; por defecto sin agrupar. Motor + menú compartidos. | `<TablaAgruparMenu>` `:359`; `ColsAgrupablesU()` `:1352`; `TablaAgrupador.Agrupar` `:1397` |
| Ordenar por columna | Clic en el encabezado ordena; reclic invierte; indicador ▲/▼. | `OrdenarPorColumna()` `:2491`; header `@onclick` `:498`; flecha `.tbl-th-arrow` `:500` |

## Encabezado (header) y columnas

| Tópico | Descripción | Referencia de código |
|---|---|---|
| Menú emergente de columna (chevron ˅) | Flecha hacia abajo **siempre visible** a la derecha de cada header (right:12px). Abre menú estilo Airtable: ocultar, renombrar/alias, agrupar, eliminar (si es dinámico). | `ChevCol()` `:1205` (`.tbl-th-edit`); `AbrirColMenu()` `:1101`; `<TablaColMenu>` `:206`; CSS `tabla-base.css:37` |
| Reordenar columnas (arrastre) | Arrastrar el header reordena la columna; persiste en la config del tenant. | `IniciarReorder()` `:2536` → `propiaTablaColReorder.start` |
| Redimensionar columnas | Handle a la derecha de cada header; arrastrar ajusta ancho, doble clic autoajusta. | `.tbl-col-rz` `:502`; `IniciarResize()` `:2589`; `ResetResize()` `:502` → `propiaTablaResize.start` |
| Botón "+" agregar campo (header) | Botón "+" al final del header que abre el modal de crear campo (columna dinámica nueva). | `.tbl-addcol-btn` `:505` → `AbrirCrearCampo` |
| Columna expander (1ª útil) | Ancho fijo 34px; en filas de datos la flecha diagonal abre la ficha al hover; en la fila de alta crea + abre la ficha. | `.tbl-exp` / `.tbl-exp-btn` `:654` → `CrearUnidadDesdeAltaAsync(true)`; CSS `tabla-base.css:82-86` |

## Selección y acciones masivas

| Tópico | Descripción | Referencia de código |
|---|---|---|
| Checkbox selección múltiple | Checkbox por fila + "seleccionar todo" en el header. | header `.tbl-sel` `:490`; fila `:518`; `ToggleSel()` `:991`; `ToggleSelAll()` `:993`; `TodasSeleccionadas` `:1000` |
| Barra de acciones masivas | Aparece al seleccionar ≥1 fila; permite eliminar en lote (con confirmación). | `:425-440` (`.dst-selbar`); `EliminarSeleccionadasAsync` `:1004`; CSS `:3079` |
| Menú contextual (clic derecho) | Clic derecho sobre una fila abre menú (abrir ficha, copiar enlace, eliminar). | `AbrirCtx()` `:2564`; `<TablaCtxMenu>` `:203` |

## Alta inline (fila nueva) y botón flotante

| Tópico | Descripción | Referencia de código |
|---|---|---|
| CTA colapsable "Agregar registro" | Por defecto la fila de alta está colapsada como botón `.row-add--sm` "Agregar registro"; al pulsarla se abre la fila de edición. **Ubicación (canon Unidades):** es la **ÚLTIMA fila del `<tbody>`** de la propia `<table>` (`<tr class="newrow" id="dst-cta-row">`), con celdas vacías `tbl-sel` + `tbl-exp` y un `<td colspan>` que abarca el resto; así el botón queda **INDENTADO** (empieza alineado con la 1ª columna de datos, después del checkbox + expander), con fondo `--propia-bg-table-head` y `border-top`. | `.row-add--sm` `:768` (`<tr class="newrow" id="dst-cta-row">` `:764`) → `AbrirAlta()` `:1020`; `CerrarAlta()` `:1021`; CSS `tabla-base.css:109` |
| ⚠️ Divergencia: ubicación del botón de alta en Usuarios | **Usuarios NO lo ubica igual.** No es una fila de la tabla: es un `<div class="utb-cta-row">` **fuera del grid** (hermano, después de las filas), con `padding:8px 14px` + `border-top`. Resultado: el botón "Agregar registro" queda **pegado al borde izquierdo** del contenedor, **sin indentar ni alinear con las columnas** (a diferencia de Unidades, donde arranca tras checkbox+expander). Para homogenizar al canon habría que renderizarlo como fila del grid con las primeras celdas vacías (o replicar el `colspan`/indent). Diagnóstico 2026-10-06 (ALEX). | Unidades `DistribucionPanel.razor:764-772` (tr dentro de `<tbody>`); Usuarios `UsuariosTablaVista.razor:156-161` (`.utb-cta-row`); CSS `usuarios.css:142` vs `tabla-base.css:109` |
| Botón flotante (FAB) | Botón flotante abajo-izquierda que aparece al desplazar cuando la fila de alta no se ve; abre el alta y baja a ella. | `.tbl-fab` `:782` → `FabAgregarAsync()` `:1027`; CSS `tabla-base.css:122` |
| Scroll infinito (por lotes) | Centinela (IntersectionObserver) que carga el siguiente lote al llegar al final y controla el FAB. | `CargarMas()` `:1229`; `propiaUI.dstInfinite` `:1910` |
| Fila de alta inline (todos los campos) | ✅ **El alta captura ya todos los campos visibles** (fijos + dinámicos), igual que la edición. Fijos: `num`/código, `tipo`, `area`, `coef`, `matricula`, `refpago` y —desde 2026-10-06 (ALEX)— **`estado`** (select de las opciones configuradas), `habitaciones`/`banos`/`parqueaderos` (number), `pagaadmin` (Si/No), `cuota` (number), `observaciones` (texto) y `modcontrib1..5` (number). `torre`/`piso`/`agrupacion` siguen automáticos (derivados). Dinámicos: todos por tipo. Inputs "fantasma" (`.nf-in`/`.nf-cell`). El `default` "En la ficha" ya solo cubre lo verdaderamente derivado. | fijos `:712-787` (casos añadidos `:747-787`); `CrearUnidadRequest` `:1803` (ya recibe estado/hab/banos/parq/pagaadmin/cuota/obs/modcontrib); dinámicos `:663-704`; CSS `.nf-in` |
| Botones de confirmar/cancelar fila | En la fila de alta: ✓ guardar (28px, brand) y ✗ cancelar (bordeado). Enter guarda, Esc cancela. | `.dst-nf-ok` `:753` / `.dst-nf-cancel` `:756` → `CrearUnidadDesdeAltaAsync(false)` / `CerrarAlta`; `Key()` `:1036`; CSS `:3191-3194` |
| Selector de persona flotante | En la fila de alta, el campo persona usa `SelectorPersona Flotante="true"` (panel position:fixed que no recorta el overflow). | `:700` (`<SelectorPersona Flotante="true">`) |
| Código inteligente TORRE-NÚMERO | El número de unidad se teclea como `A1-202`; la torre sale del código y el piso se infiere. | `:712` (placeholder/título del input Nº) |
| Fila recién creada resaltada (verde) | La fila creada queda resaltada (verde) hasta el próximo refresco. | `.tbl-row-new` + `_uniRecienCreados` `:517` |
| Edición inline de celdas existentes | Celdas editables in situ (PUT MERGE, no pisa lo no enviado); solo las permitidas por reglas del módulo. | celdas `.nf-cell`/dinámicas; servicio `MiCopropiedadService.*.cs` (`ActualizarUnidadAsync`) |

## Gestor de campos (crear / editar / campos fijos y vinculados)

| Tópico | Descripción | Referencia de código |
|---|---|---|
| Modal para crear campo nuevo | Modal estilo Airtable para crear una columna/campo dinámico (nombre, tipo, opciones); con tipos avanzados. | `<CrearCampoModal>` `:182`; `Components/Shared/Modals/CrearCampoModal.razor` |
| Editar campo fijo / de sistema | Modal para editar un campo fijo (alias, opciones de lista, etc.) sin romper su naturaleza de sistema. | `<EditarCampoSistemaModal>` `:195`; trigger `_editSisClave`; `AbrirEditarCampoSistema` |
| Editar / eliminar campo dinámico | El engranaje de una columna dinámica edita el campo (con opción Eliminar). | `AbrirColMenu` (caso dinámico) `:1101`; `CrearCampoModal` en modo `Editar` `:186` |
| Gestor de campos (config avanzada) | Modal "Configuración avanzada de campos": orden, visibilidad, tipo y comportamiento de **todos** los campos (fijos + dinámicos) de la unidad. Componente compartido. | `_configCamposAbierto` `:212`; `<ConfigCamposEntidad Prefijo="unidades">` `:228`; `Components/Shared/ConfigCamposEntidad.razor` |
| Listado de campos relacionados / entidades vinculadas | La unidad muestra/gestiona campos de entidades vinculadas (Persona, Vehículo, Mascota) con sus propios campos fijos y dinámicos (patrón vinculados estilo Airtable). | `GestionarUnidadesModal.razor`; entidades `Domain/Entities/UnidadVinculadosCampoEntities.cs`; catálogo `Application/MiCopropiedad/UnidadCamposSistema.cs` |
| Catálogo de campos del sistema | Define los campos fijos de la unidad (clave, label, encabezado de plantilla, visible por defecto). | `Application/MiCopropiedad/UnidadCamposSistema.cs` |

## Datos, footer e importación

| Tópico | Descripción | Referencia de código |
|---|---|---|
| Footer / conteo / paginador | Barra inferior fija dentro de la caja de la tabla: conteo "Mostrando X-Y de N" + selector 25/50/100 + anterior/siguiente. Visible aun con 0 filas; sin footer al agrupar. | `<TablaPager>` (`Components/Shared/TablaPager.razor`); CSS `.dst-pager` (ver skill `homogenizar-vista-tabla` §14) |
| Carga por Excel / plantilla | Importación masiva desde Excel (plantilla multi-hoja: Unidades/Zonas/Equipos) y CSV; crea/actualiza unidades (upsert por número). | `importar-carga` → `UnidadesCargaImportService.cs`; `importar-csv` `:2870`; `GestionarUnidadesModal.razor` |
| Modo oscuro por tokens | Colores solo por `var(--propia-*)`; prohibido hex/`white` en `.razor`; subir `?v=N` al editar CSS cacheado. | `wwwroot/css/propia-tokens.css`; `Components/App.razor` (links `?v=`) |

## Cross-cutting (reutilizar, no reinventar)

| Tópico | Descripción | Referencia de código |
|---|---|---|
| Frame de tabla canon | Clases base de la vista tabla (header sticky, celdas, expander, addcol, fab, row-add). | `wwwroot/css/modules/tabla-base.css` |
| Interop de tabla (JS) | Scroll infinito + FAB, reordenar y redimensionar columnas, flotantes con flip/clamp. | `propia-ui.js` (`dstInfinite`, `dstScrollToCta`, `propiaFloatPos`); `propiaTablaColReorder`; `propiaTablaResize` |
| Menús compartidos | Menú de columna, menú de agrupar y menú contextual son componentes compartidos (misma UX en todos los módulos). | `TablaColMenu.razor`, `TablaAgruparMenu.razor`, `TablaCtxMenu.razor`, `TablaGruposRender.razor`, `TablaPager.razor` |

---

### Estado de homogeneización (quién ya copia el canon)

- **Menú de columna (chevron ˅ siempre visible a la derecha, gap 13px):** Unidades, Equipos, Mascotas, Zonas, Vehículos, Residentes, PQRSD, Tareas/Tableros, Seguros/Pólizas, Contratos, **Usuarios y Directorio** (normalizados 2026-10-06). ✓ todas.
- **Header limpio (sin separadores verticales dobles):** normalizado en Roles (2026-10-06).
- **Checkbox selección + acciones masivas + botón de alta `row-add--sm`:** Unidades (canon); replicado en Roles (2026-10-06); Usuarios/Directorio ya con alta `row-add--sm`.
- **Pendiente de revisar:** paginador viejo de Contratos (migrar a `TablaPager`); verificar FAB/footer en módulos que aún no cierran la homogeneización.

> Referencia viva del patrón y del avance: skill `homogenizar-vista-tabla` y
> `03. NOTAS DE DESARROLLO/Homogeneidad UI - Barra de tabla (inventario).md`.
