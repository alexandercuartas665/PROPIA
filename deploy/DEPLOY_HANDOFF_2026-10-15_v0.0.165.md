# DEPLOY HANDOFF - PROPIA v0.0.165 (2026-10-15)

> **LEEME PRIMERO.** Handoff autocontenido. La tanda **0.0.165 es SOLO CODIGO (CSS/Razor), sin migracion
> ni config**. PERO este deploy ACUMULA desde el ultimo que haya en prod: si prod aun no tiene las 2
> migraciones de 0.0.156 y 0.0.158, **hay que aplicarlas** (ver "Migraciones / BD").

## Resumen de 1 linea
Vista-tabla: los anchos de columna por defecto pasan de `fr`/`minmax-fr` a **px fijos** para que el header
y las filas alineen (se elimina la "columna fantasma"/desfase en Residentes, Mascotas, Vehiculos y Zonas),
y en **Unidades Privadas** la columna "+" **deja de estar fija a la derecha** (scrollea con el resto).

## Que desplegar
- Rama: `main`, HEAD **`97c1511d`**.
- Version visible: **0.0.165** (`src/Propia.Web/Propia.Web.csproj <Version>`).
- Repo: https://github.com/alexandercuartas665/PROPIA
- Detalle acumulado 0.0.150 -> 0.0.164 (responsive movil, fichas, propietario override, etc.):
  `deploy/DEPLOY_HANDOFF_2026-10-14_v0.0.149.md` (addenda al final de ese doc).

## Migraciones / BD
**La tanda 0.0.165 NO agrega migracion.** Es 100% CSS/Razor (anchos de columna + un override CSS scoped).

**Pendientes del acumulado (aplicar SOLO si prod no las tiene ya):**
1. **`20261015011139_AddPersonaComentarios`** (de 0.0.156) - tabla nueva `persona_comentarios` + RLS
   (ENABLE+FORCE+policy `tenant_isolation`+GRANT `propia_app`). Aditiva.
2. **`20261015110445_AddVehiculoMascotaPropietario`** (de 0.0.158) - columna nullable
   `propietario_persona_id` (uuid) en `unidad_placas` y `unidad_mascotas`. Aditiva, no toca RLS.

Aplicar con owner `propia` (design-time), p. ej.:
```
cd src/Propia.Api
"$USERPROFILE/.dotnet/tools/dotnet-ef.exe" database update --project ../Propia.Infrastructure --startup-project .
```
`database update` aplica solo lo que falte; si prod viene de una version anterior, aplica tambien las que
correspondan (ver los handoffs previos para el detalle de cada una). Ambas son aditivas y reversibles.

## Config / env / secrets
**Sin cambios.**

## Cambios de codigo (1 commit, 7 archivos, +41/-23)
Commit `97c1511d` - "Vista tabla: columnas px fijo para alinear header/filas; Unidades el + deja de ser columna fija".

**Causa raiz (comun a las tablas de grilla):** en las vista-tabla de **grilla-por-fila** (`res-`, `mas-`, `veh-`,
`zc-`) cada fila (header, dato y alta) es una grilla CSS independiente. Un track flexible (`fr` o `minmax(px,1fr)`)
se resuelve segun el contenido de CADA elemento, asi que el header (labels cortos) quedaba mas angosto que las
filas -> columnas desalineadas y una franja vacia a la derecha del header (la "columna fantasma"). Los anchos
guardados (resize) enmascaraban el defecto cuando existian. **Fix:** anchos por defecto en **px fijos** (todas las
filas comparten el mismo track -> alinean) + `min-width:max-content` en la grilla (el ancho natural se mantiene y
la tabla da **scroll horizontal** en vez de comprimir).

- **`ResidentesPanel.razor`** - `.res-listgrid` de `min-width:1000px` (fijo) a `min-width:max-content`;
  `ColWidth` de `fr`/`minmax-fr` a px (nombre 260, telefono 150, email 240, custom 150).
- **`MascotasPanel.razor`** - `ColWidth` a px (nombre 200, raza 150, propietario 220, custom 150).
  `.mas-grid` ya tenia `min-width:max-content`.
