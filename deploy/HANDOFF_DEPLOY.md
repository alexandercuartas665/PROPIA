# HAND-OFF DEPLOY - PROPIA

> Generado 2026-09-10, actualizado **2026-09-28 (TANDA 2: carga masiva single-tenant + ficha unidad + AGRUPAR compartido + hubs)**.
> Version a desplegar: **0.0.99** (csproj bumpeado). HEAD `main` (`0a9e147`) incluye ademas de la tanda 0.0.98:
> carga masiva sin columna COPROPIEDAD (single-tenant), desplegable de UNIDAD PRIVADA en Residentes, la ficha
> de unidad reestilizada (editor `.cpe-*`), el componente compartido de agrupacion `TablaAgruparMenu`, y la
> tarjeta de carga masiva movida arriba de las pestanas en los hubs.
> Prod (ultimo dato conocido, deploy 2026-09-24): **0.0.95** (`025676b`). Este deploy sube TODO lo acumulado
> desde ahi (0.0.96 Usuarios + 0.0.98 + esta tanda 0.0.99). Lo nuevo de ESTA tanda esta en la seccion
> **ESTADO 2026-09-28 TANDA 2**; la anterior en **ESTADO 2026-09-28 (SPLIT TAREAS)**.
> Repo: https://github.com/alexandercuartas665/PROPIA  ·  rama `main` (HEAD al desplegar).
> Companion: `DEPLOY_CHECKLIST.md` (misma carpeta) con el detalle version por version.
> Este archivo es el resumen operativo para la sesion de deploy. **Vive en el repo** (`deploy/`) y se
> copia a la carpeta de trabajo; asi no se pierde.

---

## ESTADO 2026-10-07 DEPLOY 0.0.112 (LEER PRIMERO)

Rama `equipo/alex-header-zonas-equipos` -> mergear a `main`. Version **0.0.112** (desde 0.0.111).
Detalle completo en **`deploy/DEPLOY_HANDOFF_2026-10-07_v0.0.112.md`** (leer ese). Resumen:

- **B2b - rol activo por sesion** (SIN migracion): el JWT lleva `rol_id`; el RBAC autoriza con el rol activo;
  `POST /connect/switch-rol`; gate "elige tu rol" para multi-rol. Aditivo (tokens viejos/mono-rol = sin cambio).
- **Unidades: eliminado el concepto Torre** (1 MIGRACION DESTRUCTIVA): la unidad se identifica solo por su
  codigo (`numero`); se quitaron entidad/tabla/columna/FK `Torre`, CRUD `/torres`, tools MCP de torres, y
  `TorreNombre`/`TorreId` de los DTOs. **+ validacion de codigo duplicado** (sin mayus/tildes, con mensaje).

### 1 MIGRACION NUEVA — DESTRUCTIVA — APLICAR EN PROD (owner `propia`, `dotnet ef database update`)
- `20261007214045_RemoveTorreDeUnidades` — **hornea** el codigo en `numero` (`<torreShort>-<numero>`,
  desambigua duplicados por tenant) y luego **DROP** FK + tabla `torres` + columna `torre_id`. Irreversible
  (el Down recrea la estructura vacia pero los codigos ya horneados no se des-hornean).
- **ORDEN CRITICO:** desplegar el codigo 0.0.112 PRIMERO (no consulta `torres`), y recien DESPUES aplicar la
  migracion. Si se aplica antes de que el codigo nuevo este vivo, el codigo viejo (0.0.111) da 500 (consulta
  `torres`/`torre_id`). `database update` aplica tambien `20261007135437` si prod venia por detras de 0.0.111.

---

## ESTADO 2026-10-06 DEPLOY 0.0.103 (LEER PRIMERO)

Rama `equipo/alex-header-zonas-equipos` -> **mergeada a `main`** (HEAD `0d110c5`). Version **0.0.103**.
Este deploy acumula: **catalogo global de listas (Olas 1-4A)**, **alta inline de Unidades completa**, y la
**vista-tabla reordenar/redimensionar columnas** (Contratos + PQRSD). Detalle del catalogo:
`deploy/PLAN_CATALOGOS_SUPER_ADMIN.md`.

### 2 MIGRACIONES NUEVAS — APLICAR EN PROD (owner `propia`, `dotnet ef database update`)
1. `20261006120327_ContratoTipoCategoriaAString` — `contrato_servicios.tipo_contrato` y `.categoria` de
   `integer` (enum) a `text`, mapeando los valores existentes (int -> etiqueta) con un `CASE`. Habilita que
   "Tipo de contrato"/"Categoria" sean listas editables por copropiedad. Rollback real = redeploy anterior
   (su `Down` vuelve a int; texto nuevo sin mapeo quedaria NULL).
2. `20261006164654_AddCatalogoOpcion` — tabla GLOBAL `catalogo_opciones` (sin tenant_id, **sin RLS** a
   proposito: tabla de plataforma; `RlsCoverageTests` solo mira tablas con tenant_id, asi que no aplica) +
   GRANT a `propia_app`. Rollback = redeploy anterior, sin revertir esquema (la tabla queda sin uso).

### CONFIG/ARRANQUE NUEVO — seeder idempotente
`CatalogoListasSeeder.EnsureAsync` corre en CADA arranque (Web y Api), SIEMPRE. Siembra las 12 listas de
fabrica (unidad.tipo/estado, contrato.tipocontrato/categoria, vehiculo.tipovehiculo, mascota.tipo,
equipo.categoria/tipo/estado, zona.categoria/estado, persona.sexo) desde los enums/semillas. Solo inserta
lo que falta; NO pisa lo que A&D edite. No-op tras la primera vez. Va en try/catch: si falla, loguea y NO
bloquea el arranque. **La migracion debe estar aplicada ANTES de arrancar** (si no, loguea warning y al
proximo arranque siembra).

### Que cambia para el usuario
- **Consola A&D -> nueva pagina `/admin/catalogos`** (pestana Plataforma): editar labels/orden/color/activo
  y agregar opciones de las 12 listas, para TODAS las copropiedades, sin tocar codigo. La clave no se edita
  (los datos apuntan a ella); las semilla no se borran, se desactivan.
- Cada modulo (Unidades, Contratos, Vehiculos, Mascotas, Equipos, Zonas, Personas-sexo) **lee sus labels/
  opciones del catalogo**, componiendo el override por copropiedad encima. Sin catalogo, usa el fallback de
  codigo (comportamiento identico al actual), asi que es seguro aun si el seeder no corrio.
- **Alta inline de Unidades** ahora captura estado, habitaciones, banos, parqueaderos, paga admin, cuota,
  observaciones y modulos contributivos (antes "En la ficha"). Sin migracion (el request ya los soportaba).
- **Contratos y PQRSD**: reordenar (arrastrar header) + redimensionar columnas (tirador + doble-clic).

### Pendiente (NO en este deploy): PQRSD-unify (Ola 4 Tranche B) — proxima sesion.

## ESTADO 2026-10-02 ROLES REFINO VISUAL + MENU (LEER PRIMERO)

Rama `equipo/alex-roles-refino` mergeada a `main` (commits `95da282` Roles refino + `43172f3` EditarCampoSistema).
**NO trae migraciones nuevas** (el esquema de Roles ya entro con la migracion de 2026-09-29 OLA A de abajo).
Es UI + un cambio de DATO en la tabla global `menu_overrides`.

**UI (auto-deploy desde main):**
- Roles y Permisos (`RolesPanel.razor`): matriz muestra SOLO acciones validas por modulo (MP-04/MP-06; el
  resto pinta "."); ficha del rol como MODAL (`.usr-modal-roles`); bandeja migrada al frame canonico `.tbl`
  (llena el ancho, header sticky, footer); barra Filtros/Buscar/Ordenar/Agrupar; badge base "Sistema".
- EditarCampoSistema (`EditarCampoSistemaModal.razor` + `DistribucionPanel.razor`): campos de sistema tipo
  lista muestran opciones rigidas (solo lectura) + permiten agregar propias.
- CSS bumps: `usuarios.css?v=9`, `roles.css?v=4`, (App.razor).
- Menu base (`MenuCatalog.cs`): `mi-usuarios` "Usuarios y roles" -> "Usuarios"; nuevo item `mi-roles`
  "Roles" -> `/roles`.

