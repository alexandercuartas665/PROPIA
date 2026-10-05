# Plan - Alta inline de Usuarios y Directorio al canon de Unidades Privadas

> Objetivo: que la **fila de alta inline** (ingreso de registros) de **Usuarios** y **Directorio** sea
> IDENTICA (al milimetro: estilo visual, boton flotante, comportamiento) a la de **Unidades Privadas**,
> que es la referencia canonica. Chulá cada tarea (checkbox verde) a medida que avanza.
> Estado: PROPUESTA - esperando revision/ajuste de Alex antes de ejecutar. Fecha: 2026-10-05.

## Referencia y fuentes

- **Referencia (canon):** Unidades Privadas = `DistribucionPanel.razor` + `wwwroot/css/modules/tabla-base.css`
  (clases `nf-in`, `nf-cell`, `newrow--edit`, `row-add--sm`, `dst-nf-ok`/`dst-nf-cancel`, `tbl-row-new`,
  `tbl-exp-btn`, `tbl-fab`, `tbl-foot`).
- **Spec milimetrica:** skill `homogenizar-vista-tabla` (secciones 8-11 alta inline + bordes/colores, 14 footer/FAB).
- **A corregir:** Usuarios = `UsuariosTablaVista.razor` + `usuarios.css` (`utb-*`). Directorio =
  `DirectorioPersonasVista.razor` (+ `DirectorioEmpresasVista.razor`) + `directorio.css` (`dtab-*`).
- Nota tecnica: Unidades usa `<table>` HTML; Usuarios y Directorio usan **CSS grid** (celdas `<div>`). La
  tecnica del "marco que pinta la celda" (`nf-cell::after`) hay que adaptarla a las celdas grid.

## Reglas de oro (aplican a TODAS las tareas)

- Colores SOLO por tokens `var(--propia-*)`; prohibido hex nuevo. ASCII en codigo. Subir `?v=N` del CSS en App.razor.
- Validar **lado a lado en Chrome** (Unidades vs el modulo) con `getComputedStyle`/`getBoundingClientRect`;
  no marcar "listo" sin comparar milimetrico (ver memoria gate-milimetrico-lado-a-lado).
- No romper consumidores (UsuariosPanel, DirectorioPanel, selects de rol/etiqueta, seleccion multiple).
- Un commit por tarea; mergear a main y validar antes de la siguiente.

## Diferencias detectadas (resumen del inventario)

| # | Caracteristica | Unidades (REF) | Usuarios | Directorio |
|---|---|---|---|---|
| 1 | Disparo del alta | CTA colapsada -> abre fila (`_altaAbierta`); FAB **abre** | fila siempre visible; FAB solo scroll | fila siempre visible; FAB solo scroll |
| 2 | Guardado | check-icono `dst-nf-ok` + cancelar `dst-nf-cancel` + Enter/Esc | boton-texto "+ Agregar"; Enter; sin cancelar | boton-texto "+ Agregar"; Enter; sin cancelar |
| 3 | Inputs | `nf-in` sin borde; marco violeta+glow lo pinta la celda; fila violeta-soft-2 | `utb-inp` dashed gris -> solido violeta | `dtab-inp` dashed gris -> solido violeta |
| 4 | Controles por campo | texto, select, number, date, SelectorPersona Flotante | select tipo-doc, texto, select rol | texto, select tipo-doc, select etiqueta |
| 5 | Boton agregar | icono check violeta + cancelar | `utb-add` texto violeta-suave | `dtab-add` texto violeta-suave |
| 6 | Resaltado verde fila nueva | SI (`tbl-row-new`) | **NO existe** | SI (`dtab-row-new`) |
| 7 | Columna expander en alta | SI (crear+abrir ficha) | **NO hay** | SI |
| 8 | FAB+footer+scroll | tbl-fab+tbl-foot; pagina por lotes | tbl-fab+tbl-foot; render-all | tbl-fab+tbl-foot; render-all |

---

## OLA A - Inputs de la fila de alta al canon `.nf-in` (lo mas visible)  -- HECHA (f96a45d)

- [x] A1. Usuarios: `.utb-inp` sin borde en reposo (transparente); el foco dibuja marco brand 1.5px + glow
      radio 8 (tecnica `.dst-nf-inp`, el input mismo); radius 8, padding 10/12, font 13; fila de alta con
      fondo `--propia-brand-soft-2`. (Validado en Chrome: focus -> rgb(109,79,227) + glow .14 3px.)
