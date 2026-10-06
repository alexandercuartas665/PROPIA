# Plan por olas — Mudar las listas del sistema al Super Admin (catálogo global A&D)

> Autor del pedido: Alex. Redacción: sesión ALEX, 2026-10-06.
> **Es función NUEVA (regla 7.0): este documento es el PLAN, no hay nada construido todavía.**
> Objetivo de Alex: que las listas editables (Tipo de unidad, Estado de la propiedad, etc.) vivan en
> la consola A&D (Super Admin) para "no pelear con código" al corregir ortografía o agregar opciones.

## 1. Objetivo y alcance

Hoy las "Opciones de la lista" de los campos de sistema salen de **3 capas mezcladas**:

1. **Código** — enum (`TipoUnidad` en `Domain/Enums/MiCopropiedadEnums.cs`) y semillas
   (`UnidadCamposSistema.EstadosSemilla = {Habitada, Desocupada, Arrendada}`). El texto visible lo
   resuelve `Etiqueta()` en la UI. **Global a todo el producto, solo cambia con deploy.**
2. **BD por copropiedad** — `unidad_campos_config.Opciones` (orden/ocultar/color) + tipos propios en
   `tipos_unidad_custom` (`TenantEntity`). Editable desde el modal "Editar campo".
3. **Super Admin** — hoy **NADA**.

**Meta:** subir la capa 1 (semilla/fábrica) a un **catálogo global editable por A&D** en la consola,
manteniendo la capa 2 (override por copropiedad) tal cual. Así la ortografía y las opciones de fábrica
se editan sin tocar código ni desplegar.

### Qué SÍ se muda (inventario COMPLETO — Ola 0 cerrada 2026-10-06)
Picklists que el usuario ve/edita como "lista" (campos `Tipo == Seleccion` en los `*CamposSistema`).
**Hallazgo clave:** ya existe el flag `OpcionesEditables` por campo que separa Libre (true) de
enum-fijo (false) — es exactamente la taxonomía Libre / Solo-label. Lo reusamos.

| Módulo | Lista | Origen hoy | `OpcionesEditables` | Clase |
|---|---|---|---|---|
| Unidades | **Tipo** | enum `TipoUnidad` + `tipos_unidad_custom` | true | Libre (semilla=enum) |
| Unidades | **Estado de la propiedad** | semilla `EstadosSemilla` (código) + config tenant | true | **Libre** |
| Personas/Directorio | **Tipo de residente** | enum | true | Libre |
| Personas/Directorio | **Tipo de ID** | enum | true | Libre |
| Personas/Directorio | **Sexo** | enum | true | Libre |
| PQRSD | **Categoría** | `pqrsd_categorias` (BD por tenant) | false (gestión propia) | **Libre** (ya) |
| PQRSD | Tipo (petición/queja/…) | enum/código | false | **Solo-label** (plazo legal Ley 1755) |
| PQRSD | Etapa / Estado | enum/código | false | **Solo-label** (workflow + SLA) |
| Contratos | Tipo de contrato | ya `string` por tenant | — (string) | **Libre** (ya) |
| Contratos | Categoría | ya `string` por tenant | — (string) | **Libre** (ya) |
| Contratos | Vencimiento / Estado | enum | false | **Solo-label** |
| Contratos | Tipo de servicio | enum | false | Solo-label |
| Mantenimiento | Tipo | enum | false | Solo-label (revisar) |
| Mantenimiento | Tablero | — | false | Solo-label |
| Mantenimiento | Prioridad | enum | false | Solo-label |
| Mantenimiento | Periodicidad | enum | false | Solo-label |
| Equipos | Categoría, Tipo, Estado | enum | revisar por campo | mixto |
| Zonas | Categoría, Estado | enum | revisar por campo | mixto |
| Vehículos | Tipo de vehículo | enum | revisar | mixto |
| Mascotas | Tipo | enum `TipoMascota` | revisar | mixto |

> Pendiente fino de Ola 0: confirmar `OpcionesEditables` real por campo en Equipos/Zonas/Vehículos/
> Mascotas (algunos ya son Libre). No bloquea: el modelo trata ambas clases.