**ACCION MANUAL EN PROD (correr tu, Alex): mover "Roles" a la seccion Configuracion del menu.**
El menu real lo arma `menu_overrides` (tabla GLOBAL, sin tenant_id). En dev ya se aplico. En prod, el
`parent_key` de la seccion "Configuracion" puede tener OTRO id (es un custom-sec-*). Pasos:
1. Ver el id real de la seccion Configuracion en prod:
   `SELECT node_key,label FROM menu_overrides WHERE label ILIKE 'config%' AND is_custom=true;`
2. Con ese `<ID_CONFIG>`, correr (ajusta `sort_order` si choca con otro item de esa seccion):
   ```sql
   BEGIN;
   UPDATE menu_overrides SET label='Usuarios', updated_at=now() WHERE node_key='mi-usuarios';
   UPDATE menu_overrides SET sort_order=6, updated_at=now() WHERE node_key='mi-directorio';
   INSERT INTO menu_overrides (id, node_key, label, parent_key, sort_order, created_at, is_custom, hidden)
   VALUES (gen_random_uuid(), 'mi-roles', NULL, '<ID_CONFIG>', 5, now(), false, false);
   COMMIT;
   ```
   (En dev el id fue `custom-sec-3954c8fdc250481f9197e077ba2c6893` y quedo Usuarios(4) -> Roles(5) -> Terceros(6).)
   Si NO se corre: "Roles" igual funciona y aparece, pero bajo "Mi Copropiedad" (el catalogo base), no en Configuracion.

---

## ESTADO 2026-09-29 MODULO 2.5 v2.0 OLA A (matriz de roles por copropiedad + bandeja - LEER PRIMERO)

Rama `equipo/atlas-usuarios-roles-25`. **Trae UNA migracion nueva ADITIVA que SI hay que aplicar en
prod**: `20260929212521_Modulo25V2_RolCategoria_MatrizPorTenant` (owner `propia`, `dotnet ef database update`).
- Agrega `roles_copropiedad.categoria` (int, default 1 = Administrativo).
- Crea tabla `rol_permisos_tenant` (override de la matriz de permisos POR COPROPIEDAD) con **RLS FORCE +
  policy `tenant_isolation` (USING+WITH CHECK) + GRANT a propia_app**. Fix critico: antes editar la
  matriz de un rol base (global) mutaba la matriz de TODAS las copropiedades; ahora cada una guarda su
  override y lee la matriz efectiva (default global + override del tenant). Cubierta por `RlsCoverageTests`.
- Puramente aditiva y reversible (Down). **NO re-siembra roles**: el re-seed de los 7 roles base + el
  remapeo de usuarios (Propietario/Residente->Portal Residente, Operario->Personal Operativo, etc.) es
  el **D2**, que va en una migracion de DATOS SEPARADA posterior (con OK de Alex antes de aplicar).
- UI: bandeja de Roles nueva en `/roles` (`RolesPanel.razor` + `css/modules/roles.css?v=1`, ya linkeado
  en `App.razor`); el modal viejo de roles en Usuarios se elimino (el boton "Roles y permisos" navega a
  `/roles`). Sin config nueva.

## ESTADO 2026-09-29 MODULO 2.5 v2.0 D2 (re-seed de roles base + remapeo de usuarios)

Rama `equipo/atlas-roles-d2` (sale de Ola A). **Migracion de DATOS** (no toca esquema):
`20260930021045_Modulo25V2_D2_ReseedRolesBase`. Debe correr DESPUES de la migracion de Ola A.
- **Correr como owner `propia`** (`dotnet ef database update`). `propia` es superuser/BYPASSRLS, asi que
  el remap alcanza todas las copropiedades. **NO aplicar como `propia_app`**: con RLS FORCE y sin
  `app.tenant_id` la policy filtraria a 0 filas y el remap se saltaria en silencio.
- Que hace (todo en 1 transaccion, con aserciones `DO` que ABORTAN si queda cualquier huerfano):
  deja **7 roles base** (Administrador, Consejero, Coordinador, Asistente [categoria Administrativo];
  Personal Operativo [ex-Operario, Operativo]; Portal Residente [NUEVO, fusiona Propietario+Residente,
  categoria Portal Residente]; Terceros [NUEVO]); **borra** Propietario, Residente, Inmobiliaria,
  Revisor Fiscal, Vigilante; **remapea** usuarios+invitaciones de Propietario/Residente a Portal
  Residente; Portal Residente HEREDA la matriz de Propietario (10 celdas, decision de Alex); Contador
  pasa a **personalizado** del tenant de su usuario; facetas de siembra Propietario+Residente se
  fusionan por tenant en Portal Residente.
- **Destructiva y NO reversible** (Down = no-op deliberado: el merge Propietario+Residente pierde la
  distincion). **Antes de aplicar en prod: BACKUP.**
- **DEV: APLICADO y verificado 2026-09-29** (7 roles base OK; 244 usuarios en Portal Residente; Contador
  tipo=Personalizado tenant-scoped; matriz Portal Residente=10 celdas; facetas="1,2"; **0 huerfanos** en
  usuarios_tenant / usuario_invitaciones / rol_permisos / rol_permisos_tenant). **PROD: PENDIENTE del
  dale de Alex.**

---

## ESTADO 2026-09-28 TANDA 2 (carga masiva single-tenant + ficha + AGRUPAR - LEER PRIMERO)

Tanda posterior a 0.0.98. Todo esta en `main` (HEAD `0a9e147`). **Es 100% migration-free: NO agrega ni una
migracion vs 0.0.98** (las 3 migraciones aditivas siguen siendo las de la seccion "ESTADO 2026-09-28 (SPLIT
TAREAS)"; si prod ya recibio 0.0.98 no hay nada que aplicar). Cambios (6 commits, `191393c..0a9e147`):

1. **Carga masiva SINGLE-TENANT** (`7e59b65`): la plantilla Excel ya **NO emite la columna COPROPIEDAD** en
   ninguna de las 7 hojas. Cada archivo carga en **una sola copropiedad: la activa**. El importador
   (`UnidadesCargaImportService`) enruta las filas sin COPROPIEDAD a la copropiedad activa con **UPSERT**
   (grupo interno "SoloVacias"); la logica de grupos multi-copropiedad no se toco. La fila de ejemplo se
   marca con el sentinel `EJEMPLO (borrar fila)` en una celda de texto y el importador la ignora en cualquier
   hoja. **COMPAT: los archivos VIEJOS que aun traigan COPROPIEDAD (incl. "Todas las copropiedades") se
   siguen procesando** sin cambios. Migration-free.
2. **Residentes: desplegable de UNIDAD PRIVADA** (`7e59b65`): en las hojas PERSONAS/VEHICULOS/MASCOTAS la
   columna UNIDAD PRIVADA trae un desplegable (`LISTA_UNIDADES`) con los codigos TORRE-NUMERO de las unidades
   ya cargadas. En onboarding (sin unidades) queda como texto libre. Los campos dinamicos siguen saliendo
   como columnas `[Label]` por cada campo VISIBLE de la copropiedad (verificado en runtime). Migration-free.
3. **Ficha de unidad reestilizada** (`6e5b411`, merge de `equipo/atlas-ficha-unidad`): `GestionarUnidadesModal`
   pasa a editor `.cpe-*` de 4 pestanas. **Cache-busting: servir `unidades-modal.css?v=4`** (ya bumpeado en
   `Components/App.razor`). Migration-free.
4. **Descarga de plantilla solo en el hub** (`03e3a4c`): se quito el boton "Descargar plantilla" de la vista
   Unidades standalone (`/distribucion`); la descarga vive UNICAMENTE en los hubs (`HubCargaMasiva`). Se
   conserva "Cargar plantilla" en standalone. Migration-free.
5. **AGRUPAR centralizado** (`a6790d7`, merge de `equipo/atlas-orden-agrupar`): nuevo componente compartido
   **`Components/Shared/TablaAgruparMenu.razor`** (gemelo de `TablaOrdenarMenu`, agrupacion N-niveles, solo
   tokens `--propia-*`), cableado en **Directorio** como modulo de referencia. Sin CSS nuevo (estilos inline
   en el componente). Migration-free. (El rollout al resto de modulos, incluido Unidades, sigue en curso.)
