# PLAN DE PENDIENTES POR OLAS - sesion ALEX (dev principal)

> Backlog priorizado. Mezcla lo que veniamos trabajando (FAB + scroll infinito) con los hallazgos
> de la sesion auditora (QA). Cada item no trivial lleva su plan de 5 lineas + OK de Alex antes de
> codear (CLAUDE.md + protocolo de auditoria). Ultima revision: 2026-10-05.
> Validacion: por API / recarga dura (NO `docker exec psql`: hay un Postgres stale en :5433).

## Ola 0 - En curso (no bloquea nada)

| Item | Detalle | Estado |
|---|---|---|
| FAB Roles | boton flotante + scroll infinito (canon Unidades) en RolesPanel | en curso (build) |

## Ola 1 - Seguridad + bloqueos funcionales (P1-P3, lo mas urgente)

| Item | Sev | Detalle | Archivos |
|---|---|---|---|
| D-1 | ALTA (seguridad) | alta de Unidad con numero duplicado vuelca stack trace crudo (rutas, esquema BD) a la UI. Traducir PostgresException SqlState 23505 a error de negocio limpio; asegurar que el handler global NO serialice el stack al body. Revisar otras altas inline. | MiCopropiedadService.*, UnidadesPlantillaService, Distribucion.razor, handler global |
| V-1 | ALTA (onboarding) | en Vehiculos/Mascotas con pocas filas el dropdown de UNIDAD (.suc2-drop) se recorta por el overflow:auto del scroll. Render del dropdown en position:fixed (portal). Re-verificar 6 consumidores (Vehiculos, Mascotas, GestionarPqrsdModal, NuevaPqrsdWizardModal, Reservas, Porteria). | SelectorUnidadCodigo.razor (compartido) |
| T-1 | MEDIA | no se puede editar una tarea creada con el admin como solicitante (POST acepta, PUT rechaza). Alinear validacion create/update (permitir actor logueado/admin como solicitante). | TareasController, TareasService.* |

## Ola 2 - FAB + scroll infinito (homogeneidad vista-tabla)

| Item | Detalle |
|---|---|
| Usuarios (vista Tabla) | FAB + scroll infinito (quitar paginador; UsuariosTablaVista + manejo de paging en UsuariosPanel) |
| Seguros | FAB + scroll infinito (hoy paginador) |
| Contratos | FAB + scroll infinito (hoy paginador) |
| PQRSD | FAB + scroll infinito (hoy paginador) |

## Ola 3 - Funcionalidad faltante + UX menores

| Item | Sev | Detalle |
|---|---|---|
| S-2 | MEDIA | falta "Eliminar poliza" en UI (el DELETE /api/seguros/polizas/{id} ya existe). Cablear con modal de confirmacion. (Seguros.razor) |
| T-2 | menor | banner de error de Tareas muestra JSON crudo `{"error":...}`; mostrar solo el texto |
| C-1 | menor | modal borrar Contrato generico; incluir nombre/numero del contratista |
| R-1 | menor | editar telefono inline en Residentes reordena/duplica visual; `@key="v.Id"` en el @foreach |
| CM-1 | menor | Comunicaciones: lista stale ("0 comunicados") tras cancelar wizard; refrescar al cerrar |
| RL-1 | menor | borrado de rol personalizado sin modal de confirmacion; agregar (confirmar comportamiento) |
| S-1 | menor | banner transitorio "Error inesperado" al crear poliza; reproducir con datepicker real |

## Ola 4 - Tests + deuda/infra

| Item | Sev | Detalle |
|---|---|---|
| B-4 | MEDIA (tests) | UsuariosAccesosFlowTests 2->4 rojos por reseed Roles V2 (espera 5 base, hay 7; .First(Nombre==...) falla). Actualizar tests (confirmar con Alex que 7 base es intencional). |
| B-1 | deuda | RLS: 8 tablas con tenant_id sin RLS (RlsCoverageTests). Documentar globales o agregar RLS. |
| E-1 | infra | dos Postgres en :5433 (WSL real via ::1 vs docker propia-postgres stale). Alinear/renombrar el stale. |

## Ola 5 - Confirmar con Alex (posible por diseno)

| Item | Detalle |
|---|---|
| P-1 | no hay borrar presupuesto/vigencia; confirmar si es "descartar borrador" o por diseno financiero |
| stale-count | /mi-copropiedad mostro "6 unidades" con /distribucion en 5; confirmar si es conteo cacheado |

## Deploy pendiente (manual, tuyo)

- SQL del menu de Roles en prod (menu_overrides: Configuracion -> Usuarios/Roles/Terceros).
- Migracion D2 ReseedRolesBase (SENSIBLE: solo con OK explicito de Alex).

## NO tocar (clasificados NO-bug por la auditoria)

- PQRSD / Presupuesto / Asambleas sin borrado (por diseno legal/auditoria).
- Comunicaciones con soft-delete (por diseno).
- Otros flujos sin "eliminar" que son intencionales.
