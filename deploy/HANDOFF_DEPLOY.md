# HAND-OFF DEPLOY - PROPIA

> Generado 2026-09-10. Version a desplegar: **0.0.92**. Prod actual: **0.0.67**.
> Repo: https://github.com/alexandercuartas665/PROPIA  ·  rama `main`  ·  ultimo commit `82949b6`.
> Companion: `DEPLOY_CHECKLIST.md` (misma carpeta) con el detalle version por version.
> Este archivo es el resumen operativo para la sesion de deploy. **Vive en el repo** (`deploy/`) y se
> copia a la carpeta de trabajo; asi no se pierde.

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

`COPROPIEDAD`, `UNIDAD PRIVADA` y `PRINCIPAL` estan blindadas y salen siempre.

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

## 1. Que se despliega (0.0.68 -> 0.0.92, acumulado sobre prod 0.0.67)

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

## 2. Migraciones a aplicar (14 pendientes vs prod, todas ADITIVAS)

```bash
cd src/Propia.Api
dotnet ef database update --project ../Propia.Infrastructure --startup-project .
```

`ef database update` aplica SOLO las que falten (compara `__EFMigrationsHistory`). Las **6 nuevas de esta
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

- [ ] Login OK; el footer muestra `v0.0.92`.
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