6. **Carga masiva ARRIBA de las pestanas** (`0a9e147`): la tarjeta `HubCargaMasiva` se movio de debajo de
   `.cfg-tabs` a encima (entre titulo y pestanas) en los dos hubs (`ConfiguracionCopropiedad` scope config y
   `ResidentesHub` scope residentes). Solo reorden de markup. Migration-free.

**Verificacion post-deploy TANDA 2:**
- [ ] **Carga masiva:** descargar la plantilla del hub -> **NINGUNA hoja pide COPROPIEDAD**; en Residentes la
      columna UNIDAD PRIVADA trae desplegable con las unidades; los campos dinamicos visibles salen como `[Label]`.
      Importar OK (cae en la copropiedad activa). Reimportar un archivo VIEJO con COPROPIEDAD -> tambien OK.
- [ ] **Hubs:** la tarjeta "Carga masiva por plantilla" (Descargar/Cargar) aparece ARRIBA de las pestanas en
      `/configuracion-copropiedad` y `/residentes-hub`.
- [ ] **Unidades standalone** (`/distribucion`): solo boton "Cargar plantilla" (sin "Descargar").
- [ ] **Ficha de unidad:** abrir una unidad -> editor de 4 pestanas `.cpe-*` con estilos correctos (verificar
      que se sirve `unidades-modal.css?v=4`, sin CSS viejo cacheado).
- [ ] **Agrupar (Directorio):** el boton "Agrupar" abre el menu compartido (`AGRUPAR POR` / `Agregar agrupacion`).

---

## ESTADO 2026-09-28 (SPLIT TAREAS + ORTOGRAFIA A-F - LEER PRIMERO)

Tanda posterior al handoff del 09-24. Todo esta en `main` (HEAD `fbfea4c`). Trae: menu de columna estilo
Airtable en las bandejas, cierre de la reorg de hubs, **descripcion de campo** (1 migracion aditiva),
**confirmacion in-app** (adios `confirm()` nativo), **split del modulo Tareas** (1 migracion aditiva),
**Contratos/Seguros -> tarea al tablero General al vencer**, y la **auditoria ortografica completa (tildes),
Olas A-F**. Las secciones 0-7 de mas abajo siguen vigentes TAL CUAL. **Solo 2 migraciones, ambas aditivas
y sin RLS nueva; el resto es migration-free.**

### CON MIGRACION (3 nuevas vs el handoff 09-24, TODAS ADITIVAS, sin RLS nueva)
El `ef database update` (seccion 2) aplica solo lo que falte. Vs `20260924143830_AddUsuarioCampos`:
1. **`20260926023023_AddDescripcionCamposDefinicion`** - agrega `descripcion text NULL` a las 7 tablas
   `*_campos_definiciones` (unidad/persona/vehiculo/mascota/tercero/zona/usuario). Solo columnas; no toca
   RLS (esas tablas ya la tienen). `Down()` dropea las columnas.
2. **`20260927131731_AddTableroEsGeneral`** - agrega `es_general boolean NOT NULL default false` a
   `tableros`. Marca el tablero "General" unico del nuevo modulo Tareas. Aditiva; `Down()` dropea la columna.
3. **`20260928161525_AddDescripcionCampoConfig`** - agrega `descripcion text NULL` a `unidad_campos_config`
   (config de campos de SISTEMA por copropiedad; la usa el nuevo editor focalizado "Editar campo" para
   guardar la descripcion de un campo fijo). Aditiva; `Down()` dropea la columna.

Ninguna crea tablas -> **no suma a las 8 tablas sin RLS conocidas** (seccion 6); `RlsCoverageTests` sigue
igual. Comprobacion:
```sql
select count(*) from information_schema.columns
 where table_name like '%_campos_definiciones' and column_name='descripcion';   -- debe dar 7
select column_name from information_schema.columns
 where table_name='tableros' and column_name='es_general';                      -- debe existir
select column_name from information_schema.columns
 where table_name='unidad_campos_config' and column_name='descripcion';         -- debe existir
```

### "Editar campo" en columnas de sistema = editor focalizado (UI, migration 3 arriba)
Antes, "Editar campo" del menu de columna solo salia en campos propios; sobre un campo fijo no hacia nada
util. Ahora sale siempre: en campo propio abre el editor del campo; en campo de SISTEMA abre un editor
focalizado (alias/descripcion/visibilidad; el tipo es fijo). Aplica a Distribucion, Residentes, Mascotas,
Vehiculos, Zonas, Equipos y Directorio, y tambien a PQRSD, Seguros y Servicios. Tambien se homogenizo el
boton "+ Agregar registro" en Directorio y Usuarios. Todo migration-free salvo la columna `descripcion`
(migracion 3).

### Fix ancho de tabla en Directorio y Usuarios (UI, migration-free)
Esas 2 tablas quedaban mas angostas que las demas por un contenedor de pagina con padding horizontal
extra (`.dir-page` 34px, `.usr-wrap` 28px) que Distribucion/Mascotas no tienen. Se quito ese padding para
que usen el ancho total. **Cache-busting:** el deploy debe servir `directorio.css?v=4` y `usuarios.css?v=6`
(ya bumpeados en `Components/App.razor`).

### CopropiedadSwitcher milimetrico (UI, migration-free)
Integrado por fast-forward de `equipo/atlas-copropiedad-switcher` (rama de ATLAS, re-revisada por VIGIA):
el selector de copropiedad (modal galeria/tabla + creacion en formulario unico) calca la maqueta
`modal-copropiedad.html` con tokens claro/oscuro. **1 solo archivo** (`CopropiedadSwitcher.razor`), SIN
backend, SIN migracion. Decision de producto: se muestran las imagenes reales del logo (sin fallback de
avatar). Verificado en runtime lado a lado con la maqueta.

### SPLIT DEL MODULO TAREAS (cambio de comportamiento - leer)
- **`/tareas`** ahora es el **tablero General unico**: siempre abre listo para recibir actividades y trae la
  columna **ORIGEN** (que modulo emitio la tarea: PQRSD, Contratos, Seguros, Mantenimiento, o "Propia" si es
  manual). El **selector de tablero destino de PQRSD desaparecio**: toda tarea de modulo cae en el General
  marcada con su Origen (inmutable, lo fija el modulo emisor).
- **`/tableros`** es el **modulo nuevo de proyectos** (los tableros multi-columna de antes), EXCLUYE el
  General.
- **Migracion de datos (lazy, sin script):** al abrir Tareas por primera vez tras el deploy, el tablero
  default existente se marca `es_general=true` (get-or-create idempotente en `AsegurarTableroGeneralAsync`).
  Las tareas historicas se quedan donde estan (Origen = Propia); solo las nuevas de modulos caen al General.
- **MENU (ver 0.7): es DATA.** El codigo agrega al catalogo los items **"Tareas"** (`/tareas`) y **"Tableros"**
  (`/tableros`) bajo Operacion; pero la ORGANIZACION del menu vive en `menu_overrides`. Reimportar
  `menu-propia.json` en Super Admin si se quiere la organizacion afinada (sin el import, ambos son
  alcanzables por su ruta desde el catalogo del codigo).

### CONTRATOS/SEGUROS -> TAREA AL GENERAL (migration-free)
- El job `ContratosVencimientoJob` (2 corridas/dia), ademas de la alerta de vencimiento, ahora **emite UNA
  tarea de seguimiento al tablero General** cuando un contrato/poliza entra en "por vencer": Origen =
  **CONTRATOS** / **SEGUROS**. Idempotente (no duplica si ya hay una tarea no eliminada para esa entidad).
- **DE PASO SE ARREGLO** el bug preexistente que estaba en la seccion 6 de este handoff: el job hacia
  `if (contratos.Count == 0) continue;` y **nunca alertaba polizas** en copropiedades sin contratos con
  fecha fin. Ahora recorre ambos. (El `catch` por tenant ya logueaba desde antes.)

### ORTOGRAFIA (tildes) - Olas A-F, con BACKFILLS SELF-HEALING (sin paso manual)
Se corrigieron tildes en UI, catalogos y plantillas. Lo relevante para deploy:
- **Backfills idempotentes por tenant (NO son migracion, NO hay script manual):** los catalogos que ya
  estaban sembrados en ASCII se renombran solos la primera vez que cada tenant abre el modulo
  (`ExecuteUpdateAsync` con `WHERE nombre_viejo`, tenant-scoped; tras el rename es UPDATE de 0 filas).
  Cubre: estado PQRSD "En gestion"->"En gestion" con tilde; estados de Cartera (Notificacion/Pre-juridico/
  Juridico); rubros de Presupuesto; TRD de Documentos (series/subseries/tipologias); cargo EquipoOrg
  "Asistente de Facturacion". **Respetan ediciones del usuario** (solo tocan la fila que conserva el ASCII
  por defecto). No hay nada que ejecutar a mano.
