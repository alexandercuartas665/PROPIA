# DEPLOY HANDOFF - PROPIA v0.0.140 (2026-10-13)

> **LEEME PRIMERO.** Handoff autocontenido de este deploy. Fuente de verdad acumulada: `HANDOFF_DEPLOY.md`
> (seccion "ESTADO 2026-10-13 Homogeneidad alta inline + fila recien creada violeta + limpieza CSS").

## Resumen de 1 linea
Deploy **100% UI/CSS**: homogeneizacion del alta inline al canon violeta + resaltado de "fila recien creada"
de verde a violeta en los 11 modulos vista-tabla + limpieza de CSS muerto en Unidades. **Sin migraciones.**

## Que desplegar
- Rama: `main`, HEAD **`afd785b3`** (contiene hasta el commit `8c9b6fff` de codigo + `afd785b3` de docs).
- Version visible: **0.0.140** (`src/Propia.Web/Propia.Web.csproj <Version>`).
- Repo: https://github.com/alexandercuartas665/PROPIA

## Migraciones / BD
**NINGUNA en esta tanda.** No hay cambios de esquema, RLS, seeds ni catalogos.
> OJO si prod viene de ANTES de 0.0.132: revisar las secciones intermedias de `HANDOFF_DEPLOY.md` por
> migraciones ADITIVAS pendientes de tandas previas aun no aplicadas en prod (p.ej. 0.0.125
> `20261012230340_AddMascotaTipoCustom`). Esta tanda NO agrega ninguna.

## Config / env / secrets
**Sin cambios.** No hay variables de entorno, flags ni appsettings nuevos.

## Cambios de codigo (commits 0.0.133 -> 0.0.140)
| Version | Commit | Que |
|---|---|---|
| 0.0.133 | `e192630d` | Roles: bandeja homogenizada (FiltroDinamico + Ordenar + Agrupar + menu de columna + ver/crear categorias) + matriz de permisos simetrica (checkbox deshabilitado) |
| 0.0.134 | `fcfcfd66` | Alta inline al canon violeta en 8 modulos (Vehiculos, Mascotas, Residentes, Contratos, Mantenimiento, Tareas, Seguros, Roles) |
| 0.0.135 | `f368c88a` | Alta inline Zonas y Equipos (padding/font que faltaban) |
| 0.0.136 | `b4949d73` | Unidades: fila recien creada violeta (antes verde) |
| 0.0.137 | `b45174de` | Unidades: foco del alta = borde sobre el input (no anillo ::after) |
| 0.0.138 | `a06cd16b` | Unidades: aire vertical en la celda del alta (el glow no se recorta) |
| 0.0.139 | `790cd41a` | Fila recien creada VIOLETA en los 11 modulos vista-tabla |
| (doc)   | `a8e17370` | Skill homogenizar-vista-tabla actualizada al canon violeta (no afecta runtime) |
| 0.0.140 | `8c9b6fff` | Unidades: limpieza de ~85 selectores .dst-* muertos (144 lineas) |
| (doc)   | `afd785b3` | Este handoff + checklist |

## CSS cache-bust (ya en `App.razor`, viajan con el artefacto)
`tabla-base.css ?v=10` · `servicios.css ?v=19` · `mantenimiento.css ?v=5` · `directorio.css ?v=12` ·
`pqrs.css ?v=23` · `roles.css ?v=15` · `tareas.css ?v=28`. El resto del alta es inline en el `.razor`.
Si hay CDN/proxy delante: el `?v` fuerza la recarga; opcionalmente purgar cache de esos archivos.

## Pasos de deploy
1. `git fetch && git checkout main && git pull` (quedar en `afd785b3` o posterior).
2. Build/publish Release del artefacto (como en deploys anteriores de solo-codigo).
3. Desplegar el artefacto. **No** correr `dotnet ef database update` (no hay migracion nueva en esta tanda).
4. Verificar la version en el pie/login: **v0.0.140**.

## Verificacion post-deploy (rapida)
- Abrir Unidades Privadas (`/distribucion`): tabla + registros, menu Columnas, vista Tarjetas, boton Agregar
  abre la fila de alta con input "fantasma" + borde/glow violeta al foco + hint "Tab/Esc".
- Crear un registro en cualquier modulo vista-tabla: la fila recien creada se resalta en **violeta** (no verde).
- Roles (dentro de Usuarios): bandeja con Filtros/Ordenar/Agrupar/menu de columna; matriz con checkbox gris
  deshabilitado donde no aplica.

## Rollback
Redeploy del artefacto previo. **No hay esquema que revertir** (tanda sin migraciones).

## Pendiente conocido (NO bloquea deploy; diagnostico, se corrige aparte)
- Token CSS cosmetico roto en `DistribucionPanel.razor`: `.cg-row-warn td { background: var(--propia-bg-card)af2; }`
  (la fila de aviso del log de carga Excel no se resalta). Inofensivo.
- Codigo C# muerto en `DistribucionPanel.razor`: ruta de import CSV huerfana + banner `_impResult` inalcanzable.
