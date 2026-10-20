# DEPLOY HANDOFF - PROPIA v0.0.206 (2026-10-20)

> **LEEME PRIMERO.** Handoff autocontenido. Esta tanda **0.0.180 -> 0.0.206** entrega la herramienta
> **"Vistas del sistema" (widgets)** en Mi Copropiedad: **1 migracion nueva** (`AddVistasSistema`) y un
> **seed** para que las 8 vistas de ejemplo "caigan" en TODAS las copropiedades. **Sin cambios de env/secrets.**

## Resumen de 1 linea
Nuevo explorador de datos "Vistas del sistema" en `/mi-copropiedad`: arma widgets (tarjetas / tabla / tabla
pivote / grafico) sobre 8 fuentes reales (residentes, unidades, cartera, PQRSD, tareas, contratos, zonas,
equipos), con campos personalizados (EAV) como columnas/dimensiones/filtros y edicion de registros desde un
modal; vistas COMPARTIDAS por copropiedad. Se siembran 8 vistas de ejemplo en cada copropiedad.

## Que desplegar
- Rama: `main`, HEAD **`4eab8376`**.
- Version visible: **0.0.206** (`src/Propia.Web/Propia.Web.csproj <Version>`).
- Repo: https://github.com/alexandercuartas665/PROPIA
- Base: parte de **0.0.180** (`bd08d36e`, handoff `deploy/DEPLOY_HANDOFF_2026-10-18_v0.0.180.md`).

## Migraciones / BD
**Esta tanda agrega 1 migracion:**
- **`20261020022307_AddVistasSistema`** - tabla nueva `vistas_sistema` (id, nombre, orden, `definicion` jsonb,
  tenant_id, auditoria) + 2 indices + **RLS** (ENABLE+FORCE+policy `tenant_isolation`+GRANT `propia_app`).
  Aditiva, reversible.

**Pendientes acumuladas** (aplicar SOLO si prod aun no las tiene; venian del handoff 0.0.180):
1. `20261015011139_AddPersonaComentarios`  (tabla `persona_comentarios` + RLS). Aditiva.
2. `20261015110445_AddVehiculoMascotaPropietario`  (columna nullable `propietario_persona_id`). Aditiva.

Aplicar con el owner `propia` (design-time). `database update` aplica solo lo que falte:
```
cd src/Propia.Api
"$USERPROFILE/.dotnet/tools/dotnet-ef.exe" database update --project ../Propia.Infrastructure --startup-project .
```
Alternativa manual de AddVistasSistema: `01_migracion_AddVistasSistema.sql` (este paquete).

## Seed - "hacer caer los widgets como semillas en TODAS las copropiedades"  (PASO NUEVO)
Por defecto cada copropiedad se auto-siembra la **primera vez** que alguien abre `/mi-copropiedad` (el panel
POSTea las 8 vistas si no hay ninguna). Para que las 8 vistas aparezcan **de una vez en todas** las
copropiedades existentes, correr tras la migracion:
```
psql "<CONNECTION_STRING>" -f 02_seed_vistas_todas_las_copropiedades.sql
```
- **Idempotente**: solo siembra en copropiedades que aun NO tengan vistas; re-ejecutable sin duplicar.
- **Respeta RLS**: el script itera `tenants` y fija `app.tenant_id` por tenant para pasar la policy.
- Correr como `propia` (o cualquier rol con INSERT/SELECT en `vistas_sistema` y SELECT en `tenants`).
- Las copropiedades nuevas (creadas despues) se siembran solas al primer arranque del panel; no hay que
  re-correr el seed salvo que quieras forzar un backfill.

## Config / env / secrets
**Sin cambios.** (El unico cambio en `.csproj` es el `<Version>` 0.0.180 -> 0.0.206.)

## Cache-bust de assets estaticos (ya resuelto por `?v` en App.razor)
Al desplegar el nuevo `App.razor` los `?v` fuerzan recarga; no hay purga manual. Nuevo/cambiado:
- `css/modules/vistas.css` **nuevo, ?v=6** (estilos `.pv-*` del explorador + modales + menu de ancho).

## Cambios de codigo (0.0.180 -> 0.0.206) - feature "Vistas del sistema"
Todo en `main`. Backend: entidad `VistaSistema` + `IVistaSistemaService`/`VistaSistemaService` +
`VistasSistemaController` (`/api/mi-copropiedad/vistas`: GET abierto al tenant; POST/PUT/DELETE/orden/sembrar
gateados con `[RequierePermiso(MiCopropiedad, Editar)]`). Frontend: `Components/Shared/Vistas/VistasEngine.cs`
(motor puro: filtros/pivote/agregacion/SVG) + `VistasSistemaPanel.razor` (UI), embebido en `/mi-copropiedad`
(solo vista suelta, bajo el hero).

