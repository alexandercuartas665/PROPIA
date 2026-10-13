# DEPLOY HANDOFF - PROPIA v0.0.141 (2026-10-13)

> **LEEME PRIMERO.** Handoff autocontenido de este deploy. Fuente de verdad acumulada: `HANDOFF_DEPLOY.md`
> (seccion "ESTADO 2026-10-13 Super Admin: limite de copropiedades por organizacion + ver plan").

## Resumen de 1 linea
Super Admin: editar el **limite de copropiedades por organizacion** (override) y **ver el plan** de cada una,
desde `/admin/organizaciones`. **Lleva 1 migracion aditiva.**

## Que desplegar
- Rama: `main`, HEAD **`978950c2`** (o posterior, si se agrega el commit de este handoff).
- Version visible: **0.0.141** (`src/Propia.Web/Propia.Web.csproj <Version>`).
- Repo: https://github.com/alexandercuartas665/PROPIA

## Migraciones / BD  (SI, aplicar)
- **`20261013194852_AddOrganizacionLimiteCopropiedades`** - ADITIVA.
  Agrega 1 columna NULLABLE: `organizaciones.limite_copropiedades integer null`.
  Sin backfill, sin tabla nueva. `organizaciones` es GLOBAL (sin RLS) -> no hay bloque RLS. Reversible.
  Inerte para el codigo viejo (null = comportamiento actual).
- Aplicar: `cd src/Propia.Api && dotnet ef database update --project ../Propia.Infrastructure --startup-project .`
  (owner `propia`, design-time factory). O el metodo de migracion habitual de prod.
> Si prod viene de ANTES de 0.0.141, revisar en `HANDOFF_DEPLOY.md` migraciones aditivas pendientes de tandas
> previas (p.ej. 0.0.125 `AddMascotaTipoCustom`). La tanda intermedia 0.0.133-0.0.140 NO llevaba migraciones.

## Config / env / secrets
**Sin cambios.** No hay variables de entorno, flags, appsettings, seeds ni catalogos nuevos.

## Cambios de codigo
- `Organizacion.cs` (+ `LimiteCopropiedades int?` override; null = usar el del plan).
- `SuperAdmin/Dtos.cs`: `OrganizacionDto` += `LimiteOverride`, `PlanNombre`, `LimitePlan`, `LimiteEfectivo`
  (params opcionales = compatible con clientes/tests que lo deserializan); `CambiarLimiteOrganizacionRequest`.
- `ISuperAdminService`/`SuperAdminService.cs`: `ListOrganizacionesAsync` trae plan + limites (via suscripcion
  activa); `CambiarLimiteCopropiedadesAsync` (+log `CHANGE_LIMITE_ORGANIZACION`).
- `AdminController.cs`: `PUT ~/api/admin/organizaciones/{orgId}/limite` (policy SuperAdmin).
- `MisCopropiedadesService.cs`: enforcement = `override de la org ?? limite del plan ?? (sin suscripcion -> 1)`.
- `Organizaciones.razor`: columna Plan + celda `count / limite` (badge "propio" si hay override) + modal Editar.

## Pasos de deploy
1. `git fetch && git checkout main && git pull` (quedar en `978950c2` o posterior).
2. **Aplicar la migracion** en la BD de prod (ver arriba).
3. Build/publish Release del artefacto y desplegar.
4. Verificar version en el pie/login: **v0.0.141**.

## Verificacion post-deploy
- Entrar a `/admin/organizaciones` (consola A&D): aparece la columna **Plan** y, en Copropiedades, `count / limite`
  con boton **Editar**.
- Editar el limite de una organizacion a un numero -> la fila muestra `.../N` con badge **"propio"**; dejarlo
  vacio -> vuelve a regir el limite del plan.
- (Opcional) Con un override por debajo del conteo actual, el admin de esa organizacion NO puede crear otra
  copropiedad (mensaje "El limite permitido es N ... ya tienes M").

## Rollback
Redeploy del artefacto previo. La migracion es aditiva e inerte para el codigo viejo; opcionalmente
`database update` a la migracion anterior para quitar la columna (no necesario).
