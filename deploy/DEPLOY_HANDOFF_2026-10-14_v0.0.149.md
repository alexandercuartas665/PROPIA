# DEPLOY HANDOFF - PROPIA v0.0.149 (2026-10-14)

> **LEEME PRIMERO.** Handoff autocontenido. **Solo codigo (sin migracion, sin config).** Rediseno del modal
> "Editar usuario" al canon de la maqueta, con celular/cargo/estado editables.

## Resumen de 1 linea
El modal "Editar usuario" se rehace a 2 columnas como la maqueta: identidad (solo lectura) + Celular editable +
Rol/Cargo multi a la izquierda; Estado + Datos del sistema + Persona unica + Zona de acceso a la derecha.
Se retiran del modal Gobierno/Tableros/Etiquetas. **Sin migracion.**

## Que desplegar
- Rama: `main`, HEAD **`ab92c710`**.
- Version visible: **0.0.149** (`src/Propia.Web/Propia.Web.csproj <Version>`).
- Repo: https://github.com/alexandercuartas665/PROPIA

## Migraciones / BD  (NO, no aplicar)
**Ninguna migracion.** Cargo multi ya tenia su tabla (`usuario_tenant_cargos`); el celular vive en
`personas.telefono`; la aceptacion de datos ya existe en `personas`. De 0.0.148 a 0.0.149 no hay cambios de esquema.

## Config / env / secrets
**Sin cambios.**

## Cambios de codigo (1 commit, 8 archivos, +192/-534)
Commit `ab92c710` - "Modal Editar usuario: rediseno al canon de la maqueta".

**UI** (`UsuariosPanel.razor`, `usuarios.css`, `App.razor`):
- Modal reescrito a 2 columnas (clases `usr-*`, calca de la maqueta `modal-usuario.html`):
  - Izquierda: avatar+foto, Nombre/Documento/Correo (solo lectura), **Celular (WhatsApp) editable**,
    **Rol** (multi, chips + disponibles) y **Cargo** (multi, nuevo, chips + disponibles del catalogo "usuario.cargo").
  - Derecha: **Estado** (select), **Datos del sistema** (Ultimo acceso, Fecha de vinculacion, Aceptacion de
    politica de datos - solo lectura), callout **Persona unica**, **Zona de acceso** (Revocar).
- Se RETIRAN del modal: Gobierno/Comites/Revisor/Equipo, Tableros de trabajo, Etiquetas y el box de Directorio
  (los endpoints de backend siguen existiendo; solo se quito su UI de este modal). Se elimino el codigo muerto
  asociado (handlers + estado) -> neto -342 lineas.
- CSS nuevo `.usr-sysrow` (filas label/valor de "Datos del sistema"); `usuarios.css` a `?v=22` en `App.razor`.

**Backend** (`UsuariosAccesosController.cs`, `UsuariosService.cs`, `IUsuariosService.cs`, `Dtos.cs`):
- Nuevo **`PUT /api/usuarios/{id}/telefono`** (`ActualizarTelefonoUsuarioRequest`): actualiza `Persona.Telefono`
  resolviendo la Persona desde el UsuarioTenant (el resto de la identidad sigue solo-lectura, se edita en Directorio).
- `UsuarioListaDto` ampliado **aditivamente** con `AceptoTratamientoDatos` + `FechaAceptacionDatos` (de `Persona`),
  proyectados en la query de lista. Guardar persiste celular + roles + cargos + estado.
- Estado: solo Activo<->Inactivo (reusa `POST .../revocar` y `POST .../reactivar`); "Pendiente/Pendiente de
  contacto" los fija el sistema (no se eligen a mano).

## Pasos de deploy
1. `git fetch && git checkout main && git pull` (quedar en `ab92c710` o posterior).
2. **No hay migracion que aplicar.**
3. Build/publish Release del artefacto y desplegar.
4. Verificar version en el pie/login: **v0.0.149**.

## Verificacion post-deploy
- **Usuarios** (`/usuarios`): abrir una ficha -> el modal es de 2 columnas como la maqueta.
  - Editar **Celular** -> Guardar -> reabrir: el numero persiste (queda en `personas.telefono`).
  - Agregar/quitar **Cargo** (chips) -> Guardar: persiste en `usuario_tenant_cargos`.
  - **Estado** -> Inactivo = revoca (sale de Activos); Activo (desde Inactivos) = reactiva.
  - Datos del sistema muestran Ultimo acceso / Fecha de vinculacion / Aceptacion de datos.
  - Ya NO aparecen Gobierno/Tableros/Etiquetas/box de Directorio en el modal.

