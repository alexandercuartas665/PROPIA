# PLAN DE PENDIENTES POR OLAS - sesion ALEX (dev principal)

> Backlog priorizado. Mezcla lo que veniamos trabajando (FAB + scroll infinito) con los hallazgos
> de la sesion auditora (QA). Cada item no trivial lleva su plan de 5 lineas + OK de Alex antes de
> codear (CLAUDE.md + protocolo de auditoria). Ultima revision: 2026-10-05.
> Validacion: por API / recarga dura (NO `docker exec psql`: hay un Postgres stale en :5433).

## Ola 0 - Hecha

| Item | Detalle | Estado |
|---|---|---|
| FAB Roles | boton flotante + scroll infinito (canon Unidades) en RolesPanel | HECHO (388fcf4) |

## Ola 1 - Seguridad + bloqueos funcionales (P1-P3) - HECHA

| Item | Sev | Estado | Detalle | Archivos |
|---|---|---|---|---|
| D-1 | ALTA (seguridad) | HECHO (41afa7c) | duplicado de unidad -> 400 limpio (traduce 23505 a InvalidOperationException); handler global registrado en TODOS los entornos (JSON sin stack, intercepta el Developer Exception Page). Validado por API. | MiCopropiedadService.Unidades.cs, Program.cs |
| V-1 | ALTA (onboarding) | HECHO (0ccc903) | dropdown de unidad flota (position:fixed via propiaFloatPos, patron SelectorPersona Flotante) para escapar del overflow del scroll. Validado en Vehiculos y Mascotas (panel flota con 15 items, sin recorte). Aplica a los 6 consumidores. | SelectorUnidadCodigo.razor (compartido) |
| T-1 | MEDIA | HECHO (97202cd) | ActualizarTareaAsync solo re-valida pertenencia al Directorio del solicitante/asignado cuando CAMBIA (PUT=MERGE); el solicitante por defecto (admin) ya no bloquea la edicion. Validado por API: editar -> 204; cambiar a persona no vinculada -> 400. | TareasService.Tareas.cs |

## Ola 2 - FAB + scroll infinito (homogeneidad vista-tabla) - HECHA

| Item | Detalle | Estado |
|---|---|---|
| Usuarios (vista Tabla) | render-all + footer + FAB sobre .utb-scroll | HECHO (5a20835) |
| Seguros | render-all + footer + FAB sobre .seg-scroll | HECHO (e91e4d1) |
| Contratos | render-all + footer + FAB sobre #ctrTableWrap (toggle validado) | HECHO (8800a94) |
| PQRSD | render-all + footer + FAB sobre #pkTableWrap; FAB abre wizard (IrNueva) | HECHO (5269f5a) |

## Ola 3 - Funcionalidad faltante + UX menores

| Item | Sev | Detalle |
|---|---|---|
| S-2 | MEDIA | **HECHO (de3e1f9)** - "Eliminar poliza" cableado en el modal ficha con dialogo de confirmacion (DELETE existente, cascade). Validado por UI (7->6). |
| T-2 | menor | **HECHO (e9cedad)** - helper ErrTxtAsync extrae 'error' del JSON; ~22 banners ya no vuelcan el crudo. (code-verified: /tareas renderiza; API retorna {"error"}) |
| C-1 | menor | **HECHO (64fafba)** - modal borrar Contrato muestra proveedor + numero. Validado por UI ("...con Limpieza Brillante?"). |
| R-1 | menor | **HECHO (f1197b8)** - `@key="r.UnidadPersonaId"` en la fila de Residentes. (code-verified: /residentes renderiza 593 filas). |
| CM-1 | menor | **HECHO (16e3a0d)** - cancelar wizard recarga la lista (CancelarCrearAsync -> CargarAsync). Verificado: wizard abre/cierra y re-renderiza. |
| RL-1 | menor | **HECHO (7ae3062)** - confirmacion antes de borrar rol. Validado end-to-end por UI (dialogo con nombre -> borra). |
| S-1 | menor | PENDIENTE (aparte) - banner transitorio "Error inesperado" al crear poliza; reproducir con datepicker real. |

## Ola 4 - Tests + deuda/infra - HECHA

| Item | Sev | Detalle |
|---|---|---|
| B-4 | MEDIA (tests) | **HECHO (6537d1f)** - UsuariosAccesosFlowTests 12/12 verde. Catalogo canonico confirmado 7 base / 0 extendidos globales (D2). Se corrigieron tambien 2 rojos preexistentes (faltaba vincular la persona al Directorio antes de invitar, S-02b). |
| B-1 | deuda | **HECHO (c14e002)** - las 8 tablas sin RLS son BaseEntity con tenant_id descriptivo; documentadas en TablasExentas (RlsCoverageTests) con justificacion por tabla. Sin migracion (forzar RLS romperia consola A&D / auditoria / notificaciones). RlsCoverageTests 2/2 verde. |
| E-1 | infra | **HECHO (b75cf7a)** - verificado por marcador que hay UNA sola DB (los 2 listeners :5433 son el dual IPv4/IPv6 de Docker Desktop al mismo contenedor). Se fijo el host dev a 127.0.0.1 (Api/Web + factory de migraciones) para quitar la ambiguedad ::1/wslrelay. No reproduce "dos Postgres" aqui. |

## Ola 5 - Confirmar con Alex (posible por diseno)

| Item | Detalle |
|---|---|
| P-1 | no hay borrar presupuesto/vigencia; confirmar si es "descartar borrador" o por diseno financiero |
| stale-count | /mi-copropiedad mostro "6 unidades" con /distribucion en 5; confirmar si es conteo cacheado |

## Deploy pendiente (manual, tuyo)

- SQL del menu de Roles en prod -> HECHO por Alex manualmente (2026-10-05).
- Migracion D2 ReseedRolesBase (SENSIBLE) -> confirmar si ya se aplico en prod.

## NO tocar (clasificados NO-bug por la auditoria)

- PQRSD / Presupuesto / Asambleas sin borrado (por diseno legal/auditoria).
- Comunicaciones con soft-delete (por diseno).
- Otros flujos sin "eliminar" que son intencionales.
