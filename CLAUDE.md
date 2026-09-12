# CLAUDE.md - PROPIA

> Este archivo lo lee Claude en CADA turno. Todo lo que diga aqui debe ser cierto HOY. Si algo cambia
> (puertos, rutas, estado), se actualiza aqui primero. Ultima revision: 2026-09-12 (VIGIA, sesion auditora).

## Que es PROPIA

SaaS multi-tenant de gestion integral de copropiedades (propiedad horizontal en Colombia, Ley 675 de 2001),
operado por **A&D GROUP S.A.S**. Cliente y autor: **Alex Alvis**. Prod real: `https://app.propia-ad.com`
(las URLs `*.up.railway.app` estan muertas). Version en el pie de la app: `v0.0.9x`.

## Estado real del desarrollo (no "setup inicial")

Hay **~25 modulos construidos y en uso** en dev y prod: Super Admin (consola A&D dentro de Propia.Web),
Onboarding, Mi Copropiedad (unidades con codigo inteligente `TORRE-NUMERO`, gestor de campos estilo
Airtable con entidades vinculadas, carga por Excel), Directorio, Usuarios/Roles/RBAC, Tareas (multi-tablero),
PQRSD (71 endpoints), Mantenimiento/Equipos/Zonas, Contratos y Seguros, Porteria, Reservas, Comunicaciones,
Documentos, Reportes, Presupuesto/Cartera, Notificaciones (email/WhatsApp/in-app), IA (agente + MCP tools),
jobs en segundo plano, modo oscuro por tokens, RLS en 230 tablas.

