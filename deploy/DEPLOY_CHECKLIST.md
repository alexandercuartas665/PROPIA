# PROPIA - Checklist de deploy

> Actualizado 2026-09-10. Version visible: **0.0.92**
> (`src/Propia.Web/Propia.Web.csproj` `<Version>`). Bumpear en cada deploy.
>
> **ATENCION en esta tanda: 7 migraciones nuevas, una de ellas de SEGURIDAD (RLS).**
> Ver seccion 3. Todas aditivas; ninguna borra datos.
>
> **PERMISOS: quien puede usar el gestor de campos (leer antes de reportar un bug).**
> Las escrituras de "Configurar" exigen `MI_COPROPIEDAD / Editar`, con bypass solo si el rol es
> EXACTAMENTE `Administrador`. La matriz `rol_permisos` esta sembrada **escasa**: solo Administrador
> tiene los 6 permisos de MI_COPROPIEDAD; Consejero y Propietario tienen 1 (ver); el resto (Asistente,
> Contador, Coordinador, Operario, Residente, Vigilante, Inmobiliaria, Revisor Fiscal) **ninguno**.
> Es decir, **un 403 al activar un campo es lo esperado para casi cualquier rol**, no una falla.
> Caso traicionero: un Administrador de la copropiedad A que **cambia a la copropiedad B sin tener
> vinculo alli** pierde el bypass (`GetRolActorAsync` devuelve null) y todo escribir da 403, aunque la
> cabecera le siga mostrando "Admin". Comprobar con:
> `select ut.rol, ut.estado, t.nombre from usuarios_tenant ut join tenants t on t.id=ut.tenant_id join personas p on p.id=ut.persona_id where lower(p.email)='EMAIL';`
> **Decision pendiente:** si se espera que un Coordinador o Asistente configure campos, hay que
> habilitarles `MI_COPROPIEDAD / Editar` en la matriz por defecto. Hoy no pueden.
>
> **Nuevo en 0.0.92 (una imagen EXTERNA ya no se rompe al resolver su URL):**
> - `ResolveUrl` asumia que toda URL absoluta era un blob propio con host viejo y la reescribia, asi que
>   **ningun proveedor podia almacenar una imagen alojada fuera de la plataforma** (Local devolvia solo el
>   path; R2 la colgaba de su endpoint) -> 404 e imagen rota. Ahora solo se reescribe lo propio.
> - **Contexto:** las imagenes de copropiedad en prod estaban como rutas `/uploads/...` sobre el disco
>   EFIMERO de Railway; se perdian en cada redeploy. Se limpiaron y se pusieron URLs externas, que
>   necesitan este fix para verse. **Pendiente:** confirmar `Storage__Provider=R2` en Railway, o toda
>   imagen que suba un usuario se volvera a perder.
> - **Solo codigo, sin migracion.**
>
> **Nuevo en 0.0.91 (el 403 de Configurar ya dice por que y que hacer):**
> - El aviso mostraba "forbidden" a secas. El backend ya distingue la causa en `reason`
>   (`permiso_insuficiente`, `rol_insuficiente`, `sin_persona`) y no se estaba leyendo. Ahora el mensaje
>   es accionable en cada caso, y el 500 sugiere revisar migraciones sin aplicar.
> - **Solo codigo, sin migracion.**
>
> **Nuevo en 0.0.90 (un guardado fallido de configuracion ya no se queda mudo):**
> - `GuardarCampoConfigAsync` se tragaba el error (catch vacio + no-2xx sin aviso). Con eso, un entorno
>   con las migraciones SIN aplicar -donde el PUT revienta con "column oculto does not exist"- era
>   **indistinguible de un boton roto**: el ojo y el alias parecian no responder. Ahora el panel muestra
>   el codigo HTTP y el mensaje del servidor. **Solo codigo, sin migracion.**
> - Alcance: cubre "el servidor respondio con error". Si lo que se cae es el circuito de Blazor (servidor
>   reiniciandose), el clic no ejecuta codigo C# y aplica el banner de reconexion propio de Blazor Server.
>
> **NO HAY QUE CREAR NI CONFIRMAR NINGUN CAMPO EN PROD.** Los 5 modulos contributivos son **columnas del
> sistema**: aparecen solos en todas las copropiedades en cuanto corre la migracion, sin seed ni script.
> Lo unico manual es que **nacen ocultos**, asi que cada copropiedad que los quiera usar debe activarlos
> en Unidades > Configurar (el ojo). Lo mismo para las 4 pestanas nuevas: los campos del SISTEMA de
> Personas/Vehiculos/Mascotas/Terceros salen solos; los campos PROPIOS los crea cada copropiedad.
>
> **Nuevo en 0.0.89 (5 modulos contributivos + la plantilla solo trae lo visible):**
> - `unidades_privadas` +`modulo_contributivo_1..5` `numeric(7,4)` NULL (misma precision que
>   `coeficiente_propiedad`: son el mismo tipo de dato). Aparecen en el gestor de campos justo debajo de
>   Coeficiente y **ocultos por defecto**; se activan como cualquier columna y se editan inline.
>   Migracion `AddModulosContributivosUnidad`.
> - **Cambio de comportamiento en la plantilla:** la hoja UNIDADES ahora emite **solo las columnas de los
>   campos VISIBLES** segun `unidad_campos_config` (incluidos los dinamicos `[Label]`). Es decir, si una
>   copropiedad oculta un campo, ese campo **deja de salir en la plantilla de carga**.
> - **GUARDA:** `COPROPIEDAD`, `UNIDAD PRIVADA` y `PRINCIPAL` no tienen clave de configuracion, asi que no
>   se pueden ocultar ni por error. Si se pudieran, la plantilla quedaria inservible (las dos primeras
>   identifican la fila y `PRINCIPAL` vincula los anexos).
> - La fila de ejemplo y los dropdowns se ubican **por encabezado, no por posicion**: al filtrar columnas,
>   con el esquema posicional anterior el ejemplo se habria corrido a la columna equivocada.
> - El importador lee `MODULO CONTRIBUTIVO 1..5`; celda vacia no pisa el valor existente en una recarga.
> - **Ojo (pendiente de producto):** estos 5 campos son libres, **sin validacion de que cada modulo sume
>   100%**. El coeficiente principal si la tiene (RN-02). Ver seccion 5.
>
> **Nuevo en 0.0.88 (Carga por Excel: los campos dinamicos ya se guardan):**
> - **Bug que se corrige:** la plantilla emitia las columnas de campos dinamicos de la hoja UNIDADES
>   (rotuladas `[Label]`) pero el importador NO las leia. El administrador llenaba `[N de medidor]` para
>   200 unidades y el dato se descartaba **sin un solo error**.
> - Ahora el importador resuelve las columnas `[Label]` contra el catalogo de cada entidad y guarda el
>   valor, en las **6 hojas con catalogo** (UNIDADES, PERSONAS, VEHICULOS, MASCOTAS, ZONAS COMUNES,
>   EQUIPOS), al crear y al actualizar. La plantilla emite esas columnas en las 6 (antes solo UNIDADES).
> - Celda vacia no escribe (una recarga conserva el valor anterior). Upsert idempotente.
> - Una columna `[X]` que no corresponde a ningun campo configurado se reporta **una vez por hoja**.
> - La hoja **TERCEROS queda fuera a proposito**: carga terceros del Directorio (no crea `UnidadEmpleada`,
>   ni tiene columna UNIDAD PRIVADA), mientras el catalogo "Terceros" cuelga de `UnidadEmpleada`. Requiere
>   decision de modelo -> ver seccion 5.
> - **Solo codigo** (UnidadesPlantillaService + UnidadesCargaImportService), sin migracion.
>
> **Nuevo en 0.0.87/0.0.86 (SEGURIDAD: RLS en 18 tablas que no la tenian):**
> - Un test de cobertura nuevo (`RlsCoverageTests`) descubrio que **26 de las 230 tablas con `tenant_id`
>   no tenian RLS EN ABSOLUTO** (`relrowsecurity=false` y cero politicas), no que les faltara el FORCE.
>   El `HasQueryFilter` de EF las seguia filtrando -no habia fuga por la via normal de la app- pero les
>   faltaba la red de seguridad final: una consulta cruda sin filtro no tenia nada que la detuviera.
> - **1 migracion**: `AddRlsTablasFaltantes` protege las **18 que son TenantEntity** (contratos, directorio,
>   etiquetas de usuario, informes, seguros/polizas y las 2 configs de PQRSD). **Las mas sensibles eran
>   Directorio y Seguros.** Solo SQL, idempotente, con `Down()`.
> - **Riesgo revisado antes de aplicarla** (FORCE RLS ata tambien al dueno de la tabla: un lector sin
>   tenant de sesion pasaria de "ve todo" a "ve cero filas" en silencio): ninguna de las 18 se lee con
>   `IgnoreQueryFilters`, SQL crudo ni Dapper; ningun seeder ni controller de SuperAdmin las toca; y los
>   dos caminos que corren fuera de un request normal fijan el tenant antes de leer (formulario publico de
>   PQRSD y job de vencimientos). Suite de integracion sin regresiones.
> - **El test de cobertura se deja FALLANDO a proposito**, con la lista de exentas vacia, para que nadie
>   pueda agregar una tabla de tenant sin RLS sin romper la suite. Reporta 8 tablas pendientes -> seccion 5.
> - **Regla nueva para el futuro:** cualquier migracion que rellene datos en esas 18 debe fijar
>   `app.tenant_id` primero, o vera cero filas.
>
> **Nuevo en 0.0.85/0.0.84 (Gestor de campos: Fase 3 + listas con semilla):**
> - **Las 4 pestanas del panel "Configurar" ya son funcionales** (Personas, Vehiculos, Mascotas, Terceros):
>   catalogo de campos propios por entidad + los campos del SISTEMA de cada ficha (alias, visibilidad,
>   ubicacion y, donde la columna real lo permite, tipo y formato).
> - **2 migraciones**: `AddCamposEntidadesVinculadasUnidad` (8 tablas nuevas, cada una con RLS FORCE +
>   policy + GRANT) y `AddEntidadAUnidadCampoConfig` (`unidad_campos_config` +`entidad` varchar(20) default
>   'unidad'; el indice unico pasa a `(tenant_id, entidad, campo_clave)`).
> - **La seccion TERCEROS no existia en la ficha**: el backend estaba completo y `_empleadas` se cargaba en
>   memoria, pero no se renderizaba en ninguna parte. Se le construyo pestana y seccion.
> - **Listas del sistema:** las opciones pasan a JSON `[{K, Oculta}]` (el orden del arreglo ES el orden;
>   se sigue leyendo el formato legado de una-por-linea). Los tres estados de fabrica
>   (Habitada/Desocupada/Arrendada) ya son **semilla de verdad**: con candado, no se eliminan, solo se
>   ocultan - la misma regla que los tipos base, que ahora tambien se pueden ocultar. Las propias si se
>   renombran y se eliminan. La lista se administra en un **popup** encima del modal.
> - **Al ocultar una opcion, las unidades que ya la usan la conservan** y su selector la sigue ofreciendo
>   solo en esa fila: sin eso, ocultar "Parqueadero" habria hecho que 327 unidades mostraran otro tipo y se
>   corrompieran al primer clic. El popup muestra cuantas unidades usan cada opcion.
> - Un `tipo` guardado que no corresponde a ningun valor del enum ya no se disfraza como el primero de la
>   lista: se muestra `(sin definir: N)` -> ver seccion 5, hay 1 unidad asi en dev.
> - Alta de campos propios: se agregan Moneda, Hora, Telefono, Correo y Enlace.
>
> **Nuevo en 0.0.83 (Carga de unidades: anexos idempotentes):**
> - En la recarga de la plantilla, los vinculos de anexo de la carga previa hacian fallar cada anexo con
>   "La unidad X ya esta asociada a otra unidad" (las unidades hacen upsert y "eliminar y cargar de nuevo"
>   no borraba los vinculos). Ahora la 2a pasada de anexos es idempotente: mismo principal -> se omite;
>   distinto principal con "eliminar y cargar de nuevo" -> re-apunta; distinto sin reemplazar -> reporta.
>   **Solo codigo (UnidadesCargaImportService), sin migracion.**
>
> **Nuevo en 0.0.82 (Configurar unidades = administrador de campos):**
> - El boton "Configurar" lista TODOS los campos de la unidad: los del sistema con candado (no se
>   eliminan) + alias por campo (se aplica en encabezados de tabla y ficha) + opciones de lista para
>   Tipo y Estado (con "Restaurar semilla"); al quitar una opcion de Estado en uso se advierte con el
>   conteo. Campos personalizados se siguen creando/editando/eliminando.
> - **1 migracion nueva**: `AddUnidadCampoConfig` (tabla `unidad_campos_config` con RLS FORCE + policy +
>   GRANT) -> ver seccion 3.
>
> **Nuevo en 0.0.81 (Unidades: tipos configurables):**
> - Nuevo tipo base **"Cajeros"** (enum `TipoUnidad`).
> - El boton **"Configurar"** de Unidades Privadas ahora administra los **tipos de unidad**: los base
>   vienen como semilla (fijos, no removibles) y el tenant puede AGREGAR/QUITAR tipos propios (reusa
>   `tipos_unidad_custom` + endpoints GET/POST/DELETE `/api/mi-copropiedad/tipos-unidad`). Los selectores
>   de tipo (alta inline, edicion inline y ficha) mezclan base + propios; etiqueta/filtros/agrupacion
>   muestran el nombre propio.
> - **1 migracion nueva**: `AddUnidadTipoCustom` (`unidades` +`tipo_custom_id` uuid null) -> ver seccion 3.
>
> **Nuevo desde 0.0.74 (modulo Mantenimiento reelaborado, 0.0.75 -> 0.0.80):**
> - **Programacion (tab principal) en tabla, con alta INLINE por columna.** Una fila por
>   `ProgramacionTarea`. La fila de alta captura por columna: Tipo (Equipo/Zona), Activo (filtrado por
>   tipo), Titulo, Tercero, Contrato, Tablero, Prioridad, Periodicidad, Proxima ejecucion; "+ Agregar"
>   crea sin modal. El expansor de la fila de alta abre el formulario completo para crear.
> - **Tercero (proveedor) y Contrato en la programacion, ambos OPCIONALES.**
>   - Tercero = componente **SelectorPersona** (busca en el Directorio de la organizacion + crea
>     persona/empresa al vuelo; panel flotante). Guarda id + snapshot de nombre.
>   - Contrato = lista filtrada a los contratos del tercero elegido (match por
>     `ProveedorPersonaId`/`ProveedorEmpresaId` del contrato).
>   - Se muestran como columnas (ocultables por "Campos").
> - **Se quito el "Cron (avanzado)"** del `ProgramadorTareaModal`: la programacion es siempre por
>   Periodicidad. Afecta tambien las fichas de equipo/zona que comparten el modal.
> - **FIX flotante del SelectorPersona (CSP):** el panel de resultados se posicionaba con un helper
>   definido via `eval`, que la CSP del sitio bloquea (`script-src` sin `unsafe-eval`), dejando el panel
>   en flujo y deformando la celda. El helper `window.propiaFloatPos` se movio a `wwwroot/js/propia-ui.js`
>   (**subir `?v=` en App.razor si se toca ese archivo; ya en `?v=11`**) y `.sp-pop--float` es
>   `position:fixed`. Corrige el flotante en TODO uso del componente (tambien Contratos/Servicios).
> - **Planes preventivos** paso a vista-tabla con alta inline (0.0.75) y luego el **tab se retiro** junto
>   con **Intervenciones** (0.0.80): el modulo queda en **Programacion + Calendario**. El codigo de esos
>   bloques permanece pero sin boton (restaurable).
> - **Calendario de programaciones** con vistas **Mes** y **Semana**: pinta cada programacion en sus
>   fechas de ejecucion proyectando las recurrencias segun periodicidad (respeta FechaFin); clic en un
>   evento abre la programacion para editar.
> - **2 migraciones nuevas** (aditivas): `AddProgramacionProveedorContrato` (`programacion_tareas`
>   +`proveedor_id` +`contrato_id` uuid null) y `AddProgramacionProveedorNombre` (+`proveedor_nombre`
>   text null). RLS de la tabla ya cubre las columnas nuevas -> ver seccion 3.
>
> **Nuevo desde 0.0.73:**
> - **Mantenimiento: pestana "Programacion" en tabla (patron vista-tabla).** `/mantenimiento` abre por
>   defecto en la pestana **Programacion**: una tabla con **una fila por programacion** (`ProgramacionTarea`,
>   el "programador de tareas" que antes solo vivia dentro de las fichas de equipo/zona). Cumple el skill
>   `homogenizar-vista-tabla`: expansor en la 1a columna -> abre el `ProgramadorTareaModal` completo (editar,
>   con Titulo/Descripcion/Tablero/Prioridad/Periodicidad-Cron/Fecha/Responsables/correo); alta inline
>   (selector de activo equipos+zonas + "+ Programar" abre el modal apuntando a ese activo); Filtros
>   (`FiltroDinamico`), Buscar, Agrupar (activo/tablero/periodicidad/prioridad), Campos (visibilidad de
>   columnas); toggle Activa inline (`PUT /api/programaciones/{id}/activa`) y eliminar (`DELETE`); footer
>   con conteo visible tambien con 0 filas. Carga fusionando
>   `GET /api/programaciones?moduloOrigen=equipo|zona`. `<ModalHost />` declarado en la pagina.
>   **Solo codigo, sin migracion.** Reusa los endpoints existentes de `ProgramacionesController`;
>   no toca las fichas (`FichaEquipoModal`/`FichaZonaModal`) ni otros modulos. Verificado local
>   end-to-end (crear/editar/toggle/eliminar, agrupar, campos, filtros; registro de prueba borrado).
>
> **Nuevo desde 0.0.71:**
> - **Mantenimiento: cronograma en tabla inline.** La pestana "Activos" de `/mantenimiento` es una tabla con
>   una fila por activo (equipos + zonas unificados); la Frecuencia se programa inline (crea/actualiza/
>   desactiva el plan preventivo reutilizando `/api/mantenimiento/planes`, que genera tareas + calendario).
>   **Solo codigo, sin migracion.**
>
> **Nuevo desde 0.0.69:**
> - **Campos dinamicos tipados en Zonas Comunes y Equipos** (misma feature que Unidades): boton "Configurar"
>   por modulo (catalogo con Tipo/Opciones), campos como COLUMNAS editables inline en la tabla + input en la
>   fila de alta. **1 migracion nueva** (`AddEquipoZonaCampoDefiniciones`: 4 tablas nuevas
>   `equipo_campos_definiciones/valores` y `zona_campos_definiciones/valores`, con RLS FORCE + policy + GRANT;
>   ver seccion 3).
> - **Expansor de la fila de alta -> abre el modal de creacion** en Unidades, Zonas y Equipos (antes creaba
>   desde el draft). Solo codigo.
>
> **Nuevo desde 0.0.68:**
> - **Unidades: campos dinamicos como COLUMNAS de la tabla** (header + valor por fila + input en la fila de
>   alta), no como tira suelta. Nuevo endpoint `GET /api/mi-copropiedad/unidades-campos-valores` (bulk).
>   Se **retira "Zona comun"** del selector de tipo de unidad (el valor del enum se conserva por datos
>   historicos). **Sin migracion.**
>
> **Nuevo desde 0.0.67:**
> - **Unidades Privadas: Estado como lista + campos dinamicos tipados.** El campo Estado de la unidad pasa
>   de texto libre a **dropdown** (Habitada / Desocupada / Arrendada) en la ficha (modal). Nuevo boton
>   **"Configurar"** en la barra del modulo (junto a la plantilla) para administrar campos dinamicos con
>   TIPO (Texto / Texto largo / Numero / Fecha / Lista de opciones / Si-No) + opciones para listas. Los
>   campos dinamicos ahora salen en la **fila de alta inline** y en el **modal crear/editar**, renderizados
>   por tipo. **1 migracion nueva** (`AddUnidadCampoTipoOpciones`: `unidad_campos_definiciones` +`tipo` int
>   default 0, +`opciones` text null; aditiva/segura -> ver seccion 3). Verificado local end-to-end
>   (crear campo lista, alta inline con valor, persistencia, edicion en ficha).
>
> **Nuevo desde 0.0.66 (CORRECCION del build fix):**
> - **CRIPTOGRAFIA se queda en 9.x (0.0.66 rompio el login en prod).** El fix de 0.0.66 fijaba tambien
>   `Microsoft.AspNetCore.Cryptography.KeyDerivation` a `10.0.*`. Eso mezclo cripto 10.x con DataProtection
>   9.x en la instancia desplegada y **rompio el login** (el formulario no completaba: antiforgery/cripto).
>   Correccion (Opcion A): se retiro `KeyDerivation` del `Directory.Build.props` (solo queda plumbing
>   NO-cripto DI/Options en `10.0.*`) y se pineo la familia Identity **hacia abajo** para que la cripto se
>   quede en 9.x: `Microsoft.Extensions.Identity.Stores` y `Microsoft.AspNetCore.Identity.EntityFrameworkCore`
>   a `9.0.19`, y `Microsoft.AspNetCore.Identity` a `2.3.11` (asi nadie exige el inexistente 9.0.20 que
>   arrastraba la cripto a 10.x). **DataProtection intacto en 9.0.x** (mismo key ring; los secretos
>   Gemini/SMTP siguen descifrables). Verificado: restore fresh `-warnaserror` sin NU1603/NU1605; toda la
>   cripto resuelta en 9.x (KeyDerivation 9.0.19, DataProtection 9.0.20, cero 10.x); build OK; circuito
>   Blazor (`/_blazor/negotiate` 200) y `/connect/login` (401 con creds invalidas) OK en navegador.
>   **REGLA:** nunca subir KeyDerivation ni DataProtection a 10.x sin migrar prod deliberadamente.
> - Incluye el **hotfix 403 de OCR** (0.0.65) y el resto de 0.0.64-0.0.66. 0.0.65/0.0.66 nunca quedaron en
>   prod estables; **0.0.67 es el artefacto bueno**.
>
> **Nuevo desde 0.0.64:**
> - **HOTFIX 403 en el extractor de IA (OCR).** Mismo bug de RBAC que 0.0.64 corrigio en Seguros, pero en
>   `OcrController.cs`: el `[RequiereRol("Administrador")]` estaba a nivel de CLASE y gateaba tambien
>   `POST /api/ocr/extraer-ia` y `/api/ocr/extraer` (lectura), por lo que un usuario no-Administrador recibia
>   403 al pulsar "Cargar PDF y extraer (IA)" en Seguros/Contratos. Se retiro el gate de clase (queda
>   `[Authorize]`); `extraer-ia` y `extraer` quedan abiertos al tenant (son LECTURA: devuelven JSON de campos
>   detectados, no persisten) y `analizar` + `analizar/continuar` (agente documental que SI persiste en
>   Servicios/Cartera con dryRun=false) quedan gateados a Administrador por metodo. **Solo codigo, sin migracion.**
>   Verificado local con un usuario rol "Propietario": `extraer-ia` responde 200 (antes 403).
>
> **Nuevo desde 0.0.63:**
> - **HOTFIX (critico, ya en produccion antes de este deploy):** el modulo **Seguros** devolvia **403** al
>   solo VER la lista de polizas. El `[RequiereRol("Administrador")]` estaba a nivel de CLASE y gateaba
>   tambien los GET (polizas/campos/reclamaciones/pdf-origen). `RequiereRolFilter` hace match exacto de rol
>   SIN bypass, asi que cualquier usuario del tenant cuyo rol no sea literalmente "Administrador" recibia 403.
>   Se retiro el gate de clase (queda `[Authorize]`) y se movio `[RequiereRol("Administrador")]` a los
>   endpoints de ESCRITURA (POST/PUT/DELETE). GET abiertos al tenant (convencion RBAC). **Solo codigo.**
> - **Seguros + Contratos: cargar PDF y prellenar con IA.** El modal de Contratos (`/contratos`) ahora tiene
>   "Cargar PDF y extraer (IA)" igual que Seguros. Nuevo perfil "contrato" en `OcrController.PerfilCampos`
>   (contratista, nit, numero_contrato, objeto, valor_total, valor_mensual, contacto, fecha_inicio, fecha_fin).
>   Manda el documento nativo a Gemini (`/api/ocr/extraer-ia`) y mapea al formulario. En Seguros: mensaje
>   honesto cuando la IA no encuentra datos de poliza (antes decia "documento leido" aunque no llenara nada)
>   y mensaje amable ante un 404 de "Ver PDF origen" (poliza sin PDF cargado). **Solo codigo.**
> - **Auditoria PQRSD (Parte A/B + Parte C G-01/G-02/G-05):** alertas automaticas de plazo (job diario
>   `PqrsdAlertaPlazoJob` al 80%/vencido, notifica al responsable o admins + alerta de dashboard + historial;
>   idempotente por umbral), notificaciones al radicador/administracion en el ciclo (radicado, cambio de
>   estado, cierre, inconformidad), validaciones de adjuntos (lista blanca de formatos + 25MB + 5 por
>   expediente + magic bytes, S-09), feedback por fila en Configurar PQRSD (recarga ligera) y config en 3 tabs
>   primarios. **1 migracion nueva** (`AddPqrsdAlertaPlazoNotificada`, columna aditiva -> ver seccion 3).
>
> **Nuevo desde 0.0.62:** Super Admin - registro de ingresos por organizacion (logins exitosos y fallidos,
> con copropiedad/IP/navegador; tabla `login_audit_events`). Cuenta de correo saliente por copropiedad
> (SMTP/Gmail con contrasena de aplicacion cifrada; tabla `tenant_email_configs`; tab "Correo" en Configurar
> PQRSD con envio de prueba). PQRSD respuesta: modal en 3 columnas (contactos | documento | plantillas),
> gestion de plantillas movida a Configurar PQRSD > Plantillas, modal mas ancho y editor mas alto, chat de
> Actividad a alto completo (compositor siempre visible). Adjuntos PQRSD: previsualizacion inline de PDF
> (S-09 ahora sirve application/pdf inline, seguro) + tarjetas con trazabilidad. Configurar PQRSD reorganizado
> en 3 tabs primarios (Flujo / Radicacion / Respuestas). **2 migraciones nuevas** (`login_audit_events`,
> `tenant_email_configs`) -> aditivas/seguras (ver seccion 3).
>
> **Nuevo desde 0.0.61:** PQRSD respuestas al ciudadano (consecutivo de radicado de salida por respuesta;
> canal por destinatario correo/WhatsApp; envio por plantilla de WhatsApp con link compartido enriquecido;
> consecutivos de expediente y de respuesta configurables). PQRSD cierre con motivo al mover a columna
> terminal (pide motivo -> define estado legal -> archiva). Tareas dentro de PQRSD ahora reusa el modulo
> Tareas real (componente `Shared/Tareas/TableroTareas.razor`, hospedado en el modal). Fix OCR IA (Gemini
> se habilita sin endpoint; modelo default `gemini-3.6-flash`). Fix auto-descarga de PDF al abrir el modal
> PQRSD. **3 migraciones nuevas** (ver seccion 3) -> todas aditivas/seguras.
>
> **Nuevo desde 0.0.60:** Planes (limite de copropiedades por organizacion, default 1; plan promocional
> no facturable y no cambiable directamente), extraccion de documentos con IA (Gemini como proveedor de OCR),
> y PQRSD campos dinamicos (autoguardado con coalescing, sin marco, 3 anchos configurables, VENCIDO con
> colores semaforo). El bloque PQRSD es **solo codigo** (reusa `pqrsd_campos.columna`, sin migracion nueva).

