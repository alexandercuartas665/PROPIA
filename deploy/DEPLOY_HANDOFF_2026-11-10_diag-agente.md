# DEPLOY HAND-OFF - 2026-11-10 - Arnes de diagnostico del agente "pensando" (prod)

> Estado: **COMITEADO Y PUSHEADO a `main`**. Commit `a73559e2`
> ("feat(diag): arnes de logs para cazar agente que 'piensa' pero el server responde 200").
> `origin/main == a73559e2`. Listo para deployar. Reemplaza la nota previa
> `Downloads/deploy/HANDOFF_DIAG2_logs-circuito-agente_COMO-USAR.md` (mismo contenido, ya en el repo).

## 0. Para que es

Cazar el fallo de produccion (reportado en el Lenovo de Alex): al probar el **Agente Documental
(DeepSeek)** la UI queda en "Escribiendo..." aunque el backend ya respondio. Esta entrega **no arregla**
el bug: instala logs/traza para **ubicar donde muere** la actualizacion server->cliente. Es aditivo,
reversible y **no cambia el comportamiento** sin activar sus flags. Build verde, sin warnings nuevos.

## 1. Que entra en el deploy (commit a73559e2, 4 cambios, SIN migracion)

1. **`src/Propia.Web/Diagnostics/DiagnosticCircuitHandler.cs`** (nuevo): loguea el ciclo del circuito
   Blazor en el server -> `[CIRCUIT] abierto / conexion ARRIBA / conexion ABAJO / cerrado` con el
   `CircuitId`. El `ABAJO` justo tras una accion delata que se perdio el canal en vivo.
2. **`src/Propia.Web/Program.cs`**: `DetailedErrors` se puede ENCENDER en prod con la env var
   `Blazor__DetailedErrors=true` (para que una excepcion de render/handler del server llegue a la
   consola del cliente). Registra el handler de arriba.
3. **`src/Propia.Web/Components/App.razor`**: modo diagnostico de cliente **opt-in con `?diag=1`** en la
   URL (persiste en localStorage; apagar con `?diag=0`). Imprime en consola: el **transporte** del
   circuito (WebSockets vs LongPolling) con su traza, y CUALQUIER error JS no capturado
   (`[PROPIA-DIAG] window.error` / `promesa rechazada`). Sin el flag no hace nada.
4. **`src/Propia.Web/Components/Pages/Capa2/IaAgentes.razor`**: loguea `[PROBAR] inicio ...` y
   `[PROBAR] fin ... ok=.. ms=.. -> render emitido` al probar un agente.

## 2. Deploy

- Deployar `main` (Railway, servicio Web) en `a73559e2`. **No hay migracion ni cambio de BD.**
- **Sin bump de version** (los cambios estan detras de flags; el pie sigue en v0.0.278). Si quieres el
  bump de release "como siempre", decimelo y lo hago en un commit aparte; no es necesario para esto.
- OPCIONAL (recomendado para esta caceria): en Railway, servicio Web, setear
  **`Blazor__DetailedErrors=true`** y redeploy. **Es TEMPORAL: quitarla al terminar** (expone detalle de
  excepciones del server al cliente).

## 3. Reproduccion por Alex (Lenovo)

1. Ir a **`https://app.propia-ad.com/ia/agentes?diag=1`** (el `?diag=1` queda guardado).
2. F12 -> **Console** (dejar "Preserve log" ON) y pestana **Network** (filtro WS).
3. Abrir el Agente Documental, escribir "hola", Enviar. Esperar ~90s.
4. Copiar/screenshot de la consola completa (sobre todo lineas `[PROPIA-DIAG]`,
   `Selecting transport ...`, `WebSocket connected` o `LongPolling`, y cualquier `error`).

## 4. Leer la evidencia -> causa (cruzar consola del Lenovo con logs de Railway: `[CIRCUIT]` y `[PROBAR]`)

| Consola del Lenovo | Railway | Causa | Accion |
|---|---|---|---|
| `Selecting transport 'LongPolling'` y/o el GET de recepcion queda pending | `[CIRCUIT] conexion ABAJO` cerca del envio | WebSocket bloqueado -> fallback long-polling + recepcion retenida por proxy/red/VPN/extension | Hotspot del celular; Incognito sin extensiones; otra red. Es entorno, no codigo |
| `WebSocket connected` y SIN `[PROPIA-DIAG] window.error` | `[PROBAR] fin ... render emitido` pero UI pegada; sin `[CIRCUIT] ABAJO` | El server emitio el diff y el cliente no lo pinto (congelamiento de render) | `chrome://gpu` / desactivar aceleracion por hardware; otro navegador |
| `[PROPIA-DIAG] window.error: ...` o `promesa rechazada` al enviar | (lo que sea) | Un error JS del cliente aborta el pintado del DOM (extension/script) | Ver el stack; probar Incognito sin extensiones |
| (con `Blazor__DetailedErrors=true`) excepcion del SERVER en consola | excepcion en logs | Excepcion de render del server especifica de ese dato/cuenta | Mirar el stack; abrir bug de codigo |
| `[CIRCUIT] conexion ABAJO` repetido + overlay "Reconectando..." | idem | El circuito se cae de verdad (red inestable) | Red del Lenovo |

**Regla de oro:** si Railway muestra `[PROBAR] fin ... render emitido` y la UI sigue en "Escribiendo...",
queda **demostrado** que el server respondio y el fallo es cliente/transporte (NO el agente ni el backend).

## 5. Revertir

- Quitar la env var `Blazor__DetailedErrors` (o ponerla en false) y redeploy.
- El `?diag=1` es opt-in; se apaga con `?diag=0` o limpiando el sitio. Los logs `[CIRCUIT]`/`[PROBAR]`
  son baratos y se pueden dejar; si molestan, subir la categoria `Propia.Web.Diagnostics` a Warning.
- El codigo es revertible con `git revert a73559e2` (no toca BD).

## 6. Estado / verificacion

- `dotnet build` del Web: **verde, sin warnings nuevos** (los 4 archivos tocados no generan warnings;
  los que salen son preexistentes de otros modulos).
- Verificado en dev en la sesion previa (Web 5105): banner diag + traza
  `Selecting transport 'WebSockets'`; server `[CIRCUIT] abierto/ARRIBA/ABAJO/cerrado`;
  `[PROBAR] inicio/fin ok=true ms=... -> render emitido`. Sin regresion en el flujo normal de probar.
- Antivirus abajo en el Lenovo daba el mismo error -> NO era Kaspersky; por eso este arnes apunta a
  transporte/render del circuito.
