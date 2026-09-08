# PROPIA - Checklist de deploy

> Actualizado 2026-09-08. Version visible: **0.0.63**
> (`src/Propia.Web/Propia.Web.csproj` `<Version>`). Bumpear en cada deploy.
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

- [ ] Login OK; el footer muestra `v0.0.63`.
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