## 1. Variables de entorno OBLIGATORIAS en produccion

Sin estas, el arranque falla o se rompen integraciones (fail-closed, por diseno):

| Variable | Motivo | Si falta |
|---|---|---|
| `Jwt__SigningKey` | Clave de firma JWT real (>=32 chars, NO la de dev) | **El arranque falla** (S-15) |
| `Propia__WebhookToken` | Token del webhook Evolution (WhatsApp) | Webhooks 401; WhatsApp deja de entrar (S-07) |
| `Meta__AppSecret` | HMAC de los webhooks de Meta | Webhooks Meta 401 en prod (S-07) |
| `Storage__Provider=R2` + credenciales R2 (`R2__*`) | Storage de blobs en prod | Imagenes no se guardan/sirven |
| `ConnectionStrings__*` | Postgres de prod (owner + app con RLS) | No arranca |

## 2. Variables de entorno RECOMENDADAS (endurecimiento)

| Variable | Motivo |
|---|---|
| `Metrics__ScrapeToken` | Protege `/metrics` (S-17). Si se define, el scrape debe mandar `X-Metrics-Token` o `Authorization: Bearer`. |
| `ForwardedHeaders__KnownNetworks__0` | Rango/CIDR del proxy de Railway para que el rate limiter use la IP real (S-12). Formato CIDR, p.ej. `100.64.0.0/10`. |
| `RateLimit__AuthPermitPerMinute` | Ajuste del limite de login por IP (default 15/min). |
| `Onboarding__PurgaNoConfirmadosHoras` | Ventana de purga de registros no confirmados (default 48h, S-04b). |