- [x] A2. Directorio (Personas + Empresas): idem con `.dtab-inp` (comparten directorio.css). Validado reposo
      + fila + regla `:focus` identica a Usuarios (v=8 cargado).
- [x] A3. Validado lado a lado vs Unidades (reposo transparent, radius 8, pad 10/12, font 13, fila soft-2).

## OLA B - Control de guardado: check-icono + cancelar (reemplaza el boton-texto)  -- HECHA (f96a45d)

- [x] B1. Usuarios: `utb-add` "+ Agregar registro" reemplazado por check-icono violeta lleno (`utb-nf-ok`,
      guardar=Invitar) + cancelar (`utb-nf-cancel`); Enter = guardar, Escape = limpiar (`CancelarAlta`).
- [x] B2. Directorio Personas (`CancelarAltaP`) + Empresas (`CancelarAltaE`): idem `dtab-nf-ok`/`dtab-nf-cancel`.
- [x] B3. Validado: botones check/cancel presentes y estilados 1:1 (`dst-nf-ok`/`dst-nf-cancel`); texto viejo
      retirado. (Guardar reusa el metodo existente; cancelar resetea el borrador via @key.)

## OLA C - CTA colapsable + FAB que ABRE la fila  -- HECHA (3a80850)

- [x] C1. Usuarios: flag `_altaAbierta`; CTA colapsada `row-add--sm` por defecto; CTA o FAB abren la fila;
      guardar/cancelar/Escape colapsan. (Validado ciclo completo en Chrome.)
- [x] C2. Directorio Personas + Empresas: idem. (Personas validado; Empresas mismo codigo.)
- [x] C3. Validado: FAB/CTA abren la fila; tras guardar/cancelar vuelve a la CTA colapsada.

## OLA D - Expander + resaltado verde  -- CERRADA (opcion A)

- [x] D3. Directorio: `dtab-row-new` (verde) + expander en la fila de alta abierta -> intactos tras Ola C.
      Directorio YA coincide con Unidades en #6 (verde) y #7 (expander).
- [x] D1. Usuarios - columna expander: **OMITIDO (opcion A, "dale ola d").** Las filas de Usuarios ya tienen
      boton lapiz "Editar" que abre el usuario (redundante) + era cirugia de grid de valor marginal.
- [x] D2. Usuarios - resaltado verde: **N/A por diseno.** Invitar crea un usuario PENDIENTE (pestana
      Pendientes, no la tabla de Activos), asi que no hay fila recien creada en la tabla actual para pintar.

## OLA E - Decisiones a confirmar con Alex (NO ejecutar sin OK)

- [x] E1. Controles por campo: **RESUELTO (Alex): dejar los campos sin edicion como en Unidades.**
- [x] E1b-Rol. **HECHO (f78ce3e):** crear rol basico inline desde el select de Rol del alta ("+" ->
      nombre -> POST /api/roles -> seleccionado). Validado en Chrome.
- [ ] E1b-Cargo. **BLOQUEADO / decision de Alex.** El alta de Usuarios es una INVITACION (usuario
      Pendiente, sin registro materializado), asi que no hay a quien pegarle un valor de Cargo en el alta
      (misma raiz que anulo el verde D2). Opciones: (A) Cargo editable en usuarios EXISTENTES + "+ agregar
      valor" (en el alta queda "-"); (B) Cargo en Directorio personas (el alta crea fila real -> funciona en
      el alta); (C) arrastrar cargo por la invitacion (backend + migracion, contradice 2b). PENDIENTE elegir.
- [x] E2. Render-all vs lotes: **dejar render-all** (es solo rendimiento, no visual; recomendacion aceptada).
- [x] E3. Footer de Usuarios unificado a "N registros - mostrando M" (como Unidades/Directorio).

## OLA F - Gate milimetrico final

- [ ] F1. Abrir Unidades + Usuarios + Directorio lado a lado en Chrome; comparar 1:1 colores, espaciados y
      comportamiento de la fila de alta (caracteristicas 1-8); chular cada una. Entregar evidencia medida.

---

## Alcance / notas

- Objetivo mínimo para "igual que Unidades": Olas A, B, C, D (lo visual + interaccion). Ola E son decisiones
  (no trabajo seguro). Ola F es el gate.
- Directorio parte mas cerca (ya tiene verde + expander); su foco es A, B, C.
- Usuarios es el mas alejado (suma D1 expander + D2 verde).
- Espejo de este plan en el repo: `deploy/PLAN_ALTA_INLINE.md`.