- **Carga masiva ahora TOLERA acentos:** los encabezados de la plantilla pasaron a llevar tilde
  (TELEFONO->con tilde, AREA, MATRICULA, etc.), pero el importador (`UnidadesCargaImportService`) compara
  insensible a acentos, asi que **un archivo viejo con encabezados en ASCII sigue importando igual**. No
  rompe plantillas guardadas por los clientes.
- **Carga masiva ahora es single-tenant (0.0.98):** la plantilla ya no pide COPROPIEDAD; todo carga en la
  copropiedad activa. Los archivos viejos con COPROPIEDAD se siguen procesando (ver 0.2). Residentes gana
  desplegable de UNIDAD PRIVADA con las unidades ya cargadas.
- El resto (labels/placeholders/`.razor`, mensajes de toast, `<option>`) es texto visible, migration-free.

### Verificacion post-deploy especifica de esta tanda
- [ ] **Migraciones:** correr los 2 `select` de arriba (descripcion x7, es_general en tableros).
- [ ] **Tareas:** `/tareas` abre el General con columna **ORIGEN**; generar un PQRSD nuevo -> aparece una
      tarea en el General con Origen **PQRSD**. `/tableros` lista proyectos SIN el General.
- [ ] **Contratos/Seguros:** (si hay datos por vencer) tras la corrida del job, el General muestra tareas con
      Origen Contratos/Seguros. Una copropiedad SIN contratos ya recibe alertas de **polizas** (bug 09-24
      resuelto).
- [ ] **Ortografia:** kanban PQRSD muestra la columna **"En gestion" con tilde**; Cartera/Presupuesto/TRD de
      Documentos con tildes; formularios y menus con tildes, sin caracteres rotos (mojibake).
- [ ] **Carga masiva (single-tenant + compat):** descargar la plantilla -> **NINGUNA hoja pide COPROPIEDAD**;
      en Residentes la columna UNIDAD PRIVADA trae desplegable con las unidades cargadas. Importar OK (cae en
      la copropiedad activa). **Compat:** reimportar un archivo VIEJO con COPROPIEDAD (y/o encabezados ASCII)
      -> tambien importa sin errores.
- [ ] **Menu:** reimportar `menu-propia.json` si se quiere Tareas/Tableros en su ubicacion afinada (ver 0.7).

---

## ESTADO 2026-09-24 (VISTA TABLA + CAMPOS EN USUARIOS - LEER PRIMERO)

Esta tanda cierra la homogeneizacion de la **vista tabla** en los modulos de administracion y agrega
**campos dinamicos a Usuarios**. Las secciones 0-7 de abajo siguen vigentes TAL CUAL. **Casi todo es
migration-free y ya esta en `main`; lo unico con migracion es Usuarios (1 migracion aditiva).**

### YA EN `main` (migration-free, reusa columnas/config existentes)
Commits `0ba2cc0` (paquete vista tabla) y `63aa46e` (correcciones de auditoria):
- **Vista tabla estandar completa** en Residentes, Mascotas, Vehiculos, Usuarios y Directorio
  (Personas/Empresas): seleccion + accion masiva, alta inline, menu **Columnas**, ordenar, **arrastrar/
  mover columnas** (pointer-drag JS, no DnD nativo), **ancho + autoajuste**, menu **clic-derecho** (sin
  "Duplicar"), footer `TablaPager`. Componentes compartidos nuevos: `CrearCampoModal`, `TablaCtxMenu`.
- **Directorio - columnas dinamicas**: reusa el subsistema `personas` (compartido con Residentes); el
  valor vive sobre el **vinculo activo principal** de la persona/empresa (coincide con Residentes). Borrado
  masivo = **inactivar vinculo** (se expuso `VinculoId` en los DTOs). Personas y Empresas comparten el
  mismo set de campos.
- **Formula/tipos avanzados habilitados en Zonas y Equipos** (antes inalcanzables): su `ConfigCamposEntidad`
  ahora pasa `TiposAvanzados="true"`. Reusa el EAV existente (`zona_campos_*` / `equipo_campos_*`). **Sin
  migracion.**
- **Usuarios - acciones de la vista tabla**: revocar masivo + **columnas configurables server-side**. Esto
  reusa `unidad_campos_config` con `entidad='usuario'`: se **amplio la whitelist** `EntidadesCampoConfig`
  en codigo (aditivo, **sin migracion**; la tabla ya es generica Entidad+CampoClave).
- **Limpieza CSS**: se eliminaron assets muertos y ausentes en la maqueta (`wwwroot/lib/` bootstrap por
  defecto, `assets/libs/fontawesome`, `assets/libs/lucide`, `assets/css/styles-rtl.css`). **Cache-busting:
  el deploy debe servir `usuarios.css?v=3` y `propia-ui.js?v=14`** (ya bumpeados en `Components/App.razor`).
- **Correcciones de auditoria vs maqueta**: valor CSS malformado en Distribucion (`.cg-summary.warn`),
  backdrop del modal y menu contextual 1:1 con la maqueta, inputs de alta punteados en Mascotas/Vehiculos.
- Verificado en runtime (5105) sobre la copropiedad demo; datos de prueba borrados. Build 0 errores.

### CON MIGRACION - Usuarios campos dinamicos (NUEVO, 1 migracion aditiva)
> **Estado:** codigo completo y compilando (0 errores), **commiteado y en el lote de este deploy (0.0.96)**.
> AVISO IMPORTANTE: la migracion **no se pudo aplicar en dev** (el modo automatico bloqueo la BD compartida),
> asi que **el deploy es la primera vez que corre `AddUsuarioCampos` y la primera vez que se ejercita el EAV
> de Usuarios en runtime**. Por eso la verificacion de humo de Usuarios (crear campo + editar valor + F5)
> es **paso #1 obligatorio post-deploy**; si algo falla, el `Down()` de la migracion dropea solo esas 2
> tablas y no afecta nada mas (ver seccion Rollback).

- **Que agrega**: paridad total de la vista tabla en Usuarios: crear campo ("+"/modal), gestor de campos,
  **Formula/tipos avanzados**, y **celda editable por tipo**. Unico ausente (como en todos): "Duplicar".
- **Migracion `20260924143830_AddUsuarioCampos`** (aditiva): 2 tablas nuevas
  `usuario_campos_definiciones` y `usuario_campos_valores`, **cada una con ENABLE + FORCE ROW LEVEL
  SECURITY + policy `tenant_isolation` + GRANT a `propia_app`** (patron identico a Equipo/Zona). No toca
  ninguna tabla existente. El registro del valor es el **`usuarios_tenant.id`** (membresia del usuario en
  la copropiedad), no el usuario global.
- **Endpoints nuevos** (en `MiCopropiedadController`, gateados con `MI_COPROPIEDAD`): `usuarios-campos`
  (GET/POST/PUT/DELETE) y `usuarios-campos-valores` (GET, PUT `/{registroId}/{definicionId}`).
- **Sin pasos manuales de datos**: los campos nacen vacios; cada copropiedad crea los suyos (igual que en
  los demas modulos). El gestor de Usuarios usa `SinConfigSistema` para NO chocar con el menu Columnas.

### Migraciones de esta tanda (2 nuevas vs origin/main 025676b)
El `ef database update` de la seccion 2 aplica solo lo que falte (compara `__EFMigrationsHistory`). Vs el
punto de prod (`025676b` = 0.0.95) quedan **2 aditivas**:
1. `20260915135206_EquipoAtlas_AddTableroCamposConfig` - viene con la integracion de atlas-tareas (estaba
   en rama en el handoff del 09-15, ahora integrada a main). Tabla `tablero_campos_config`.
2. `20260924143830_AddUsuarioCampos` - las 2 tablas de campos de Usuarios (ver arriba), con RLS.
- Verificacion rapida de que quedaron aplicadas:
```sql
select relname, relrowsecurity, relforcerowsecurity
from pg_class where relname in ('usuario_campos_definiciones','usuario_campos_valores','tablero_campos_config');
-- las 3 deben existir; las de usuario_* con relrowsecurity=t y relforcerowsecurity=t
```
- `RlsCoverageTests`: las 2 tablas nuevas ya llevan RLS con el patron estandar, asi que **no** deben sumar
  a las 8 tablas sin RLS conocidas (seccion 6). Correr la suite de integracion para reconfirmar.