> En prod `ForwardedHeaders:Enabled` es `true` por defecto (no es Development). CSP y nosniff van siempre.

## 3. Migraciones a aplicar en prod

Aplicar todas las pendientes (incluye la nueva de esta tanda):

```bash
cd src/Propia.Api
dotnet ef database update --project ../Propia.Infrastructure --startup-project .
```

Ultimas migraciones del repo (verificar que esten aplicadas). `ef database update` aplica SOLO las que
falten en ese entorno, comparando contra `__EFMigrationsHistory`:
- `20260910202226_AddModulosContributivosUnidad`  (Unidades: `unidades_privadas` +`modulo_contributivo_1..5` numeric(7,4) NULL. 5 AddColumn y nada mas)  <-- NUEVA (0.0.89)
- `20260910140101_AddRlsTablasFaltantes`  (**SEGURIDAD**: activa RLS -ENABLE + FORCE + policy `tenant_isolation` + GRANT `propia_app`- en 18 tablas de tenant que no tenian ninguna: contrato_campos, contrato_campo_valores, contrato_etapas, contrato_expedientes, directorio_contactos, directorio_adjuntos, etiquetas_usuario, usuario_tenant_etiquetas, informes, informe_secciones, informe_plantillas, informe_plantilla_secciones, polizas, poliza_campos, poliza_campo_valores, poliza_reclamaciones, pqrsd_formulario_publico_configs, pqrsd_tareas_configs. **Solo SQL, sin cambios de esquema.** Idempotente. Ver la nota de riesgo del encabezado)  <-- NUEVA (0.0.87)
- `20260910132959_AddEntidadAUnidadCampoConfig`  (`unidad_campos_config` +`entidad` varchar(20) NOT NULL default `'unidad'`; DROP del unico `(tenant_id, campo_clave)` y CREATE de `(tenant_id, entidad, campo_clave)`. Las filas existentes quedan con `'unidad'`)  <-- NUEVA (0.0.85)
- `20260910120849_AddCamposEntidadesVinculadasUnidad`  (8 tablas nuevas de catalogo de campos para las entidades vinculadas a la unidad: `persona_campos_definiciones`/`_valores`, `vehiculo_*`, `mascota_*`, `tercero_*`. **Cada una con RLS FORCE + policy tenant_isolation + GRANT propia_app en la propia migracion**)  <-- NUEVA (0.0.84)
- `20260910005638_AddUnidadCampoConfig`  (Unidades: tabla nueva `unidad_campos_config` -alias + opciones de campos fijos como Estado-, con RLS FORCE + policy tenant + GRANT propia_app)  <-- NUEVA (0.0.82)
- `20260909222434_AddUnidadTipoCustom`  (Unidades: `unidades_privadas` +`tipo_custom_id` uuid null; tipo de unidad propio del tenant)  <-- NUEVA (0.0.81)
- `20260909203008_AddProgramacionProveedorNombre`  (Programacion: `programacion_tareas` +`proveedor_nombre` text null; snapshot del nombre del tercero)  <-- NUEVA (0.0.78)
- `20260909195925_AddProgramacionProveedorContrato`  (Programacion: `programacion_tareas` +`proveedor_id` uuid null, +`contrato_id` uuid null; tercero + contrato opcionales)  <-- NUEVA (0.0.77)
- `20260909013856_AddEquipoZonaCampoDefiniciones`  (Zonas/Equipos: 4 tablas nuevas `equipo_campos_definiciones`, `equipo_campos_valores`, `zona_campos_definiciones`, `zona_campos_valores`, con RLS FORCE + policy tenant + GRANT propia_app; campos dinamicos tipados)  <-- NUEVA (0.0.71)
- `20260908220459_AddUnidadCampoTipoOpciones`  (Unidades: `unidad_campos_definiciones` +`tipo` int default 0, +`opciones` text null; campos dinamicos tipados)  <-- NUEVA (0.0.68)
- `20260908120902_AddPqrsdAlertaPlazoNotificada`  (PQRSD: `pqrsd_expedientes` +`alerta_plazo_notificada` int null; idempotencia de las alertas de plazo del job diario)  <-- NUEVA (0.0.64)
- `20260908023154_AddTenantEmailConfig`  (Correo: tabla nueva `tenant_email_configs` con RLS; SMTP host/puerto/usuario/from + clave de aplicacion cifrada)  <-- NUEVA (0.0.63)
- `20260908021743_AddLoginAuditEvents`  (Super Admin: tabla nueva `login_audit_events`, GLOBAL sin RLS; ingresos exitosos y fallidos)  <-- NUEVA (0.0.63)
- `20260907154737_PqrsdDestinatarioCanalesFlags`  (PQRSD: `pqrsd_respuesta_destinatarios` -`canal`, +`enviar_correo` bool, +`enviar_whats_app` bool)  <-- (0.0.62)
- `20260907151436_AddPqrsdCanalYWhatsAppConfig`  (PQRSD: tabla nueva `pqrsd_whatsapp_configs` con RLS; `pqrsd_respuesta_destinatarios` +`canal` int (luego retirada), +`telefono` varchar(30))  <-- (0.0.62)
- `20260907143804_AddPqrsdConsecutivos`  (PQRSD: tabla nueva `pqrsd_consecutivo_configs` con RLS; `pqrsd_respuestas` +`numero_radicado` varchar(40) null)  <-- (0.0.62)
- `20260905150507_AddPlanLimiteCopropiedades`  (Planes: `planes` +`limite_copropiedades` int null, +`es_promocional` bool)
- `20260905143647_AddPolizaPdfOrigen`  (Seguros: `polizas` +`pdf_origen_key` text null)
- `20260905135921_AddDocumentExtractionLog`  (IA: tabla nueva `document_extraction_logs`, global sin RLS)
- `20260904173502_AddSuperAdminLockout`  (S-03b: lockout de SuperAdmin)
- `20260903210728_V01PanelSnapshotSinRlsMasUnidades`
- `20260903171507_AddTenantLinkPago`
- `20260903154017_S02UniquePersonaIdEnUsuarios`

