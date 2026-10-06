# HAND-OFF DEPLOY - PROPIA (2026-10-05)

> Portada de ESTE deploy. Detalle historico en `HANDOFF_DEPLOY.md` (companion en esta carpeta),
> paso-a-paso en `DEPLOY_CHECKLIST.md`, backlog por olas en `PLAN_OLAS.md`.
> Reemplaza como portada vigente al `DEPLOY_HANDOFF_2026-10-03.md` (se deja por historia).

## Objetivo del deploy

- **Rama / HEAD:** `main` @ `acbfe54`
- **Version:** `0.0.101`
- **Repo:** https://github.com/alexandercuartas665/PROPIA (rama `main`)
- **Mecanismo:** auto-deploy desde `main` (Railway). Al estar `main` en `acbfe54`, prod toma ese commit.
- **Base anterior:** deploy 2026-10-03 era `main` @ `2742ddc` (v0.0.99). Esta tanda suma ~50 commits.
- **NOTA:** esta portada se actualizo durante el 2026-10-05 de `288ebbb` (v0.0.100) a `acbfe54` (v0.0.101)
  para sumar B-1, E-1 y el rework de alta inline de Usuarios/Directorio + expander (ver seccion de abajo).
  NO agrega migraciones.

## Que entra en esta tanda (commits nuevos sobre `2742ddc`)

**Homogeneidad header / vista-tabla (pre-FAB):**
- `f7c4002`, `f025bad` (bump version 0.0.99 -> 0.0.100), `81f6390`, `3e86e65`, `cc8691d`, `cc1b0ff`,
  `7d166d2`, `73d381b`, `cb97412` - chevron del header al borde (1:1 Unidades) en Zonas/Equipos/
  Vehiculos/Mascotas/Residentes/Grupo A/Grupo B; boton "+" agregar campo; tabla a ancho completo en
  Usuarios/Roles/Terceros; fix hueco interno en Usuarios Activos.

**FAB flotante + scroll infinito (canon Unidades) en las 5 vistas-tabla que faltaban:**
- `388fcf4` Roles, `5a20835` Usuarios (tabla), `e91e4d1` Seguros, `8800a94` Contratos, `5269f5a` PQRSD.
  (Reemplazan el paginador por render-all + footer de conteo + FAB brand que aparece al desplazar.
  En PQRSD el FAB abre el wizard de radicacion.)

**Ola 1 - Seguridad / bloqueos funcionales:**
- `41afa7c` **D-1**: alta de unidad duplicada -> error de negocio limpio (400) en vez de 500/stack;
  handler global de excepciones responde SIEMPRE JSON sin stack (tambien en Development).
- `0ccc903` **V-1**: el dropdown de unidad (SelectorUnidadCodigo) flota (position:fixed) y ya no lo
  recorta el overflow del scroll (afecta Vehiculos, Mascotas, PQRSD modales, Reservas, Porteria).
- `97202cd` **T-1**: se puede editar una tarea cuyo solicitante/asignado no esta en el Directorio
  (la validacion de pertenencia solo corre cuando ese responsable CAMBIA).

**Ola 3 - Funcionalidad faltante + UX menores:**
- `de3e1f9` **S-2**: "Eliminar poliza" en el modal ficha con confirmacion (DELETE ya existia).
- `e9cedad` **T-2**: el banner de error de Tareas muestra el texto, no el JSON crudo.
- `64fafba` **C-1**: el modal de borrar contrato identifica al contratista (proveedor + numero).
- `f1197b8` **R-1**: `@key` en filas de Residentes (evita reordenar/duplicar al editar inline).
- `16e3a0d` **CM-1**: cancelar el wizard de comunicados recarga la lista (evita lista stale).
- `7ae3062` **RL-1**: confirmacion antes de eliminar un rol personalizado.

**Tests:**
- `6537d1f` **B-4**: `UsuariosAccesosFlowTests` 12/12 verde (catalogo canonico 7 base / 0 extendidos
  globales tras Roles V2; + 2 rojos preexistentes del baseline corregidos).

