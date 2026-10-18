# DEPLOY HANDOFF - PROPIA v0.0.180 (2026-10-18)

> **LEEME PRIMERO.** Handoff autocontenido. La tanda **0.0.166 -> 0.0.180 es SOLO CODIGO (Razor/CSS/JS),
> SIN migracion nueva y SIN cambios de config/env/secrets**. PERO este deploy ACUMULA desde lo que haya
> hoy en prod: si prod aun no tiene las 2 migraciones de 0.0.156 y 0.0.158, **hay que aplicarlas**
> (ver "Migraciones / BD"). Son las MISMAS dos pendientes que ya figuraban en el handoff 0.0.165.

## Resumen de 1 linea
Estabilizacion de vista-tabla (alta inline que guarda con Tab/clic-fuera tambien agrupado; separadores de
header en Mascotas/Vehiculos; alineacion en Residentes), ficha de Tercero en modal (se retiran las fichas de
pagina completa), validacion de rango numerico en el alta de Unidades, **Mi Copropiedad standalone reducido a
solo el hero**, y fix del modal de "Cargar plantilla (Excel)" que ahora se estila en todas las paginas.

## Que desplegar
- Rama: `main`, HEAD **`bd08d36e`**.
- Version visible: **0.0.180** (`src/Propia.Web/Propia.Web.csproj <Version>`).
- Repo: https://github.com/alexandercuartas665/PROPIA
- Base: parte de **0.0.165** (`97c1511d`). Detalle de 0.0.150 -> 0.0.164:
  `deploy/DEPLOY_HANDOFF_2026-10-14_v0.0.149.md`; de 0.0.165: `deploy/DEPLOY_HANDOFF_2026-10-15_v0.0.165.md`.

## Migraciones / BD
**La tanda 0.0.166 -> 0.0.180 NO agrega NINGUNA migracion.** Es 100% Razor/CSS/JS. La ultima migracion del
repo sigue siendo `20261015110445_AddVehiculoMascotaPropietario`.

**Pendientes del acumulado (aplicar SOLO si prod no las tiene ya):**
1. **`20261015011139_AddPersonaComentarios`** (de 0.0.156) - tabla nueva `persona_comentarios` + RLS
   (ENABLE+FORCE+policy `tenant_isolation`+GRANT `propia_app`). Aditiva.
2. **`20261015110445_AddVehiculoMascotaPropietario`** (de 0.0.158) - columna nullable
   `propietario_persona_id` (uuid) en `unidad_placas` y `unidad_mascotas`. Aditiva, no toca RLS.

Aplicar con owner `propia` (design-time):
```
cd src/Propia.Api
"$USERPROFILE/.dotnet/tools/dotnet-ef.exe" database update --project ../Propia.Infrastructure --startup-project .
```
`database update` aplica solo lo que falte. Ambas son aditivas y reversibles. **Si prod ya esta en 0.0.165 o
superior, no hay nada que aplicar.**

## Seed / catalogos (se auto-siembra al arrancar, sin paso manual)
- **Catalogo `tercero.servicio`** (nuevo, aditivo): lista para la ficha de Tercero
  (Mantenimiento, Vigilancia, Aseo, Jardineria, Juridico, Contable, Servicios publicos, Otros).
  `CatalogoListasSeeder.Registro()`. No requiere migracion ni accion manual: el seeder corre en el arranque.

## Config / env / secrets
**Sin cambios.** (El unico cambio en `.csproj` es el `<Version>`.)

## Cache-bust de assets estaticos (ya resuelto por `?v` en App.razor)
Al desplegar el nuevo `App.razor` los `?v` fuerzan recarga; no hay purga manual. Cambiaron:
- `css/modules/mi-copropiedad.css` **?v=1 -> ?v=4**
- `css/modules/config-campos.css` **?v=8 -> ?v=9**
- `js/propia-ui.js` **?v=23 -> ?v=24**

## Rutas retiradas (pueden quedar enlaces viejos -> 404/blanco, es esperado)
- `/directorio/personas/{id}` y `/directorio/empresas/{id}` (fichas de pagina completa). La ficha de
  persona/empresa/tercero vive ahora SOLO en modal; el directorio (`/directorio`) sigue igual. Se eliminaron
  `PersonaFicha.razor` y `EmpresaFicha.razor`. `GlobalSearch` ahora lleva a `/directorio`.