> Todas son **seguras** (aditivas). Las nuevas de 0.0.63: `tenant_email_configs` (TenantEntity, con RLS +
> GRANT a `propia_app` en la propia migracion) y `login_audit_events` (GLOBAL, sin RLS, como
> `document_extraction_logs` - la lee el Super Admin). Las de 0.0.62: dos tablas nuevas
> (`pqrsd_consecutivo_configs`, `pqrsd_whatsapp_configs`, ambas con RLS) + columnas; la columna `canal` la
> agrega `151436` y la retira `154737` en la MISMA tanda (transitoria, sin perdida de datos), y
> `enviar_correo`/`enviar_whats_app` entran con `default false`. Aplicar en orden (lo hace `ef database update`).
> El increment de campos dinamicos PQRSD (0.0.61) no agregaba migracion (usa `pqrsd_campos.columna`).

## 4. Post-deploy (verificacion)

- [ ] Login OK; el footer muestra `v0.0.92`.
- [ ] **Si algo de "Configurar" no responde:** el panel muestra un aviso rojo que dice la causa.
      Un **500** con "column ... does not exist" = faltan migraciones (seccion 3). Un **403** = permisos:
      el mensaje distingue si es el rol, el permiso, o que el usuario no esta vinculado a esa
      copropiedad. Probar con un usuario con rol EXACTAMENTE `Administrador` en esa copropiedad.