- **`VehiculosPanel.razor`** - `ColWidth` a px (propietario 220, custom 150). `.veh-grid` ya tenia `max-content`.
- **`ZonasComunesPanel.razor`** - `ColWidth` a px (nombre 220, categoria 150, reservable 110, aforo 100,
  estado 130, tarifa 130, descripcion/reglas 200, custom 150) + `.zc-grid` gana `min-width:max-content`.
- **`EquiposActivosPanel.razor`** - el override movil `@media (max-width:640px) .ea-listgrid { min-width:720px }`
  pasa a `min-width:max-content` (consistente con la regla base que ya era max-content; evita re-comprimir en movil).
- **`DistribucionPanel.razor`** (Unidades, tabla `.tbl` real) - la columna "+" (`.tbl-addcol`) deja de estar
  fija a la derecha: override **scoped** `.dst-scroll .tbl th.tbl-addcol, .dst-scroll .tbl td.tbl-addcol
  { position:static; right:auto; }` (gana por especificidad al `position:sticky;right:0` de `tabla-base.css`
  **sin tocar ese archivo** -> PQRSD/Contratos y demas `.tbl` quedan INTACTOS). Ademas el fallback de ancho de
  columna de sistema `"1fr"` (invalido en `<col>`) pasa a `"150px"`.

**No se toco `tabla-base.css`** (por eso ningun otro modulo `.tbl` cambia).

## Pasos de deploy
1. `git fetch && git checkout main && git pull` (quedar en `97c1511d` o posterior).
2. **Migracion:** si prod NO tiene 0.0.156/0.0.158, aplicar `AddPersonaComentarios` y
   `AddVehiculoMascotaPropietario` (ver "Migraciones / BD"). La tanda 0.0.165 en si no agrega ninguna.
3. Build/publish Release del artefacto y desplegar.
4. Verificar version en el pie/login: **v0.0.165**.
   (Las vista-tabla son `.razor` compilado + `tabla-base.css` NO cambio, asi que no hay `?v` nuevo que purgar.)

## Verificacion post-deploy
- **Residentes** (`/residentes`): en ancho angosto, los headers (incl. TELEFONO/EMAIL del final) quedan
  alineados con su columna; la tabla da scroll horizontal; sin hueco antes de EMAIL.
- **Residentes-hub -> Mascotas / Vehiculos** (`/residentes-hub`): headers (NOMBRE/RAZA/PROPIETARIO,
  UNIDAD/PLACA/TIPO/PROPIETARIO) alineados con las filas; sin "columna fantasma" a la derecha del header.
- **Zonas Comunes** (`/zonas-comunes`): idem; scroll horizontal cuando no cabe.
- **Unidades Privadas** (`/distribucion`): al scrollear a la derecha, la columna "+" (crear campo) **ya NO
  queda pegada** al borde derecho: aparece al final del contenido, como en los demas modulos.
- **PQRSD / Contratos** (`/pqrs`, `/contratos`): SIN cambios (su "+" sigue como estaba; no se toco `tabla-base.css`).

## Verificado en dev (navegador integrado, demo 6d93d9fa, Web 5105)
Build 0 errores. Medido con getBoundingClientRect a ancho 720-760px y scroll al extremo:
- Residentes: header y 6 filas con `grid-template-columns` identico, desalineacion **0 px**, telefono 150px, scroll OK.
- Mascotas: header/filas/alta identicos, desalineacion **0 px**, header=filas=1126px (sin fantasma), scroll OK.
- Vehiculos: desalineacion **0 px**, header=filas=894px, scroll OK (anchos naturales 150/130/120/220).
- Zonas: desalineacion **0 px**, `min-width:max-content`, header=filas=1313px, scroll OK.
- Unidades: la columna "+" pasa de `position:sticky` a `static` (antes quedaba fija en left=677; ahora
  scrollea 4144->677); header/cuerpo alineados (desalineacion 0).

Nota dev-only (NO afecta prod): en la copropiedad demo se limpiaron unos anchos de columna guardados por resize
accidental (Residentes telefono=465px; Vehiculos tipovehiculo=671/placa=330/unidad=303) via la config
`unidades-config` para validar con anchos naturales. Es dato del tenant demo, no parte del artefacto.

## Rollback
Redeploy del artefacto 0.0.164. **La tanda 0.0.165 no toca BD**, asi que el rollback de ESTA tanda no requiere
revertir nada de esquema. (Las migraciones 0.0.156/0.0.158 son aditivas e inertes para el codigo que no las use.)