### Qué NO se muda (queda en código, a propósito)
Las **máquinas de estado internas** NO son listas editables: `EstadoFactura`, `EstadoSuscripcion`,
`EstadoSesion`, `EstadoVotacion`, `EstadoAcuerdoPago`, `ResultadoVotacion`, etc. Cambiar sus valores
rompe lógica. A lo sumo se les edita el **label** si aparecen en UI, nunca el conjunto.

## 2. Principio de diseño (lo que decide todo lo demás)

**Separar CLAVE (estable, para datos) de LABEL (editable, para mostrar).**

- La **clave** es inmutable y es a lo que apuntan los datos históricos (hoy, el `int` del enum o el
  texto guardado). NUNCA la edita A&D.
- El **label** (y orden/color/activo) es lo que A&D edita en el catálogo global.
- **Herencia:** `catálogo GLOBAL (A&D)` → `override por copropiedad` (la capa 2 actual) →
  resolvedor único. Una copropiedad puede renombrar/ocultar/reordenar/añadir **sobre** lo global.
- **DECISIÓN Alex (2026-10-06):** modelo **global A&D + override por tenant** (no se centraliza;
  las copropiedades siguen pudiendo ocultar/renombrar/agregar sobre lo global), y **A&D puede
  agregar/quitar en TODAS las listas, incluso las atadas a enums.**
- **Clases de lista (ya no restringen el CRUD, pero sí qué datos lleva la opción):**
  - **Libre:** opción = label + orden + color (Estado, Categorías, Tipos, etc.).
  - **Con-lógica (antes "Solo-label"):** la opción lleva **metadata de negocio** que hoy está
    hardcodeada en el `switch` del enum (ej. PQRSD tipo → **días de plazo legal**; etapa → **es
    terminal / cuenta SLA**; prioridad → peso). Como A&D ahora puede AGREGAR opciones aquí, el código
    debe **leer esa metadata del catálogo** en vez de `switch`ear el enum. Al crear una opción, A&D
    captura su metadata (con un default seguro si falta).
- **Integridad:** NO se borra el enum de golpe. Las claves existentes conservan su clave estable
  (enum int para lo que tiene lógica; string-clave para lo Libre y lo ya migrado a `string`). Las
  opciones NUEVAS que cree A&D nacen con **string-clave** (no son valores del enum). El código deja de
  depender del `switch(enum)` y pasa a depender de la **metadata por clave** del catálogo.
- **Riesgo asumido:** quitar/ocultar una opción con lógica (ej. una etapa de PQRSD) puede afectar
  expedientes en curso → el CRUD avisa "en uso" y, para las con-lógica, preferir **ocultar** sobre
  borrar. Las opciones nuevas sin metadata válida usan el default y se marcan para revisión.

## 3. Modelo de datos propuesto (a validar en Ola 0)

- Tabla **GLOBAL** (sin `tenant_id`), p.ej. `catalogo_opcion`:
  `Lista` (discriminador: `unidad.tipo`, `unidad.estado`, `pqrsd.tipo`, …), `Clave` (estable: enum int
  para las existentes con lógica, string para Libres y nuevas), `Label`, `Orden`, `Color?`, `Activo`,
  `EsSemilla` (vino del enum/semilla; se puede ocultar pero avisa), y **`Meta` (JSON)** para la
  metadata de negocio de las listas con-lógica (ej. `{ "plazoDias": 15 }` en PQRSD tipo, `{ "terminal":
  true, "cuentaSla": false }` en etapa). Las Libres dejan `Meta` vacío.
- **RLS atípica:** NO lleva `tenant_id`. Lectura para todos los tenants (runtime), **escritura solo
  A&D** (endpoints admin gateados `~/api/admin/catalogos`). Ojo con `RlsCoverageTests`: hay que
  declararla como tabla global exenta (como otras tablas de plataforma), no como tabla de tenant.