- [ ] **Modulos contributivos:** en Unidades > Configurar aparecen "Modulo Contributivo 1..5" debajo de
      Coeficiente y **ocultos**; al activar uno sale como columna y guarda decimales (ej. 12.3456).
- [ ] **Plantilla filtrada:** con un modulo activo, la plantilla trae SOLO ese (no los otros 4); ocultar
      un campo (ej. Matricula) lo saca de la plantilla, y la fila de EJEMPLO sigue alineada con sus
      encabezados. `COPROPIEDAD`, `UNIDAD PRIVADA` y `PRINCIPAL` deben salir SIEMPRE.
- [ ] **RLS (critico de esta tanda):** despues de aplicar las migraciones, entrar a **Directorio**,
      **Seguros** y **Contratos** y confirmar que **siguen listando datos**. Si alguno queda vacio, es que
      un lector corre sin tenant de sesion y el FORCE RLS lo dejo en cero filas: revisar logs por
      `row-level security` / `42501` y revertir con el `Down()` de `AddRlsTablasFaltantes` si hace falta.
      (En dev quedo verificado: Directorio 200 filas, Seguros 7, Contratos 21, cero errores en logs.)
- [ ] **RLS (comprobacion de catalogo):** las 18 tablas quedan con `relrowsecurity` y
      `relforcerowsecurity` en `true` y >=1 politica. En toda la BD deben quedar solo **8** tablas de
      tenant sin RLS (las de la seccion 5).
