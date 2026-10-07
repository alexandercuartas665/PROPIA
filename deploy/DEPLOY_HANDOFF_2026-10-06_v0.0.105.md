# HAND-OFF DEPLOY - PROPIA (2026-10-06) - v0.0.105

> Sucede a `DEPLOY_HANDOFF_2026-10-06_v0.0.104.md`. Detalle historico en `HANDOFF_DEPLOY.md`.

## Objetivo del deploy

- **Rama / HEAD:** `main` (tras merge de `equipo/alex-header-zonas-equipos`)
- **Version:** `0.0.105` (sube desde `0.0.104`)
- **Repo:** https://github.com/alexandercuartas665/PROPIA (rama `main`)
- **Mecanismo:** auto-deploy desde `main` (Railway).
- **SIN MIGRACIONES. SIN cambios de config/arranque.** Cambios de UI (frontend) + backend menor (un
  metodo/endpoint nuevo en el catalogo). Nada que aplicar en BD.

## Que entra en esta tanda (1 commit sobre v0.0.104)

**`4b1996e` - Catalogo: Unidades 'tipo' respeta el 'activo' global + boton Re-sembrar en A&D**

1. **Fix (bug de prod):** ocultar un tipo de unidad en la consola A&D (`/admin/catalogos`) NO lo quitaba
   de los selectores de los tenants. El universo de 'tipo' salia del enum y el catalogo global solo
   sobreescribia el label; el flag `activo` nunca se consultaba (a diferencia de 'estado', que ya leia del
   catalogo). Ahora `DistribucionPanel` compone `TiposBaseUniverso` = enum filtrado por las claves ACTIVAS
   del catalogo global; si el catalogo no cargo, cae al enum completo (fallback). Las unidades que ya usan
   un tipo desactivado lo conservan (`TiposBaseConVigente`). La validacion de nombre de tipo propio sigue
   usando el enum completo (anti-colision). Como `catalogo_opciones` es GLOBAL (sin copia por tenant),
   ocultar en A&D aplica a TODOS los tenants (salvo el TTL de 30s del cache por proceso).

2. **Feature:** boton **"Re-sembrar listas base"** en `/admin/catalogos`. Reinserta las opciones de fabrica
   que FALTEN en todas las listas (idempotente; no pisa lo editado) e invalida el cache del lector. Util
   para restaurar una opcion base borrada o registrar listas nuevas sin reiniciar. Seeder refactorizado:
   `SembrarAsync(db)` reutilizable devuelve el conteo; `EnsureAsync` (arranque) lo envuelve. Endpoint
   `POST /api/admin/catalogos/resembrar` gateado por SuperAdmin.

**Version:** bump `0.0.104 -> 0.0.105` (csproj `Propia.Web`).

## Archivos tocados (6 + csproj)

```
src/Propia.Web/Propia.Web.csproj                                      (Version 0.0.105)
src/Propia.Web/Components/Pages/Capa2/DistribucionPanel.razor         (TiposBaseUniverso: respeta 'activo' global)
src/Propia.Infrastructure/Catalogos/CatalogoListasSeeder.cs           (SembrarAsync reutilizable + conteo)
src/Propia.Application/Catalogos/ICatalogoListasAdmin.cs              (ResembrarAsync)
src/Propia.Infrastructure/Catalogos/CatalogoListasAdminService.cs     (ResembrarAsync impl)
src/Propia.Api/Controllers/AdminCatalogosController.cs                (POST /resembrar)
src/Propia.Web/Components/Pages/Capa0/Admin/Catalogos.razor          (boton Re-sembrar)
```

## Migraciones
**NINGUNA.** (Si prod viene por detras, el `database update` aplica en orden las de tandas previas.)

## Verificacion ya hecha (dev, tenant demo)
- Con "Cama" desactivada en A&D, ya NO aparece en ninguno de los 279 selectores de tipo de Unidades
  (`/distribucion`), incluida la fila de alta. Un tipo activo si aparece.
- Endpoint `/api/admin/catalogos/resembrar` registrado y gateado (401 sin token SuperAdmin valido, no 404).
- **NO probado end-to-end el clic del boton Re-sembrar** (la sesion de la consola A&D de dev habia expirado;
  no se ingresan credenciales del founder). La logica (`SembrarAsync`) es la misma del arranque, que corrio
  limpio en este deploy. Confirmar el clic en post-deploy.

## Verificacion post-deploy (humo)
- [ ] `/admin/catalogos`: desactivar un tipo de unidad -> en una copropiedad, ese tipo desaparece del alta/
      edicion de Unidades (esperar ~30s por el cache); un tipo activo sigue apareciendo; una unidad que ya
      lo usaba conserva su tipo.
- [ ] `/admin/catalogos`: boton "Re-sembrar listas base" -> mensaje de resultado (0 si no falta ninguna).
- [ ] Pie de la app muestra `v0.0.105`.

## Rollback
- Redeploy del artefacto anterior (v0.0.104). Sin migraciones; rollback limpio.

---
Generado 2026-10-06. Companions: `HANDOFF_DEPLOY.md`, `DEPLOY_HANDOFF_2026-10-06_v0.0.104.md`.