**Agregado despues de `288ebbb` (hasta `acbfe54`, mismo dia) - UI/servicio/tests, SIN migraciones:**
- `41f1a9d` bump de version `0.0.100` -> `0.0.101`.
- `c14e002` / `90642ee` **B-1**: se exoneran del contrato RLS por-tenant 8 tablas globales/plataforma
  (BaseEntity con `tenant_id` descriptivo, no operativo) con justificacion; `RlsCoverageTests` 2/2 verde.
- `b75cf7a` **E-1** (infra DEV): fija el host de la BD dev a `127.0.0.1` (quita ambiguedad `::1`/wslrelay).
  Solo afecta la cadena de conexion de desarrollo; NO cambia prod.
- **Alta inline de Usuarios y Directorio al canon Unidades** (`001a071`, `f96a45d`, `3a80850`, `f344cdd`,
  `f78ce3e`, `a0daf30`, `86dd80b`, `975ddbc`, `acbfe54`):
  - Inputs de la fila de alta al canon (borde transparente en reposo, foco violeta con glow), botones
    guardar/cancelar con iconos check/x, CTA colapsable + FAB que abre la fila (Usuarios tabla y
    Directorio Personas/Empresas).
  - Usuarios: rol basico creable inline desde el alta (como "agregar tipo" en Unidades); un solo campo
    "Nombre completo" (se divide al invitar); columnas "Tipo doc" y "Documento" separadas; anchos de
    columna rebalanceados para que header/fila/alta alineen; **columna expander** (abre la ficha en filas
    de datos; confirma el alta en la fila nueva). `UsuarioListaDto` ahora expone `TipoDocumento` (campo
    ya existente en `personas`, sin cambio de esquema).
  - Directorio ya tenia verde + expander; solo se homogenizo el alta (Olas A/B/C).

Todo validado en runtime (Chrome / API, tenant demo). Solo ASCII en codigo; colores por tokens.

## Migraciones

- **ESTA tanda NO agrega migraciones nuevas** (verificado: `git diff 2742ddc..288ebbb -- .../Migrations`
  = vacio). Todo lo nuevo es UI / servicio / tests.
- **IMPORTANTE (runtime depende de Roles V2):** el comportamiento de Roles (7 roles base, Portal
  Residente, etc.) y el test B-4 asumen que la **data migration D2 ya esta aplicada**. Si prod aun no
  la tiene, aplicarla en esta ventana (ver abajo, con OK de Alex).
- **Pendientes de tandas previas ya en `main`** (aplicar en prod solo si prod viene por detras):
  - `20260927131731_AddTableroEsGeneral`
  - `20260928161525_AddDescripcionCampoConfig`
  - `20260929212521_Modulo25V2_RolCategoria_MatrizPorTenant` (aditiva: `roles_copropiedad.categoria`
    + tabla `rol_permisos_tenant` con RLS)
  - `20260930021045_Modulo25V2_D2_ReseedRolesBase`  **<-- DATA MIGRATION SENSIBLE: re-siembra los 7
    roles base y remapea usuarios. Aplicar SOLO con OK explicito de Alex.**
  - `20260930121006_AddPolizaReclamacionComentario`
  - `20260930123455_AddContratoContratistaContacto`
  - Comando (owner `propia`, design-time factory):
    ```
    cd src/Propia.Api
    "$USERPROFILE/.dotnet/tools/dotnet-ef.exe" database update --project ../Propia.Infrastructure --startup-project .
    ```

## ACCION MANUAL EN PROD: mover "Roles" a la seccion Configuracion del menu

(Sigue PENDIENTE de la tanda anterior; el menu real lo arma `menu_overrides`, tabla GLOBAL sin
tenant_id. Script standalone en `menu-roles-prod.sql` en esta carpeta.)

1. Ver el id real de la seccion Configuracion en prod:
   ```sql
   SELECT node_key, label FROM menu_overrides WHERE label ILIKE 'config%' AND is_custom = true;
   ```
2. Con ese `<ID_CONFIG>` (ajustar `sort_order` si choca):
   ```sql
   BEGIN;
   UPDATE menu_overrides SET label='Usuarios', updated_at=now() WHERE node_key='mi-usuarios';
   UPDATE menu_overrides SET sort_order=6, updated_at=now() WHERE node_key='mi-directorio';
   INSERT INTO menu_overrides (id, node_key, label, parent_key, sort_order, created_at, is_custom, hidden)
   VALUES (gen_random_uuid(), 'mi-roles', NULL, '<ID_CONFIG>', 5, now(), false, false);
   COMMIT;
   ```
   (En dev el id fue `custom-sec-3954c8fdc250481f9197e077ba2c6893`; quedo Usuarios(4) -> Roles(5) -> Terceros(6).)
   Si NO se corre: "Roles" igual funciona, pero bajo "Mi Copropiedad", no en Configuracion.