## Cambios de codigo (15 commits, 0.0.166 -> 0.0.180)
Agrupados por tema (todos en `main`, de `97c1511d` exclusivo a `bd08d36e`):

### A. Vista-tabla: alta inline guarda con Tab/clic-fuera tambien con agrupacion/orden activos
- `db14d2f5` Unidades y `1267f83b` (8 modulos: Directorio personas/empresas, Equipos, Zonas, Seguros,
  Residentes, Mascotas, Vehiculos). El cableado de `altaAutoSave` estaba gateado dentro del guard de
  agrupacion, asi que con la tabla agrupada el Tab/clic-fuera no creaba la fila. Se desacopla (se mantiene
  gateado solo el scroll infinito). Archivos: los *Panel.razor respectivos + `DirectorioPersonasVista/EmpresasVista`.

### B. Terceros: ficha en modal + retiro de las fichas de pagina completa
- `95bb3fca` **FichaTerceroModal.razor** (nuevo): ficha de Tercero en el modal estandar (is-fixedh), mapeada
  sobre Persona/Empresa + etiquetas (sin migracion); servicio por catalogo `tercero.servicio`. Cableada en
  la bandeja de Terceros (DistribucionPanel/Directorio).
- `bc3fd65c` Se eliminan las vistas de pagina completa (PersonaFicha/EmpresaFicha) de TODO el sistema; se quita
  "Abrir en el Directorio" de los modales de ficha; `GlobalSearch` -> `/directorio`.

### C. Residentes (alta) y SelectorPersona
- `062f9562` Alta de Residentes: el desplegable de unidad pasa a flotante (`position:fixed` con flip arriba si
  no cabe, via `propiaFloatPos`); columna "Documento" oculta por defecto; cajas de alta homogeneas (borde
  punteado canon).
- `db237733` `SelectorPersona` gana `AnchoMinPanel` para que el panel flotante siga el ancho del input y no
  invada la columna vecina (ROL). `propia-ui.js` `propiaFloatPos(a, p, minW)`.
- `b1fb95a6` Alta de Residentes: se alinea la fila de alta con header/filas (se quita el padding izquierdo de 14px).

### D. Mascotas / Vehiculos: separadores verticales del header
- `4461facb` + `e7cba8de` Mascotas: separadores en el header entre columnas (canon Residentes/Zonas), con fix
  de altura (los separadores con margenes negativos colapsaban el header a 27px -> se usa padding, no margenes).
- `b5fee9c6` Vehiculos: mismo patron correcto.

### E. Unidades: validacion de rango numerico en el alta inline
- `4300d382` `DistribucionPanel.CrearUnidadDesdeAltaAsync` valida el rango ANTES del POST: coeficiente y
  modulos contributivos son `NUMERIC(7,4)` (max 999.9999); area `NUMERIC(10,2)` y cuota `NUMERIC(14,2)`. Evita
  el 500 crudo por `Postgres 22003 numeric field overflow` y muestra un mensaje claro dejando el alta abierta.

### F. Mi Copropiedad (vista standalone `/mi-copropiedad`): reducida a SOLO el hero
Cuatro recortes por pedido de Alex. **IMPORTANTE: solo afecta la ruta suelta `/mi-copropiedad` (no `Embedded`).
El hub de Configuracion monta `MiCopropiedadPanel` con `Embedded=true` y esos bloques ya no se mostraban alli;
la identidad se sigue editando desde el hub de Configuracion.**
- `7faac65d` Quita el rail lateral del Asistente IA (columna derecha con AskAi) -> solo columna de contenido.
- `87f24ffe` Quita "Hitos de configuracion" + "Guiame paso a paso" (y su wizard), y el bloque
  "Recaudo y cuentas bancarias" (el recaudo se gestiona desde Finanzas).
- `bd08d36e` Quita tambien la tarjeta "Identidad". La pagina queda unicamente con el hero (banner, logo,
  nombre, direccion, chips, KPIs).
- Se elimino el codigo/CSS que quedo sin uso (campos, metodos, reglas `.mc-rail*`, `.mc-cta-guia`,
  `.mc-hitos-*`, `.mc-read*`).