### Verificacion post-deploy especifica de esta tanda
- [ ] **Usuarios (tras aplicar la migracion):** en `/usuarios` (vista Tabla) el boton **Campos** abre el
      gestor; el "+" crea una columna; crear un campo de cada tipo + una **Formula** y editar un valor sobre
      un usuario; **F5 persiste** (valor guardado sobre `usuarios_tenant.id`). Sin la migracion, "Configurar
      campos" dara 500 `column/table does not exist`.
- [ ] **Zonas y Equipos:** el selector de tipo del gestor ya lista **Formula/Usuario/Directorio**.
- [ ] **Directorio:** columnas dinamicas visibles en Personas y Empresas; el valor coincide con Residentes
      para la unidad principal; "Quitar del directorio" (masivo) inactiva el vinculo sin borrar la persona.
- [ ] **CSS:** con `?v=3`/`?v=14` servidos, iconos (flaticon) intactos tras quitar fontawesome/lucide.

---

## ESTADO 2026-09-15 (POST-INTEGRACION B - LEER PRIMERO)

**Desplegable HOY = `origin/main` @ `40ab5da`, version `0.0.95`.** Se integro el paquete B
(`feature/hub-config`) a main. Las secciones 0-7 de abajo siguen vigentes TAL CUAL (migraciones, config,
permisos, RLS); esta seccion dice QUE sumo B y que sigue faltando. **B es 100% migration-free.**

### Lo que ENTRO en main con B (todo SIN migracion, reusa columnas/config existentes)
- **Hub "Configuracion Copropiedad"** (`/configuracion-copropiedad`): 9 pestañas (General, Unidades,
  Residentes, Mascotas, Vehiculos, Zonas, Equipos, Usuarios, Directorio) que reusan cada modulo COMPLETO
  via patron *Panel + parametro Embedded. Item de sidebar nuevo (MenuCatalog, en codigo). Rutas sueltas
  y accesos previos INTACTOS. Verificado runtime (las 9 pestañas rinden el modulo completo).
- **Fase 2 replicada a Vehiculos, Mascotas y Residentes**: los 3 tipos (Formula/Usuario/Directorio) ahora
  tambien en esos modulos (antes solo en Unidades). Reusan los catalogos EAV existentes (unidad_placas /
  unidad_mascotas / unidad_personas + sus campos). Residentes ademas: toggle Tabla/Tarjetas.
- **Gestor de campos (ConfigCamposEntidad, componente COMPARTIDO -> lo heredan las 9 superficies):**
  - **Formula MULTI-PASO**: la formula pasa de 1 operacion a una lista de PASOS (op + operandos: campo
    Numero/Moneda, constante, o resultado de un paso anterior) + operaciones binarias Resta/Division/
    Multiplicacion. Retrocompatible: las formulas de 1-op ya guardadas computan IDENTICO. Test de
    regresion 11/11 verde. Verificado runtime (formula de 2 pasos = Coef-10, F5 persiste).
  - **Selector de tipo amigable**: dropdown con categorias + descripcion por tipo.
  - **Crear == Editar**: al editar, el tipo se agrupa igual que al crear; los avanzados salen bloqueados
    (candado + tooltip) porque no se convierten sin recrear.
- **Fix del modal del gestor**: el CSS `.cg-*` del modal se movio a `config-campos.css` (global) para que
  el gestor FLOTE en cualquier superficie/pestaña (antes vivia inline en Distribucion y caia inline fuera).
- **Deploy docs** (este handoff + checklists) actualizados.

Pendiente #4 (ortografia de nombres/valores) DIFERIDO por decision de Alex (revision por-modulo despues).

### Migraciones: SIN NOVEDAD (B no agrega ninguna)
La ultima migracion en `main` sigue siendo `20260913005909_AddCostoEstimadoProgramacionTarea`. B es
migration-free. El set pendiente vs prod 0.0.67 es EXACTAMENTE el de la **seccion 2** (9 aditivas). Si
prod ya recibio 0.0.93/0.0.94, **no queda ninguna migracion pendiente** y 0.0.95 seria solo-codigo.
(Nota: entre 0.0.93 y 0.0.95 hubo un bump intermedio 0.0.94 "para deploy"; ni ese ni B tocaron el esquema.)

### CRITICO - LO QUE **NO** ESTA EN MAIN (aun en ramas)
- **Fase 2 en Seguros/Contratos (SELLO)** y **Mantenimiento (YUNQUE)** - en curso; rebasean sobre este
  main (heredan el gestor corregido) y luego se integran. Migration-free (reusan el componente).
- **Ronda de ajustes de homogeneidad**: ATLAS (Tareas, orden + T-11 + T-09) **CON migracion
  `tablero_campos_config`** (rama `equipo/atlas-tareas`, no aplicada), FARO (PQRSD), YUNQUE (color/forma).
- **Directorio / Usuarios con campos propios (EAV nuevo, CON migracion)** - solo planificado, sin codigo.

### Verificacion de lo que SI va (B)
- Build Release del Web sobre el main mergeado: **0 errores**.
- Runtime (5105): hub 9 pestañas OK; Fase 2 en Veh/Mas/Residentes OK (columnas, captura, F5, cross-tenant);
  formula multi-paso OK (2 pasos, computo correcto, F5); modal del gestor FLOTA en todas. Datos de prueba borrados.
- Tests: gestor de formula 11/11 (unit); Fase 2 integracion (Unidades 7/7, Veh/Mas 10/10, Residentes 5/5 Release).
- Baseline de integracion: **224 verdes / 8-9 rojos PREEXISTENTES** (deuda conocida). Correr `dotnet test`
  en el commit exacto antes de desplegar para reconfirmar.

**Paso post-deploy del MENU (ver 0.7): el hub agrega el item "Configuracion Copropiedad" desde el catalogo
del CODIGO (no requiere import), pero la ORGANIZACION del menu sigue siendo data (menu_overrides). Importar
el JSON como siempre si se quiere la organizacion afinada.**

---

## 0. LO CRITICO (leer antes de aplicar)

### 0.1 Hay una migracion de SEGURIDAD

Un test nuevo descubrio que **26 de las 230 tablas con `tenant_id` no tenian RLS EN ABSOLUTO**
(`relrowsecurity = false` y cero politicas) - no que les faltara el FORCE. El `HasQueryFilter` de EF las
seguia filtrando, asi que **no habia fuga por la via normal de la aplicacion**, pero les faltaba la red de
seguridad final: una consulta cruda que olvidara el filtro no tenia nada que la detuviera. Las mas
sensibles eran **Directorio** y **Seguros**.

`AddRlsTablasFaltantes` protege las **18 que son TenantEntity**.

**Riesgo revisado antes de aplicarla** (FORCE RLS ata tambien al dueno de la tabla, y un lector sin tenant
de sesion pasaria de "ve todo" a "ve cero filas" **en silencio, sin error**): ninguna de las 18 se lee con
`IgnoreQueryFilters`, SQL crudo ni Dapper; ningun seeder ni controller de SuperAdmin las toca; y los dos
caminos que corren fuera de un request normal fijan el tenant antes de leer. En dev quedo verificado en el
navegador y la suite de integracion no tuvo regresiones.

**Aun asi, la verificacion #1 post-deploy es entrar a Directorio, Seguros y Contratos y confirmar que
listan datos.** Si alguno queda vacio, revertir con el `Down()` de esa migracion.

**Regla nueva permanente:** cualquier migracion futura que rellene datos en esas 18 tablas debe fijar
`app.tenant_id` primero, o vera cero filas.

### 0.2 Cambio de comportamiento en la plantilla de carga

La hoja UNIDADES ahora emite **solo las columnas de los campos VISIBLES**. Si una copropiedad oculta un
campo en "Configurar", ese campo **deja de salir en la plantilla**. Es lo pedido, pero si alguien reporta
que "desaparecio una columna de la plantilla", la causa es esa.

`UNIDAD PRIVADA` y `PRINCIPAL` estan blindadas y salen siempre.