## Rollback
Redeploy del artefacto 0.0.148. **No requiere tocar la BD** (sin migracion en esta tanda).

---

## Addendum v0.0.150 (2026-10-14) - fix expander del alta inline
El expander de la fila de alta ("Crear y abrir la ficha") estaba `disabled` hasta tener nombre+documento+rol,
por lo que al hacer clic con datos incompletos no pasaba nada (boton mudo, sin aviso). Ahora va SIEMPRE
habilitado (canon skill vista-tabla seccion 8): al clic valida los minimos y, si faltan, muestra el toast
"Para crear y abrir la ficha: nombre completo, documento y al menos un rol"; si estan completos, crea el
usuario (Invitacion pendiente) y abre su ficha. Archivo: `UsuariosTablaVista.razor`. Sin migracion.
Verificado en runtime (toast cuando faltan datos; modal abre al completar). Version visible **0.0.150**.

---

## Addendum v0.0.151 (2026-10-14) - modal de CREACION de usuario desde el expander del alta
Pedido de Alex: "apenas llene la primera columna (documento) que el expander deje abrir el modal para crear".
Ahora el expander de la fila de alta ABRE un modal **"Crear usuario"** (nuevo, distinto del editar) precargado
con lo que haya en la fila -aunque sea solo el documento-; alli la identidad es EDITABLE (nombre/documento/
correo), se eligen Rol (requerido) y Cargo (chips), y "Crear usuario" valida los minimos (documento + nombre
completo + >=1 rol) y reusa el flujo de alta: crea la persona en el Directorio si no existe, crea el UsuarioTenant
en Invitacion pendiente, persiste celular, y ABRE la ficha del creado para continuar. El modal de EDITAR (usuarios
existentes) sigue con identidad de solo lectura. Archivos: `UsuariosPanel.razor`, `UsuariosTablaVista.razor`.
Sin migracion. Verificado en runtime (documento solo -> modal crear -> completar -> crea + abre ficha; split de
nombre OK). Version visible **0.0.151**.

---

## Addendum v0.0.152 (2026-10-14) - UN SOLO modal para crear y editar
Alex: "debe ser el mismo modal". El modal separado de creacion se FUSIONO con el de editar: ahora es un unico
bloque (`@if (_editUser is not null || _creando)`) que en modo creacion (`_creando`) muestra la identidad
EDITABLE (nombre/documento/correo), Estado fijo "Invitacion pendiente" y boton "Crear usuario"; y en modo
edicion (usuario existente) la identidad es de solo lectura, con Estado (select), Datos del sistema y Zona de
acceso, y boton "Guardar cambios". Al crear, el mismo modal pasa a modo edicion del recien creado.
Archivo: `UsuariosPanel.razor`. Sin migracion. Verificado en runtime (crear -> edita el creado; editar existente
sin cambios). Version visible **0.0.152**.

---

## Addendum v0.0.153 (2026-10-14) - Directorio/Terceros: el expander del alta abre el modal de creacion
Alex: en Terceros/Directorio, al llenar el primer campo y tocar el expander, no abria nada. Se REUTILIZO el modal
de creacion existente (el del boton "Nuevo", `_showCrear`): ahora el expander de la fila de alta (Personas y
Empresas) va SIEMPRE habilitado y ABRE ese modal precargado con lo que haya en la fila. Para Persona, si hay
documento auto-ejecuta la busqueda por documento (dedup) y muestra el formulario; si no, abre en la fase de
busqueda. Para Empresa, abre con NIT/razon social precargados. Handlers nuevos `AbrirCrearDesdePersona`/
`AbrirCrearDesdeEmpresa` (reemplazan `CrearPersonaTablaYAbrir`/`CrearEmpresaTablaYAbrir`); expanders siempre
habilitados (quitado el gate por `DraftValido`). Archivos: `DirectorioPanel.razor`, `DirectorioPersonasVista.razor`,
`DirectorioEmpresasVista.razor`. Sin migracion. Verificado en runtime (Personas y Empresas abren el modal con el
primer campo). Version visible **0.0.153**.

