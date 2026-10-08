# HAND-OFF DEPLOY - PROPIA (2026-10-07) - v0.0.112

> Portada de ESTE deploy. Trae **1 MIGRACION NUEVA Y DESTRUCTIVA** (elimina la tabla `torres`).
> Leer "Migraciones" y "Orden de release" antes de aplicar nada.

## Objetivo
- **Rama / HEAD:** `equipo/alex-header-zonas-equipos` (merge a `main` en el release)
- **Version:** `0.0.112` (desde `0.0.111`)
- **Mecanismo:** merge a `main` + push -> auto-deploy en Railway.
- **TRAE 1 MIGRACION NUEVA** (`20261007214045_RemoveTorreDeUnidades`), **DESTRUCTIVA** (DROP de `torres`
  + columna `torre_id`). Es ADITIVO-INCOMPATIBLE con el codigo viejo: ver "Orden de release".

## Que entra (2 cambios sobre v0.0.111) - 3 commits

**1) Usuarios 2.5 v2.0 **B2b** - rol activo por sesion (`37d66a43`) [SIN MIGRACION]**
- El JWT lleva el rol ACTIVO (claim `rol_id`); el enforcement RBAC autoriza con ESE rol, no con el
  principal. Reusa las tablas `usuario_tenant_roles`/`usuario_tenant_cargos` que YA se desplegaron en
  v0.0.111 (migracion `20261007135437`), por eso **no agrega migracion**.
- `TokenService` (claim `rol_id`); `AuthService` (`GetMeAsync` devuelve `AvailableRoles`+`ActiveRolId`;
  nuevo `SwitchRolAsync`; `RefreshAsync` conserva `rol_id`); `AuthController` nuevo `POST /connect/switch-rol`.
- `RolesService` + filtros `RequiereRol`/`RequierePermiso` resuelven contra el rol activo validado.
  **Admin bypass SOLO si el rol activo es Administrador.** Fallback al rol principal si el token no trae
  `rol_id` (compat tokens viejos / usuarios mono-rol).
- Front: `RolActivoGate` (modal bloqueante "elige tu rol" si hay >1 rol sin elegir, cubre login + cambio de
  copropiedad + sesiones viejas) + `UserMenu` muestra/cambia el rol activo.
- **Compatibilidad:** aditivo. Tokens viejos siguen funcionando (caen al rol principal). Usuarios mono-rol
  no ven el gate. No hay breaking de comportamiento para quien tiene un solo rol.

**2) Unidades - eliminar el concepto Torre (`077bd6b3` + pulido `0ab9dbd7`) [1 MIGRACION DESTRUCTIVA]**
- Se elimina del todo la entidad/tabla/columna/FK `Torre`. **La unidad se identifica solo por su codigo**
  (texto libre en `UnidadPrivada.Numero`). Se quito el CRUD `/torres` + las tools MCP `CrearTorre`/`ListarTorres`,
  y `TorreId`/`TorreNombre` de los DTOs de Unidad + Cartera/Presupuesto/Dashboard/PQRSD. El generador y los
  importadores (CSV `agrupacion`, plantilla xlsx hoja "Torres") pasan a **prefijo-en-codigo** (no crean torres).
- **Validacion de duplicado** (lo que origino el cambio): al crear/renombrar una unidad se rechaza el codigo
  ya existente en la copropiedad, SIN distinguir mayusculas ni tildes (`NormalizarCodigo` + `ValidarCodigoUnicoAsync`),
  con mensaje "Ya existe la unidad 'X' en esta copropiedad." El indice unico `(tenant_id, numero)` queda de respaldo.
- **Migracion `20261007214045`:** (a) **hornea** el codigo efectivo en `numero` para las unidades que tenian
  torre (`<ultima palabra del nombre de la torre>-<numero>`, ej. `A1-101`) y desambigua duplicados por tenant
  (sufijo `-2`, `-3`...); (b) DROP FK + DROP tabla `torres` + DROP columna `torre_id`. El horneado **NO es
  reversible** (ver Down()).
- **Compatibilidad / breaking:** se eliminaron los endpoints `/torres` y las tools MCP de torres -> cualquier
  consumidor externo de esas rutas recibiria 404 (no se espera ninguno). La columna "Agrupacion por torre"
  desaparece de la tabla de unidades (la columna Principal/Individual = anexo/principal se conserva).