**Cambio 0.0.98: la plantilla ya NO trae la columna `COPROPIEDAD`** (en ninguna de las 7 hojas). Cada
archivo carga en **UNA sola copropiedad: la activa**. El importador enruta las filas (sin COPROPIEDAD) a la
copropiedad activa con **UPSERT** (grupo interno "SoloVacias"); la logica de grupos multi-copropiedad no se
toco, solo se retiro la columna del generador. **Compat: los archivos VIEJOS que aun traigan COPROPIEDAD se
siguen procesando** (el importador reconoce la columna y el valor "Todas las copropiedades"). La fila de
ejemplo se marca con el sentinel `EJEMPLO (borrar fila)` en una celda de texto (UNIDAD PRIVADA / NOMBRE /
PLACA) y el importador la ignora en cualquier hoja.

**Plantilla de Residentes: la columna `UNIDAD PRIVADA` de PERSONAS/VEHICULOS/MASCOTAS trae un desplegable**
con los codigos de las unidades ya cargadas (TORRE-NUMERO, o Numero suelto). En onboarding (aun sin unidades)
la columna queda como texto libre y se referencia contra la hoja UNIDADES del mismo archivo.

### 0.3 NO hay que crear ni confirmar ningun campo en prod

Los **5 modulos contributivos** son **columnas del sistema**: aparecen solos en todas las copropiedades en
cuanto corre la migracion. **No hay seed, ni script, ni paso manual de creacion.**

Lo unico manual es que **nacen ocultos**: cada copropiedad que los quiera usar los activa en
**Unidades > Configurar** con el icono del ojo. Igual para las 4 pestanas nuevas: los campos del SISTEMA
de Personas/Vehiculos/Mascotas/Terceros salen solos; los campos PROPIOS los crea cada copropiedad.

Consecuencia practica: **tras el deploy nadie vera nada nuevo en la tabla de unidades hasta que active los
campos.** Eso es lo esperado, no un fallo.

### 0.4 PERMISOS: quien puede usar el gestor de campos

**Esto ya mordio en produccion** (403 al activar un campo). Las escrituras de "Configurar" exigen
`MI_COPROPIEDAD / Editar`, con bypass solo si el rol es **EXACTAMENTE** `Administrador`. Y la matriz
`rol_permisos` esta sembrada **escasa**:

| Rol | Permisos MI_COPROPIEDAD habilitados |
|---|---|
| **Administrador** | 6 |
| Consejero, Propietario | 1 (solo ver) |
| Asistente, Contador, Coordinador, Operario, Residente, Vigilante, Inmobiliaria, Revisor Fiscal | **0** |

O sea: **un 403 al activar un campo es lo ESPERADO para casi cualquier rol**, no un bug.

**Caso traicionero:** un Administrador de la copropiedad A que **cambia a la copropiedad B sin tener
vinculo alli** pierde el bypass (`GetRolActorAsync` devuelve null) y todo escribir da 403, aunque la
cabecera le siga mostrando "Admin". Comprobar:

```sql
select ut.rol, ut.estado, t.nombre
from usuarios_tenant ut
  join tenants t on t.id = ut.tenant_id
  join personas p on p.id = ut.persona_id
where lower(p.email) = 'EMAIL_DEL_USUARIO';
```

Si para esa copropiedad no hay fila, o el rol no es exactamente `Administrador`, ahi esta la causa.

**Decision pendiente:** si se espera que un Coordinador o un Asistente configure campos, hay que
habilitarles `MI_COPROPIEDAD / Editar` en la matriz por defecto. Hoy no pueden.

### 0.7 EL MENU LATERAL ES DATA, NO CODIGO (paso manual post-deploy)

La reorganizacion del menu (grupo **Configuracion** con Unidades Privadas, Residentes, Mascotas,
Vehiculos, Equipos y Zonas, Usuarios, Directorio, Lineas WhatsApp, Lista negra; y **Mi Copropiedad**
reducido a "Mi copropiedad") **NO viaja por git**. Vive en la tabla global `menu_overrides` y se
gestiona desde **Super Admin > Configuracion de menu**. Un `git push` NO la lleva a prod.

**Paso post-deploy (una sola vez):**
1. Desplegar el codigo PRIMERO (para que existan en el catalogo las rutas nuevas `/vehiculos` y
   `/mascotas`; si se importa antes, esas dos entradas se descartan en silencio al guardar).
2. En prod: **Super Admin > Configuracion de menu > Importar JSON** (el `menu-propia.json` que exporta
   Alex desde dev) **> Guardar cambios**.
3. El import **reemplaza** TODO el menu de prod (borra los overrides viejos y reinserta los del JSON):
   asi desaparece el "Residentes" dummy (`/proximamente`) y queda el modulo real, sin pasos extra.

**Sin este import, el deploy NO falla:** los modulos `/vehiculos` y `/mascotas` son alcanzables igual
(salen en su ubicacion por defecto, Mi Copropiedad, desde el catalogo del codigo). Lo que el import
cambia es solo la ORGANIZACION del menu. El cache del menu es en memoria (TTL 10 min) y se invalida
solo al Guardar, asi que el cambio se ve al instante tras importar.

### 0.6 IMAGENES: las de las copropiedades son URLs EXTERNAS

En prod las imagenes de copropiedad estaban guardadas como rutas del almacenamiento LOCAL
(`/uploads/tenants/...`). En Railway el disco es **efimero**: se borra en cada redeploy, asi que daban 404
y se veian rotas. Se limpiaron esas 8 URLs muertas (4 logos + 3 fachadas + una cadena vacia) y se pusieron
**URLs externas** (Unsplash, licencia libre) que no dependen del disco y sobreviven a los redeploys.

**Para que se vean hace falta el fix de 0.0.92** (ver seccion 1): hasta ese deploy siguen rotas, porque el
bug estaba en el codigo, no en el dato.

**Pendiente de infraestructura:** confirmar `Storage__Provider=R2` y las `R2__*` en Railway. Mientras no
este, **cualquier imagen que suba un usuario por la app se volvera a perder en el siguiente deploy**.

```bash
railway variables | grep -iE "Storage__Provider|R2__"
```

Nota: las fotos actuales son de archivo, genericas. Conviene reemplazarlas por fotos reales de cada
copropiedad cuando se tengan.

### 0.5 Si "Configurar" parece no responder

Desde 0.0.91 el panel **avisa y dice la causa**:
- **500** con `column ... does not exist` -> **faltan migraciones** (seccion 2).
- **403** -> **permisos** (seccion 0.4). El mensaje distingue si es el rol, el permiso, o que el usuario
  no esta vinculado a esa copropiedad.

(Hasta 0.0.89 se quedaba mudo: un entorno sin migrar o sin permisos era indistinguible de un boton roto.
Ese fue justamente el sintoma que costo diagnosticar.)

---

## 1. Que se despliega (0.0.68 -> 0.0.93, acumulado sobre prod 0.0.67)

- **0.0.93 - Vehiculos y Mascotas son modulos propios; el panel Campos de Unidades queda solo con la
  unidad; y llega el lote del equipo (Tareas + Mantenimiento).** Detalle:
  - **Vehiculos (`/vehiculos`) y Mascotas (`/mascotas`)** salen de la ficha de la unidad a modulos
    independientes con la vista tabla estandar y su propio panel de Campos. **Sin migracion**: reusan
    `unidad_placas` y `unidad_mascotas` y sus catalogos de campos, que ya existian. Backend nuevo: GET
    agregado por copropiedad y **PUT** de placa/mascota (antes solo habia alta y baja; la edicion en
    linea los necesita), gateado con `MI_COPROPIEDAD/Editar`.
  - **Panel "Campos" de Unidades = solo la unidad.** Se unifico la entrada (se fue el boton "Configurar"
    duplicado) y salieron las pestanas Personas/Vehiculos/Mascotas/Terceros. Personas se configura en su
    modulo (`/residentes`); Vehiculos/Mascotas en los suyos. Terceros configuraba las empleadas de la
    unidad (no el Directorio) y quedaba confuso: se quito.
  - **Carga por Excel (afinada sobre 0.0.88):** un campo VISIBLE ahora si sale como columna en la
    plantilla y se importa (faltaban 9 de sistema); TIPO propio de la copropiedad se ofrece y se importa,
    y ya no se reescribe a "Apartamento" en silencio; las listas de los desplegables pasaron a una hoja
    oculta (sin tope de longitud). **Sin migracion.**
  - **Lote del equipo (con migracion, ver seccion 2):** Tareas T-01..T-05 (permisos en 41 endpoints,
    validacion de pertenencia al tenant, un solo camino de cambio de estado, aislamiento), permiso
    Operario-crear-tareas, e indices `tenant_id` en las tablas de tableros; Mantenimiento M-01 (job
    diario del preventivo, idempotente, sin columna nueva).
  - **Menu lateral reorganizado -> es DATA, ver 0.7.** No viaja por git; se importa en Super Admin.