- [ ] **Configurar > pestanas Personas / Vehiculos / Mascotas / Terceros:** cada una lista sus campos del
      sistema; crear un campo propio (ej. "Numero de chip" en Mascotas) y comprobar que aparece en esa
      seccion de la **ficha** de la unidad y que guarda su valor. La ficha debe tener pestana **Terceros**.
- [ ] **Listas (popup):** "Gestionar lista" abre un popup ENCIMA del modal (no expande la grilla). Los
      tres estados de fabrica y los tipos base salen con candado y **sin boton de eliminar**, solo el ojo
      para ocultar. Ocultar una opcion la saca de los selectores, y una unidad que ya la usaba la
      **sigue mostrando en su fila**. El popup muestra el conteo de unidades por opcion.
- [ ] **Carga por Excel con campos dinamicos:** con al menos un campo propio configurado, descargar la
      plantilla y verificar que la hoja correspondiente trae la columna `[Nombre del campo]`; llenarla,
      cargar el archivo y confirmar **en la ficha** que el valor quedo guardado. Una columna `[X]`
      inventada debe producir **un** aviso por hoja, no uno por fila.
- [ ] **Unidades > Configurar (campos):** lista TODOS los campos con candado en los del sistema; un alias
      (ej. Coeficiente -> "Coef. Prop.") cambia el encabezado de la tabla y la ficha; Tipo y Estado tienen
      "Opciones" (Tipo: base + propios con Cajeros; Estado: editar + "Restaurar semilla"); quitar un Estado
      en uso advierte con el conteo. Un tipo propio se puede asignar a una unidad y se muestra su nombre.
