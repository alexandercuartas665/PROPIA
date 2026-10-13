# DEPLOY HANDOFF - PROPIA v0.0.144 (2026-10-13)

> **LEEME PRIMERO.** Handoff autocontenido. Fuente de verdad acumulada: `HANDOFF_DEPLOY.md`
> (seccion "ESTADO 2026-10-13 Expander del alta + hotfix del limite de copropiedades").

## Resumen de 1 linea
Expander del alta que crea+abre ficha en 5 modulos mas, y HOTFIX: el error del limite de copropiedades ahora
nombra la organizacion. **UI + servicio, SIN migraciones.**

## Que desplegar
- Rama: `main`, HEAD **`12115655`**.
- Version visible: **0.0.144** (`src/Propia.Web/Propia.Web.csproj <Version>`).
- Repo: https://github.com/alexandercuartas665/PROPIA

## Migraciones / BD  (NO, ninguna en esta tanda)
- **SIN migraciones.** Prod ya esta en 0.0.141 (su error de limite dice "El limite permitido es..."), asi que
  la migracion `AddOrganizacionLimiteCopropiedades` (columna `organizaciones.limite_copropiedades`) YA se aplico.
- Si por algun motivo se despliega sobre una prod ANTERIOR a 0.0.141, aplicar esa migracion primero
  (`dotnet ef database update`, owner `propia`; ver `DEPLOY_HANDOFF_2026-10-13_v0.0.141.md`). De 0.0.141 a 0.0.144
  NO hay migracion.

## Config / env / secrets
**Sin cambios.**

## Cambios de codigo
Commits `554195f5` (0.0.142), `4d99d7c1` (0.0.143), `12115655` (0.0.144):
- **Expander del alta -> crear + abrir ficha** (como Unidades), vista Tabla:
  - Zonas (`ZonasComunesPanel.razor`), Equipos (`EquiposActivosPanel.razor`): `<span>` muteado -> `<button>` ->
    `CrearYAbrirFichaAsync` (que ahora tambien persiste `_nuCampos` y cierra el alta inline).
  - Residentes (`ResidentesPanel.razor`): -> `AgregarAsync(true)` (ya tenia el parametro `abrir`) + CSS de
    visibilidad del expander en el alta.
  - Seguros (`Seguros.razor`): -> `CrearPolizaDesdeAltaAsync(true)` (nuevo: abre el modal tras crear).
  - Contratos (`Servicios.razor`): el boton del alta ahora `CrearDesdeAltaAsync(true)` abre el modal tras crear.
- **HOTFIX limite de copropiedades** (`MisCopropiedadesService.CrearAsync`): el error nombra la organizacion
  ("El limite de copropiedades de la organizacion 'X' es N y ya tiene M...") + log Information con
  tenantActivo/orgDestino(id+nombre)/override/limite-plan/limite-efectivo/conteo. Sin cambio de la regla.

## Pasos de deploy
1. `git fetch && git checkout main && git pull` (quedar en `12115655` o posterior).
2. Build/publish Release del artefacto y desplegar. **NO** correr `dotnet ef database update` (no hay migracion).
3. Verificar version en el pie/login: **v0.0.144**.

## Verificacion post-deploy
- En un modulo vista-tabla (p.ej. Zonas): "+ Agregar" -> escribir nombre -> clic en el icono de expandir de la
  fila -> crea el registro y abre su ficha "Editar datos".
- Intentar crear una copropiedad por encima del limite: el error ahora dice el NOMBRE de la organizacion
  ("El limite de copropiedades de la organizacion 'X' es N y ya tiene M...").

## Rollback
Redeploy del artefacto previo (sin esquema que revertir).

## Nota de soporte (caso "amplie el limite pero sigue pegado")
El limite es POR ORGANIZACION. La copropiedad nueva cuelga de la org de la copropiedad que el usuario
ADMINISTRA (la activa). Si sus copropiedades estan en varias orgs, hay que ampliar el limite de la org que
RECIBE la nueva (la que el nuevo mensaje nombra / la que muestra ese conteo en Super Admin), no otra. El log
de `MisCopropiedadesService` imprime la org exacta (id + nombre) y su override para rastrearlo.