- **0.0.92 - Una imagen EXTERNA ya no se rompe al resolver su URL.** Ninguno de los dos proveedores de
  almacenamiento podia guardar una imagen alojada fuera de la plataforma: `ResolveUrl` asumia que toda URL
  absoluta era un blob propio con host viejo y la reescribia (Local le arrancaba el host y devolvia solo el
  path; R2 colgaba el path de su propio endpoint). En ambos casos -> 404 e imagen rota. Ahora solo se
  reescribe lo propio (path con `/uploads/`, o host del bucket/dominio publico en R2); el resto queda
  intacto. **Es el fix que hace visibles las imagenes de copropiedad (ver 0.6).** Solo codigo.
- **0.0.91 - El 403 de Configurar ya dice por que y que hacer.** El backend distingue la causa en `reason`
  (`permiso_insuficiente`, `rol_insuficiente`, `sin_persona`) y no se estaba leyendo. Ver 0.4 y 0.5.
- **0.0.90 - Un guardado fallido de configuracion ya no se queda mudo.** Ver 0.5.
- **0.0.89 - 5 modulos contributivos + plantilla filtrada por visibilidad.**
  `unidades_privadas` +`modulo_contributivo_1..5` `numeric(7,4)` NULL (misma precision que
  `coeficiente_propiedad`). En el gestor de campos salen debajo de Coeficiente y **ocultos por defecto**.
  El importador los lee; celda vacia no pisa el valor existente. Ver 0.2 para la plantilla.
  **Sin validacion de suma al 100%** -> seccion 6.
- **0.0.88 - Carga por Excel: los campos dinamicos ya se guardan.** La plantilla emitia las columnas
  `[Label]` pero **el importador no las leia**: el dato se descartaba sin un solo error. Ahora se guardan
  en las 6 hojas con catalogo (UNIDADES, PERSONAS, VEHICULOS, MASCOTAS, ZONAS COMUNES, EQUIPOS), al crear
  y al actualizar. Una columna `[X]` desconocida avisa una vez por hoja.
- **0.0.87/0.0.86 - SEGURIDAD: RLS en 18 tablas.** Ver 0.1.
- **0.0.85/0.0.84 - Gestor de campos Fase 3 + listas con semilla.** Las 4 pestanas del panel "Configurar"
  (Personas, Vehiculos, Mascotas, Terceros) ya son funcionales: campos propios por entidad + los campos
  del sistema de cada ficha con alias, visibilidad, ubicacion y -donde la columna real lo permite- tipo y
  formato. **La seccion TERCEROS no existia en la ficha** (el backend estaba completo pero no se
  renderizaba en ninguna parte): se le construyo pestana y seccion. Los tres estados de fabrica ya son
  semilla de verdad (candado, solo se ocultan), igual que los tipos base. La lista se administra en un
  popup. Al ocultar una opcion, las unidades que ya la usan la conservan. Migraciones
  `AddCamposEntidadesVinculadasUnidad` (8 tablas con su RLS) y `AddEntidadAUnidadCampoConfig`.
- **0.0.87 - Etapa B:** tipo de dato cambiable (solo entre tipos compatibles con la columna real) y
  formato (decimales, separador de miles, moneda, maximo de caracteres).
- **0.0.86 - Todos los campos como columnas + drag & drop.** La "ubicacion" del panel ES el orden de la
  tabla. Orden y visibilidad pasan de `localStorage` por usuario a configuracion **por copropiedad**.
- **0.0.83 - Carga de unidades: anexos idempotentes.**
- **0.0.82 - Configurar unidades = administrador de campos.** Migracion `AddUnidadCampoConfig`.
- **0.0.81 - Tipos de unidad configurables** (+tipo base "Cajeros"). Migracion `AddUnidadTipoCustom`.
- **0.0.74-0.0.80 - Modulo Mantenimiento reelaborado** (Programacion en tabla con alta inline, Tercero +
  Contrato opcionales, sin Cron, Calendario Mes/Semana, fix del flotante del SelectorPersona por CSP).
- **0.0.69/0.0.71 - Campos dinamicos tipados en Zonas y Equipos.**
- **0.0.68 - Unidades: campos dinamicos como columnas + Estado dropdown.**
- **0.0.64 - PQRSD alertas de plazo.**
- Incluye lo de 0.0.63-0.0.67 que no haya llegado a prod (Contratos IA, hotfix 403 OCR, etc.).

## 2. Migraciones a aplicar (17 pendientes vs prod, todas ADITIVAS)

```bash
cd src/Propia.Api
dotnet ef database update --project ../Propia.Infrastructure --startup-project .
```

`ef database update` aplica SOLO las que falten (compara `__EFMigrationsHistory`). Las **9 nuevas de esta
tanda**, en orden:

1. `20260910005638_AddUnidadCampoConfig` - tabla nueva `unidad_campos_config` con RLS FORCE + policy + GRANT.
2. `20260910112409_AddUnidadCampoConfigTipoFormatoOrden` - +`tipo` int null, +`formato` text null,
   +`oculto` bool default false, +`orden` int null.
3. `20260910120849_AddCamposEntidadesVinculadasUnidad` - **8 tablas nuevas** (persona/vehiculo/mascota/
   tercero x definiciones+valores), **cada una con ENABLE + FORCE + policy tenant_isolation + GRANT**.
4. `20260910132959_AddEntidadAUnidadCampoConfig` - +`entidad` varchar(20) NOT NULL default `'unidad'`;
   DROP del unico `(tenant_id, campo_clave)` y CREATE de `(tenant_id, entidad, campo_clave)`.
5. `20260910140101_AddRlsTablasFaltantes` - **SEGURIDAD**, ver 0.1. Solo SQL, sin cambios de esquema.
6. `20260910202226_AddModulosContributivosUnidad` - `unidades_privadas` +`modulo_contributivo_1..5`
   numeric(7,4) NULL.

7. `20260910235018_EquipoAtlas_AddIndicesTenantIdTareas` - **solo indices**: 6 CREATE INDEX de
   `tenant_id` en `tableros`, `tablero_usuarios`, `tablero_campos`, `tarea_campo_valores`,
   `tarea_adjuntos`, `tarea_subtareas`. No cambia esquema ni datos.
8. `20260912205405_AddPermisoCrearTareasOperario` - seed en la tabla global `rol_permisos`: da al rol
   Operario el permiso Crear sobre TAREAS. Solo datos (idempotente).
9. `20260913005909_AddCostoEstimadoProgramacionTarea` - `programacion_tareas` +`costo_estimado`
   numeric(14,2) NULL. Modulo Mantenimiento (YUNQUE): costo estimado de una programacion. Aditiva, sin
   datos ni indices; el `Down()` hace DROP de la columna.

(Las anteriores 0.0.68-0.0.83 estan listadas en `DEPLOY_CHECKLIST.md`.)

**Comprobacion rapida de que quedaron aplicadas:**

```sql
select column_name from information_schema.columns
where table_name = 'unidad_campos_config' order by 1;
-- deben estar: alias, campo_clave, entidad, formato, id, oculto, opciones, orden, tenant_id, ...

select count(*) from information_schema.columns
where table_name = 'unidades_privadas' and column_name like 'modulo_contributivo%';  -- debe dar 5
```

> Ninguna borra datos. La 4 recrea un indice unico; la 5 no toca esquema.

## 3. REGLA CRITICA (no romper el login otra vez)

- **Criptografia se queda en 9.x.** NO subir `Microsoft.AspNetCore.Cryptography.KeyDerivation` ni
  `DataProtection` a 10.x. Mezclar 10.x rompio el login en 0.0.66.
