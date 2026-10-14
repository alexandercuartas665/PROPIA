# DEPLOY HANDOFF - PROPIA v0.0.146 (2026-10-13)

> **LEEME PRIMERO.** Handoff autocontenido. Fuente de verdad acumulada: `HANDOFF_DEPLOY.md`
> (seccion "ESTADO 2026-10-13 Subida de imagenes Identidad").

## Resumen de 1 linea
Arregla la subida de imagenes (logo/fachada) de la Identidad de la copropiedad + tarjeta drag & drop.
**Solo codigo (sin migracion, sin config).**

## Que desplegar
- Rama: `main`, HEAD **`0e64eaf4`**.
- Version visible: **0.0.146** (`src/Propia.Web/Propia.Web.csproj <Version>`).
- Repo: https://github.com/alexandercuartas665/PROPIA

## Migraciones / BD  (NO, no aplicar)
**Ninguna migracion.** De 0.0.145 a 0.0.146 no hay cambios de esquema ni de funciones SQL.
(La ultima migracion sigue siendo `20261013214928_AddUsuarioTenantArchivado`, de la tanda 0.0.145.)

## Config / env / secrets
**Sin cambios obligatorios** para que compile/arranque.

> **Recomendado para prod (persistencia de imagenes):** el bug de subida ya esta corregido, pero en prod las
> imagenes subidas se guardan con `LocalBlobStorage` (filesystem efimero en Railway) y **se pierden en cada
> redeploy**. Para que persistan, activar el storage R2 (Cloudflare):
> - `Storage__Provider=R2`
> - `Storage__R2__AccountId`, `Storage__R2__AccessKeyId`, `Storage__R2__SecretAccessKey`,
>   `Storage__R2__BucketName`, `Storage__R2__PublicUrl`.
> Es independiente de este fix (puede ir en una tanda aparte); sin R2 las subidas funcionan pero no sobreviven
> al siguiente deploy.

## Cambios de codigo (6 archivos, +96/-22)
- **CAUSA RAIZ del 401 al subir:** en Blazor un parametro de componente tipo `string` pasado como
  `Attr="_jwt"` (sin `@`) se toma como el **literal** `"_jwt"`, no como el campo. `IdentidadForm` recibia el
  literal y enviaba `Bearer _jwt` al API -> `IDX14100: JWT is not well formed, there are no dots` -> 401.
  - `Components/Pages/Capa2/MiCopropiedadPanel.razor`: `Jwt="_jwt"` -> `Jwt="@_jwt"`.
  - `Components/Shared/Modals/EditarIdentidadModal.razor`: `Jwt="Jwt"` -> `Jwt="@Jwt"`.
- `Components/Shared/IdentidadForm.razor`: subida robusta (bufferiza el `InputFile` a bytes antes del POST
  via `ByteArrayContent`, el stream de Blazor Server no es reenviable) + **tarjetas drag & drop** para Logo y
  Fachada (arrastrar/soltar o clic, preview, boton quitar, campo URL alterno).
- `wwwroot/css/propia-tokens.css`: clases `.pmm-drop*` (drop-zone, preview, boton quitar, estado `is-drag`).
- `Components/App.razor`: bump `?v` del link de `propia-tokens.css` (para que el CSS nuevo llegue a cache viejo).
- `Propia.Web.csproj`: `<Version>` 0.0.145 -> 0.0.146.

## Pasos de deploy
1. `git fetch && git checkout main && git pull` (quedar en `0e64eaf4` o posterior).
2. **No hay migracion que aplicar.**
3. Build/publish Release del artefacto y desplegar.
4. Verificar version en el pie/login: **v0.0.146**.
5. (Opcional pero recomendado) activar storage R2 para persistir imagenes (ver "Config").

## Verificacion post-deploy
- Entrar a `Configuracion Copropiedad` (General) > seccion **Identidad** > **Editar datos**.
- En la tarjeta **Logo** (y **Fachada**): arrastrar/soltar una imagen o hacer clic -> se sube, aparece la
  **preview** (sin el error "No se pudo subir la imagen").
- **Guardar cambios** -> recargar -> el logo persiste; abrir el selector "Mis copropiedades" -> la tarjeta de
  la copropiedad muestra el logo.
- (Si NO se activo R2) tener presente que esa imagen se perdera en el siguiente redeploy hasta que R2 este activo.

## Rollback
Redeploy del artefacto previo (0.0.145). **No requiere tocar la BD** (esta tanda no lleva migracion).