- **Override por tenant:** se reusa lo existente (`*_campos_config.Opciones` + `tipos_unidad_custom`).
  No se inventa una tabla nueva por tenant.
- **Resolvedor único** (servicio + cache): `label/opciones = merge(catálogo global, override tenant)`.
  Reemplaza/alimenta los `Etiqueta()` / `LeerOpciones()` dispersos. Fuente única de verdad.

## 4. Plan por OLAS

> Estado: [ ] pendiente · (🔄) en curso · [x] hecho. Cada ola es desplegable por sí sola.

### Ola 0 — Inventario + decisión de modelo (SIN código)
- [ ] Inventariar TODAS las listas de cara al usuario (completar la tabla de §1 con módulos faltantes:
      Mantenimiento equipos/zonas, Reservas, Tareas prioridad, Comunicaciones, etc.).
- [ ] Clasificar cada una: **Libre** vs **Solo-label**; marcar la lógica atada (quién `switch`ea sobre
      el valor). Esto define qué permite la consola por lista.
- [ ] Cerrar el modelo de datos de §3 con Alex (nombre de tabla, RLS global, discriminador, cache).
- **Hecho =** tabla de inventario completa + modelo aprobado por Alex. Sin esto NO se codea.

### Ola 1 — Infraestructura del catálogo global  ✅ (2026-10-06, sesión ALEX)
- [x] Entidad `CatalogoOpcion` (`BaseEntity`, GLOBAL sin tenant_id) + migración `AddCatalogoOpcion`
      (tabla `catalogo_opciones`: lista/clave/label/orden/color/activo/es_semilla/meta jsonb; índice
      único (lista,clave) + (lista,orden); GRANT a propia_app). **Sin RLS a propósito** (tabla de
      plataforma; `RlsCoverageTests` solo mira tablas CON tenant_id → no aplica, sin exención).
      **Migración aplicada en dev** (OK de Alex).
- [x] Seed de arranque idempotente (`CatalogoListasSeeder`): importa `TipoUnidad` (clave `e:<Enum>`,
      label de `TiposUnidadCatalogo`) y `EstadosSemilla`. Corre SIEMPRE, solo inserta lo que falta,
      NO pisa lo editado. Verificado en BD: `unidad.tipo`=20, `unidad.estado`=3, todas semilla/activas.
- [x] Servicio lector `ICatalogoListas` + `CatalogoListasService` con cache de proceso (TTL 30s +
      invalidación). Registrado en DI.
- [x] Tests de paridad (`CatalogoListasParityTests`, puros): 3/3 verde — el registro cubre todo el
      enum con label/clave/orden de fábrica.
- **Pendiente de enganche:** NINGÚN módulo lee aún del catálogo (eso es Ola 3). Nada cambió en pantalla.
- **Archivos:** `Domain/Entities/CatalogoOpcion.cs`, `Application/Catalogos/ICatalogoListas.cs`,
  `Infrastructure/Catalogos/{CatalogoListasService,CatalogoListasSeeder}.cs`, config en
  `ConfigureGlobalesCore`, DbSet, DI, Program.cs (Web+Api), migración, test.

### Ola 2 — Super Admin UI (consola A&D)  ✅ (2026-10-06, sesión ALEX)
- [x] Sección "Catálogos de listas" en la consola A&D (`/admin/catalogos`, en la pestaña Plataforma):
      índice de listas a la izquierda; a la derecha tabla con orden (subir/bajar), nombre visible
      editable, color, activo (switch), clave read-only + badge "semilla", botón Guardar por fila, y
      fila para agregar opción. Página `Pages/Capa0/Admin/Catalogos.razor` + entrada en `AdminNavMenu`.
- [x] Backend: `AdminCatalogosController` (`[Route("api/admin/catalogos")]`, policy **SuperAdmin**):
      GET listas / GET opciones / POST crear / PUT actualizar / POST reordenar. Servicio
      `ICatalogoListasAdmin` + `CatalogoListasAdminService` (genera string-clave slug para opciones
      nuevas; invalida el cache del lector en cada escritura). `CatalogoListasRegistro` (nombres
      amigables + flag con-lógica) en Application. **Sin migración.**