---

## Addendum v0.0.154 (2026-10-14) - Residentes: el expander (alta y edicion) de un tercero EMPRESA abre ficha en modal, no navega
Alex: en Residentes el expander "no abre el modal y salta a una pagina". Causa: para un residente que es
EMPRESA/tercero, tanto el expander del alta (`AgregarAsync(true)`) como el de la fila existente (`AbrirFicha`)
hacian `Nav.NavigateTo("/directorio/empresas/{id}")` -> saltaban al modulo Directorio ("un modulo extrano").
El caso PERSONA ya abria su ficha en modal (`FichaResidenteModal`); solo el caso empresa navegaba. Fix: nuevo
componente **`FichaEmpresaModal.razor`** (ficha rapida de la empresa: razon social, nombre comercial, correo,
telefono, direccion, sitio web + documentos RUT/camara, reutilizando `/api/directorio/empresas/{id}` GET/PUT y
`/api/directorio/adjuntos/Empresa/{id}`), y en `ResidentesPanel.razor` ambos expanders ahora llaman a
`AbrirFichaEmpresaAsync` (abre el modal en sitio, 640px) en vez de navegar. El PUT preserva TipoEmpresa/Sector/
Regimen/Logo/RepresentanteLegal (campos que la ficha rapida no edita). Archivos: `FichaEmpresaModal.razor` (nuevo),
`ResidentesPanel.razor`. Sin migracion. Verificado en runtime (claude-in-chrome) en los 3 caminos: alta persona ->
FichaResidenteModal; alta empresa -> FichaEmpresaModal (NIT 12914665, sin salir de /residentes); edicion empresa
existente (A-1223) -> FichaEmpresaModal. Datos de prueba creados y borrados (total 595, empresas 1). Version visible
**0.0.154**.

---

## Addendum v0.0.155 (2026-10-14) - FichaResidenteModal: rediseno al frame estandar de ficha + asignar rol (invitar como usuario)
Pedido de Alex (maqueta `D:\DesarrolloIA\Maqueta\modal-residente.html`): el modal de residente al frame estandar
rico (como Tarea/PQRSD/Contrato) MAS poder asignar el rol de acceso. **Ola 1+2 de 3** (falta COMENTARIOS, Ola 3).

**UI - FichaResidenteModal.razor (reescrito):** del quick-ficha de 720px al frame estandar `is-fixedh`
(1056px x 86vh fijo, radio 12): cabecera con navegacion ‹ › entre registros (recorre las personas visibles),
**titulo editable** (nombre; reparte en nombres/apellidos), subtitulo "relacion · CC doc", acciones de cabecera
(copiar enlace al registro, menu ···) y cerrar. Cuerpo en 2 columnas (calca `.fr-*`): izquierda foto o avatar de
iniciales + "Cambiar foto" + chips (relacion + rol); derecha DATOS en grilla (Nombres, Apellidos, Correo, Telefono,
Fecha de nacimiento, Genero, Documento [solo lectura], Unidad [solo lectura]) + DOCUMENTOS (adjuntos de persona).
Pie Cerrar / Guardar cambios. Medido lado-a-lado vs la maqueta servida por HTTP (shell 1056 x 86vh radio 12,
cabecera ~81px, cuerpo 2col 4fr/8fr gap 18, avatar 150) con getComputedStyle; gap de line-height corregido.

