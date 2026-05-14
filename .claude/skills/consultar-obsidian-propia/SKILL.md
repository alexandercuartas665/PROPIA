---
name: consultar-obsidian-propia
description: Consultar el vault de Obsidian del proyecto PROPIA antes de tomar decisiones de diseno, implementacion o cambios funcionales. USA cuando el usuario pida implementar un modulo (ej. "2.1 Onboarding", "modulo 2.3", "Super Admin", "Tareas y Proyectos"), cuando haya dudas sobre alcance, reglas de negocio, dependencias entre modulos, principios de arquitectura, actores, flujos o estructura de datos, o cuando una funcionalidad nueva pueda chocar con decisiones ya documentadas. NO uses para cambios puramente tecnicos (build, lint, refactor sin cambio funcional) ni cuando el usuario haya pegado la spec inline en el chat.
---

# Consultar el vault Obsidian de PROPIA

## Por que existe esta skill

PROPIA tiene 25 modulos especificados a detalle en un vault Obsidian. El vault es la **fuente de verdad** de alcance, reglas de negocio, actores, flujos, dependencias y principios de arquitectura. Implementar codigo sin consultar la spec correspondiente lleva a:

- Construir features fuera de alcance MVP (perdida de tiempo).
- Saltarse reglas criticas de seguridad o multi-tenancy.
- Romper dependencias entre modulos.
- Inventar comportamientos que ya estan resueltos.

Siempre que haya una duda sobre **que** o **por que**, la respuesta esta en el vault. El **como** se decide en codigo.

## Ubicacion del vault

```
C:\Users\acuartas\Documents\Personal\OneDrive\Clientes\05. Propia\02. Obsidian\PROPIA\PROPIA\
```

## Documentos clave (leer en este orden cuando hay duda macro)

1. **`02. INVENTARIO MODULOS\INVENTARIO GENERAL.md`** - Indice navegable de los 25 modulos. **Punto de partida siempre.** Aqui esta el codigo del modulo, su objetivo, la fase, la matriz de dependencias y el orden de construccion.

2. **`02. INVENTARIO MODULOS\STACK TECNOLOGICO.md`** - Decisiones tecnicas (PostgreSQL + RLS, .NET 9 LTS, Blazor, EF Core, MudBlazor, Wompi, Anthropic SDK, etc.) y por que. Si vas a introducir una nueva libreria o tecnologia, contrastar primero contra este archivo.

3. **`02. INVENTARIO MODULOS\HOJA DE RUTA DESARROLLO.md`** - Hoja de ruta paso a paso. Donde estamos parados y cual es el siguiente paso.

4. **`01. REQUERIMIENTO\PropIA_Modulos_Maestro.md`** - Referencia macro de los 25 modulos y los **12 principios de arquitectura** (data del tenant, identidad unica, WhatsApp nativo, IA central, tareas como eje, etc.).

5. **`01. REQUERIMIENTO\PropIA_Roadmap_Dev.md`** - Fases del roadmap, alcance MVP por modulo, prioridades, dependencias y tablas DB clave.

## Carpetas de specs por modulo

```
01. REQUERIMIENTO\
    Capa 0. Consola de Administracion\   (4 modulos: 0.1 a 0.4)
    Capa 1. Administrador Multi-tenant\  (5 modulos: 1.1 a 1.5)
    Capa 2. Copropiedad\                 (16 modulos: 2.1 a 2.16)
```

Cada archivo se llama `{codigo}. {nombre-kebab}_v{version}.md` y tiene estructura: Descripcion general, Principios, Actores, Flujos, Funcionalidades, Tablas DB, Dependencias.

## Cuando activar esta skill

**Activa SIEMPRE que:**

- El usuario diga "implementa modulo X", "modulo 2.X", "Super Admin", "Onboarding", "Mi Copropiedad", "Tareas", "Asambleas", "PQRS", "Mantenimiento", "Portería", "Reservas", "Comunicaciones", "Documentos", "Reportes", "Directorio", "Billing", etc.
- Vayas a disenar un nuevo endpoint, entidad, flujo, pantalla, regla o integracion del producto.
- Haya duda sobre alcance (que entra en MVP y que en Fase 2/3).
- Surja una pregunta sobre dependencias entre modulos.
- Estes por introducir una decision de arquitectura nueva.
- El usuario refiera un termino del dominio que pueda estar en la spec (ej. "ficha viva", "selector tipo Slack", "Comite de Convivencia", "transferencia de custodia").

**NO actives si:**

- El usuario pego la especificacion completa en el chat.
- La tarea es puramente tecnica sin cambio funcional (formatear codigo, renombrar variables, fix de linter).
- El usuario explicitamente dice "no consultes el vault" o "voy a darte yo el contexto".

## Como aplicar la skill (procedimiento)

1. **Identifica el modulo afectado.** Si no esta claro, leer primero `INVENTARIO GENERAL.md` para ubicar el codigo del modulo.

2. **Lee la spec del modulo** en la carpeta de su capa. Resume mentalmente: alcance MVP, alcance Fase 2/3, actores, dependencias, tablas DB, reglas criticas.

3. **Lee las specs de modulos dependientes** que aparecen en la matriz de dependencias del inventario.

4. **Verifica los 12 principios de arquitectura** en `PropIA_Modulos_Maestro.md`. No los rompas.

5. **Confirma alineacion con el stack** en `STACK TECNOLOGICO.md` antes de elegir una libreria o patron.

6. **Reporta al usuario** un resumen corto antes de codear:
   - Modulo: codigo y nombre.
   - Alcance que vas a cubrir en esta iteracion.
   - Que queda fuera (Fase 2/3 o futuro).
   - Dependencias necesarias.
   - Reglas criticas que vas a respetar.

7. **Procede a implementar** unicamente despues de la confirmacion del usuario (o si el usuario dijo claramente "implementalo end-to-end").

## Como mantener sincronizado vault y codigo

- Al cerrar un modulo: actualizar el tracker en `INVENTARIO GENERAL.md` con la nueva version/fecha/estado.
- Si una decision de codigo contradice la spec: NO codear hasta consultar al usuario. La spec es la verdad hasta que se actualice.
- Cambios funcionales aprobados por el usuario que afecten la spec: pedir al usuario que actualice la nota del modulo en Obsidian antes de mergear.

## Ejemplo de uso

**Usuario:** "Implementa el modulo 2.6 Presupuesto y Pagos."

**Yo (activando esta skill):**

1. Leo `INVENTARIO GENERAL.md` para confirmar codigo y dependencias de 2.6.
2. Leo `01. REQUERIMIENTO\Capa 2. Copropiedad\2.6. Presupuesto-cuotas-pagos_v1_0.md`.
3. Leo dependencias: 2.3 Mi Copropiedad (consume coeficientes) y 2.7 Cartera (es alimentado por 2.6).
4. Reviso principios: "sin modulo contable propio - integracion con Siigo/Alegra via API".
5. Reviso stack: Wompi como pasarela, Siigo/Alegra para contabilidad.
6. Reporto al usuario: "Voy a implementar 2.6 con alcance MVP X, dependiendo de 2.3 ya construido, integrando Wompi en sandbox. Queda para Fase 2: integracion Siigo. Confirmas?"
7. Implemento tras confirmacion.

## Recordatorio final

**Si tienes duda, ve al vault. Siempre.** El vault no es opcional - es la unica fuente de verdad de que es PROPIA. Codigo sin spec es codigo que se reescribe.