- Build de Railway con restore fresh `-warnaserror`: verificar 0 NU1603/NU1605.
- **CSP:** el sitio corre sin `unsafe-eval` (solo `wasm-unsafe-eval`). No introducir JS que dependa de
  `eval()`; los helpers van en `wwwroot/js/*.js` (subir `?v=` en `Components/App.razor` al tocarlos).
  El drag & drop de esta tanda usa eventos nativos de Blazor, sin JS.

## 4. Variables de entorno obligatorias en prod

`Jwt__SigningKey` (>=32 chars real), `Propia__WebhookToken`, `Meta__AppSecret`,
`Storage__Provider=R2` + `R2__*`, `ConnectionStrings__*` (owner + app con RLS).
Recomendadas: `Metrics__ScrapeToken`, `ForwardedHeaders__KnownNetworks__0` (CIDR del proxy Railway).

## 5. Post-deploy (verificacion minima)

- [ ] Login OK; el footer muestra `v0.0.93`.
- [ ] **Menu (ver 0.7):** tras importar el JSON en Super Admin y Guardar, el grupo **Configuracion**
      muestra Unidades Privadas, Residentes, Mascotas, Vehiculos, Equipos y Zonas...; y **Mi Copropiedad**
      queda solo con "Mi copropiedad". Entrar a `/vehiculos` y `/mascotas` y confirmar que listan.
- [ ] **Imagenes de copropiedad:** el selector "Mis copropiedades" muestra la foto de cada una, no el
      icono roto. Si sale rota, revisar que el `src` renderizado conserve el host completo (era el bug
      de 0.0.92).
- [ ] **PRIMERO (RLS):** entrar a **Directorio**, **Seguros** y **Contratos** y confirmar que **listan
      datos**. Si alguno sale vacio -> revisar logs por `row-level security` / `42501` y revertir el
      `Down()` de `AddRlsTablasFaltantes`. (En dev: Directorio 200 filas, Seguros 7, Contratos 21.)
- [ ] Las 18 tablas quedan con `relrowsecurity` y `relforcerowsecurity` en `true` y >=1 politica; en toda
      la BD deben quedar solo **8** tablas de tenant sin RLS (las de la seccion 6).
- [ ] **Probar "Configurar" con un usuario cuyo rol sea EXACTAMENTE `Administrador` en esa copropiedad**
      (ver 0.4): con otro rol el 403 es lo esperado, no un fallo.
- [ ] **Si no responde:** el aviso rojo dice la causa. 500 con `column ... does not exist` = faltan
      migraciones (seccion 2). 403 = permisos (seccion 0.4).
- [ ] **Modulos contributivos:** en Unidades > Configurar salen "Modulo Contributivo 1..5" debajo de
      Coeficiente y **ocultos**; activar uno lo saca como columna y guarda decimales (ej. 12.3456).
- [ ] **Plantilla filtrada:** con un modulo activo, la plantilla trae SOLO ese (no los otros 4); ocultar
      un campo (ej. Matricula) lo saca de la plantilla y la fila de EJEMPLO sigue alineada.
      `COPROPIEDAD`, `UNIDAD PRIVADA` y `PRINCIPAL` deben salir SIEMPRE.
- [ ] **Configurar > pestanas Personas/Vehiculos/Mascotas/Terceros:** cada una lista sus campos del
      sistema; crear un campo propio (ej. "Numero de chip" en Mascotas) y comprobar que aparece en esa
      seccion de la **ficha** y guarda su valor. La ficha debe tener pestana **Terceros**.
- [ ] **Listas:** "Gestionar lista" abre un **popup** encima del modal. Los tres estados de fabrica y los
      tipos base salen con candado y **sin eliminar**, solo el ojo. Ocultar una opcion la saca de los
      selectores pero una unidad que ya la usaba la **sigue mostrando en su fila**.
- [ ] **Columnas:** mostrar/ocultar cualquier campo como columna y reubicarlo arrastrando; el orden se
      mantiene al recargar **y lo ven todos los usuarios de la copropiedad** (ya no es por usuario).
- [ ] **Carga por Excel con campos dinamicos:** con un campo propio configurado, descargar la plantilla,
      verificar la columna `[Nombre del campo]`, llenarla, cargar y confirmar el valor en la ficha. Una
      columna `[X]` inventada debe dar **un** aviso por hoja.
- [ ] `/mantenimiento`: solo tabs Programacion y Calendario; alta inline; el panel del SelectorPersona
      FLOTA sin deformar la fila; Calendario Mes/Semana.
- [ ] `/health` = 200; cabeceras de seguridad presentes; un rol no-admin recibe 403 en una escritura gateada.

## 6. Pendientes que necesitan DECISION (no bloquean el deploy)

- **Permisos del gestor de campos.** Hoy solo el rol `Administrador` puede configurar campos. Decidir si
  Coordinador/Asistente deben poder, y en ese caso habilitarles `MI_COPROPIEDAD / Editar` en la matriz por
  defecto (hoy la siembra les deja 0 permisos en ese modulo). Ver 0.4.
- **8 tablas de tenant siguen sin RLS** y el test de cobertura **falla a proposito** listandolas: 6 con
  `tenant_id` NULLABLE (`calendario_eventos`, `notificaciones`, `login_audit_events`, `sistema_logs`,
  `usuario_sesiones`, `document_extraction_logs`) y 2 de Capa 1 cruzadas por diseno
  (`org_colaborador_copropiedades`, `panel_snapshot_copropiedades`). Decidir por cada una: politica que
  tolere `NULL`, o exencion documentada en el test.
- **Modulos contributivos sin validacion de suma.** Son campos libres. El dominio YA tiene
  `TipoCoeficiente` + `UnidadCoeficiente` con la regla RN-02 (cada tipo debe sumar 100%) y sus endpoints
  (`tipos-coeficiente`, `unidades/{id}/coeficientes`), pero **sin ninguna UI que los consuma**. Decidir si
  los modulos migran a ese modelo (permite N modulos y valida la suma) o se quedan como columnas planas.
- **TERCEROS = dos conceptos con el mismo nombre.** El catalogo de la pestana cuelga de `UnidadEmpleada`
  (empleadas de servicio de una unidad); la hoja TERCEROS de la plantilla carga terceros del **Directorio**
  (crea Empresa/Persona global + vinculo, sin columna UNIDAD PRIVADA). Por eso esa hoja quedo sin columnas
  dinamicas. Decidir: renombrar la pestana a "Empleadas/Personal" y dar al Directorio su propio catalogo,
  o que la hoja TERCEROS cree `UnidadEmpleada`.
- **Campos de sistema de Personas incompletos en el panel:** la hoja PERSONAS de la plantilla tiene `SEXO`,
  `FECHA NACIMIENTO` y `PROFESION`, que no aparecen en la pestana de configuracion porque viven en la tabla
  global `personas`, no en `UnidadPersona`.
- **Visibilidad/orden de los campos de SISTEMA de las fichas vinculadas:** se guardan pero todavia no
  cambian el render de la ficha. Los campos **propios** si se ordenan y ocultan.
- **Separador de miles:** se guarda y se aplican decimales y maximo de caracteres, pero el separador no se
  pinta dentro de celdas editables (reparsear "1.234" es donde se corrompen coeficientes y cuotas).
- **Dato malo (revisar en prod):** en dev habia 1 unidad con `tipo = 0`, fuera del enum `TipoUnidad` (que
  empieza en 1). Antes se mostraba como "Apartamento" y cualquier edicion la habria guardado asi; ahora se
  ve `(sin definir: 0)`. Buscar en prod:
  `select id, numero, tipo from unidades_privadas where tipo not between 1 and 20;`
- **Bug preexistente en `ContratosVencimientoJob`:** el `continue` de la linea 45
  (`if (contratos.Count == 0) continue;`) salta al siguiente tenant, asi que el bloque de alertas de
  **polizas** (linea 83) nunca corre para una copropiedad sin contratos con fecha fin -> esa copropiedad
  **no recibe alertas de vencimiento de seguros**. Ademas la linea 115 (`catch { errores++; }`) se traga
  toda excepcion por tenant sin log.

## 7. Rollback

Si algo falla: redeploy del artefacto **0.0.67** (ultimo bueno en prod). Las migraciones son aditivas, no
hace falta revertir esquema para volver (las columnas/tablas nuevas quedan sin uso).

**Excepcion:** si el problema es RLS (modulos que salen vacios), correr el `Down()` de
`AddRlsTablasFaltantes`, que desactiva la RLS de esas 18 tablas y las deja como estaban.