- [x] Gotcha resuelto: el Web hostea los controllers (`MapControllers`), así que la ruta del controller
      DEBE llevar `api/` (`api/admin/catalogos`); con `admin/catalogos` chocaba con la página Blazor
      (`AmbiguousMatchException`). Patrón confirmado contra `BillingController`.
- [ ] (pendiente, menor) Aviso de "en uso" al ocultar/renombrar (conteo por tenant). No bloquea.
- **Hecho =** verificado en runtime (consola A&D, login founder dev, 3 write paths → BD): editar label
      (clave estable) ✓, agregar opción (clave slug, es_semilla=false) ✓, reordenar ✓. Datos de prueba
      revertidos/borrados. Build 0 errores, sin warnings nuevos.

### Ola 3 — Enganchar Unidades (PILOTO)  ✅ (2026-10-06, sesión ALEX)
- [x] `Etiqueta(TipoUnidad)` ahora lee el label del catálogo global (`unidad.tipo`, clave `e:<Enum>`);
      si el catálogo no cargó, fallback a `TiposUnidadCatalogo`. Es la ÚNICA función de labels de tipo,
      así que el cambio propaga a tabla/selects/grupos/config. La clave enum no cambia (datos intactos).
- [x] Estado: el universo + labels salen de `unidad.estado` (`EstadoUniverso`, fallback `EstadoSemilla`).
      Así A&D puede agregar estados globales y renombrar; el override por copropiedad
      (`unidad_campos_config`, tipos propios) se compone encima sin cambios (`LeerOpciones` igual).
- [x] Endpoint tenant read-only `GET /api/catalogos?lista=X` (`CatalogosController`, `[Authorize]`);
      el panel lo carga en `CargarAsync`.
- [x] **PRUEBA DE ORO verificada:** en A&D renombré `e:Bodega` → "Bodega TEST"; Unidades (tenant demo)
      mostró "Bodega TEST" en el selector de tipo **sin deploy ni recompilar**, sin "Bodega" viejo suelto;
      clave `e:Bodega` intacta. Revertido. Build 0 errores.
- **Scope del piloto (deliberado):** para TIPO se enrutan los LABELS; el universo de tipos base sigue
      saliendo del enum (NO se quitan/agregan tipos base desde el catálogo todavía: agregar un tipo nuevo
      necesita decidir su almacenamiento —enum vs custom— y desactivar uno base necesita cuidado con
      unidades en uso). ESTADO sí toma universo del catálogo (es texto libre). Resto en Ola 4.
- **Pendiente menor:** backfill/normalización de ortografía de fábrica desde A&D (ya es posible; se hará
      cuando Alex lo pida por lista).

### Ola 4 — Resto de módulos (uno por uno, reusando el patrón piloto)

> Mapa de resolución por módulo (hecho 2026-10-06): 3 patrones. **A** = editable por tenant
> (`OpcionCampo`, como Unidades) → solo re-rutear la semilla al catálogo. **B** = enum read-only, label
> por `switch` bespoke en cada panel → re-rutear el label (como Tipo de Unidad), universo sigue del enum.
> **C** = tabla por tenant con CRUD propio + lógica fuerte (PQRSD) → NO colapsar sin preservar metadata.

**Tranche A (listas libres / labels, bajo riesgo):**
- [x] **Contratos · Tipo de contrato + Categoría** (patrón A). Semillas → catálogo
      (`contrato.tipocontrato`/`contrato.categoria`); `OpcionesFijasContrato` lee del catálogo con
      fallback; override por tenant intacto. **Verificado:** renombré "Obra"→"Obra TEST" en A&D y
      Contratos lo mostró. (2026-10-06)
- [x] **Vehículos · Tipo** / **Mascotas · Tipo** (patrón B). `TipoLabel` re-ruteado al catálogo
      (`vehiculo.tipovehiculo` con el map Automovil→"Carro"; `mascota.tipo`=enum), fallback al switch.
      **Verificado:** renombré "Carro"→"Carro TEST" en el catálogo y el select de Vehículos lo mostró. (2026-10-06)
