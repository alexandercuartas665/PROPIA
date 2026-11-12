# CLAUDE.md - PROPIA

> Este archivo lo lee Claude en CADA turno. Todo lo que diga aqui debe ser cierto HOY. Si algo cambia
> (puertos, rutas, estado), se actualiza aqui primero. Ultima revision: 2026-11-11 (sesion dev principal).

## Que es PROPIA

SaaS multi-tenant de gestion integral de copropiedades (propiedad horizontal en Colombia, Ley 675 de 2001),
operado por **A&D GROUP S.A.S**. Cliente y autor: **Alex Alvis**. Prod real: `https://app.propia-ad.com`
(las URLs `*.up.railway.app` estan muertas). Version en el pie de la app: `v0.0.2xx`.

## Estado real del desarrollo (no "setup inicial")

Hay **~25 modulos construidos y en uso** en dev y prod: Super Admin (consola A&D dentro de Propia.Web),
Onboarding, Mi Copropiedad (unidades con codigo inteligente `TORRE-NUMERO`, gestor de campos estilo
Airtable con entidades vinculadas, carga por Excel), Directorio, Usuarios/Roles/RBAC, Tareas (multi-tablero),
PQRSD, Mantenimiento/Equipos/Zonas, Contratos y Seguros, Servicios publicos (recibos leidos con IA),
Porteria, Reservas, Asambleas, Comunicaciones, Documentos, Plantillas de documentos, Reportes,
Presupuesto/Cartera, Notificaciones (email/WhatsApp/in-app), IA (agentes + MCP tools), jobs en segundo plano,
modo oscuro por tokens, RLS en todas las tablas con `tenant_id`.