- [ ] **Carga de unidades (recarga):** volver a cargar la MISMA plantilla (con o sin "eliminar y cargar de
      nuevo") NO debe reportar "La unidad X ya esta asociada a otra unidad" para anexos ya vinculados al
      mismo principal (se omiten). Con "eliminar y cargar de nuevo" un anexo con principal distinto se
      re-apunta.
- [ ] **Mantenimiento > Programacion:** `/mantenimiento` abre en "Programacion" (tabla) y solo hay 2 tabs:
      Programacion y Calendario (sin Intervenciones ni Planes preventivos; sin botones de header). En la
      fila de alta INLINE: elegir Tipo (Equipo/Zona) filtra el Activo; capturar Titulo; el Tercero busca en
      el Directorio y permite crear persona/empresa (su panel FLOTA sin deformar la celda); el Contrato se
      filtra a los del tercero elegido; "+ Agregar" crea sin modal. El expansor de la fila de alta abre el
      formulario completo. El expansor de una fila existente reabre para editar. Toggle Activa, eliminar,
      Filtros, Agrupar y Campos funcionan; footer "Mostrando N de N" visible con 0 filas.
- [ ] **Mantenimiento > Calendario:** vistas **Mes** y **Semana**; cada programacion se pinta en sus fechas
      de ejecucion con recurrencia por periodicidad; clic en un evento abre la programacion para editar.
- [ ] **SelectorPersona (float):** al buscar un tercero en la fila de alta, el panel de resultados FLOTA
      anclado al input (no empuja la fila). Verifica que `js/propia-ui.js` cargue `?v=11` (define
      `window.propiaFloatPos`); la CSP del sitio NO permite `unsafe-eval`, por eso el helper vive en ese JS.
- [ ] **Extractor IA (hotfix 403):** un usuario NO-Administrador pulsa "Cargar PDF y extraer (IA)" en
      Seguros y en Contratos -> prellena (antes -> "forbidden"/403). El agente documental de Servicios
      Publicos ("Analizar con el Agente Documental") sigue exigiendo Administrador.
- [ ] **Seguros (hotfix 403):** un usuario del tenant que NO sea Administrador entra a Juridico > Seguros y
      VE la lista de polizas (antes -> 403). Crear/editar/eliminar una poliza sigue exigiendo Administrador.
- [ ] **Contratos IA:** Juridico > Contratos > "Nuevo contrato" > "Cargar PDF y extraer (IA)" con un PDF de
      contrato -> prellena contratista/NIT/valor/vigencia/objeto y muestra "N campos prellenados". Con un
      documento que no es contrato -> mensaje honesto de que no encontro datos.
- [ ] **Seguros IA:** "Cargar PDF y extraer (IA)" con una poliza -> prellena; con otro documento -> aviso
      claro (no el optimista de antes). "Ver PDF origen" sin archivo -> mensaje amable (no el 404 crudo).
- [ ] **PQRSD alertas de plazo:** el job `PqrsdAlertaPlazoJob` corre (ver `job_ejecuciones`); un expediente
      al 80%/vencido genera alerta de dashboard + notificacion al responsable/admins + entrada de historial,
      sin re-alertar en la siguiente corrida (columna `alerta_plazo_notificada`).
- [ ] Super Admin > Organizaciones > "Ingresos": lista los logins (exitosos y fallidos) de la organizacion.
      Un login fallido y uno exitoso quedan registrados con copropiedad/IP.
- [ ] Configurar PQRSD > Correo: guardar una cuenta SMTP (Gmail app-password) y "Probar" envia un correo real.
      La clave queda cifrada (no se ve). Configurar PQRSD se ve en 3 tabs (Flujo/Radicacion/Respuestas).
- [ ] PQRSD adjuntos: al seleccionar un PDF se previsualiza inline en el visor (no descarga); tarjetas con
      trazabilidad (subido por / fecha / tipo). Un archivo NO-imagen/NO-pdf (docx) sigue descargando.
- [ ] PQRSD respuesta: modal en 3 columnas (contactos | documento | plantillas); las plantillas se crean en
      Configurar PQRSD > Plantillas. Chat de Actividad: el compositor queda siempre visible (no requiere scroll).
- [ ] Planes: crear una 2a copropiedad con plan que permite 1 -> bloqueo con mensaje; un plan promocional
      no se puede cambiar de plan directamente.
- [ ] PQRSD: editar un campo dinamico -> se guarda solo (sin boton); 3 anchos alinean en linea; VENCIDO en rojo.
- [ ] PQRSD: mover un expediente a la columna "Cerrada" (chip del modal o drag en el tablero) -> pide motivo
      de cierre -> al confirmar queda Cerrada + archivada (sale del tablero activo, pasa a "Cerrados").
- [ ] PQRSD: la pestana "Tareas" del modal muestra el tablero real (crear/mover/editar/subtarea) y esas
      tareas aparecen en /tareas (tablero "PQRSD"). Configurar consecutivos de expediente y de respuesta.
- [ ] `/health` = 200; `/metrics` exige token (si se configuro).
- [ ] Webhooks Evolution/Meta responden (no 401) con los secretos puestos.
- [ ] Cabeceras de seguridad presentes (`Content-Security-Policy`, `X-Content-Type-Options`,
      `X-Frame-Options`) en la Web.
- [ ] Un rol no-admin recibe 403 en una escritura gateada; Administrador no (RBAC S-06).
- [ ] Subida de imagen valida (logo/fachada) OK; un archivo no-imagen se rechaza (magic bytes, S-09).

## 5. Pendientes NO bloqueantes (post-deploy)

- **Modulos contributivos sin validacion de suma:** los 5 campos nuevos son libres. El coeficiente
  principal si valida (RN-02: la suma de cada tipo debe dar 100%). Si un modulo debe cuadrar al 100% como
  exige la Ley 675, hay que agregar esa validacion. Nota: el dominio YA tiene `TipoCoeficiente` +
  `UnidadCoeficiente` con esa regla implementada y endpoints (`tipos-coeficiente`,
  `unidades/{id}/coeficientes`), pero **sin ninguna UI que los consuma**. Decidir si los modulos deben
  migrar a ese modelo (permite N modulos y valida la suma) o quedarse como columnas planas.
- **RLS-08 (decision de producto, no plomeria):** quedan **8 tablas de tenant sin RLS** y el test
  `RlsCoverageTests.Toda_tabla_con_tenant_id_tiene_RLS_FORCE_y_politica` **falla a proposito** listandolas.
  No se les puede aplicar el patron tal cual:
  - 6 tienen `tenant_id` **NULLABLE**, asi que una politica `tenant_id = current_tenant_id()` les ocultaria
    las filas de tenant nulo: `calendario_eventos`, `notificaciones`, `login_audit_events`, `sistema_logs`,
    `usuario_sesiones`, `document_extraction_logs`.
  - 2 son de Capa 1 y cruzan copropiedades por diseno: `org_colaborador_copropiedades`,
    `panel_snapshot_copropiedades`.
  Hay que decidir por cada una: politica que tolere `NULL`, o exencion **documentada** en la lista
  `TablasExentas` del test con su justificacion. Mientras no se decida, la suite queda con ese fallo.
- **TERCEROS: dos conceptos con el mismo nombre.** El catalogo de la pestana "Terceros" cuelga de
  `UnidadEmpleada` (empleadas de servicio **de una unidad**), pero la hoja TERCEROS de la plantilla carga
  terceros del **Directorio** (crea Empresa/Persona global + vinculo, sin columna UNIDAD PRIVADA). Por eso
  esa hoja quedo sin columnas dinamicas. Decidir: renombrar la pestana a "Empleadas/Personal" y darle al
  Directorio su propio catalogo, o que la hoja TERCEROS cree `UnidadEmpleada`.
- **Campos de sistema de Personas incompletos en el panel:** la hoja PERSONAS de la plantilla tiene `SEXO`,
  `FECHA NACIMIENTO` y `PROFESION`, que no aparecen en la pestana de configuracion porque viven en la tabla
  global `personas`, no en `UnidadPersona`. Es un limite defendible; si se quieren configurar, hay que
  ampliar el catalogo.
- **Visibilidad/orden de los campos de SISTEMA de las fichas vinculadas:** se guardan pero todavia **no
  cambian el render de la ficha** (hay que volver dinamica la grilla de columnas fijas de Personas y
  recomponer las tarjetas de Vehiculos/Mascotas/Terceros). Los campos **propios** si se ordenan y ocultan.
- **Separador de miles en celdas editables:** el formato se guarda y se aplican decimales y maximo de
  caracteres, pero el separador de miles NO se pinta dentro de un input editable (reparsear "1.234" es
  donde se corrompen coeficientes y cuotas). Si se quiere, la forma segura es formatear al perder el foco.
- **Dato malo en dev:** 1 unidad (`202` de "Conjunto Altos del Bosque") tiene `tipo = 0`, que no existe en
  el enum `TipoUnidad` (empieza en 1). Antes se mostraba como "Apartamento" y cualquier edicion de esa fila
  lo habria guardado asi; ahora se ve `(sin definir: 0)`. Hay que decidir a que tipo corregirla.
  Vale la pena revisar en **prod** si hay filas con `tipo` fuera de 1..20.
- **Bug preexistente en `ContratosVencimientoJob`** (hay tarea abierta): el `continue` de la linea 45
  (`if (contratos.Count == 0) continue;`) salta al siguiente tenant, asi que el bloque de alertas de
  **polizas** de la linea 83 nunca corre para una copropiedad sin contratos con fecha fin: esa copropiedad
  **no recibe alertas de vencimiento de seguros**. Ademas la linea 115 (`catch { errores++; }`) se traga
  toda excepcion por tenant sin log.
- **R-03** (rendimiento estructural): partir `@code` de TareasKanban (5443), Servicios (3223),
  GestionarUnidadesModal (2110), GestionarPqrsdModal (1897) a <800 lineas extrayendo subcomponentes
  (patron TareaCard), uno a uno con verificacion. No bloquea el deploy.
- **V-07 prod**: normalizar el catalogo de plantillas de agentes IA en PROD (en dev ya esta).
- **CI**: confirmar `SeguridadAuthFlowTests` verde en un runner sin carga (el fix del lockout ya esta
  aplicado; el runner local estaba saturado).
- **S-09 opcional**: bucket R2 privado + URLs prefirmadas para imagenes de marca/fotos (baja sensibilidad;
  los documentos sensibles ya se sirven gateados).
- **S-19 opcional**: mover el JWT de localStorage a cookie HttpOnly (mitigado hoy por CSP + sanitizacion).
