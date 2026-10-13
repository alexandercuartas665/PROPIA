# DEPLOY HANDOFF - PROPIA v0.0.145 (2026-10-13)

> **LEEME PRIMERO.** Handoff autocontenido. Fuente de verdad acumulada: `HANDOFF_DEPLOY.md`
> (seccion "ESTADO 2026-10-13 Selector: vista Archivadas").

## Resumen de 1 linea
Vista "Archivadas" en el selector "Mis copropiedades" (archivar/activar copropiedades POR USUARIO). **Lleva 1 migracion.**

## Que desplegar
- Rama: `main`, HEAD **`3c1e864a`**.
- Version visible: **0.0.145** (`src/Propia.Web/Propia.Web.csproj <Version>`).
- Repo: https://github.com/alexandercuartas665/PROPIA

## Migraciones / BD  (SI, aplicar)
- **`20261013214928_AddUsuarioTenantArchivado`**:
  - Columna `usuarios_tenant.archivado boolean not null default false` (TenantEntity; RLS existente la cubre).
  - DROP + CREATE de la funcion `get_tenants_for_persona(uuid)` para devolver tambien `archivado` y `ultimo_acceso`.
  - CREATE de la funcion `set_usuario_tenant_archivado(uuid, uuid, boolean)` (SECURITY DEFINER, row_security off).
  - Reversible. **Importante: el CODIGO 0.0.145 espera la nueva firma de `get_tenants_for_persona`** (lee 6
    columnas), asi que codigo y migracion van JUNTOS (no desplegar el codigo sin aplicar la migracion).
- Aplicar: `cd src/Propia.Api && dotnet ef database update --project ../Propia.Infrastructure --startup-project .`
  (owner `propia`).
> Prod ya esta en 0.0.141 (migracion `AddOrganizacionLimiteCopropiedades` ya aplicada). De 0.0.141 a 0.0.145 la
> UNICA migracion es esta (`AddUsuarioTenantArchivado`); las tandas 0.0.142-0.0.144 no llevaban migracion.

## Config / env / secrets
**Sin cambios.**

## Cambios de codigo
- `UsuarioTenant.cs` (+`Archivado bool`). Migracion arriba (columna + 2 funciones SQL).
- `TenantInfo` (Application) / `TenantInfoDto` (Shared) += `Archivado`, `UltimoAcceso`. `AuthService.LoadAvailableTenantsAsync`
  lee las columnas nuevas de la funcion. (GoogleSignInService solo lee 2 columnas -> no cambia.)
- `MisCopropiedadesController.cs`: `PUT ~/api/mis-copropiedades/{tenantId}/archivar` y `/desarchivar`.
- `IMisCopropiedadesService`/`MisCopropiedadesService.cs`: `ArchivarAsync` (via `set_usuario_tenant_archivado`).
- `CopropiedadSwitcher.razor`: 3 vistas (Galeria/Tabla/Archivadas), icono archivar en tarjetas + boton en tabla,
  Activar en Archivadas, Tabla sin Rol y con "Ultimo ingreso", CSS.

## Pasos de deploy
1. `git fetch && git checkout main && git pull` (quedar en `3c1e864a` o posterior).
2. **Aplicar la migracion** en la BD de prod (ver arriba). Codigo + migracion JUNTOS.
3. Build/publish Release del artefacto y desplegar.
4. Verificar version en el pie/login: **v0.0.145**.

## Verificacion post-deploy
- Abrir el selector "Mis copropiedades" (tocar el nombre de la copropiedad arriba): aparecen 3 vistas
  (Galeria, Tabla, **Archivadas**).
- En Galeria, pasar el mouse sobre una tarjeta -> aparece el icono de archivar; archivar -> la copropiedad
  sale de Galeria/Tabla y aparece en **Archivadas** (con contador en el toggle).
- En Archivadas -> **Activar** -> la copropiedad vuelve a la lista.
- En la vista **Tabla**: columnas Copropiedad | Codigo | Ultimo ingreso | Archivar (sin Rol).
- Confirmar que es por-usuario: otro usuario de la misma copropiedad la sigue viendo normal.

## Rollback
Redeploy del artefacto previo **junto con** `database update` a la migracion anterior (el codigo nuevo depende de
la nueva firma de `get_tenants_for_persona`; no dejar el codigo viejo con la funcion de 6 columnas por mucho que
no rompa). La columna `archivado` es aditiva e inerte para el codigo viejo.