## Migraciones (APLICAR EN PROD, owner `propia`)
**1 migracion nueva:** `20261007214045_RemoveTorreDeUnidades` - DESTRUCTIVA (hornea codigo + DROP de `torres`
y `torre_id`). Aplicada y verificada en un PostgreSQL real por los tests de integracion (Testcontainers).
**NO** se aplico a la BD dev compartida del equipo (coordinacion). Comando:
```
cd src/Propia.Api
"$USERPROFILE/.dotnet/tools/dotnet-ef.exe" database update --project ../Propia.Infrastructure --startup-project .
```
`database update` aplica TODAS las pendientes: si prod viene por detras, aplica tambien `20261007135437`
(multi-rol, de v0.0.111) antes que esta. Orden correcto garantizado por EF.

### Orden de release (CRITICO por ser destructiva)
1. **Primero** desplegar el codigo nuevo (0.0.112) y que quede vivo. El codigo nuevo NO consulta `torres`.
2. **Despues** aplicar la migracion (`database update`). Si se aplica ANTES de que el codigo nuevo este
   arriba, el codigo viejo (0.0.111) que SI consulta `torres`/`torre_id` empezaria a dar 500.
   En Railway el flujo normal (build+release del nuevo artefacto, luego migracion) respeta esto.
3. Idealmente, aviso al equipo antes de aplicarla en cualquier BD compartida.

**3) Cosmetico - codigo de unidad en violeta (`f2cb9892`) [SIN migracion]**
- El codigo de unidad se muestra en violeta de marca (`var(--propia-brand-text)`, peso 700, igual que el
  modulo Residentes) en TODOS los modulos que lo muestran: Unidades (tabla+lista), Mascotas, Vehiculos,
  Cartera + ficha, Presupuesto, PQRSD, Reservas, Porteria, Asamblea y el selector de unidad compartido.
- Solo CSS/markup (sin hex, usa el token). **Cache-bust:** `App.razor` sube `pqrs.css?v=17` y
  `porteria.css?v=2` (se editaron esos 2 CSS de modulo). Verificado en runtime: color computado
  `rgb(75,43,176)` = `--propia-brand-text`.

## Config / arranque
- Sin configuracion nueva. El seeder del catalogo (`usuario.cargo`) ya venia de v0.0.111.
- No hay variables de entorno nuevas.

## Verificacion hecha (dev)
- `dotnet build Propia.sln` -> 0 errores, sin warnings nuevos.
- Integracion (Testcontainers, PostgreSQL real): MiCopropiedad 19/19 (incluye el test nuevo de codigo
  duplicado y el del generador con prefijo), Cartera/Presupuesto/Rbac 16/16, Asamblea/Campos/Plantillas 33/33,
  Application 19/19. **Todo verde.**
- E2E B2b (Chrome): usuario multi-rol -> gate obliga a elegir; rol no-admin -> permisos caen + escritura
  gateada 403 (sin bypass); volver a Administrador -> bypass restaurado; switch-rol a rol no-propio -> 403.
- E2E Torre (Chrome): crear `a1-102`/`A1-102` con `A1-102` existente -> rechazado con mensaje limpio;
  codigo nuevo real se crea; **todos los modulos afectados cargan sin error** (Mi Copropiedad, Distribucion,
  Residentes/Mascotas/Vehiculos, Cartera, Presupuesto, PQRSD, Tareas, Reservas + selector de unidad por
  codigo, Dashboard, GlobalSearch). Log del servidor: 0 Error/Fatal, 0 excepciones, 0 SQL a `torres`.

## Humo post-deploy
- [ ] Desplegar codigo nuevo ANTES de aplicar la migracion (ver "Orden de release").
- [ ] Aplicar `database update` (aplica `20261007214045`, y `20261007135437` si prod venia por detras).
- [ ] Pie de la app en `v0.0.112`.
- [ ] /usuarios: login de un usuario con 2+ roles -> aparece el gate "elige tu rol"; con un solo rol no.
- [ ] /distribucion: crear una unidad con un codigo que ya existe (otra combinacion de mayusculas) -> se
      rechaza con "Ya existe la unidad '...'"; crear un codigo nuevo -> se crea. No hay columna de torre.
- [ ] Residentes / Cartera / Presupuesto / PQRSD / Tareas / Dashboard cargan sin error.

## Rollback
- **B2b** es aditivo: redeploy de 0.0.111 lo revierte sin tocar datos.
- **Torre** es DESTRUCTIVO: la migracion elimina `torres`/`torre_id` y hornea los codigos. Preferir
  **fix-forward**. Si hay que volver: redeploy del artefacto 0.0.111 + ejecutar el `Down()` de
  `20261007214045` (recrea la estructura `torres`/`torre_id` vacia, con `torre_id` null), pero los `numero`
  ya horneados NO se des-hornean (el codigo viejo los mostraria con el prefijo incluido). No revertir el
  esquema a mano.

---
Generado 2026-10-07 (v0.0.112). Companion: `DEPLOY_HANDOFF_2026-10-07_v0.0.111.md`, `HANDOFF_DEPLOY.md`.
