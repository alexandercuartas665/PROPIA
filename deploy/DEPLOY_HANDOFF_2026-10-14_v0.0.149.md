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
