# PROPIA - Checklist de deploy

> Actualizado 2026-09-08. Version visible: **0.0.68**
> (`src/Propia.Web/Propia.Web.csproj` `<Version>`). Bumpear en cada deploy.
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

- [ ] Login OK; el footer muestra `v0.0.69`.
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

- **R-03** (rendimiento estructural): partir `@code` de TareasKanban (5443), Servicios (3223),
  GestionarUnidadesModal (2110), GestionarPqrsdModal (1897) a <800 lineas extrayendo subcomponentes
  (patron TareaCard), uno a uno con verificacion. No bloquea el deploy.
- **V-07 prod**: normalizar el catalogo de plantillas de agentes IA en PROD (en dev ya esta).
- **CI**: confirmar `SeguridadAuthFlowTests` verde en un runner sin carga (el fix del lockout ya esta
  aplicado; el runner local estaba saturado).
- **S-09 opcional**: bucket R2 privado + URLs prefirmadas para imagenes de marca/fotos (baja sensibilidad;
  los documentos sensibles ya se sirven gateados).
- **S-19 opcional**: mover el JWT de localStorage a cookie HttpOnly (mitigado hoy por CSP + sanitizacion).