**Forma de trabajo (desde 2026-11-11):** UNA sesion de desarrollo sobre `main` + la sesion de deploy, que
trabaja en `feat/railway-deploy` (sigue a main y sube la version). No hay ramas ni worktrees de equipo: el
esquema de sesiones en paralelo (ATLAS/FARO/YUNQUE/SELLO/VIGIA) termino y sus ramas se borraron (respaldo en
`D:\DesarrolloIA\Propia-respaldo-ramas-2026-11-11\`). Commit y push directo a `main` solo cuando Alex lo pide.
Regla 7.0: **reparar lo existente antes que construir; funciones nuevas solo discutidas con Alex.**

## Fuente de verdad funcional - vault Obsidian

```
D:\Obsidian\Propia\
```

- `01. REQUERIMIENTO\Capa 2. Copropiedad\2.x. <Modulo>_v1_0.md` - spec por modulo (RN-xx, fases, MVP).
- `01. REQUERIMIENTO\Capa 3. Copropiedad Convalidacion\` - convalidacion spec vs implementacion.
- `02. INVENTARIO MODULOS\INVENTARIO GENERAL.md` - indice de los 25 modulos + dependencias.
- `03. NOTAS DE DESARROLLO\` - homogeneidad UI (vista tabla), notas para desarrollador, estructura BD.
- `06. TRABAJO EN EQUIPO\` - historico del esquema de sesiones en paralelo (ya no vigente).

La skill `consultar-obsidian-propia` (en `.claude/skills/`) se usa ante cualquier duda funcional. La ruta
antigua `C:\Users\acuartas\...` NO existe en esta maquina.

Disenos: la replica (maqueta) vive en `D:\DesarrolloIA\Maqueta\replica` (`PROPUESTAS.md` + `js/mod-*.js` +
`css/replica.css`). Cuando se pide "milimetrico", comparar app y replica LADO A LADO (claro/oscuro,
escritorio/tablet/celular).

## Stack real

- Backend: ASP.NET Core Web API .NET 9, C# 13. Frontend: **Blazor Web App con render InteractiveServer**
  (hay WebAssembly registrado pero la app opera en Server). **No se usa MudBlazor**; UI propia sobre assets
  NexLink (Bootstrap 5) + CSS en `wwwroot/css/modules/*.css` y tokens en `wwwroot/css/propia-tokens.css`.
- DB: PostgreSQL 17 en Docker (host **5433**), db `propia_dev`. Owner `propia` (solo migraciones, design-time
  via `PropiaDbContextFactory`); runtime `propia_app` / `PropiaAppDev2026!` (respeta RLS).
- ORM: EF Core 9 + Npgsql. Auth: Identity + JWT propio (`/connect/login`, `/connect/switch-tenant`).
- Jobs: **`BackgroundJobScheduler` propio (IHostedService, estilo Hangfire-lite)**, no Hangfire. Flag
  `Jobs:Enabled` (env `Jobs__Enabled=false` para el Web y cualquier instancia que no deba correr jobs).
- Excel: ClosedXML. PDF: Chromium (PuppeteerSharp). Storage dev: `LocalBlobStorage` en `wwwroot/uploads`; prod R2.
- IA: proveedores configurables en Super Admin (DeepSeek, Gemini, Claude). WhatsApp: Meta Cloud API.
  Pagos: Wompi. Hosting: Railway.

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
| Contratos / Seguros | `MiCopropiedadController` (contratos), `SegurosController` | `GobiernoYServicios.cs`, `Seguros/SegurosService.cs` | `ContratoServicio.cs`, `Poliza.cs` | `Pages/Capa2/Servicios.razor` (solo `/contratos`), `Seguros.razor` |
| Servicios publicos (`/servicios`) | `ServiciosPublicosController` | `ServiciosPublicos/ServiciosPublicosService.cs`, `ReglasServicioPublico.cs` | `ServicioPublico.cs` | `Pages/Capa2/ServiciosPublicosPagina.*` + `Capa2/Spx/` |
| Lectura de documentos con IA | `OcrController` (`/api/ocr/extraer-agente`, perfiles contrato/poliza/recibo_servicio) | Agente Documental | - | `Shared/IaIngesta/*` |
| Agentes IA (semillas) | `AdminAiAgentTemplatesController`, `IaController` | `AiAgentTemplateService`, `AgentesSistemaService` | `AiAgentTemplate.cs`, `InfraestructuraIaEntities.cs` | `Capa0/Admin/PlantillasAgentesIa.razor`, `Capa2/IaAgentes.razor` |
| Jobs | `MonitoriaController` (disparo manual) | `Infrastructure/Jobs/*Job.cs` | - | - |

Patron transversal de campos dinamicos: `<X>CampoDefinicion` (catalogo por tenant) + `<X>CampoValor` (EAV)
+ `<X>CampoConfig` (alias/oculto/orden de campos fijos). Existe para Unidad, Persona, Vehiculo, Mascota,
Tercero, Equipo, Zona, Tarea, Pqrsd, Poliza, Contrato, Plantilla, Servicio publico. **Reusarlo, no reinventarlo.**

Agentes IA: **el Super Admin manda**. Las plantillas (Super Admin > Plantillas de Agentes IA) tienen version;
"Actualizar copropiedades" lleva prompt, tools, proveedor y modelo a los agentes de las copropiedades
(respeta los que la copropiedad edito, salvo forzar). Los agentes del sistema se buscan por CLAVE
(`PlantillaAgente.ClaveDocumental` / `ClaveAuxiliar`, nunca por nombre) y se crean al usarlos si faltan.
No hay scripts SQL de prompts.

## Multi-tenancy (regla critica)

- Toda tabla operativa lleva `tenant_id`, `HasQueryFilter` sobre `TenantEntity`, y **RLS con FORCE +
  policy** en la migracion. `RlsCoverageTests` falla si una tabla con `tenant_id` no tiene RLS.
- Middleware fija `app.tenant_id` por request. **Cualquier codigo que corra fuera de un request (jobs,
  seeds, tools MCP, procesos del Super Admin sobre varias copropiedades) debe iterar tenants con `SetTenant`
  + `CloseConnectionAsync`**; sin eso, con `propia_app` la policy devuelve 0 filas en silencio.
- Backfills en migraciones sobre tablas con FORCE: `NO FORCE ROW LEVEL SECURITY` solo durante el UPDATE.
- `personas` es global: al asignar/vincular una persona, validar que pertenece al tenant actual.
- Nunca SQL raw sin filtro de tenant; nunca `IgnoreQueryFilters()` sin fijar tenant.

## Convenciones de codigo

- Solo ASCII en codigo y comentarios. Espanol en UI (con tildes), ingles en identificadores. `PascalCase`
  clases, `snake_case` tablas, IDs `Guid`, DTOs con sufijo `Dto`/`Request`/`Response`.
- **Colores SOLO por tokens** `var(--propia-*)` de `propia-tokens.css`; prohibido hex y `white` en `.razor`;
  al cambiar CSS/JS subir `?v=N` en `App.razor`. Regla: `grep -rE "#[0-9A-Fa-f]{6}" Components --include=*.razor`
  no debe crecer.
- `PUT` es MERGE: nunca pisar campos que el cliente no envio (para borrar un opcional, nombrarlo explicito).
- RBAC: escrituras gateadas con `[RequierePermiso(ModuloCodigo.X, AccionPermiso.Y)]`; GET abiertos al
  tenant; Admin hace bypass. No usar `[RequiereRol("...")]` literal en modulos nuevos.
- Vista tabla: seguir la skill `homogenizar-vista-tabla` al pie de la letra (referencia: Unidades / Plantillas).
- Blazor: `@bind` es `onchange`; en alta usar `@bind:event="oninput"`. `InputFile`: bufferizar a bytes al
  seleccionar. `<input type=date>` con `@bind` sobre string falla: usar `value` + `@oninput`.
- EF: hijos con `Id` ya asignado (BaseEntity) se agregan por su `DbSet`, no solo por la navegacion (si no,
  EF los toma como existentes: UPDATE de 0 filas).

## DEFINICION DE LISTO (obligatoria; "listo" sin esto no es listo)

Una tarea esta terminada solo cuando se cumplen TODAS:

1. **Compila y prueba**: `dotnet build` sin warnings nuevos; `dotnet test` con el baseline conocido (ver
   Testing). Un test nuevo o modificado cubre el cambio cuando toca API/servicio.
2. **Verificado en runtime con datos reales, no por lectura del codigo**: se ejecuto el flujo completo contra
   la instancia local, con clic real, y se comprobo el RESULTADO (fila en BD, respuesta del API, archivo
   descargado y abierto). Para exportes/plantillas: abrir el archivo generado y **contar columnas/filas contra
   la configuracion de la copropiedad**; no basta con que descargue.
3. **Evidencia en la entrega**: que se probo, con que datos, que se midio y que valor dio. Si algo no se
   pudo probar, se dice "NO probado" en vez de "listo".
4. **Sin regresion en lo vecino**: si se toco un servicio compartido (plantilla, config de campos, jobs,
   IaIngesta, OcrController), se prueba al menos un caso de cada consumidor.
5. **Datos de prueba borrados**, migracion aplicada solo con OK de Alex, nota del modulo en Obsidian al dia
   y `deploy/HANDOFF_DEPLOY.md` actualizado si hay migracion o config nueva.

Antes de codificar una tarea no trivial: responder un plan de 5 lineas (que, archivos, migracion si/no,
archivos compartidos, como se va a verificar) y esperar OK. Si la tarea es ambigua, preguntar, no asumir.

## Testing

- Unitarios en `Propia.Application.Tests` (`Domain.Tests` no tiene pruebas); integracion con Testcontainers
  (PostgreSQL real, ~4 min la suite) en `Propia.Integration.Tests`. Aislamiento de tenant obligatorio por
  entidad nueva.
- **Baseline 2026-11-11: unitarias 102/102; integracion 446 verdes / 5 rojos preexistentes** (4
  `BillingFlowTests`, 1 `SuperAdminFlowTests`). Son deuda, NO excusa: cualquier rojo nuevo es una regresion
  del cambio en curso. Meta: bajar ese numero, no normalizarlo.
- Con API/Web corriendo, `dotnet test` puede fallar por DLL bloqueadas (MSB3027): parar los propios procesos
  o usar `--artifacts-path` a una carpeta temporal.

## Entorno local (lo que rompe si no se sabe)

- Dos procesos: **API en `https://localhost:7113`** (forzar con
  `ASPNETCORE_URLS="https://localhost:7113;http://localhost:5153" dotnet run --no-launch-profile`, con
  `ASPNETCORE_ENVIRONMENT=Development`; el launch profile apunta a 7205 y el Web se cuelga en el spinner
  si la API no esta en 7113) y **Web en `http://localhost:5105`** (con `Jobs__Enabled=false`).
- El navegador entra SIEMPRE por `http://localhost:5105` (el https dev lo bloquea Kaspersky con un interstitial).
  Sin credenciales: `/dev/login` -> "Entrar como admin demo" (copropiedad demo). La consola Super Admin NO se
  abre con ese usuario.
- Uploads: junction `src/Propia.Api/wwwroot/uploads -> src/Propia.Web/wwwroot/uploads`; si desaparece, 404
  en PDFs/adjuntos. Recrear con `New-Item -ItemType Junction`.
- Antes de recompilar, parar SOLO los propios procesos (por PID/puerto); **nunca `taskkill /IM Propia.*.exe`**.
  Reiniciar el Web corta el circuito Blazor de las pestanas abiertas (el overlay recarga solo cuando vuelve).
- `dotnet-ef` no esta en PATH: `"$USERPROFILE/.dotnet/tools/dotnet-ef.exe"`.
- Verificacion en navegador: Chrome MCP con el navegador **ASUS** (`javascript_tool` devuelve `{}` en async;
  para inputs Blazor, setter nativo + `new Event('input',{bubbles:true})`), o el navegador integrado. Lo que
  usa pdf.js / requestAnimationFrame se cuelga en pestanas ocultas o de fondo: traer la pestana al frente.

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
- [ ] Si toca un modulo del inventario, actualizar su nota Obsidian.
- [ ] `deploy/HANDOFF_DEPLOY.md` actualizado si hay migracion o config nueva.
- [ ] `git add` con lista explicita de archivos (nunca `-A` sobre `src`); commit/push a `main` solo con OK de Alex.