## Verificacion post-deploy (humo)

Vista-tabla (Roles, Usuarios/tabla, Seguros, Contratos, PQRSD):
- [ ] Sin paginador; footer con el conteo; FAB flotante brand abajo-izquierda que aparece al desplazar
      y se oculta al llegar a la fila de alta. En PQRSD el FAB abre "Radicar nueva PQRSD".

Ola 1:
- [ ] Mi Copropiedad: crear una unidad con numero repetido -> mensaje "Ya existe una unidad con el
      numero X" (400), NO pantalla de error/500.
- [ ] Vehiculos/Mascotas: el desplegable de UNIDAD en la fila de alta se ve completo (no recortado).
- [ ] Tareas: editar una tarea creada por el admin -> guarda (no "la persona no pertenece...").

Ola 3:
- [ ] Seguros: abrir una poliza -> boton "Eliminar" -> confirma -> desaparece de la lista.
- [ ] Contratos: borrar un contrato -> el modal nombra al contratista.
- [ ] Roles: eliminar un rol personalizado -> pide confirmacion.
- [ ] Comunicaciones: abrir y cancelar el wizard -> la lista queda consistente.

Alta inline (Usuarios / Directorio):
- [ ] Usuarios (tabla): la fila de alta nace colapsada (CTA "+ Agregar registro"); el FAB flotante la
      abre; header/fila/alta alineados; columnas "Tipo doc" y "Documento" separadas; el expander (flecha
      diagonal) abre la ficha en filas existentes.
- [ ] Usuarios (alta): un solo campo "Nombre completo"; se puede crear un rol basico inline con el "+".
- [ ] Directorio (Personas/Empresas): fila de alta al canon (inputs fantasma, guardar check/x, FAB).
- [ ] Carga Excel de Unidades: importar la plantilla y verificar que la columna COEFICIENTE entra con sus
      decimales (p.ej. 1,25 -> 1.2500). (Nota: la BD guarda 4 decimales; ver deuda abajo.)

## Rollback

- Redeploy del artefacto anterior (ultimo bueno en prod). Esta tanda es UI/servicio/tests SIN esquema
  nuevo, asi que volver no requiere revertir migraciones de esta tanda.
- El override de menu es dato: para revertir, borrar la fila `mi-roles` de `menu_overrides` y restaurar
  `mi-usuarios.label` / `mi-directorio.sort_order`.
- D2 (si se aplico en esta ventana) NO es auto-reversible (remapea usuarios); su rollback esta en el
  `Down` de la migracion pero pierde la distincion Propietario/Residente. Coordinar con Alex.

## Deudas conocidas (NO bloquean este deploy)

- **Coeficiente de unidad: escala `numeric(7,4)` (4 decimales).** El import de Excel lee y guarda bien
  (verificado end-to-end el 2026-10-05: 1,25/1,3/2/3/0,5 -> correctos), pero la columna solo retiene 4
  decimales mientras la ayuda de la plantilla dice "Max 5 decimales". Coeficientes con 5+ decimales se
  redondean; uno `< 0,00005` se volveria 0. Si se manejan coeficientes fraccionarios finos, decidir con
  Alex migrar a `numeric(9,6)` + alinear la ayuda. (Pendiente, requiere migracion.)
- **El import NO llena la tabla multi-tipo `UnidadCoeficiente`** (solo el campo legacy
  `UnidadPrivada.CoeficientePropiedad`, que es el que lee la grilla y la ficha). Si algun panel de "tipos
  de coeficiente" lee de la multi-tipo, las unidades importadas se veran vacias ahi.

---
Generado 2026-10-05 (actualizado a `acbfe54` / v0.0.101). Companions: `HANDOFF_DEPLOY.md`,
`DEPLOY_CHECKLIST.md`, `PLAN_OLAS.md`, `menu-roles-prod.sql`.
