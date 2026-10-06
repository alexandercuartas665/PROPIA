# HAND-OFF DEPLOY - PROPIA (2026-10-06)

> Portada de ESTE deploy. Detalle historico en `HANDOFF_DEPLOY.md` (companion en esta carpeta),
> paso-a-paso en `DEPLOY_CHECKLIST.md`, plan del catalogo en `PLAN_CATALOGOS_SUPER_ADMIN.md`.
> Reemplaza como portada vigente al `DEPLOY_HANDOFF_2026-10-05.md` (se deja por historia).

## Objetivo del deploy

- **Rama / HEAD:** `main` @ `91a550c`
- **Version:** `0.0.103`
- **Repo:** https://github.com/alexandercuartas665/PROPIA (rama `main`)
- **Mecanismo:** auto-deploy desde `main` (Railway). Prod toma ese commit.
- **Base anterior:** deploy 2026-10-05 era `main` @ `acbfe54` (v0.0.101). Esta tanda pasa por v0.0.102
  (`7f00e34`) y queda en **v0.0.103** (`91a550c`).
- **TRAE 2 MIGRACIONES NUEVAS (aplicar en prod, ver seccion Migraciones).** Fuera de eso, sin breaking
  changes: todo lo nuevo tiene fallback al codigo (comportamiento identico si el catalogo esta vacio).

## Que entra en esta tanda (commits nuevos sobre `acbfe54`)

**Vista-tabla: header/chevron + Roles (bandeja):**
- `d7244bd`, `00bd9f3` - header de Roles al canon (sin separadores verticales, igual a Usuarios/Terceros).
- `1f32abc` - Roles: checkbox de seleccion + acciones masivas + boton "+".
- `459b31a` - Roles: roles del sistema primero, quita "+" del header, boton de alta al canon.
- `0ff6391` - normaliza el menu de columna del header (chevron siempre visible) al canon de Unidades.

**"Listas en la config del campo" (enum -> lista editable por copropiedad):**
- `fd631db` **[MIGRACION]** - Contratos: "Tipo de contrato" y "Categoria" pasan de enum a string editable
  por copropiedad. Trae `20261006120327_ContratoTipoCategoriaAString` (ver Migraciones).
- `cb59b3e` - PQRSD: la categoria se edita desde la config del campo (modal "Editar campo", lee/crea
  `pqrsd_categorias`).

**Vista-tabla: reordenar + redimensionar columnas (canon Unidades):**
- `e76b3e9` Contratos (reorder+resize), `f6b5650` fix del resize de Contratos (ancho visual real),
  `9ad7e35` PQRSD (reorder+resize). Interop compartido `propiaTablaColReorder`/`propiaTablaResize`;
  orden+ancho persisten en `unidades-config` (ancho en el JSON `Formato` clave "w", sin migracion).

**Alta inline de Unidades - captura TODOS los campos de sistema:**
- `23122d1` - la fila de alta ahora captura estado, habitaciones, banos, parqueaderos, paga admin, cuota,
  observaciones y modulos contributivos (antes mostraban "En la ficha"). **Sin migracion** (el
  `CrearUnidadRequest` ya soportaba esos campos; solo faltaba la UI).

**Catalogo global de listas al Super Admin (Olas 1-4 tranche A):**
- `042b493` **[MIGRACION]** Ola 1 (infra: tabla `catalogo_opciones` + lector con cache + seeder).
- `0e3c4d5` Ola 2 (consola A&D `/admin/catalogos`: editar labels/orden/color/activo + agregar + reordenar).
- `77a275c` Ola 3 (piloto Unidades: Tipo/Estado leen del catalogo).
- `a7f2187` / `ce9acb5` / `76aa751` Ola 4 tranche A (Contratos tipo/categoria; Vehiculos tipo; Mascotas
  tipo; Equipos categoria/tipo/estado; Zonas categoria/estado; Personas sexo).
- 12 listas sembradas de fabrica; cada modulo las lee componiendo el override por copropiedad encima.
- Docs: `3c09402`, `c3441c2`, `0d110c5` (plan + inventario + comparativa).

**Version:** `7f00e34` (0.0.101 -> 0.0.102), `91a550c` (0.0.102 -> 0.0.103 + handoff consolidado).

Todo validado en runtime (navegador integrado / API, tenant demo + consola A&D founder dev). Solo ASCII en
codigo; colores por tokens. Datos de prueba revertidos.

## Migraciones (APLICAR EN PROD, owner `propia`, design-time factory)

**ESTA tanda trae 2 migraciones nuevas, ambas aditivas en el sentido de que el rollback es redeploy del
artefacto anterior sin revertir esquema:**

