# HAND-OFF DEPLOY - PROPIA (2026-10-07) - v0.0.111

> Portada de ESTE deploy. Trae **1 MIGRACION NUEVA** (aplicar en prod, ver Migraciones).

## Objetivo
- **Rama / HEAD:** `main` (tras merge de `equipo/alex-header-zonas-equipos`)
- **Version:** `0.0.111` (desde `0.0.110`)
- **Mecanismo:** auto-deploy desde `main` (Railway).
- **TRAE 1 MIGRACION** (`20261007135437`). Sin breaking changes de comportamiento: el modelo es ADITIVO y
  compatible (el flujo de permisos/login actual sigue igual; multi-rol "rol activo por sesion" = pendiente B2b).

## Que entra (rediseno Usuarios 2.5 v2.0, spec e107) - 6 commits sobre v0.0.110

**B1 (`58586f05`) - modelo multi-rol + Cargo + estado nuevo [MIGRACION]:**
- Entidades `UsuarioTenantRol` (N:N usuario<->rol) y `UsuarioTenantCargo` (N:N usuario<->cargo string).
- Enum `EstadoUsuarioTenant += PendienteContacto=4` (valor nuevo, sin reordenar).
- Catalogo global `usuario.cargo` (5 opciones, editable por A&D; se siembra al arranque).
- Migracion `20261007135437`: crea las 2 tablas + RLS (FORCE + policy) + GRANT propia_app + **BACKFILL**
  (cada acceso con RolId -> fila en usuario_tenant_roles). `UsuarioTenant.RolId` se conserva como rol principal.

**B2a (`663c74d0`) - capa de servicio multi-rol/cargo:** DTO expone Roles/Cargos; `PUT /api/usuarios/{id}/roles`
y `/cargos` (reemplazan el conjunto + rol principal + guard de ultimo Administrador).

**B3 frontend (`537e14c8`, `0a01d06d`, `cd34e918`, `b8c8c4ec`) - tabla de Usuarios al spec:**
- KPIs (Usuarios/Activos/Invitacion pendiente). Columnas exactas: Nombre completo, Documento, Correo, Celular,
  Rol, Cargo, Estado, Ultimo acceso, Fecha de vinculacion. Rol/Cargo como CHIPS; Estado badge de 4 valores.
- Control multi-select inline de **Rol** y **Cargo** en el alta (chips + "+" menu toggle anclado, "x" para quitar).
- **Alta** crea el `UsuarioTenant` en "Invitacion pendiente" con TODOS sus roles/cargos (`POST /api/usuarios/alta`)
  + dispara la invitacion. Las 3 pestanas usan la MISMA tabla filtrada por estado (Pendientes agrupa
  Invitacion pendiente + Pendiente de contacto). Validacion: documento + nombre + Correo O Celular.

**Pendiente (NO en este deploy):** B2b = RBAC/login "rol activo por sesion" (supervisado); autosave al blur en el
alta (hoy guarda con Enter / boton); acciones Reenviar/Copiar-link de invitacion por fila en Pendientes.

## Migraciones (APLICAR EN PROD, owner `propia`)
**1 migracion nueva:** `20261007135437_Modulo25V2_MultiRolCargo_EstadoPendienteContacto` - crea
`usuario_tenant_roles` + `usuario_tenant_cargos` con RLS, GRANT propia_app, y backfill del rol actual.
Aplicada en dev (257 roles backfilleados; RLS verificada). Comando:
```
cd src/Propia.Api
"$USERPROFILE/.dotnet/tools/dotnet-ef.exe" database update --project ../Propia.Infrastructure --startup-project .
```
(Aplica tambien las de tandas previas si prod viene por detras.)

## Config/arranque
El seeder del catalogo (`CatalogoListasSeeder`) siembra `usuario.cargo` en cada arranque (idempotente).

## Verificacion hecha (dev, Chrome, tenant demo)
- Migracion aplicada; backfill 257; RLS FORCE + policy en las 2 tablas nuevas.
- Servicio: PUT roles/cargos persisten; guard de ultimo Administrador -> 400.
- Tabla: KPIs, columnas exactas, Rol/Cargo chips, Estado badge 4 valores, control multi-select con menu anclado.
- E2E: alta con 2 roles + 2 cargos -> UsuarioTenant en "Invitacion pendiente" con roles/cargos persistidos,
  visible en el tab Pendientes con chips. Datos de prueba borrados (sin huerfanos).
- NO se corrio la suite de integracion completa (Testcontainers ~4 min); las tablas nuevas cumplen RLS
  (tenant_id + FORCE + policy), verificado a mano -> RlsCoverageTests no deberia marcar rojo nuevo.

## Humo post-deploy
- [ ] Aplicar la migracion ANTES de que arranque (o al primer arranque). `/admin/catalogos` lista "Usuarios · Cargo".
- [ ] /usuarios: KPIs; columnas Nombre completo..Fecha de vinculacion; Rol/Cargo como chips; Estado badge.
- [ ] Alta: elegir varios roles + cargos (menu "+"); crear -> aparece en Pendientes con sus chips y "Invitacion pendiente".
- [ ] Pie v0.0.111.

## Rollback
- Redeploy del artefacto anterior (v0.0.110). La migracion es aditiva (tablas nuevas); el codigo viejo las
  ignora. No revertir el esquema: fijar hacia adelante.

---
Generado 2026-10-07 (v0.0.111). Companions: `HANDOFF_DEPLOY.md`, `DEPLOY_HANDOFF_2026-10-06_v0.0.110.md`.