- **`b1f6eeb7` + `2b32433e` (Ola 1)**: explorador con 4 tipos de vista (tarjetas/tabla/tabla pivote/grafico
  SVG barras/lineas/dona), filtros AND, pivote 1D/2D, agregacion count/sum/avg, ancho + reorden, constructor
  con vista previa, 8 semillas + "Restablecer ejemplos", persistencia por copropiedad.
- **`75b843c3`, `987e471e`, `db4fa32e` (Ola 2)**: campos personalizados (EAV) como columnas/dimensiones/
  filtros/medidas en los 7 datasets aplicables; edicion de registros desde modal (campos nativos + EAV en
  equipos/unidades/zonas/contratos; EAV en residentes/tareas/PQRSD). Sin migracion (reusa el EAV existente).
- **`fd293e4f` (Ola 3)**: pulido - iconos SVG, selects de opciones para campos tipo Seleccion, ortografia
  (tildes/enie) y accesibilidad (aria-label, role=dialog).
- **`dc557416`**: el constructor de vistas se abre en un MODAL (no inline).
- **`413cc165`**: control de "Ancho de la vista" (menu 1/3,1/2,2/3,completo) y "Pivotear filas/columnas" en la
  cabecera del widget, con persistencia.
- **`4eab8376`**: las tarjetas de una misma fila igualan el alto (grid `align-items:stretch`).

## Pasos de deploy
1. `git fetch && git checkout main && git pull` (quedar en `4eab8376` o posterior).
2. **Migracion**: `dotnet ef database update` como `propia` (aplica `AddVistasSistema` + cualquier pendiente).
3. **Seed**: correr `02_seed_vistas_todas_las_copropiedades.sql` (siembra las 8 vistas en todas las
   copropiedades que no tengan). Idempotente.
4. Build/publish Release del artefacto y desplegar (incluye `App.razor` con `vistas.css?v=6`).
5. Verificar version en el pie/login: **v0.0.206**.

## Verificacion post-deploy
- **`/mi-copropiedad`** (vista suelta): bajo el hero aparece la seccion **"Vistas del sistema"** con 8 widgets
  (barras, dona, pivote 1D y 2D, tabla, tarjetas) poblados con datos reales de la copropiedad.
- **Constructor**: "+ Nueva vista" abre un **modal**; elegir fuente/tipo/metrica/campos/ancho + vista previa en
  vivo; "Crear vista" lo guarda y aparece en la grilla.
- **Cabecera del widget**: el icono **Ancho** abre el menu (1/3,1/2,2/3,Completo) y reajusta el ancho; en las
  pivotes, **Pivotear** intercambia filas/columnas. Ambos persisten al recargar.
- **Campos personalizados**: si la copropiedad tiene campos custom (p.ej. en Equipos), aparecen como columna/
  dimension; el boton Editar de una fila abre el modal con esos campos + los nativos y guarda en BD.
- **Persistencia compartida**: dos usuarios de la misma copropiedad ven las mismas vistas.
- **Aislamiento**: una copropiedad NO ve las vistas de otra (RLS).

## Verificado en dev (navegador integrado, Web 5105, Postgres propia_dev)
- Build Release/Debug 0 errores (warnings preexistentes).
- Runtime: los 8 widgets renderizan con datos reales; pivote 2D Total = 626 unidades; editar nativos (zona:
  Aforo/Estado) preserva los campos no mostrados; editar valores EAV (equipos/residentes/tareas/PQRSD)
  persiste en BD; PQRSD preserva `UnidadPrivadaId`; ancho y pivotear persisten; filas igualan alto.
- **Seed probado en propia_dev**: `02_...sql` sembro 49 copropiedades (2 ya tenian) y es idempotente (segunda
  corrida: 0 nuevas, 51 omitidas). Conteo final: 51 copropiedades x 8 vistas = 408 filas. Corre bajo RLS
  (ejecutado como `propia_app`).

## Rollback
- Codigo: redeploy del artefacto 0.0.180.
- BD: la migracion es aditiva; se puede dejar `vistas_sistema` sin problema (el codigo 0.0.180 no la usa).
  Si se quiere revertir el esquema: `dotnet ef database update 20261015110445_AddVehiculoMascotaPropietario`
  (hace DROP de `vistas_sistema`). Las filas sembradas se pierden con el DROP (son datos de ejemplo
  regenerables desde la UI con "Restablecer ejemplos").

## Archivos de este paquete
- `DEPLOY_HANDOFF_2026-10-20_v0.0.206.md`  (este)
- `01_migracion_AddVistasSistema.sql`  (DDL+RLS de la migracion; alternativa manual a `dotnet ef`)
- `02_seed_vistas_todas_las_copropiedades.sql`  (seed idempotente multi-tenant de las 8 vistas)