1. `20261006120327_ContratoTipoCategoriaAString` - convierte las columnas `tipo_contrato` y `categoria` de
   `contrato_servicios` de `integer` (enum) a `text`, mapeando los valores existentes (int -> etiqueta) con
   un `CASE`. Necesaria para que "Tipo de contrato"/"Categoria" sean listas editables por copropiedad.
   Aplicada en dev. **Nota:** su `Down` vuelve a int con el CASE inverso; valores de texto nuevos que no
   mapeen a un enum quedarian NULL al revertir, por eso el rollback real es redeploy, no `Down`.
2. `20261006164654_AddCatalogoOpcion` - crea la tabla GLOBAL `catalogo_opciones` (sin tenant_id, **sin
   RLS** a proposito: tabla de plataforma; `RlsCoverageTests` solo mira tablas con tenant_id) + GRANT a
   `propia_app`. Aplicada en dev.

Comando (aplica solo lo que falte, en orden):
```
cd src/Propia.Api
"$USERPROFILE/.dotnet/tools/dotnet-ef.exe" database update --project ../Propia.Infrastructure --startup-project .
```

**Pendientes de tandas previas** (aplicar solo si prod viene por detras): ver la lista en
`DEPLOY_HANDOFF_2026-10-05.md` (Roles V2 `Modulo25V2_*`, D2 data migration SENSIBLE con OK de Alex,
polizas/contratos). El `database update` las aplica todas en orden.

## CONFIG/ARRANQUE NUEVO - seeder idempotente del catalogo

`CatalogoListasSeeder.EnsureAsync` corre en CADA arranque (Web y Api), SIEMPRE (no solo dev). Siembra las
12 listas de fabrica desde los enums/semillas; solo inserta lo que falta, NO pisa lo que A&D edite; no-op
tras la primera vez. Va en try/catch: si falla, loguea y NO bloquea el arranque. **La migracion
`AddCatalogoOpcion` debe estar aplicada ANTES de arrancar** (si no, loguea warning y siembra al proximo
arranque).

## Verificacion post-deploy (humo)

- [ ] **Consola A&D -> Plataforma -> "Catalogos de listas"** (`/admin/catalogos`): aparecen 12 listas;
      editar un label (p.ej. unidad.tipo "Deposito") -> Guardar.
- [ ] Abrir Mi Copropiedad -> Unidades: el tipo editado muestra el nuevo label (sin redeploy). La clave no
      cambia (las unidades existentes siguen bien).
- [ ] Unidades: "+ Agregar registro" -> la fila de alta captura estado/habitaciones/cuota/etc. (activar
      esas columnas en "Campos" si estan ocultas). Crear una y verificar que persisten.
- [ ] Contratos y PQRSD: arrastrar el header de una columna la reordena; el tirador del borde la
      redimensiona; doble-clic autoajusta. Persisten tras recargar.
- [ ] Contratos: "Tipo de contrato" y "Categoria" funcionan como lista (no se rompio por la migracion
      enum->string); los contratos existentes muestran su tipo/categoria.

## Rollback

- Redeploy del artefacto anterior (v0.0.101 / `acbfe54`, ultimo bueno conocido). Las 2 migraciones son
  compatibles hacia atras para el codigo viejo (el codigo viejo ignora `catalogo_opciones`; para Contratos,
  el codigo viejo espera enum int -> NO redeployar v0.0.101 DESPUES de la migracion de Contratos sin
  cuidado: si hay que volver, revertir primero esa migracion es arriesgado -> preferible fijar hacia
  adelante). En la practica: este deploy es acumulativo y seguro hacia adelante; evitar bajar de version
  una vez aplicada `ContratoTipoCategoriaAString`.

## Pendiente (NO en este deploy)

- **PQRSD-unify (Ola 4 Tranche B):** unificar tipo/etapa de PQRSD al catalogo global con metadata legal
  (plazos Ley 1755) + migracion de datos + refactor. Documentado en `PLAN_CATALOGOS_SUPER_ADMIN.md`
  (seccion "Estado al cierre"). Proxima sesion.
- Deudas previas (coeficiente `numeric(7,4)`, `UnidadCoeficiente` no poblada por el import): ver
  `DEPLOY_HANDOFF_2026-10-05.md`.

---
Generado 2026-10-06 (`main` @ `91a550c` / v0.0.103). Companions: `HANDOFF_DEPLOY.md`, `DEPLOY_CHECKLIST.md`,
`PLAN_CATALOGOS_SUPER_ADMIN.md`.