### G. Modal de "Cargar plantilla (Excel)" ahora se estila en TODAS las paginas
- `fdf52577` **Fix del bug "a veces el modal no se renderiza bien" (era determinista, no aleatorio).** El
  CUERPO del modal (`.cg-drop`, `.cg-actions`, `.cg-btn-*`, `.cg-confirm*`, `.cg-progress*` + `@keyframes
  cgIndet`, `.cg-summary`, `.cg-tbl`, `.cg-log*`, `.cg-row-warn`) vivia solo en el `<style>` inline de
  `DistribucionPanel.razor`, que solo esta en el DOM en `/distribucion`. Por eso el mismo modal abierto desde
  Residentes hub (`HubCargaMasiva`), el hub de Configuracion o Documentos caia sin estilos. Se movio todo ese
  cuerpo al CSS global `config-campos.css` (que `App.razor` carga siempre) y se quito del inline. De paso se
  corrigio un valor roto por un reemplazo de token:
  `.cg-row-warn td { background: var(--propia-bg-card)af2 }` -> `var(--propia-danger-soft)`.

## Pasos de deploy
1. `git fetch && git checkout main && git pull` (quedar en `bd08d36e` o posterior).
2. **Migracion:** si prod NO tiene 0.0.156/0.0.158, aplicar `AddPersonaComentarios` y
   `AddVehiculoMascotaPropietario` (ver "Migraciones / BD"). La tanda 0.0.166->0.0.180 no agrega ninguna.
3. Build/publish Release del artefacto y desplegar (incluye `App.razor` con los `?v` nuevos).
4. Verificar version en el pie/login: **v0.0.180**.

## Verificacion post-deploy
- **Residentes hub** (`/residentes-hub`) -> boton "Cargar plantilla (Excel)": el modal abre **estilado**
  (dropzone con borde punteado, botones Cancelar/Continuar, backdrop atenuado). Antes caia "en bruto".
- **Unidades** (`/distribucion`) -> "Cargar plantilla (Excel)": el modal sigue estilado (ahora desde el CSS
  global; sin regresion).
- **Unidades** alta inline: meter un coeficiente >= 1000 muestra "Valor fuera de rango: coeficiente..." sin
  500; un coeficiente valido (ej. 0.1139) crea la unidad.
- **Vista-tabla agrupada** (ej. Unidades agrupado por un campo): escribir en la fila de alta y presionar Tab /
  hacer clic fuera CREA el registro.
- **Mascotas / Vehiculos** (`/residentes-hub`): el header muestra las lineas divisorias verticales entre
  columnas (altura normal, ~47px).
- **Mi Copropiedad** (`/mi-copropiedad`): la vista suelta muestra SOLO el hero (sin Asistente, sin "Hitos de
  configuracion"/"Guiame", sin "Recaudo y cuentas bancarias", sin tarjeta Identidad). El hub de Configuracion
  (modo Embedded) queda igual que antes.
- **Terceros**: la ficha abre como modal; `/directorio/personas/{id}` y `/directorio/empresas/{id}` ya no son
  rutas (es esperado si algun enlace viejo las usa).

## Verificado en dev (navegador integrado, demo 6d93d9fa, Web 5105)
- Build Release/Debug 0 errores, sin warnings nuevos (los warnings existentes son preexistentes).
- Modal Excel: en `/residentes-hub` el navegador ya parsea `.cg-drop` y demas reglas (`config-campos.css?v=9`);
  el modal renderiza estilado; en `/distribucion` existe UNA sola regla `.cg-drop` (la global), sin regresion.
- Unidades alta: coef=5000 -> mensaje de rango y SIN POST; coef=0.1139 -> "Unidad creada" (dato de prueba borrado).
- `/mi-copropiedad`: `mc-layout`/`mc-main` queda con un unico hijo `.mc-hero`; no existen `.mc-rail`,
  `.mc-hitos-head`, `.mc-cta-guia` ni `.mc-readgrid`; no aparecen los textos de los bloques retirados.

## Rollback
Redeploy del artefacto 0.0.165. **La tanda 0.0.166->0.0.180 no toca BD**, asi que el rollback de ESTA tanda no
requiere revertir esquema. (Las migraciones 0.0.156/0.0.158 son aditivas e inertes para el codigo que no las
use, asi que pueden quedarse aplicadas tras un rollback de codigo.)
