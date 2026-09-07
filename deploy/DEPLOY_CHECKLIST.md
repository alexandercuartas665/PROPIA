# PROPIA - Checklist de deploy

> Actualizado 2026-09-07. Version visible: **0.0.61**
> (`src/Propia.Web/Propia.Web.csproj` `<Version>`). Bumpear en cada deploy.
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
- `20260905150507_AddPlanLimiteCopropiedades`  (Planes: `planes` +`limite_copropiedades` int null, +`es_promocional` bool)  <-- NUEVA
- `20260905143647_AddPolizaPdfOrigen`  (Seguros: `polizas` +`pdf_origen_key` text null)  <-- NUEVA
- `20260905135921_AddDocumentExtractionLog`  (IA: tabla nueva `document_extraction_logs`, global sin RLS)  <-- NUEVA
- `20260904173502_AddSuperAdminLockout`  (S-03b: lockout de SuperAdmin)
- `20260903210728_V01PanelSnapshotSinRlsMasUnidades`
- `20260903171507_AddTenantLinkPago`
- `20260903154017_S02UniquePersonaIdEnUsuarios`

> El increment de campos dinamicos PQRSD (0.0.61) **no agrega migracion**: usa `pqrsd_campos.columna` ya
> existente. Todas las migraciones nuevas son **aditivas** (columnas nullable / tabla nueva) -> seguras.

## 4. Post-deploy (verificacion)

- [ ] Login OK; el footer muestra `v0.0.61`.
- [ ] Planes: crear una 2a copropiedad con plan que permite 1 -> bloqueo con mensaje; un plan promocional
      no se puede cambiar de plan directamente.
- [ ] PQRSD: editar un campo dinamico -> se guarda solo (sin boton); 3 anchos alinean en linea; VENCIDO en rojo.
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
