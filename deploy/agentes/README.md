# Plantillas de agentes IA (semillas del Super Admin)

Plantillas de referencia, exportadas de dev el 2026-11-11 (Web 0.0.280), para dejarlas iguales en produccion.
El Super Admin manda: estas plantillas son la fuente de los agentes de cada copropiedad.

| Archivo | Plantilla | Clave (agente del sistema) |
|---|---|---|
| `plantilla-agente-documental.json` | Agente Documental | `agente-documental` |
| `plantilla-auxiliar-administrativo.json` | Auxiliar Administrativo | `auxiliar-administrativo` |
| `plantilla-asistente-copropiedad.json` | Asistente de {COPROPIEDAD_NOMBRE} | - |

El "Auxiliar de Bienvenida" es agente de PLATAFORMA (PlatformKey `bienvenida`): no se exporta ni se copia a copropiedades.

## Como aplicarlas (con sesion de Super Admin)

API base del backend (Propia.Api). Todas las rutas exigen el token de un Super Admin.

1. `GET /admin/ai-agent-templates` -> buscar cada plantilla por `name`.
   - Agente Documental y Auxiliar Administrativo ya existen tras el deploy (las semillas las crean al arrancar si faltan,
     con su clave).
2. Por cada archivo: quitar los campos que empiezan por `_` y:
   - si existe: `PUT /admin/ai-agent-templates/{id}` con el cuerpo;
   - si no existe (el Asistente): `POST /admin/ai-agent-templates`.
   - `provider`: debe ser un proveedor HABILITADO y con llave VALIDA en prod (Super Admin > Proveedores IA). Los archivos
     traen `DeepSeek`; cambiarlo si en prod el proveedor operativo es otro.
3. Revisar y propagar a las copropiedades existentes:
   - `GET /admin/ai-agent-templates/{id}/propagacion` -> conteos AlDia / Desactualizadas / Editadas / Faltantes.
   - `POST /admin/ai-agent-templates/{id}/propagar` con `{"forzarEditadas": false, "crearFaltantes": true}`.
     Las copropiedades que editaron su prompt se respetan; las que no tienen el agente lo reciben.

Lo mismo se puede hacer desde la pantalla Super Admin > Plantillas de Agentes IA (Editar / "Actualizar copropiedades").
