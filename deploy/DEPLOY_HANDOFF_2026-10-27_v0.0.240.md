# HAND-OFF DEPLOY - PROPIA - v0.0.240 (Web)

> **VERSION A DESPLEGAR = 0.0.240 (Web)**. Base: `main` HEAD `e2973c0b` (0.0.239).
> **ESTA TANDA ES SOLO CODIGO: SIN MIGRACION, SIN CONFIG, SIN PAQUETES NUEVOS.**
> La API no cambia (sigue en 0.0.212). Toda la persistencia nueva es del lado del cliente
> (localStorage); nada toca la base de datos.

Fecha: 2026-10-27. Sesion: ALEX (dev principal). Verificado en runtime (demo "Conjunto Altos del Bosque").

---

## Que entra en 0.0.240

### 1. Campos numericos: hasta 6 decimales (antes 4) - TODOS los modulos
El selector de decimales del formato numerico ahora ofrece 0..6 (antes 0..4). El resto del pipeline
(onchange + render) ya hacia `Math.Clamp(n, 0, 6)`, asi que solo faltaba extender las listas visibles.
Al ser componentes compartidos, aplica a Unidades Privadas y a todos los demas modulos.

- `Components/Shared/CrearCampoModal.razor`
- `Components/Shared/EditarCampoSistemaModal.razor`
- `Components/Shared/ConfigCamposEntidad.razor`

### 2. Totales por columna en grupos (Suma / Promedio / Minimo / Maximo / Contar) - NUEVO
Cuando la vista-tabla esta AGRUPADA, cada columna puede mostrar un total en la cabecera de grupo,
calculado sobre TODAS las filas del grupo (abarca subgrupos en multi-nivel). Es opt-in por columna
desde la flecha de la columna ("Opciones de columna"):

- Numericas: Sumar / Promediar / Minimo / Maximo / Contar (no vacios) / Sin total.
- No numericas: Contar (no vacios) / Sin total.
- Default al agrupar: Suma en columnas numericas; sin total en el resto.
- La eleccion se **persiste por modulo en localStorage** (clave `propia_agregado_<modulo>`), por navegador.
  No hay estado en servidor ni en BD.

**Alcance de esta tanda (rollout por olas):**
- **Unidades Privadas** (piloto, ya estaba desde la tanda previa; aqui se completa con Contar/Min/Max).
- **Contratos** (`/contratos`, Ola 1).
- **PQRSD** (`/pqrs`, Ola 1).
- Tareas ya tenia esta funcion de antes (implementacion de referencia; no se toco).

**Componentes compartidos tocados (reutilizables por las siguientes olas):**
- `Components/Shared/TablaColMenu.razor` - nuevo parametro `ModoAgregado` + record `AggOferta`
  + acciones `AgregarSuma/Promedio/Minimo/Maximo/Conteo/Ninguno`. Si un modulo no pasa `ModoAgregado`,
  el menu no muestra la seccion (retrocompatible).
- `Components/Shared/TablaGruposRenderTr.razor` - nuevo parametro opcional `CabeceraTemplate`
  (`RenderFragment<NodoGrupo<T>>`). Si es null, render clasico de una sola celda con colspan
  (Seguros y cualquier otro consumidor siguen igual). El modulo que quiere totales aporta el `<tr>`
  completo de cabecera.
- `wwwroot/css/modules/tabla-grupos.css` - celdas de agregado (`.tgrt-agg/.tgrt-aggv/.tgrt-aggtag`)
  centralizadas + la franja de grupo movida al nivel `<td>` (ver punto 3). **Bump a `?v=3`** en `App.razor`.

### 3. Fix: la franja (fajon) del total de grupo se cortaba en el primer total
El fondo y el divisor de la fila de grupo estaban en `.tgrt-lead` (el span interno de la celda lider),
por lo que se cortaban en la primera celda de agregado. Se movieron a nivel de `.tgrt-hd > td`, asi la
franja cubre TODA la fila tambien cuando hay totales por columna. Con una sola celda (colspan) se ve
identico a antes. (`wwwroot/css/modules/tabla-grupos.css`).

---

## Archivos modificados (10)

```
src/Propia.Web/Propia.Web.csproj                                 (Version 0.0.239 -> 0.0.240)
src/Propia.Web/Components/App.razor                              (tabla-grupos.css ?v=2 -> ?v=3)
src/Propia.Web/Components/Shared/CrearCampoModal.razor           (decimales 0..6)
src/Propia.Web/Components/Shared/EditarCampoSistemaModal.razor   (decimales 0..6)
src/Propia.Web/Components/Shared/ConfigCamposEntidad.razor       (decimales 0..6)
src/Propia.Web/Components/Shared/TablaColMenu.razor              (ModoAgregado / AggOferta / acciones)
src/Propia.Web/Components/Shared/TablaGruposRenderTr.razor       (CabeceraTemplate opcional)
src/Propia.Web/Components/Pages/Capa2/DistribucionPanel.razor    (Unidades: Contar/Min/Max)
src/Propia.Web/Components/Pages/Capa2/Servicios.razor            (Contratos: totales en grupos)
src/Propia.Web/Components/Pages/Capa2/PqrsKanban.razor           (PQRSD: totales en grupos)
src/Propia.Web/wwwroot/css/modules/tabla-grupos.css             (celdas agg + franja full-row)
```

## Pasos de deploy

1. Mergear/desplegar el codigo de Web (no requiere `dotnet ef database update`).
2. Build normal (`dotnet build`), `dotnet restore` sin cambios (sin paquetes nuevos).
3. No hay variables de entorno ni config nueva.
4. La API no requiere redeploy por esta tanda (sin cambios de contrato).
5. El bump de `tabla-grupos.css?v=3` invalida el cache del CSS; los clientes veran la franja corregida.

## Rollback

Revertir el deploy de Web a 0.0.239. No hay cambios de datos ni de esquema, por lo que el rollback es
limpio. La unica huella en cliente es la clave localStorage `propia_agregado_*`, inocua (se ignora si el
codigo no la usa).

## Verificacion en runtime (hecha, demo Conjunto Altos del Bosque)

- **PQRSD** (`/pqrs`, vista Tabla, agrupado por Categoria):
  - Columna "Vence" (numerica): Suma = `-731 d` (default); cambiada a Promedio = `-66,45 d PROM`.
  - Menu de columna ofrece los 6 modos; el modo activo queda marcado con check.
  - La franja del grupo cubre toda la fila (sin corte en el primer total).
- **Contratos** (`/contratos`, vista Tabla, agrupado por Vencimiento):
  - Franja full-row correcta; Contar sobre una columna con datos reales devuelve el conteo por grupo.
  - (La columna $ no tiene valores cargados en la data demo, por eso la suma sale vacia = comportamiento
    correcto cuando no hay valores.)
- **Unidades Privadas** (`/distribucion`, agrupado): suma de Coeficiente sobre el grupo (verificado en la
  tanda previa); Contar usa la clave de orden (cubre todas las columnas).

## Notas tecnicas

- "Contar (no vacios)" usa un getter de texto dedicado al agregado (no el del filtro) en Contratos
  (`TextoAggC`) y PQRSD (`TextoAggP`), para cubrir correctamente las columnas de sistema por su campo
  real del DTO. En Unidades reusa `SortStr` (ya resolvia toda clave fija + `cd:`).
- Formato es-CO: moneda `$ N0`, porcentaje con sufijo `%`, resto `0.##`.
- Formulas / campos calculados (Funcion) quedan EXCLUIDOS del agregado numerico (no son fuente sumable).