**Fase actual: estabilizacion y reparacion**, no construccion. Desde 2026-09-10 el trabajo se reparte en
4 sesiones dev por modulo (ATLAS=Tareas, FARO=PQRSD, YUNQUE=Mantenimiento, SELLO=Contratos) + 1 auditora
(VIGIA) + la sesion dev principal (main). Reglas y fichas en
`D:\Obsidian\Propia\06. TRABAJO EN EQUIPO\` (leer "Protocolo de trabajo en paralelo.md" antes de tocar codigo).
Regla 7.0: **reparar lo existente antes que construir; funciones nuevas solo discutidas con Alex.**

## Fuente de verdad funcional - vault Obsidian

```
D:\Obsidian\Propia\
```

- `01. REQUERIMIENTO\Capa 2. Copropiedad\2.x. <Modulo>_v1_0.md` - spec por modulo (RN-xx, fases, MVP).
- `01. REQUERIMIENTO\Capa 3. Copropiedad Convalidacion\` - convalidacion spec vs implementacion.
- `02. INVENTARIO MODULOS\INVENTARIO GENERAL.md` - indice de los 25 modulos + dependencias.
- `03. NOTAS DE DESARROLLO\` - homogeneidad UI (vista tabla), notas para desarrollador, estructura BD.
- `06. TRABAJO EN EQUIPO\` - protocolo del equipo, fichas por agente, tablero, solicitudes a dev principal.

La skill `consultar-obsidian-propia` (en `.claude/skills/`) se usa ante cualquier duda funcional. La ruta
antigua `C:\Users\acuartas\...` NO existe en esta maquina.

## Stack real

- Backend: ASP.NET Core Web API .NET 9, C# 13. Frontend: **Blazor Web App con render InteractiveServer**
  (hay WebAssembly registrado pero la app opera en Server). **No se usa MudBlazor**; UI propia sobre assets
  NexLink (Bootstrap 5) + CSS en `wwwroot/css/modules/*.css` y tokens en `wwwroot/css/propia-tokens.css`.
- DB: PostgreSQL 17 en Docker (host **5433**), db `propia_dev`. Owner `propia` (solo migraciones, design-time
  via `PropiaDbContextFactory`); runtime `propia_app` / `PropiaAppDev2026!` (respeta RLS).
- ORM: EF Core 9 + Npgsql. Auth: Identity + JWT propio (`/connect/login`, `/connect/switch-tenant`).
- Jobs: **`BackgroundJobScheduler` propio (IHostedService, estilo Hangfire-lite)**, no Hangfire. Flag
  `Jobs:Enabled` (env `Jobs__Enabled=false` para instancias secundarias).
- Excel: ClosedXML. PDF: Chromium. Storage dev: `LocalBlobStorage` en `wwwroot/uploads`.
- IA: Anthropic SDK. WhatsApp: Meta Cloud API. Pagos: Wompi. Hosting: Railway.

## Arquitectura (Clean/Onion)

`Propia.Domain` <- `Propia.Application` <- `Propia.Infrastructure` <- `Propia.Api` / `Propia.Web`.
`Propia.Web` referencia a `Propia.Api` y hospeda la consola de A&D (login unificado). `Propia.SuperAdmin`
es un subproyecto muerto: no tocar. `Propia.Shared` = DTOs compartidos.

Mapa rapido por modulo (donde vive cada cosa):

| Modulo | API controller | Servicio | Entidades | UI |
|---|---|---|---|---|
| Unidades / Mi Copropiedad | `MiCopropiedadController` | `MiCopropiedadService.*.cs`, `UnidadesPlantillaService` | `UnidadPrivada.cs`, `UnidadVinculadosCampoEntities.cs` | `Pages/Capa2/Distribucion.razor`, `Modals/GestionarUnidadesModal.razor` |
| Tareas | `TareasController` | `Tareas/TareasService.*.cs` | `TableroEntities.cs` | `Shared/Tareas/TableroTareas.razor` |
| PQRSD | `PqrsdController`, `PublicPqrsdController` | `Pqrsd/PqrsdService.*.cs` | `PqrsdEntities.cs` | `Pages/Capa2/PqrsKanban.razor`, `Modals/*Pqrsd*.razor` |
| Mantenimiento | `MantenimientoController`, `ProgramacionesController` | `Mantenimiento/MantenimientoService.cs` | `MantenimientoEntities.cs`, `Equipo/ZonaFichaEntities.cs` | `Pages/Capa2/Mantenimiento.razor` |
| Contratos / Seguros | `MiCopropiedadController` (contratos), `SegurosController` | `GobiernoYServicios.cs`, `Seguros/SegurosService.cs` | `ContratoServicio.cs`, `Poliza.cs` | `Pages/Capa2/Servicios.razor`, `Seguros.razor` |
| Jobs | `MonitoriaController` (disparo manual) | `Infrastructure/Jobs/*Job.cs` | - | - |

Patron transversal de campos dinamicos: `<X>CampoDefinicion` (catalogo por tenant) + `<X>CampoValor` (EAV)
+ `<X>CampoConfig` (alias/oculto/orden de campos fijos). Existe para Unidad, Persona, Vehiculo, Mascota,
Tercero, Equipo, Zona, Tarea, Pqrsd, Poliza, Contrato. **Reusarlo, no reinventarlo.**

## Multi-tenancy (regla critica)

- Toda tabla operativa lleva `tenant_id`, `HasQueryFilter` sobre `TenantEntity`, y **RLS con FORCE +
  policy** en la migracion. `RlsCoverageTests` falla si una tabla con `tenant_id` no tiene RLS.
- Middleware fija `app.tenant_id` por request. **Cualquier codigo que corra fuera de un request (jobs,
  seeds, tools MCP) debe iterar tenants con `SetTenant` + `CloseConnectionAsync`**; sin eso, con
  `propia_app` la policy devuelve 0 filas en silencio (error real encontrado en `PqrsdMantenimientoService`).
- `personas` es global: al asignar/vincular una persona, validar que pertenece al tenant actual.
- Nunca SQL raw sin filtro de tenant; nunca `IgnoreQueryFilters()` sin fijar tenant.

## Convenciones de codigo

- Solo ASCII en codigo y comentarios. Espanol en UI, ingles en identificadores. `PascalCase` clases,
  `snake_case` tablas, IDs `Guid`, DTOs con sufijo `Dto`/`Request`/`Response`.
- **Colores SOLO por tokens** `var(--propia-*)` de `propia-tokens.css`; prohibido hex y `white` en `.razor`;
  al cambiar tokens subir `?v=N` en `App.razor`. Regla: `grep -rE "#[0-9A-Fa-f]{6}" Components --include=*.razor`
  no debe crecer.
- `PUT` es MERGE: nunca pisar campos que el cliente no envio.
- RBAC: escrituras gateadas con `[RequierePermiso(ModuloCodigo.X, AccionPermiso.Y)]`; GET abiertos al
  tenant; Admin hace bypass. No usar `[RequiereRol("...")]` literal en modulos nuevos.
- Vista tabla: seguir la skill `homogenizar-vista-tabla` al pie de la letra (Tareas es la referencia).
- Blazor: `@bind` es `onchange`; en alta usar `@bind:event="oninput"`. `InputFile`: bufferizar a bytes al
  seleccionar. `<input type=date>` con `@bind` sobre string falla: usar `value` + `@oninput`.

## DEFINICION DE LISTO (obligatoria; "listo" sin esto no es listo)

Una tarea esta terminada solo cuando se cumplen TODAS:

1. **Compila y prueba**: `dotnet build` sin warnings nuevos; `dotnet test` con el baseline conocido (ver
   Testing). Un test nuevo o modificado cubre el cambio cuando toca API/servicio.
2. **Verificado en runtime con datos reales, no por lectura del codigo**: se ejecuto el flujo completo en
   Chrome (MCP) contra la instancia local, con clic real, y se comprobo el RESULTADO (fila en BD, respuesta
   del API, archivo descargado y abierto). Para exportes/plantillas: abrir el archivo generado y **contar
   columnas/filas contra la configuracion de la copropiedad** (ej. campos activos en `unidades-config`
   vs columnas emitidas); no basta con que descargue.
3. **Evidencia en la entrega**: que se probo, con que datos, que se midio y que valor dio. Si algo no se
   pudo probar, se dice "NO probado" en vez de "listo".
4. **Sin regresion en lo vecino**: si se toco un servicio compartido (plantilla, config de campos, jobs),
   se prueba al menos un caso de cada consumidor.
5. **Datos de prueba borrados**, migracion aplicada solo con OK de Alex, y entrega registrada en Obsidian
   (ficha del agente: "Registro de trabajo" + "Avance") cuando se trabaja en el equipo.

Antes de codificar una tarea no trivial: responder un plan de 5 lineas (que, archivos, migracion si/no,
archivos compartidos, como se va a verificar) y esperar OK. Si la tarea es ambigua, preguntar, no asumir.

## Testing

- Unitarios en `Propia.Application.Tests`/`Domain.Tests`; integracion con Testcontainers (PostgreSQL real,
  ~4 min la suite) en `Propia.Integration.Tests`. Aislamiento de tenant obligatorio por entidad nueva.
- **Baseline 2026-09-10: 224 verdes / 8 rojos preexistentes** (4 `BillingFlowTests`, 1 `RlsCoverageTests`
  con 8 tablas sin RLS, 1 `SuperAdminFlowTests`, 2 `UsuariosAccesosFlowTests`). Son deuda, NO excusa:
  cualquier rojo nuevo es una regresion del cambio en curso. Comparar con `git stash` si hay duda y
  declarar en la entrega cuales rojos son previos. Meta: bajar ese numero, no normalizarlo.

## Entorno local (lo que rompe si no se sabe)

- Dos procesos: **API en `https://localhost:7113`** (forzar con
  `ASPNETCORE_URLS="https://localhost:7113;http://localhost:5153" dotnet run --no-launch-profile`, con
  `ASPNETCORE_ENVIRONMENT=Development`; el launch profile apunta a 7205 y el Web se cuelga en el spinner
  si la API no esta en 7113) y **Web en `http://localhost:5105`**.
- El navegador entra SIEMPRE por `http://localhost:5105` (el https dev lo bloquea Kaspersky con un interstitial).
- Uploads: junction `src/Propia.Api/wwwroot/uploads -> src/Propia.Web/wwwroot/uploads`; si desaparece, 404
  en PDFs/adjuntos. Recrear con `New-Item -ItemType Junction`.
- Antes de recompilar, parar SOLO los propios procesos (por PID/puerto). Con el equipo activo, **prohibido
  `taskkill /IM Propia.*.exe`**: tumba las instancias de los otros agentes (7213-7613 / 5205-5605).
- `dotnet-ef` no esta en PATH: `"$USERPROFILE/.dotnet/tools/dotnet-ef.exe"`.
- Chrome MCP: usar el navegador **ASUS**; `javascript_tool` devuelve `{}` en async (codigo sincrono); para
  inputs Blazor, setter nativo + `new Event('input',{bubbles:true})`. El navegador integrado si espera promesas.
- Instancias secundarias arrancan con `Jobs__Enabled=false`; solo la API principal corre jobs.

## Comandos

```bash
cd deploy/docker && docker compose up -d          # BD
dotnet build && dotnet test                        # build + suite (ver baseline)
cd src/Propia.Api && dotnet ef migrations add <Nombre> --project ../Propia.Infrastructure --startup-project .
cd src/Propia.Api && dotnet ef database update --project ../Propia.Infrastructure --startup-project .
dotnet format
```

## Checklist pre-commit

- [ ] Build sin warnings nuevos; tests con baseline; `dotnet format` limpio.
- [ ] DEFINICION DE LISTO cumplida (runtime + evidencia + datos borrados).
- [ ] No expone datos entre tenants; tabla nueva con RLS + filtro + test.
- [ ] Sin hex/`white` nuevos en `.razor`; ASCII en codigo.
- [ ] Si toca un modulo del inventario, actualizar su nota Obsidian; si es entrega del equipo, ficha + tablero.
- [ ] `deploy/HANDOFF_DEPLOY.md` actualizado si hay migracion o config nueva.
