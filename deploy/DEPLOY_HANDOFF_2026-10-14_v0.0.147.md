# DEPLOY HANDOFF - PROPIA v0.0.147 (2026-10-14)

> **LEEME PRIMERO.** Handoff autocontenido. **Solo codigo (sin migracion, sin config).** Tanda grande de
> homogenizacion de la vista-tabla + arreglos de Usuarios/Roles.

## Resumen de 1 linea
Homogenizacion vista-tabla: Filtros/Agrupar con TODOS los campos (incl. dinamicos) en todos los modulos,
expander del alta = crear+abrir ficha en todos, y arreglos de Usuarios/Roles. **Sin migracion.**

## Que desplegar
- Rama: `main`, HEAD **`0969b57f`**.
- Version visible: **0.0.147** (`src/Propia.Web/Propia.Web.csproj <Version>`).
- Repo: https://github.com/alexandercuartas665/PROPIA

## Migraciones / BD  (NO, no aplicar)
**Ninguna migracion.** De 0.0.146 a 0.0.147 no hay cambios de esquema ni funciones SQL.
(La ultima migracion sigue siendo `20261013214928_AddUsuarioTenantArchivado`, de la tanda 0.0.145.)

## Config / env / secrets
**Sin cambios obligatorios.** (Recordatorio de tandas previas: para que las imagenes subidas persistan en
prod conviene activar storage R2 - `Storage__Provider=R2` + credenciales; independiente de esta tanda.)

## Cambios de codigo (9 commits, 16 archivos, +780/-471)
1. **Roles/Usuarios** (`072758ce`): editor de roles deja arriba solo Nombre + Categoria (quita la "Semilla en
   la Distribucion"); el modal "Editar usuario" pasa a **multi-rol** (chips + menu), persiste via `PUT /api/usuarios/{id}/roles`.
2. **Usuarios alta** (`96497b4c`): el alta inline exige **rol** (antes sin rol usaba "Portal Residente" solo-directorio
   y el usuario quedaba OCULTO). Se quita el lapiz de editar por fila (se abre con el expander/nombre). Filtros/Agrupar
   de Usuarios con todos los campos + dinamicos.
3. **Filtros dinamicos** (`b1a26a72`): el boton Filtros ahora incluye los campos propios (EAV "cd:") en 7 modulos
   (Distribucion, Directorio, Zonas, Equipos, Residentes, Mascotas, Vehiculos) + Directorio Agrupar dinamico.
   Helper compartido nuevo `FiltroDinamico.DeDinamico`.
4. **Agrupar = Ordenar/Columnas** (`1422cc78`, `32703bc2`, `189cfb55`): el boton Agrupar ofrece TODAS las columnas
   (fijas + dinamicas), igual que Ordenar/Columnas, en los 13 modulos vista-tabla (incl. Mantenimiento Prog+Interv).
5. **Expander del alta = crear + abrir ficha** (`57cb9a64`, `4a97b3d6`, `0969b57f`): en Usuarios (abre el modal),
   Roles (abre la matriz de permisos) y Vehiculos/Mascotas (se construyo una **ficha simple** del registro).

## Pasos de deploy
1. `git fetch && git checkout main && git pull` (quedar en `0969b57f` o posterior).
2. **No hay migracion que aplicar.**
3. Build/publish Release del artefacto y desplegar.
4. Verificar version en el pie/login: **v0.0.147**.

## Verificacion post-deploy
- **Usuarios** (`/usuarios`): agregar en la fila inline y salir con Tab -> aparece (pide rol); el expander del
  alta crea + abre el modal. Editar un usuario -> Rol es multi-select. No hay lapiz de editar por fila.
- **Roles** (`/roles`): editar un rol -> arriba solo Nombre + Categoria. El expander del alta crea el rol + abre su matriz.
- **Filtros/Agrupar**: en cualquier vista-tabla con campos propios (p.ej. `/distribucion`), Filtros y Agrupar
  listan esos campos; Agrupar lista todas las columnas igual que Ordenar.
- **Vehiculos/Mascotas**: el expander (fila o alta) abre la ficha del registro.

## Rollback
Redeploy del artefacto 0.0.146. **No requiere tocar la BD** (sin migracion en esta tanda).