**Nuevo - seccion "ACCESO AL SISTEMA (ROL)":** multi-seleccion (chips asignados con x + roles disponibles "clic
para asignar"), patron del modal de Usuario. Al **Guardar**, el rol se persiste: si la persona ya es usuario del
tenant se **actualizan sus roles** (`PUT /api/usuarios/{id}/roles`); si no, se **invita como usuario** (alta en
Invitacion pendiente con el/los rol(es): `POST /api/usuarios/alta`). El rol otorga acceso (p. ej. Portal Residente);
el detalle de permisos se edita en la bandeja Roles y permisos.

**Infra de modales:** `ModalOptions`/`ModalReference`/`ModalService`/`ModalHost` ganan `CssClass` para aplicar la
clase `is-fixedh` del frame estandar (radio 12 + alto fijo 880). Reutilizable por otras fichas.

**Backend nuevo (sin migracion):** `GET /api/usuarios/por-persona/{personaId}` -> `UsuarioPorPersonaDto`
(usuarioTenantId + estado + rolIds) o 204. Necesario porque la bandeja `/api/usuarios` **oculta** los usuarios
"solo directorio" (residentes con acceso: filtra por `RolesSemillaTenant.SoloDirectorio`), asi que no sirve para
detectar si un residente ya es usuario; se resuelve por persona (respeta el filtro de tenant). Archivos backend:
`UsuariosAccesos/Dtos.cs`, `IUsuariosService.cs`, `UsuariosService.cs`, `Api/Controllers/UsuariosAccesosController.cs`.

**Verificado en runtime (claude-in-chrome, demo 6d93d9fa):** frame milimetrico vs maqueta; navegacion ‹ 19/221 ›;
deteccion de rol (Nelson Hurtado Cuero ya es usuario con "Portal Residente" -> aparece asignado); asignar "Terceros"
+ Guardar -> PUT ok (BD: Portal Residente + Terceros), y restaurado a Portal Residente (BD de vuelta a 1 rol). El
camino de alta/invitacion quedo cableado y ejercitado (rechazo correcto "ya tiene acceso" en persona existente);
no se corrio un alta exitosa nueva porque en la demo TODOS los residentes ya son usuarios (semilla con Portal
Residente). Sin migracion. Version visible **0.0.155**. **Pendiente Ola 3: panel COMENTARIOS (tabla + RLS +
endpoints + migracion).**

---

## Addendum v0.0.156 (2026-10-15) - FichaResidenteModal Ola 3: panel COMENTARIOS (CON MIGRACION)
Cierra la Ola 3 del rediseno del modal de residente: panel de COMENTARIOS (como pide la maqueta), en la columna
derecha debajo de DOCUMENTOS. **Tiene migracion** (tabla nueva + RLS).

**MIGRACION (aplicar en deploy):** `20261015011139_AddPersonaComentarios` crea `persona_comentarios`
(id, persona_id, autor_usuario_id, texto[4000], tenant_id + auditoria) con indices (tenant_id, (persona_id, created_at))
y **RLS** (ENABLE + FORCE + policy `tenant_isolation` con `current_tenant_id()` + GRANT a `propia_app`). Aditiva: no
toca datos existentes. `RlsCoverageTests` verde (2/2) con la tabla nueva incluida.

**Backend:** entidad `PersonaComentario : TenantEntity`; DbSet + model config (HasQueryFilter por tenant) en
`PropiaDbContext` / `ConfigureDirectorioUsuariosPresupuesto`. `IDirectorioService`/`DirectorioService` ganan
`ListarComentariosPersonaAsync` + `AgregarComentarioPersonaAsync` (resuelven el nombre del autor via
ApplicationUser->Persona; capturan el usuario actual con `IHttpContextAccessor`). **OJO:** el ctor de
`DirectorioService` ahora recibe `IHttpContextAccessor? http = null` (opcional: la app lo inyecta via DI; las pruebas
que lo construyen a mano siguen compilando y `GetUsuarioActualId` devuelve Guid.Empty). Endpoints nuevos en
`DirectorioController`: `GET/POST /api/directorio/personas/{id}/comentarios` (`PersonaComentarioDto` /
`CrearPersonaComentarioRequest`).

**UI:** seccion COMENTARIOS en `FichaResidenteModal` (avatar de iniciales + autor + fecha + texto; textarea +
Publicar). Carga por GET al abrir; publica por POST y agrega el nuevo sin recargar.

**Pasos de deploy:** 1) `git pull` (HEAD de esta tanda). 2) **Aplicar la migracion** `AddPersonaComentarios`
(`dotnet ef database update` o el pipeline). 3) Build/deploy. 4) Verificar version **v0.0.156**.

**Verificado en runtime (navegador integrado, demo 6d93d9fa):** migracion aplicada en dev (tabla + RLS forced +
policy `tenant_isolation` confirmados); en el modal de Nelson Hurtado Cuero se publico un comentario -> aparece con
autor "Admin Delegado Demo", fecha y avatar; al cerrar y reabrir el modal, el comentario persiste (viene por GET de
BD). Dato de prueba borrado (persona_comentarios = 0). `RlsCoverageTests` 2/2 verde. Version visible **0.0.156**.
**Las 3 olas del rediseno del modal de residente quedan completas.**