- [ ] **Equipos · Categoría/Tipo/Estado**, **Zonas · Categoría/Estado** (patrón B): re-rutear labels.
      Ojo lógica: Equipos `tipo`=Equipo fuerza Cantidad=1; Zonas `estado`=EnMantenimiento bloquea reservas
      (no cambia por renombrar label; la clave enum se mantiene).
- [ ] **Personas · Tipo de ID** (candidato limpio, enum `TipoDocumento`), **Sexo** (enum `GeneroPersona`),
      **Tipo residente** (string-semilla espejo de `RolUnidadPersona`).

**Tranche B (con-lógica, cuidado — metadata + refactor del switch):**
- [ ] **PQRSD · Tipo** (plazo legal Ley 1755), **Etapa/Estado** (workflow terminal), **semáforo** — hoy
      en tablas por tenant (`pqrsd_tipos/estados`) con su propio CRUD + lógica (`PlazosBase`,
      `SumarDiasHabiles`, `EsTerminal`). Decidir con Alex si se unifican al catálogo global o se dejan en
      su CRUD por tenant (riesgo legal alto). **Categoría** sí es migrable (ya editable).
- [ ] **Mantenimiento · Periodicidad** (cálculo próxima ejecución `PasoPeriodicidad`), **Tipo activo**
      (módulo origen) — con metadata.
- [ ] **Contratos · Estado/Vencimiento** (derivado por fecha, `CalcularSemaforoContrato`), **Tipo de
      servicio** (cosmético) — con metadata / solo label.
- **Hecho =** cada módulo lee del catálogo; las con-lógica leen su metadata del catálogo (sin
      `switch(enum)` hardcodeado para su comportamiento).

### Ola 5 — Gobierno, limpieza y pruebas
- [ ] Deprecar las semillas hardcodeadas ya migradas (dejar solo las claves/enum backbone).
- [ ] Doc final: qué lista es Libre vs Solo-label, y "checklist de ortografía ahora desde A&D".
- [ ] Pruebas: RLS del catálogo global (read-all/write-admin), aislamiento de overrides por tenant,
      datos históricos intactos, performance del resolvedor (cache).
- **Hecho =** la ortografía/opciones de fábrica se editan desde A&D; código solo guarda claves y lógica.

## 5. Riesgos y decisiones para Alex

- **Integridad referencial:** no borrar enums; datos históricos apuntan a sus valores. El catálogo
  pone label editable sobre claves estables. (Decisión: ¿clave = enum int, o migramos todo a
  string-clave como Contratos? Recomendado: enum int para lo logic-bound, string-clave para lo Libre.)
- **Listas Solo-label:** PQRSD tipo/estado, roles, formas de pago NO pueden agregar/borrar opciones
  (romperían lógica/plazos legales). La consola debe bloquearlo explícitamente (`Protegida`).
- **RLS del catálogo global:** es la pieza más delicada (tabla sin `tenant_id`, lectura global,
  escritura solo A&D). Hay que exentarla de `RlsCoverageTests` como tabla de plataforma, no de tenant.
- **Override vs global:** mantener la regla de precedencia clara (tenant manda sobre global) para no
  sorprender a copropiedades que ya personalizaron su lista.
- **Alcance:** empezar por Unidades (piloto) y NO intentar los ~10 módulos de una; cada ola desplegable.

## 6. Qué NO entra
- Las máquinas de estado internas (facturación, asambleas, votaciones, cartera…) quedan en código.
- Los estados/columnas de tablero de Tareas (son por-tablero, otro subsistema).
- Reescribir el override por tenant: se reusa el existente, no se migra.

> Relacionado: [[Inventario vista tabla - Unidades Privadas (topicos)]] (campo Tipo/Estado y "Editar
> campo"), nota de auditoría de tildes/ortografía (3 clases: display/código/semilla-con-backfill),
> y la consola A&D en `Propia.Web` (`~/api/admin/*`).
