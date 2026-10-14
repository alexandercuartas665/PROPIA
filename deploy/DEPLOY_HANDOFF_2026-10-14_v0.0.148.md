# DEPLOY HANDOFF - PROPIA v0.0.148 (2026-10-14)

> **LEEME PRIMERO.** Handoff autocontenido. **Solo codigo (sin migracion, sin config).** Homogenizacion del
> menu de columna "Editar campo" en toda vista-tabla.

## Resumen de 1 linea
El "Editar campo" de la cabecera de columna abre SIEMPRE el mismo componente canonico, con el tipo de columna
correcto (texto / lista editable / lista de solo lectura). Cierra Usuarios, Roles y Residentes. **Sin migracion.**

## Que desplegar
- Rama: `main`, HEAD **`cfe40785`**.
- Version visible: **0.0.148** (`src/Propia.Web/Propia.Web.csproj <Version>`).
- Repo: https://github.com/alexandercuartas665/PROPIA

## Migraciones / BD  (NO, no aplicar)
**Ninguna migracion.** De 0.0.147 a 0.0.148 no hay cambios de esquema ni funciones SQL.
(La ultima migracion sigue siendo `20261013214928_AddUsuarioTenantArchivado`.)

## Config / env / secrets
**Sin cambios.** (Recordatorio de tandas previas: para que las imagenes subidas persistan en prod conviene
activar storage R2 - `Storage__Provider=R2` + credenciales; independiente de esta tanda.)

## Cambios de codigo (1 commit, 5 archivos, +87/-21)
Commit `cfe40785` - "Editar campo: canon reutilizable en toda vista-tabla".

Canon: el menu de columna "Editar campo" abre SIEMPRE `EditarCampoSistemaModal`, con 3 tipos de columna de
sistema: **A** no-lista (alias/descripcion/visibilidad), **B** lista blanda (semillas del catalogo global A&D +
agregar por copropiedad), **C** lista-excepcion (SOLO LECTURA desde su tabla/enum; gobierna funcionalidad).

1. **`EditarCampoSistemaModal.razor`**: nuevo parametro `HintReadOnly` (antes el aviso de lista de solo lectura
   estaba fijo a "consola A&D / Super Admin"); ahora cada excepcion indica su fuente real.
2. **`UsuariosPanel.razor`**: TODAS las columnas de sistema abren el modal unico (se elimino el salto al gestor
   generico `_gestorCamposAbierto`). Se unificaron 3 bloques `@if` en uno solo con helpers. `rol` y `estado` =
   **C read-only** (tabla roles / maquina de estado `EstadoUsuarioTenant`); `cargo` = B editable; resto = A.
3. **`RolesPanel.razor`**: todas las columnas abren el modal unico (antes solo `categoria`; el resto no hacia
   nada). `categoria` pasa a **C read-only**: `CategoriaRol` es un enum cerrado ("Protegida" por el plan de
   listas; no admite valores custom). El `<select>` de alta/edicion de rol sigue usando el enum (sin regresion).
4. **`ResidentesPanel.razor`**: la columna `rol` (relacion con la unidad, enum `RolUnidadPersona`) = **C read-only**.

> Seguros NO tiene columnas de sistema tipo lista (aseguradora/corredor son terceros del Directorio): su
> "Editar campo" ya era correcto, sin cambios.

## Pasos de deploy
1. `git fetch && git checkout main && git pull` (quedar en `cfe40785` o posterior).
2. **No hay migracion que aplicar.**
3. Build/publish Release del artefacto y desplegar.
4. Verificar version en el pie/login: **v0.0.148**.

## Verificacion post-deploy
- **Usuarios** (`/usuarios`): en el menu de cualquier columna -> "Editar campo" abre el modal focalizado (ya
  NO el gestor "Campos de usuarios"). En `Rol` y `Estado`: lista en solo lectura (sin "Anadir"); en `Cargo`:
  lista con "Anadir"; en `Correo`/`Documento`: solo alias/descripcion/visibilidad.
- **Roles** (`/roles`): "Editar campo" de `Categoria` muestra las 4 categorias en SOLO LECTURA (sin "Anadir");
  `Rol` (nombre) abre el modal (antes no hacia nada). Crear/editar rol sigue ofreciendo las categorias normalmente.
- **Residentes** (`/residentes`): "Editar campo" de `Rol` muestra Propietario/Residente/Familiar/Arrendatario/
  Apoderado en solo lectura.
- **Equipos/Zonas/Vehiculos/Directorio** (regresion): sus listas de sistema siguen en solo lectura con el aviso
  de catalogo A&D.

## Rollback
Redeploy del artefacto 0.0.147. **No requiere tocar la BD** (sin migracion en esta tanda).
