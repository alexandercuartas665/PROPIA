# HAND-OFF DEPLOY - PROPIA

> **SERVICIOS PUBLICOS - "CREAR SERVICIOS CON IA" EN LA PESTANA CONFIGURACION (2026-11-13): SOLO Web, sin migracion.**
> - Ajuste pedido por Alex sobre la ola D: el boton "Subir facturas" ya no pasa a Recibos. Las facturas se procesan en la misma
>   pestana Configuracion: cada servicio se crea en la tabla (resaltado) y su recibo queda como primer mes del historial
>   (documento + desglose). Facturas combinadas crean un servicio por tipo. Si el servicio ya existe se usa; si el mes ya estaba
>   registrado no se reemplaza. Lista de resultados por factura en la tarjeta.

> **LINEAS WHATSAPP - WEBHOOK EN EL ALTA DE LINEA YCLOUD (2026-11-13): SOLO Web, sin migracion.**
> - Modal "Nueva linea" con proveedor YCloud: muestra la URL universal del webhook (`https://app.propia-ad.com/webhooks/ycloud`,
>   boton Copiar), los pasos en YCloud y un campo opcional para el secreto. Con secreto, la linea nace con el webhook configurado;
>   sin secreto, al crearla se abre solo el panel Webhook de la linea. El modal ahora tiene scroll y botones fijos abajo.

> **PLANTILLAS DE WHATSAPP COMO FUENTE UNICA (2026-11-13): SIN MIGRACION, SIN CONFIG NUEVA. API y Web + 1 JOB.**
> - Las plantillas de las lineas YCloud caen solas al modulo `/ia/plantillas`: al abrirlo (`POST /api/ia/plantillas/sincronizar`,
>   no repite antes de 2 min) y por el job nuevo **PlantillasWhatsAppSync** cada 30 min (corre donde `Jobs:Enabled=true`, es decir
>   en la API; itera copropiedades con SetTenant). Boton "Sincronizar ahora".
> - Columna "Se usa en" y aviso si un modulo apunta a una plantilla que no existe, no esta aprobada o esta archivada
>   (`GET /api/ia/plantillas/usos`). PQRSD > Configurar > Respuestas > WhatsApp: la plantilla se ELIGE de una lista con las
>   aprobadas del modulo (antes era texto libre; lo ya configurado sigue funcionando y se marca si no esta aprobado).
> - **Verificar en PROD**: abrir Plantillas de WhatsApp y ver "Sincronizado ahora (N lineas)..." con las plantillas de YCloud.

> **USUARIOS - CARGA POR PLANTILLA (2026-11-13): SIN MIGRACION, SIN CONFIG NUEVA. API y Web.**
> - Usuarios > "Descargar plantilla" (`GET /api/usuarios/plantilla`: hojas Instrucciones, Usuarios y Catalogos con la lista de
>   roles activos de la copropiedad) y "Cargar plantilla" (`POST /api/usuarios/importar`, permiso UsuariosAccesos/Crear).
> - Por fila: busca la persona por tipo+documento en el Directorio (si no existe la crea), da el alta en PENDIENTE con sus roles
>   y cargos, y NO envia invitacion. Activos no se tocan; inactivos no se reactivan; errores por fila con su motivo.

> **SERVICIOS PUBLICOS - OLA D, CREAR SERVICIOS CON IA (2026-11-13): SIN MIGRACION, SOLO Web.**
> - Configuracion > bloque "Crear servicios con IA" > "Subir facturas": pasa a Recibos en modo crear. Cada recibo cuyo servicio
>   no existe sale como "Servicio nuevo" con un solo boton "Crear servicio y registrar" (crea la cuenta con tipo, prestador,
>   referencia, unidad y dia de pago del recibo, y registra ese mes con documento y desglose). "Crear y registrar todos (N)".
>   Usa los endpoints existentes `recibos/agregar-servicio` y `recibos/confirmar` (requiere la migracion de la ola C).

> **SERVICIOS PUBLICOS - OLA C, FACTURAS COMBINADAS (2026-11-13): 1 MIGRACION (solo indice) + API y Web.**
> - **MIGRACION `20261113131102_ServiciosPublicosReferenciaPorTipo`** (va DESPUES de `AddYCloudRutaUniversal`): cambia el
>   indice unico de `cuentas_servicio_publico` de (tenant_id, referencia_normalizada) a (tenant_id, referencia_normalizada, tipo).
>   No toca datos ni RLS; las cuentas actuales siguen siendo validas. Aplicada en dev. En prod: solo con OK de Alex.
> - Un contrato puede tener un servicio POR TIPO (EMCALI: energia, acueducto y aseo con el mismo numero). Una factura que
>   cobra varios servicios se reparte en una tarjeta por servicio (clave nueva `servicios` del perfil `recibo_servicio`),
>   cada una se valida contra el servicio de SU tipo y todas comparten el documento. Si el contrato no tiene servicio de ese
>   tipo, la tarjeta ofrece "Crear servicio de <tipo>".
> - **Verificar en PROD tras aplicar**: `select indexdef from pg_indexes where indexname like 'IX_cuentas_servicio_publico_tenant_id_referencia_normalizada%';`
>   debe incluir `tipo`.

> **SERVICIOS PUBLICOS - OLA B (2026-11-13): SIN MIGRACION, SIN CONFIG NUEVA. API y Web.**
> - Al leer un recibo, si es de OTRO tipo que el servicio con el que coincidio la referencia (ej. recibo de aseo de EMCALI en
>   el servicio de energia con el mismo contrato) o mide en otra unidad, la tarjeta lo avisa antes de confirmar.
> - Consumo: si la IA no lo trae pero hay lecturas, se registra lectura actual - anterior. Los consumos se muestran con
>   decimales (0,19 ya no se ve como 0). Instrucciones de lectura del perfil `recibo_servicio` reforzadas (tipo por lo que
>   COBRA el recibo; consumo nunca es la tarifa ni los dias).
> - Historial: flecha por mes que despliega el desglose en linea.

> **SERVICIOS PUBLICOS - OLA A (2026-11-13): SIN MIGRACION, SIN CONFIG NUEVA. API y Web.**
> - Historial de recibos (`/servicios` > Recibos y consumos): columna **Documento** (Ver / "Sin guardar") y boton para
>   **eliminar UN recibo** (un mes) con confirmacion; tambien desde el modal Desglose ("Eliminar este recibo"). Borra el mes,
>   su desglose (cascada) y su documento del storage si ningun otro registro lo usa.
> - `OcrController`: si el documento origen NO se puede guardar en el storage, ahora queda en el log como
>   `[OCR] No se pudo guardar el documento origen ...` (antes se perdia en silencio) y la tarjeta del recibo lo avisa.
> - **ACCION EN PROD tras el deploy (Alex reporto que en prod el PDF no queda guardado):** cargar un recibo PDF en `/servicios`
>   y buscar en el log de Railway `[OCR] No se pudo guardar el documento origen`: la excepcion dice por que falla R2
>   (credenciales, bucket, permisos). Reportar el mensaje exacto.

> **DICTADO POR VOZ + "MEJORAR REDACCION" CON IA (2026-11-13): SIN MIGRACION, SIN CONFIG NUEVA. API y Web.**
> - Microfono en las cajas `textarea[data-dictado]` (voz a texto del navegador, `js/propia-dictado.js?v=11` +
>   `css/propia-dictado.css?v=11`) y boton ✨ que propone una version mejorada del texto (el usuario decide si la usa). En
>   celular la propuesta sale como hoja inferior sobre el teclado. Activo SOLO en PQRSD (radicar, gestion, comentarios,
>   formulario publico), Polizas/Seguros y Tareas (descripcion, comentarios, campos adicionales).
> - Fix c0388aa7: el panel desaparecia al navegar por el menu sin recargar (Blazor quitaba los nodos del body).
> - Endpoint `POST /api/texto/mejorar` ([Authorize], cualquier usuario de la copropiedad) -> agente del sistema por clave
>   `corrector-redaccion`. Seeder `CorrectorTextoSeeder` (Api y Web, al arrancar): crea la plantilla global "Corrector de
>   redaccion" si no existe, con el proveedor IA habilitado de llave mas reciente; el agente se crea en cada copropiedad al
>   primer uso. **Verificar en PROD**: Super Admin > Plantillas de Agentes IA muestra "Corrector de redaccion" y su proveedor
>   tiene llave vigente (si no, cambiar el proveedor de la plantilla y "Actualizar copropiedades").
> - El modal muestra "minimo 10 caracteres - faltan N" y el boton Radicar dice cuanto falta.

> **WEBHOOK YCLOUD - URL UNIVERSAL (2026-11-13): 1 MIGRACION (funcion SQL) + API y Web.**
> - **MIGRACION `20261113002100_AddYCloudRutaUniversal`** (va DESPUES de `AddYCloudWebhookSecret`): crea la funcion
>   `resolver_tenants_ycloud(text)` SECURITY DEFINER (solo lectura, devuelve tenant_id de las lineas YCloud con ese numero) con
>   GRANT EXECUTE a propia_app y REVOKE a PUBLIC. Mismo patron que `admin_list_active_agents` (requiere que el owner de la
>   migracion pase el RLS, como ya ocurre en prod).
> - **URL para TODAS las copropiedades: `https://app.propia-ad.com/webhooks/ycloud`** (sin tenant). La copropiedad sale del
>   numero de la linea del evento y la firma se valida con el secreto de esa linea. Sirve para una cuenta YCloud por
>   copropiedad o una cuenta compartida (un solo endpoint en YCloud). La ruta con tenant `/webhooks/ycloud/{tenantId}` sigue viva.
> - Un numero YCloud ya no puede registrarse en dos lineas (ni en dos copropiedades).
> - **Verificar en PROD tras aplicar**: `select has_function_privilege('propia_app','resolver_tenants_ycloud(text)','EXECUTE');` (t).

> **WEBHOOK DE ENTRADA DE YCLOUD (2026-11-12): 1 MIGRACION (solo columna) + API y Web.**
> - **MIGRACION `20261112234705_AddYCloudWebhookSecret`** (va DESPUES de `PorteriaHuecosOperacion`): columna
>   `whats_app_lines.y_cloud_webhook_secret_encrypted` (text, nullable). Tabla existente (RLS ya aplica).
> - Endpoint publico `POST /webhooks/ycloud/{tenantId}`: mensajes entrantes (`whatsapp.inbound_message.received`, entran a la
>   misma ingesta que Meta/Evolution: conversaciones, lista negra, agentes) y estados de entrega (`whatsapp.message.updated`).
>   Firma `YCloud-Signature: t=..,s=..` (HMAC-SHA256 de "t.cuerpo" con el secreto del endpoint, ventana 10 min).
>   **En Production sin secreto configurado responde 401** (`webhook_not_configured`).
> - Configuracion por copropiedad (no hay variables de entorno): Lineas WhatsApp > boton **Webhook** de la linea YCloud muestra
>   la URL (`https://app.propia-ad.com/webhooks/ycloud/{tenantId}`); en YCloud > Developers > Webhooks se crea el endpoint con
>   esa URL y los 2 eventos, y el secreto (signing secret) se pega en PROPIA.

> **PLANTILLAS DE WHATSAPP (2026-11-12, rama feat/plantillas-whatsapp integrada a main): 1 MIGRACION (TABLA NUEVA con RLS) + API y Web.**
> - **MIGRACION `20261105113252_AddPlantillasWhatsApp`**: tabla `plantillas_whatsapp` (tenant, RLS ENABLE + FORCE + policy
>   tenant_isolation + GRANT a propia_app, en la misma migracion). OJO: queda INTERCALADA (timestamp anterior a migraciones de
>   main ya aplicadas en prod); `dotnet ef database update` la aplica igual porque no esta en `__EFMigrationsHistory`. No regenerar.
>   Correrla a mano DESPUES del deploy, solo cuando Alex lo pida. En dev ya esta aplicada.
> - Pagina `/ia/plantillas` (menu Comunicacion y documentos, "Plantillas de WhatsApp", orden 11). Endpoints `api/ia/plantillas*`
>   (importar desde YCloud por la linea WhatsApp, {id}, {id}/archivar, {id}/probar). Sin config nueva: usa ApiKey + FromNumber de
>   la linea YCloud y resuelve la WABA por API.
> - **Verificar en PROD tras aplicar**: `select relforcerowsecurity from pg_class where relname='plantillas_whatsapp';` (t).

> **PORTERIA: HUECOS DE OPERACION + UX GUARDA DE EDAD + MANUAL (2026-11-12): 1 MIGRACION (solo columnas) + API y Web + 1 AJUSTE DE MENU.**
> - **MIGRACION `20261112201435_PorteriaHuecosOperacion`** (va DESPUES de `AsambleaOla5Sala`): `correspondencias.entregado_doc`
>   (varchar 60), `correspondencias.recordatorios` (int, default 0), `correspondencias.ultimo_recordatorio_at`,
>   `novedades_turno.intervencion_id` (uuid). Tablas existentes (RLS ya aplica). Sin datos tocados.
> - **MENU (prod)**: "Porteria y accesos" (`op-porteria`) viene OCULTO en `menu_overrides` (hidden = true). Activarlo en
>   **Super Admin > Menu** (o `update menu_overrides set hidden=false where node_key='op-porteria'`). En dev ya esta activo.
> - Endpoints nuevos: `POST api/porteria/correspondencia/{id}/notificar` (recordatorio al residente),
>   `POST api/porteria/turnos/{turnoId}/alerta-seguridad`. `PUT .../entregar` ahora EXIGE quien retira (+ documento opcional).
> - Avisos: paquete registrado/recordatorio/devuelto -> al residente de la unidad (app + canal de Configuracion de Porteria:
>   WhatsApp/correo/ninguno); novedad -> administracion siempre, residente si es sobre una unidad, vigilancia (guardas de
>   turnos activos + usuarios con cargo portero/vigilancia/seguridad) con prioridad Critica si es de seguridad. Con
>   "Generar tarea" (opcion RN-14 de la copropiedad): dano/mantenimiento sobre equipo o zona -> intervencion correctiva en
>   Mantenimiento (origen Porteria); lo demas -> tarea en Tareas (origen "Porteria", tablero General).
> - UI: pestanas grandes con icono, Vehiculos y Paquetes con modales, boton "Alerta de seguridad" y "Manual"
>   (`/ayuda/porteria.html`, estatico). `porteria.css?v=5`.

> **ASAMBLEAS OLA 5: SALA VIRTUAL CON LIVEKIT (2026-11-12): 1 MIGRACION (solo columnas) + CONFIG NUEVA (env) + API y Web.**
> - **MIGRACION `20261112172720_AsambleaOla5Sala`** (va DESPUES de `AsambleaOla4Transcripcion`): `sesiones.grabacion_egress_id`
>   (varchar 100), `sesiones.grabacion_iniciada_en`, `sesion_participantes.salida_pendiente_en` (timestamptz, nullable). Sin
>   tablas nuevas (RLS ya existente) y sin datos tocados.
> - **VARIABLES NUEVAS en Railway (API y Web)** - las carga Alex con su cuenta de LiveKit (Cloud o propio):
>   `LiveKit__Url` (wss://<proyecto>.livekit.cloud), `LiveKit__ApiKey`, `LiveKit__ApiSecret`. Sin ellas la ficha muestra
>   "La sala virtual no está configurada" y todo lo demas funciona igual.
> - **Webhook en LiveKit** (Settings > Webhooks): URL `https://app.propia-ad.com/api/livekit/webhook` (o el host del API), firmado
>   con la misma API key. Sin webhook la videollamada funciona pero la asistencia NO se marca sola.
> - **Grabacion (opcional)**: `LiveKit__Grabacion__Bucket`, `__AccessKey`, `__Secret`, `__Endpoint` (R2:
>   https://<account>.r2.cloudflarestorage.com), `__Region` (auto), `__PublicUrl` (dominio publico del bucket para descargar).
>   Usar un token R2 propio con permiso solo de escritura en ese bucket (LiveKit lo recibe para subir el MP4). Sin estas
>   variables el boton Grabar queda deshabilitado. La grabacion real (egress) NO se probo: solo con tests.
> - Funcionamiento: la administracion entra como moderadora desde la ficha (pestana Sala virtual); el copropietario o apoderado
>   entra desde su enlace de la asamblea. Al entrar quedan PRESENTES todas sus unidades habilitadas; al salir, la salida se
>   confirma tras la gracia de reconexion (Configuracion de asambleas, 60 s por defecto). Manos, palabra, chat y "silenciar a
>   todos" van por el canal de datos de LiveKit (el chat no se guarda). Cliente `livekit-client@2.5.7` desde jsdelivr (CSP ya
>   lo permite). `propia-sala.js?v=2`, `asambleas.css?v=3`.
> - Gotcha local: el webhook a `http://host.docker.internal:5153` falla porque el API redirige a https con certificado de
>   localhost; para probar en local se levanta una instancia del API solo en http (otro puerto) para el webhook.

> **ASAMBLEAS OLA 4: TRANSCRIPCION DE LA SESION (2026-11-12): 1 MIGRACION (TABLA NUEVA con RLS) + API y Web.**
> - **MIGRACION `20261112162627_AsambleaOla4Transcripcion`** (va DESPUES de `AsambleaOla3Acta`): tabla nueva
>   `sesion_transcripcion_lineas` (sesion, punto, hora, tipo Voz/Sistema, persona, unidad, autor, texto, confianza, origen) con
>   tenant_id, **RLS ENABLE + FORCE + policy tenant_isolation + GRANT a propia_app** (en la misma migracion) e indice
>   (sesion_id, hora); `sesiones.transcripcion_activa` (bool, default false). Sin datos tocados.
> - Bitacora: eventos del sistema automaticos (sala, instalacion, puntos, votaciones con resultado, poderes, cierre) + voz a
>   texto con "Mi voz a texto" (Web Speech API del NAVEGADOR, Chrome/Edge; el servidor solo recibe texto). Proveedor de pago
>   preparado detras de `ITranscripcionProvider` (hoy `NavegadorTranscripcionProvider`). Endpoints: `GET/POST
>   api/asambleas/{id}/transcripcion`, `PUT {id}/transcripcion/activa`.
> - Subtitulos en la pagina publica del residente (refresco cada 4 s con transcripcion activa). El acta (ola 3) usa la
>   transcripcion: resumen por punto en el borrador base y texto completo para la IA. `propia-ui.js?v=30`.
> - El microfono requiere HTTPS (o localhost) y permiso del navegador: en prod (https://app.propia-ad.com) aplica.
> - **Verificar en PROD tras aplicar**: `select relforcerowsecurity from pg_class where relname='sesion_transcripcion_lineas';` (t).

> **ASAMBLEAS OLA 3: ACTA REDACTADA POR IA + DOBLE FIRMA + PUBLICACION (2026-11-12): 1 MIGRACION (aditiva) + API y Web.**
> - **MIGRACION `20261112151947_AsambleaOla3Acta`** (va DESPUES de `AsambleaOla2Ley675`): solo columnas en `actas` (numero,
>   borrador_html, estado/error/fecha de redaccion, redactada_por_ia, editada, firma presidente y secretario: fecha, usuario,
>   metodo, ip; comunicado_id) + indice UNICO parcial `(tenant_id, numero) WHERE numero IS NOT NULL`. Sin datos tocados.
> - **Agente IA nuevo "Redactor de actas"** (clave `redactor-actas`): lo siembra `AgenteActaAsambleaSeeder` al arrancar (Api y
>   Web), con el primer proveedor habilitado; cada copropiedad recibe su agente al primer uso. **Verificar en PROD que el
>   proveedor/API key de IA este vigente** (en dev la key estaba "cifrada con version anterior" y la redaccion cayo al
>   borrador base, que es el comportamiento previsto).
> - Al cerrar con quorum: acta No. AAAA-NNN + borrador base con los datos del sistema + redaccion por IA en segundo plano.
>   Editar/Regenerar solo sin firmas. Firmas: presidente y secretario desde su cuenta o con codigo de 6 digitos al correo
>   (SMTP global). Con las dos -> inmutable con hash SHA-256. **En Development el codigo NO se envia por correo: vuelve en la
>   respuesta** (como /dev/login); en Production siempre se envia.
> - Publicar: PDF con membrete (plantilla "Acta de asamblea", categoria Asambleas, se crea sola si falta) en Documentos +
>   comunicado en BORRADOR en Comunicaciones (no se envia solo). Plazo legal mostrado: 20 dias habiles (art. 47).
> - Se retiraron la firma unica vieja (`FirmarActaAsync` con narrativa) y su DTO. `propia-ui.js?v=29` (`propiaUI.htmlDe`).
> - **Verificar en PROD tras aplicar**: el agente "Redactor de actas" aparece en Super Admin > Plantillas de Agentes IA.

> **ASAMBLEAS: BANDEJA CANONICA + FICHA Y ASISTENTE EN MODAL (ola 1) Y REGLAS DE LA LEY 675 (ola 2) (2026-11-12): 1 MIGRACION
> (aditiva) + API y Web cambian.**
> - **MIGRACION `20261112131928_AsambleaOla2Ley675`** (va DESPUES de `AddSuperAdminApiTokens`): solo columnas nuevas, sin
>   tablas ni datos tocados. `sesiones`: `sala_propia` (bool, default false), `presidente_persona_id`, `presidente_nombre`,
>   `secretario_persona_id`, `secretario_nombre`, `instalada_en`, `quorum_instalacion_pct`, `justificacion_quorum`,
>   `motivo_cancelacion`, `punto_actual_id` (todas nullable). `sesion_documentos`: `tipo` (int, default 0 = Otro). Las tablas
>   ya tienen RLS: no hay policy nueva.
> - **Comportamiento nuevo (Ley 675)**: sin INSTALACION (presidente + secretario, con quorum) no se abren votaciones; el
>   quorum es "MAS de la mitad" (o el criterio de Configuracion) y en segunda convocatoria basta un numero plural (2+
>   unidades); al cerrar, el quorum es el de la instalacion (corregirlo exige justificacion) y los puntos sin tratar quedan
>   "No tratado"; no se cierra con una votacion abierta; una votacion a la vez. **Sesiones que esten EN CURSO al desplegar
>   no tienen instalacion: hay que instalarlas antes de abrir votaciones** (o cerrarlas con correccion justificada).
> - Al CITAR se valida el plazo de citacion (ordinaria bloquea; extraordinaria/consejo/comite pide confirmacion -> 409) y se
>   genera la **Convocatoria en PDF** (membrete predeterminado + Chromium + storage) como documento de la sesion. Si el PDF
>   falla, la citacion queda hecha y responde con aviso. Los participantes se citan a nombre del PROPIETARIO activo de la
>   unidad (antes: el admin como placeholder).
> - Endpoints nuevos en `api/asambleas`: `PUT {id}` (MERGE), `DELETE {id}` (solo Borrador/Cancelada), `POST {id}/puntos`,
>   `DELETE puntos/{id}`, `PUT {id}/cancelar`, `POST {id}/segunda-convocatoria` (3er dia habil 8 p. m. con festivos),
>   `PUT {id}/instalar`, `PUT puntos/{id}/iniciar|tratado`, `POST votaciones/{id}/votar-bloque`, `POST {id}/documentos/archivo`.
>   Poderes: un poder por unidad, apoderado del directorio de la copropiedad, limite por persona de Configuracion.
> - **Web**: `/asambleas` = bandeja canonica de solo lectura; la ficha y "Nueva sesion" son modales (`/asambleas/{id}` y
>   `/asambleas/nueva` quedan como enlaces). Paginas `AsambleaFicha`/`AsambleaNueva` borradas. CSS nuevo
>   `css/modules/asambleas.css?v=1`, `bandeja-canonica.css?v=3`. Manual estatico en `/ayuda/asambleas.html` (+ imagenes).
> - Sin config/env nueva. **Verificar en PROD tras aplicar**: abrir /asambleas, crear una sesion de prueba en Borrador,
>   citarla (ver la Convocatoria en Documentos) y eliminarla/cancelarla.

> **API DE SUPER ADMIN CON TOKENS + API DE TODAS LAS SEMILLAS (2026-11-12): 1 MIGRACION (tabla nueva global) + API y Web.**
> - **MIGRACION `20261112110203_AddSuperAdminApiTokens`** (va DESPUES de `CorregirEtiquetasListasEnum`): tabla global
>   `super_admin_api_tokens` (sin tenant_id, sin RLS; FK a `super_admin_usuarios`, indice unico por `token_hash`) + GRANT a
>   propia_app. Solo guarda el sha256 del token.
> - **Tokens**: Super Admin > Plataforma > "Tokens de API" crea tokens `pak_...` (se muestran UNA vez; vencen en 30/90/365
>   dias o nunca; se revocan). Con `Authorization: Bearer pak_...` se llama TODA la API de Super Admin (politica SuperAdmin),
>   con el poder del Super Admin que lo creo, EXCEPTO: gestionar tokens, MFA y alta/baja del equipo A&D (exigen sesion).
>   No abre endpoints de copropiedad. Escrituras con token -> `super_admin_logs` (`API_TOKEN_USO`).
> - **Semillas**: `GET /api/admin/semillas` = indice de todo lo que siembra el Super Admin con sus rutas. Nuevos CRUD en
>   `/api/admin/semillas/{roles|comunicados|etiquetas-directorio|etiquetas-documentos|reportes}` (PUT MERGE; reportes
>   solo editar/activar; "Administrador" no se renombra ni desactiva; rol en uso no se elimina).
> - Auth: nuevo esquema `SuperAdminApiToken` en los dos hosts (Api y Web); el JwtBearer ignora los `pak_`. Sin config/env nueva.
> - **Verificar en PROD tras aplicar**: entrar a Super Admin > Tokens de API, crear uno de 30 dias, probar
>   `curl -H "Authorization: Bearer pak_..." https://app.propia-ad.com/api/admin/semillas` (200) y revocarlo (401).

> **ETIQUETAS LEGIBLES EN LISTAS DE ENUM + FIN DE `eval` BLOQUEADO POR LA CSP (2026-11-12): 1 MIGRACION (solo datos) +
> Infrastructure y Web cambian.**
> - **MIGRACION `20261112100618_CorregirEtiquetasListasEnum`** (va DESPUES de `AddMembretesDocumento`; sin cambio de
>   esquema): en `catalogo_opciones` y `lista_opciones` pasa a texto legible las etiquetas sembradas con el nombre crudo
>   del enum: Mantenimiento estado de intervencion `EnEjecucion`/`EnRevision` -> "En ejecución"/"En revisión",
>   periodicidad `Unica` -> "Única"; Equipos categoria `Climatizacion`/`Recreacion` -> con tilde; Zonas categoria
>   `Circulacion` -> "Circulación" y estado `EnMantenimiento` -> "En mantenimiento"; Directorio sexo `NoBinario`/
>   `NoIdentificado` -> "No binario"/"No identificado". SOLO donde la etiqueta es exactamente la cruda (no pisa lo que
>   A&D renombro ni lo que la copropiedad edito: `override_visual = false`). Suspende el FORCE de `lista_opciones` solo
>   durante el UPDATE. Claves `e:<Enum>` y registros sin cambios. En dev: 0 etiquetas crudas restantes.
> - La semilla (`CatalogoListasSeeder.EtiquetasEnum`) ya siembra las legibles; el fold de overrides viejos acepta el
>   nombre crudo como alias (no duplica opciones).
> - **Web**: la CSP no permite `eval`; 8 llamadas `Js.InvokeVoidAsync("eval", ...)` fallaban en silencio. La mas grave:
>   al terminar el Onboarding (paso 5, activar) la sesion no se guardaba ni redirigia a /mi-copropiedad. Ahora usan
>   funciones de `propia-ui.js` (`?v=28`) y el boton de Google navega con `NavigationManager`.
> - **Verificar en PROD tras aplicar**: `select lista, clave, label from catalogo_opciones where label in ('EnEjecucion','EnRevision','Unica','Climatizacion','Recreacion','Circulacion','EnMantenimiento','NoBinario','NoIdentificado');`
>   (0 filas). Sin config/env nueva.

> **EXPEDIENTES (/documentos) CON BANDEJA + RADICADO, Y ENCABEZADOS Y PIES DE PAGINA EN PLANTILLAS (2026-11-11, Web 0.0.281 /
> API 0.0.216): 2 MIGRACIONES (aditivas) + API y Web cambian.**
> - **MIGRACION `20261112020601_AddFolderRadicado`** (va DESPUES de `AddAgentesPlantillaVersion`): `folders` + `radicado`
>   (varchar 20) con indice UNICO parcial `(tenant_id, radicado) WHERE radicado IS NOT NULL`. **Backfill**: RAD-AAAA-NNNN a todos
>   los folders existentes, consecutivo ANUAL por copropiedad (anio de `created_at` en hora de Colombia) en orden de creacion
>   (desempate: raiz antes que subfolder, orden, nombre). Suspende el FORCE de `folders` solo durante el UPDATE. En dev: 50
>   folders, 0 sin radicado. Los folders nuevos toman el siguiente al crearse (`FoldersService.CrearFolderAsync`, reintenta si
>   choca el indice); la estructura base sembrada tambien los lleva.
> - **MIGRACION `20261112025452_AddMembretesDocumento`**: tabla nueva `membretes_documento` (encabezados y pies de pagina,
>   RLS FORCE + policy + GRANT a propia_app, indice unico parcial: un predeterminado por tipo y copropiedad) y
>   `plantillas_documento` + `encabezado_modo`, `encabezado_id`, `pie_modo`, `pie_id` (default 0 = Predeterminado: las
>   plantillas existentes quedan en Predeterminado). Sin backfill: cada copropiedad nace con "Institucional" (encabezado y pie)
>   la primera vez que se consultan sus membretes o se compone un documento, armado con su config de membrete
>   (`membrete_color_acento`, `membrete_linea_legal`, `membrete_contacto_footer`, `membrete_mostrar_logo`).
> - **Cambio visible en PQRSD**: el PDF / vista previa de la respuesta oficial se compone con el encabezado y el pie
>   PREDETERMINADOS de la copropiedad (antes el membrete era fijo en codigo). El Institucional replica el aspecto anterior
>   (identidad + tipo/radicado/fecha a la derecha + linea de color; pie con linea legal, contacto y "Operado por").
>   Firmante y numeracion siguen saliendo de la config del tenant.
> - **Verificar en PROD tras aplicar**: `select count(*) filter (where radicado is null), count(*) from folders;` (0 nulos);
>   abrir /documentos (Galeria / Tabla con RADICADO); abrir Configuracion > Plantillas de documentos > "Encabezados y pies"
>   (debe aparecer Institucional x2) y generar la vista previa de una respuesta PQRSD. Sin config/env nueva.

> **AGENTES SEMILLA CON ACTUALIZACION DESDE EL SUPER ADMIN (2026-11-11, Web 0.0.280): 1 MIGRACION (aditiva + backfill) +
> API y Web cambian.**
> - **MIGRACION `20261111234845_AddAgentesPlantillaVersion`** (va DESPUES de `AddServiciosPublicosRecibosIA`):
>   `ai_agent_templates` + `version` (default 1) + `clave` (unica, parcial); `ai_agents` + `template_id` (FK SetNull),
>   `template_version`, `template_prompt_hash`. Backfill: clave `agente-documental` / `auxiliar-administrativo` a las
>   plantillas con esos nombres, y ENLACE de cada agente de copropiedad con su plantilla por nombre (con los placeholders
>   reemplazados) + huella del prompt que la plantilla le habria puesto (si el prompt actual difiere, cuenta como EDITADO por
>   la copropiedad y la actualizacion lo respeta). Suspende el FORCE de `ai_agents` solo durante el UPDATE. En dev: 80
>   Auxiliar (80 intactos), 80 Documental (78 intactos, 2 editados), 3 Asistente; 20 agentes propios quedan sin plantilla.
> - Las semillas `AgenteDocumentalSeeder` y `AuxiliarAdministrativoSeeder` ahora corren en TODOS los ambientes pero SOLO
>   crean la plantilla si falta (no pisan prompts ni tocan agentes). **Se retira** `deploy/sql/2026-11-11_agente_documental_prompt.sql`:
>   las mejoras de prompt se hacen en Super Admin > Plantillas de Agentes IA y se llevan con "Actualizar copropiedades".
> - "Actualizar copropiedades" lleva prompt, tools, PROVEEDOR y MODELO de la plantilla (la version sube si cambia cualquiera).
> - La lectura con IA (Contratos, Seguros, Servicios publicos) y el Auxiliar Administrativo buscan su agente por la CLAVE de la
>   plantilla (no por nombre) y, si la copropiedad no lo tiene, lo CREAN al usarlo desde la plantilla.
> - **ANTES de usar en PROD**: en Super Admin > Plantillas de Agentes IA, revisar que "Agente Documental" y "Auxiliar
>   Administrativo" tengan el **proveedor con llave valida** (los agentes que se creen al usarlo toman el proveedor de la
>   plantilla; en dev la plantilla decia Gemini y la copropiedad demo usaba DeepSeek).
> - **Verificar en PROD tras aplicar**: `select name, clave, version from ai_agent_templates;` (las 2 claves asignadas);
>   `select count(*) filter (where template_id is not null), count(*) from ai_agents;`; abrir Plantillas de Agentes IA >
>   "Actualizar copropiedades" en el Agente Documental y revisar la vista previa (sin aplicar si no hace falta).
>   Sin config/env nueva.

> **SERVICIOS PUBLICOS (/servicios) CON RECIBOS LEIDOS POR IA + RECARGA AUTOMATICA AL RECONECTAR (2026-11-11): 1 MIGRACION
> (aditiva + backfill) + API y Web cambian.**
> - **MIGRACION `20261111202708_AddServiciosPublicosRecibosIA`** (va DESPUES de `AddCamposAgenteDocumental`):
>   1. `cuentas_servicio_publico`: + `referencia_normalizada`, `periodicidad` (default 1 = Mensual), `dia_pago`, `valor_min`,
>      `valor_max`, `consumo_min`, `consumo_max`. **Backfill** de `referencia_normalizada` (numero_cuenta sin espacios, puntos
>      ni guiones, mayusculas) con el FORCE suspendido solo durante el UPDATE. Si una copropiedad tiene referencias
>      DUPLICADAS, la cuenta mas antigua conserva la normalizada y las demas quedan en NULL: la migracion NO falla y las
>      reporta con `RAISE NOTICE 'Servicios publicos: referencia duplicada ...'` -> **revisar la salida del deploy** y
>      corregirlas en Servicios publicos > Configuracion. Luego crea el indice UNICO parcial (tenant_id, referencia_normalizada).
>   2. `registros_consumo_servicio`: + `fecha_limite_pago`, `lectura_anterior`, `lectura_actual`, `origen` (default 1 = Manual),
>      `archivo_key`, `archivo_nombre`.
>   3. Tablas nuevas `registros_consumo_conceptos`, `servicio_publico_campo_definiciones`, `servicio_publico_campo_valores`
>      con RLS FORCE + policy `tenant_isolation` + GRANT a `propia_app`.
>   4. `contratos_servicio`: + `cuenta_servicio_publico_id` (FK SetNull a `cuentas_servicio_publico`). El vinculo viejo
>      `servicio_id` (catalogo "Servicios contratados") se CONSERVA en la base, ya no se muestra.
>   Down: borra columnas, tablas y FK.
> - API: `PUT /api/servicios-publicos/cuentas/{id}` (merge), `POST .../recibos/evaluar|confirmar|agregar-servicio`,
>   `GET .../recibos/ejemplo/{alto|ok|ajeno}` (PDF de ejemplo con **Chromium**: usa el mismo PuppeteerSharp que los PDF de
>   PQRSD), campos propios `.../campos` y `.../campos-valores`. `/api/ocr/extraer-agente`: perfil nuevo `recibo_servicio`
>   (tipos `lista`/`decimal`/`referencia`, cuadre del desglose, 2a pasada); el prompt va en el codigo. Fotos JPG/PNG/WebP van
>   directo a vision.
> - Web: `/servicios` es la pagina nueva `ServiciosPublicosPagina` (Configuracion + Recibos y consumo); `/contratos` sigue en
>   Servicios.razor (sin el catalogo viejo). Bumps: `servicios.css?v=32`, `propia-iai.js?v=2`, `propia-pdfthumb.js?v=6`.
>   `App.razor`: el overlay "Reconectando..." ahora sondea `/health` cada 2s y **recarga solo** apenas el servidor responde
>   (antes un solo intento fallido durante el deploy dejaba al usuario con el boton "Recargar pagina").
> - **Verificar en PROD tras aplicar**: `select count(*) from cuentas_servicio_publico where numero_cuenta is not null and
>   referencia_normalizada is null` (= duplicados reportados, idealmente 0); `select relname, relforcerowsecurity from pg_class
>   where relname in ('registros_consumo_conceptos','servicio_publico_campo_definiciones','servicio_publico_campo_valores')`
>   -> `t`; en /servicios > Recibos y consumo, "Prueba con uno de ejemplo" lee el recibo con el Agente Documental.
>   Sin config/env nueva.

> **AGENTE DOCUMENTAL REFORZADO + TRANSICION "LA IA CONSUME EL DOCUMENTO" (2026-11-11): 1 MIGRACION ADITIVA + API y Web cambian.**
> - **MIGRACION `20261111163506_AddCamposAgenteDocumental`** (va DESPUES de `AddPlantillaCampos`): agrega columnas nullable
>   a `polizas` (`nit_aseguradora`, `tomador`, `asegurado`, `valor_asegurado`, `prima`, `deducible`) y a `contratos_servicio`
>   (`representante_legal`, `ciudad`). Sin datos que migrar; las tablas ya tienen RLS. Down: borra las 8 columnas.
> - `/api/ocr/extraer-agente`: perfiles ampliados (contrato y poliza), normalizacion en el servidor (NIT sin puntos con DV,
>   montos, fechas en texto, si/no), vision si el texto del PDF es pobre (acepta `Imagenes` + `Texto` + `Paginas`) y 2a
>   pasada dirigida cuando faltan datos clave. El prompt de EXTRACCION va en el codigo (llega con el deploy).
> - **Prompt base del agente (chat)**: el seeder solo corre en Development, asi que en PROD se aplica con
>   `deploy/sql/2026-11-11_agente_documental_prompt.sql` (owner `propia`; quita y repone el FORCE de `ai_agents` en la misma
>   transaccion; pisa el prompt en todas las copropiedades, como el seeder). Probado en dev: 79/79. Opcional para la extraccion.
> - Web: `css/modules/ia-ingesta.css?v=1`, `js/propia-iai.js?v=1`, `js/propia-pdfthumb.js?v=5`.
> - **Verificar en PROD tras aplicar**: `\d polizas` y `\d contratos_servicio` muestran las columnas nuevas; en Contratos >
>   Nuevo contrato, soltar un PDF muestra el panel de lectura y llena el formulario. Sin config/env nueva.

> **PLANTILLAS: CAMPOS PROPIOS (columna "+") + ASUNTO COMPLETO EN EXCEL DE PQRSD (2026-11-11): 1 MIGRACION ADITIVA + API y Web cambian.**
> - **MIGRACION `20261111140504_AddPlantillaCampos`** (va DESPUES de `AddPlantillasDocumento`): crea
>   `plantilla_campo_definiciones` y `plantilla_campo_valores` (FK en cascada a la definicion y a `plantillas_documento`),
>   ambas con RLS FORCE + policy `tenant_isolation` + GRANT a `propia_app`. Sin datos que migrar. Down: borra las 2 tablas.
> - API nueva: `/api/plantillas/campos` (GET/POST/PUT/DELETE) y `/api/plantillas/campos-valores` (GET, PUT
>   `{plantillaId}/{definicionId}`); escrituras con permiso `PLANTILLAS:Editar`. `POST /api/pqrsd/descripciones` (lectura,
>   permiso `PQRS:Ver`): texto completo del asunto para la descarga de la vista tabla.
> - **Verificar en PROD tras aplicar**: `select relname, relrowsecurity, relforcerowsecurity from pg_class where relname like
>   'plantilla_campo%'` -> ambas `t|t`. Sin config/env nueva.

> **CONFIGURACION > PLANTILLAS DE DOCUMENTOS (2026-11-11): 1 MIGRACION (con DATOS + DROP) + API y Web cambian.**
> - Modulo nuevo `/configuracion/plantillas`: catalogo central de plantillas (categorias pqrsd/cartera/asambleas/
>   comunicaciones/contratos/reservas/certificados), API `/api/plantillas`, permiso nuevo `PLANTILLAS` en la matriz
>   de Roles. PQRSD (Configurar > Plantillas y la columna PLANTILLAS de Responder) lee de aqui.
> - **MIGRACION `20261111102427_AddPlantillasDocumento`** (en este orden, en una sola migracion):
>   1. crea `plantillas_documento` (RLS FORCE + policy `tenant_isolation` + GRANT a `propia_app`);
>   2. **copia** las filas activas de `pqrsd_plantillas_respuesta` con `categoria='pqrsd'` (mismo id/tenant/fechas;
>      quita el FORCE de la tabla vieja antes de leer, para que el owner vea TODAS las filas aunque no sea superusuario);
>   3. **BORRA `pqrsd_plantillas_respuesta`** (decision de Alex). El Down la recrea y devuelve las de PQRSD.
>   4. **menu**: si existe la seccion custom "Configuracion" (menu_overrides, node_type=section), agrega el override
>      `cfg-plantillas` ahi con el sort_order de `mi-config-hub` (queda despues de "Copropiedad"). Idempotente.
> - **Verificar en PROD tras aplicar**: `select count(*) from plantillas_documento` == filas activas que tenia
>   `pqrsd_plantillas_respuesta` (en dev: 90 filas / 18 tenants); y que el item aparece en Configuracion.
> - Sin config/env nueva. Bump de `propia-tokens.css?v=11` (tokens `--propia-paper-*`).

> **INTEGRACION ASAMBLEA (2026-11-11, merge en main): 1 MIGRACION NUEVA (ADITIVA) + API y Web cambian.**
> - Voto self-service del residente (enlace firmado por token, pagina publica `/asambleas/...` sin login),
>   envio de enlaces por correo, ola A (conteo al cierre, voto secreto, fallos de correo), criterio de mayoria
>   configurable por copropiedad, y rutas publicas de Asambleas y PQRSD (`/pqr/{tenantId}`) que no exigen sesion.
> - **MIGRACION `20261110201200_AsambleaCriterioMayoria`:** agrega la columna `criterio_mayoria` (int, NOT NULL,
>   **DEFAULT 2** = "mas del porcentaje") a `asamblea_config`. No necesita datos ni backfill: las filas existentes
>   toman el default. Ya aplicada en dev (`propia_dev`).
> - **Aplicar en PROD:** `cd src/Propia.Api && dotnet ef database update --project ../Propia.Infrastructure --startup-project .`
> - **Como desplegar:** redeploy de **API y Web** + la migracion. Sin config/env nueva (el correo usa el SMTP ya configurado).
> - **Rollback:** redeploy previo; la columna puede quedarse (aditiva, con default).
>

> **VERSION A DESPLEGAR = 0.0.276 (Web) + API (nuevos endpoints)** (sigue a 0.0.275, 2026-11-04).
> **1 MIGRACION NUEVA (ADITIVA) + LA API CAMBIA. Redeployar API y Web.**
> **Contratos: el modal de Contrato queda identico a la maqueta (modal-contrato.html), hermano del de Poliza.**
> Cambios de esta tanda:
> - **Modal de Contrato a 2 columnas:** la pestana **Contrato** pasa a formulario (izquierda, scroll propio) +
>   **panel de Actividad lateral** (`.act-*`, chat por-contrato, mismo look que Poliza/Tarea/PQRSD) a la derecha.
>   En contrato NUEVO queda a 1 columna (sin Actividad hasta guardar). El frame estandar 1056x86vh ya existia.
> - **Chat por-contrato (MIGRACION aditiva `20261104160730_AddContratoComentario`):** tabla nueva
>   `contrato_comentarios` (RLS FORCE + policy `tenant_isolation` + GRANT a `propia_app`, FK a `contratos_servicio`
>   ON DELETE CASCADE) + endpoints `GET/POST /api/servicios/contratos/{id}/comentarios`. Espejo EXACTO de
>   `poliza_reclamacion_comentarios`. No toca datos existentes. `ServiciosService` ahora recibe `IHttpContextAccessor`
>   (para el autor del comentario desde los claims) — ya registrado en DI, no cambia config.
> - **Pestana Documentos = panel comun `DocumentosPanel`** (miniaturas + visor inline + lightbox) sobre los
>   **adjuntos de contrato que ya existian** (endpoint `/api/servicios/contratos/{id}/adjuntos`, sin backend nuevo).
>   Se conservan arriba los **Expedientes conectados** y abajo **Programar tarea** (no estan en la maqueta pero son
>   features vivas).
> - **Configuracion de alertas:** se agregan "Avisar antes del vencimiento" (checkbox) y "Canal de aviso"
>   (informativo, default del sistema) para calcar la maqueta; el canal NO se persiste por-contrato.
> - **Vinculos internos** (Servicio/Proyecto/Valor mensual) se **conservan** (decision de Alex) aunque la maqueta
>   los retira: hoy solo se capturan ahi. Icono del header -> `file-signature`. Bump `servicios.css ?v=25`.
> - Todo por tokens `--propia-*` (ok en claro y oscuro).
>
> **Aplicar la migracion:** `cd src/Propia.Api && dotnet ef database update --project ../Propia.Infrastructure --startup-project .`
> **Como desplegar:** redeploy de **API y Web** (Railway) + aplicar la migracion (aditiva). NO tocar config/env.
> **Rollback:** redeploy del artefacto previo (0.0.275); la tabla nueva puede quedarse (es aditiva y nada mas la usa).
> **Verificado** E2E en dev (demo "Conjunto Altos del Bosque", contrato Aseo Total SAS): modal a 2 columnas con
> Actividad (mensaje persiste fila en BD + estilo "mine"), Documentos (subida real por API -> miniatura + visor +
> borrado), Alertas y Vinculos, claro y oscuro. `dotnet build` 0 err / 46 warn (baseline); RlsCoverage + test de
> aislamiento de `contrato_comentarios` verdes. Datos de prueba borrados.
>
> **VERSION PREVIA = 0.0.275 (Web) + API (nuevos endpoints)** (sigue a 0.0.274, 2026-11-03).
> **1 MIGRACION NUEVA (ADITIVA) + LA API CAMBIA. Redeployar API y Web.**
> **Seguros: el modal de Poliza queda identico al diseno CARNET / 2 columnas de la maqueta (modal-poliza.html).**
> Cambios de esta tanda:
> - **Modal de Poliza a 2 columnas:** la pestana **Poliza** pasa a formulario (izquierda) + **panel de Actividad
>   lateral** (`.act-*`, chat por-poliza, mismo look que Tarea/PQRSD) a la derecha. Antes el chat vivia dentro de
>   la pestana Reclamaciones; ahora es columna permanente de la vista por defecto.
> - **Pestana Documentos = panel comun `DocumentosPanel`** (caratula, certificados y anexos con miniaturas +
>   visor inline + lightbox). Reemplaza el viejo enlace "expediente". **MIGRACION aditiva
>   `20261104010324_AddPolizaDocumentos`**: tabla nueva `poliza_documentos` (RLS FORCE + policy `tenant_isolation`
>   + GRANT a `propia_app`, FK a `polizas` ON DELETE CASCADE) y endpoints nuevos
>   `GET/POST/DELETE /api/seguros/polizas/{id}/documentos` (POST/DELETE gateados a Administrador). No toca datos
>   existentes.
> - **Reclamaciones intactas:** CRUD completo (tabla + nueva reclamacion + cerrar con monto reconocido); solo se
>   alinearon estilos y se saco el chat (ahora en Actividad).
> - **Ortografia de produccion** en el modal: Poliza, Numero, Finalizacion, etc. **Aseguradora y Corredor** siguen
>   como relacion al Directorio (`SelectorPersona`) + snapshot de nombre.
> - Todo por tokens `--propia-*` (ok en claro y oscuro). El CSS de Seguros es inline en `Seguros.razor`, por eso
>   NO hay bump de `?v` de CSS en `App.razor`.
>
> **Aplicar la migracion:** `cd src/Propia.Api && dotnet ef database update --project ../Propia.Infrastructure --startup-project .`
> **Como desplegar:** redeploy de **API y Web** (Railway) + aplicar la migracion (aditiva). NO tocar config/env.
> **Rollback:** redeploy del artefacto previo (0.0.274); la tabla nueva puede quedarse (es aditiva y nada mas la usa).
> **Verificado** E2E en dev (demo "Conjunto Altos del Bosque", poliza AR-2025-4471): Actividad (mensaje persiste
> entre reaperturas + estilo "mine"), Documentos (subida real por API -> miniatura + visor PDF + borrado),
> Reclamaciones CRUD intacto, claro y oscuro. `dotnet build` 0 errores; tests Seguros 28/28 + RlsCoverage verdes
> (la tabla nueva pasa la cobertura de RLS). Datos de prueba borrados.
>
> **VERSION PREVIA = 0.0.274 (Web)** (sigue a 0.0.273, 2026-11-03). **SOLO CODIGO FRONTEND, SIN MIGRACION,
> SIN CONFIG. API SIN CAMBIOS (diff de Api/Infrastructure/Domain/Application vacio). Redeployar SOLO Web.**
> Cambios de esta tanda (todos UI; commits 454b09de, 63b87516, 05f3b234 + bump):
> - **Mi Copropiedad -> Identidad:** la seccion pasa a un **CARNET a 2 columnas** (resumen a la izquierda +
>   datos en lista label/valor con hairline, por secciones). Elimina la grilla `.mc-readgrid` que mostraba la
>   banda gris en filas incompletas; vacios -> "—". CSS `.mcv2-*` por tokens `--propia-*` (ok en claro y oscuro).
>   Ruta: `/configuracion-copropiedad` (pestana General). Bump `mi-copropiedad.css ?v=6`.
> - **Tareas (vista tabla):** el editor inline de **Descripcion** y el dropdown de **Asignado** ya NO se recortan
>   al abrirse en la ultima fila (pasan a `position:fixed` anclado al clic, patron `.tb-etqpop`). Asignado muestra
>   una **pildora con el nombre** (antes solo el circulo). Ayuda del alta inline corregida ("Enter o clic fuera
>   para guardar; Tab entre campos; Esc cancelar"). Bump `tareas.css ?v=58`.
> - **Editor de estados (PQRSD / Contratos / Tareas):** los mensajes de validacion/rechazo salen **dentro del
>   modal** (antes en el banner de pagina, detras del modal). Contratos ademas lee el error del API al
>   eliminar/mover etapa.
> - **SelectorPersona:** oculta la linea de documento cuando esta vacia.
>
> **Como desplegar:** redeploy de **Web** (Railway). **NO** ejecutar migraciones (no hay). **NO** tocar
> config/env. El `?v` de los CSS ya esta bumpeado, no hace falta purgar cache manualmente.
> **Rollback:** redeploy del artefacto previo (0.0.273); **no toca BD**, es seguro.
> **Verificado** E2E en dev (demo "Conjunto Altos del Bosque" + "Edificio Portal del Sol"), claro y oscuro,
> responsive; `dotnet build` 0 errores.
>
> _Nota de gap: las versiones 0.0.263-0.0.273 (olas de "color por copropiedad" en campos-lista: Ola 2b/2c/3/3b/3c,
> todas frontend) ya estan en `main` pero NO quedaron documentadas en este handoff por las sesiones del equipo.
> Este bloque 0.0.274 reanuda el registro desde el estado actual de `main`._
>
> **VERSION PREVIA = 0.0.262 (Web) + 0.0.215 (API)** (sigue a 0.0.260, 2026-10-29). **1 MIGRACION NUEVA
> (ADITIVA), SIN CONFIG. Redeployar API y Web.** **PQRSD: color por copropiedad en estado/tipo/categoria (Ola 1).**
> - **0.0.261 (Web, sin migracion):** PQRSD·**estado** -> la celda ETAPA de la tabla ahora se pinta con el color
>   del estado (igual que el dot de la columna del Kanban); y "Editar campo" de estado/tipo/categoria muestra el
>   boton "Gestionar colores y orden" que abre el panel de config en la pestana correcta.
> - **0.0.262 Web + 0.0.215 API (CON migracion):** PQRSD·**tipo** y **categoria** -> **MIGRACION aditiva**
>   `20261029153431_AddColorPqrsdTipoCategoria` (agrega columna `color` a `pqrsd_tipos` y `pqrsd_categorias`; no
>   toca datos ni la metadata legal/plazos). Picker de color en las pestanas Tipos/Categorias del panel; las
>   celdas TIPO y CATEGORIA de la tabla se pintan con el color (mapeo por TipoNombre/base y por Nombre). Color es
>   solo display: el motor legal (plazos Ley 1755, workflow) NO se toca. Los handlers de rename/dias/legal/activa
>   preservan el color (ya no lo pisan). Verificado E2E en demo (categoria Administracion -> celdas #ff8a3d; tipo
>   Peticion -> celdas #2ec4b6; revertido a null por SQL). Build 0 errores.
>
> **Aplicar la migracion:** `cd src/Propia.Api && dotnet ef database update --project ../Propia.Infrastructure --startup-project .`
> Rollback = redeploy previo (la migracion aditiva puede quedarse). Parte 2 de la Ola 1; falta Directorio·etiquetas.
>
> **VERSION PREVIA = 0.0.260 (Web)** (sigue a 0.0.259, 2026-10-29). **SOLO CODIGO, SIN MIGRACION, SIN
> CONFIG.** API sin cambios (0.0.214). **Unidades: color y ocultar por copropiedad en los campos-lista del
> sistema (Tipo/Estado) — Ola 0 del plan.** La copropiedad ya puede, desde "Editar campo" -> "Gestionar colores
> y orden", cambiar el COLOR de cada opcion (se pinta en la celda como pill) y OCULTAR opciones (en vez de
> borrar): las ocultas dejan de ofrecerse en nuevas filas pero las unidades que ya las usan las conservan; las
> opciones de fabrica (semilla A&D) no se eliminan, solo se ocultan (candado), solo las propias de la
> copropiedad se borran. El editor completo YA existia en `DistribucionPanel` (popup `_listaAbierta` con la
> regla 3 implementada) pero estaba HUERFANO (anidado en el gestor viejo `_configCamposAbierto` que ya no se
> abre, y nada lo llamaba). Fix: (1) el modal `EditarCampoSistemaModal` muestra el boton "Gestionar colores y
> orden" (`AbrirGestorAvanzado`) que reabre el editor; (2) el popup se saco a nivel superior (tiene su propio
> backdrop); (3) las celdas `tipo`/`estado` ahora pintan el color (`ColorTipoCelda`/`ColorEstadoCelda` +
> `SelectListaStyle`). Sin migracion ni cambio de schema (el override vive en `unidad_campos_config.Opciones`,
> ya existente). Verificado E2E en demo (color+ocultar+regla3+con-vigente). Rollback = redeploy previo.
>
> Nota: es la Ola 0 (solo Unidades) de un rollout por olas; el resto de modulos va en olas siguientes (ver
> Obsidian "Inventario - Color y Ocultar por copropiedad en campos-lista (olas)").
>
> **VERSION PREVIA = 0.0.259 (Web) + 0.0.214 (API)** (sigue a 0.0.258, 2026-10-29). **SOLO CODIGO, SIN
> MIGRACION, SIN CONFIG.** **OJO: esta version SI cambia la API (0.0.213 -> 0.0.214); hay que redeployar API,
> no solo Web.** **Fix: la ficha de un residente ya no da "404 (Not Found)" ni se queda en "Cargando roles...".**
> Causa: la lista de Residentes se arma de `unidad_personas`, pero la ficha (GET `/api/directorio/personas/{id}`)
> exigia una fila en `directorio_vinculos` (chequeo de seguridad S-08). Residentes de carga masiva/seed tienen el
> link de unidad pero NO esa fila -> su ficha daba 404 (y "Cargando roles..." colgado, porque la carga abortaba
> antes). Afecta varias copropiedades (en una, 11 de 13 residentes). Fix en `DirectorioService.TieneVinculoEnTenantAsync`
> (Propia.Infrastructure): ademas del vinculo de directorio, acepta como vinculo valido un link de `unidad_personas`
> en el tenant actual (TenantEntity -> ya acotado al tenant; un residente de una unidad del tenant SI esta vinculado;
> no debilita S-08 ni filtra de mas). Tambien resiliencia en `FichaResidenteModal`: el spinner de roles se libera
> siempre (`finally`) aunque la carga falle. **Sin migracion ni backfill de datos.** Verificado E2E en el demo:
> se borro el `directorio_vinculos` de un residente -> con el codigo viejo reproducia el 404 exacto; con el fix
> carga completo (nombre/correo/foto/roles); vinculo restaurado despues. Sin regresion (residentes con vinculo
> cargan igual; tests `DirectorioFlowTests` no afectados). Rollback = redeploy del artefacto previo (no toca BD).
>
> Nota aparte (no bloquea): queda la inconsistencia de datos de fondo (residentes con `unidad_personas` pero sin
> `directorio_vinculos`); el fix la hace inofensiva para la ficha. Backfill opcional + revisar el camino de alta
> de residentes (que cree ambos) queda para decidir con Alex.
>
> **VERSION PREVIA = 0.0.258 (Web)** (sigue a 0.0.255, 2026-10-29). **SOLO CODIGO, SIN MIGRACION, SIN
> CONFIG.** API sin cambios (0.0.213). Modal "Editar usuario" (`UsuariosPanel.razor` + `usuarios.css?v=28`),
> acercandolo al canon de las fichas Residentes/Terceros (3 cambios acumulados, solo frontend):
> - **0.0.256** - Redistribucion a 2 columnas canon Residentes (`.usr-ficha-cols` 4fr/8fr): columna izquierda con
>   imagen grande dedicada (avatar 150px) + ROL/CARGO debajo en seccion con divisor; columna derecha ancha con
>   los datos (Nombre, Documento, Correo, Celular) + Estado + Datos del sistema + Persona unica.
> - **0.0.257** - Pestanas **Documentos** (canon `DocumentosPanel`, reusa adjuntos de Directorio por la PersonaId
>   del usuario) e **Historial** (hitos del acceso: vinculacion/ultimo acceso/aceptacion datos/estado). Las
>   acciones del header (Guardar/Revocar) se gatean a la pestana Perfil. Solo aparecen al editar (no al crear).
> - **0.0.258** - Navegacion **anterior/siguiente** entre registros en el header (canon ficha Residentes) +
>   **edicion de identidad**: Nombres/Apellidos/Correo/Celular editables, se guardan en la Persona del Directorio
>   (`PUT /api/directorio/personas/{id}`, mismo mecanismo que Residentes; el Documento no se edita). Guard: si la
>   persona no existe (cuenta sembrada sin persona en el Directorio, p.ej. admin demo -> 404) esos campos quedan
>   de solo lectura y el celular usa el endpoint propio del usuario, sin fallo silencioso ni regresion.
>
> Verificado en runtime (demo): navegacion 1/2<->2/2; edicion del celular de un usuario real persiste y sobrevive
> al reinicio (en BD); admin demo cae al modo solo-lectura. Documentos carga por PersonaId; Historial muestra los
> hitos. Build 0 errores, sin warnings nuevos. Rollback = redeploy del artefacto previo (no toca BD).
>
> **VERSION PREVIA = 0.0.255 (Web)** (sigue a 0.0.254, 2026-10-28). **SOLO CODIGO, SIN MIGRACION, SIN
> CONFIG.** API sin cambios (0.0.213). **Usuarios - ancho del modal Editar usuario igualado a las fichas.**
> `.usr-modal-user` pasa de `width:900px` a `width:1056px; max-width:96vw; max-height:86vh`, el mismo ancho
> intrinseco que las fichas de Residentes/Terceros (is-fixedh 1056 x 86vh) y que la ficha de rol
> (`.usr-modal-roles`). En viewports anchos ambos miden 1056; en panes estrechas caen a su cap responsive
> (dif ~7px por el padding del overlay, imperceptible). CSS module (`usuarios.css?v=25`). Verificado en runtime.
> Rollback = redeploy del artefacto previo.
>
> **VERSION PREVIA = 0.0.254 (Web)** (sigue a 0.0.253, 2026-10-28). **SOLO CODIGO, SIN MIGRACION, SIN
> CONFIG.** API sin cambios (0.0.213). **Usuarios - bordes simetricos del modal Editar usuario.** La barra de
> scroll vertical de `.usr-modal-body` comia ~15px solo del lado derecho (margen izq 24 / der 24+scroll), dejando
> el formulario visualmente corrido. Fix: `scrollbar-gutter: stable both-edges` reserva el ancho de la barra en
> AMBOS lados -> gutters 39/39 simetricos, contenido centrado. CSS module (`usuarios.css?v=24` en App.razor).
> Verificado en runtime (demo). Rollback = redeploy del artefacto previo.
>
> **VERSION PREVIA = 0.0.253 (Web)** (sigue a 0.0.250, 2026-10-28). **SOLO CODIGO, SIN MIGRACION, SIN
> CONFIG.** API sin cambios (0.0.213). Homogeneizacion de las fichas Terceros/Residentes (3 cambios acumulados,
> solo frontend; `<style>` inline, no toca CSS module ni `?v=`):
> - **0.0.251** - Pestana **Documentos de Terceros** (`FichaTerceroModal`) pasa del listado viejo `.fr-doc*` al
>   componente compartido `DocumentosPanel` (miniaturas + visor inline + lightbox), igual que Residentes/Zonas
>   (`Padding="0"` porque `.pm-body` no es flex-column). Reusa el mismo backend de adjuntos de Directorio
>   (Persona/Empresa); `SubirDocumento` ahora multi-archivo. Se quito CSS `.fr-doc*` + `FmtTam` (muertos).
> - **0.0.252** - **Acceso bajo la foto en Residentes** (`FichaResidenteModal`): se retira la pestana "Acceso"
>   (de la 0.0.248) y su seccion (ACCESO AL SISTEMA + hint + chips de rol + asignar/quitar + nota de invitacion)
>   vuelve a la columna izquierda del Perfil, debajo de la foto (pedido de Alex). El rol ya no se duplica como
>   chip arriba (queda solo el de relacion). Header: lapiz/guardar/eliminar vuelven a gatearse solo en Perfil.
> - **0.0.253** - **Terceros "Servicio que presta" estructurado** (`FichaTerceroModal`): la columna izquierda
>   calca a Residentes (etiqueta "FOTO DE LA PERSONA"/"LOGO DE LA EMPRESA"; los servicios pasan de botones
>   sueltos a una seccion `.fr-access` con divisor + titulo + hint + chips pill + "Clic para asignar"). Se quito
>   CSS `.fr-tags`/`.chip`/`.chip-x` (muertos).
>
> Verificado en runtime (demo): Terceros Documentos (Luz Adriana con doc real: miniatura+visor+lightbox),
> Residentes Acceso bajo la foto (Mariano), Terceros Servicio estructurado (lectura+edicion). Build 0 errores,
> sin warnings nuevos. Rollback = redeploy del artefacto previo (no toca BD).
>
> **VERSION PREVIA = 0.0.250 (Web)** (sigue a 0.0.249, 2026-10-28). **SOLO CODIGO, SIN MIGRACION, SIN
> CONFIG.** API sin cambios (0.0.213). **Rollout del fix de "recuperar sesion ante 401 (token vencido)" al
> resto de los modales de ficha** (la 0.0.249 ya lo hizo en Terceros+Residentes). Mismo patron: ante un 401
> en la carga, `SessionHelper.HandleUnauthorizedAsync` (refresh sliding o /login), actualizar `Jwt` y reintentar
> una vez. **15 modales ahora cubiertos** (los 2 previos + 13 nuevos): FichaEquipo, FichaZona, FichaEmpresa,
> GestionarUnidades, GestionarFinanzas, GestionarEquipo, GestionarPqrsd, NuevaPqrsdWizard, NuevaTarea,
> ReporteContratosPorVencer, y GestionarGobierno/GestionarServicios/GestionarPlantillasPqrsd/RedactarRespuestaPqrsd/
> BitacoraCambios (estos 5 tragaban el 401 en un `catch` sordo -> ahora interceptan el 401 antes de dejar la
> lista vacia). **3 modales NO necesitaban el fix** (no cargan datos autenticados al abrir): GestionarZonas,
> GestionarEquiposActivos, EditarIdentidad. Cada cambio es aditivo (inyectar IJSRuntime/NavigationManager si
> faltaban + extraer/parametrizar el metodo de carga con `reintento` + una clausula catch). Sin backend ni BD;
> `<style>`/config sin cambios. Verificado: build 0 errores, sin warnings nuevos; GestionarGobierno carga
> normal en runtime; el patron ya fue probado E2E en Terceros (0.0.249). Rollback = redeploy del artefacto
> previo (no toca BD).
>
> **VERSION PREVIA = 0.0.249 (Web)** (sigue a 0.0.248, 2026-10-28). **SOLO CODIGO, SIN MIGRACION, SIN
> CONFIG.** API sin cambios (0.0.213). **Fix: los modales de ficha de Terceros y Residentes ya recuperan la
> sesion ante un 401 (token vencido), en vez de mostrar "Response status code does not indicate success: 401".**
> Causa: el JWT dura 8h; cuando vence, los modales usaban el `Jwt` snapshot y mostraban el 401 crudo (las
> paginas si refrescan). Fix: `FichaTerceroModal` y `FichaResidenteModal` ahora, ante un 401 en la carga,
> llaman `SessionHelper.HandleUnauthorizedAsync` (refresh sliding `/connect/refresh` o redireccion a /login),
> actualizan el `Jwt` y reintentan una vez -- mismo patron que `DirectorioPanel`. Inyectan `IJSRuntime` +
> `NavigationManager`; la carga se extrajo a `CargarAsync(reintento)`. Sin backend ni BD. Verificado E2E en
> runtime bajando el token a 2 min: el modal de Terceros dio **401 -> refresh -> 200** (log del API) y cargo
> los datos en vez del error; sin regresion con token valido (Terceros y Residentes cargan normal). **PENDIENTE
> (no incluido):** el mismo fix falta en los otros ~18 modales de ficha con `Jwt` snapshot (mismo bug latente);
> rollout en una ola aparte. Rollback = redeploy del artefacto previo (no toca BD).
>
> **VERSION PREVIA = 0.0.248 (Web)** (sigue a 0.0.247, 2026-10-28). **SOLO CODIGO, SIN MIGRACION, SIN
> CONFIG.** API sin cambios (0.0.213). **Ficha de Residentes reorganizada en 4 pestanas (canon Terceros):
> Perfil / Documentos / Comentarios / Acceso.** En `FichaResidenteModal` el tab unico "Ficha del residente"
> (que amontonaba datos + comentarios + un bloque "Acceso al sistema" en un solo scroll) se partio: el tab
> **Perfil** queda solo con foto/chips + DATOS; **Comentarios** y **Acceso** pasan a tabs propios; Documentos
> sigue con `DocumentosPanel` (sin cambios). El lapiz/guardar del header se habilita en Perfil **y** Acceso
> (el rol se sigue guardando junto con los datos via el checkmark; el modelo de guardado separado queda para
> una fase futura); la papelera "quitar de la unidad" solo en Perfil. Se quito CSS muerto (`.fr-roles`,
> `.fr-sectlbl--sep`). Sin cambios de backend ni de datos; `<style>` inline (no toca CSS module ni `?v=`).
> Verificado en runtime (demo, residente Mariano): 4 tabs, Perfil limpio, Comentarios y Acceso funcionan
> (chip de rol removible + roles disponibles), Documentos sin regresion. Build 0 errores / 45 warnings
> (baseline). Rollback = redeploy del artefacto previo (no toca BD).
>
> **VERSION PREVIA = 0.0.247 (Web)** (sigue a 0.0.246, 2026-10-28). **SOLO CODIGO, SIN MIGRACION, SIN
> CONFIG.** API sin cambios (0.0.213). **Documentos de Residentes AHORA en una pestana propia** (canon
> Zonas/Unidades). En `FichaResidenteModal` los documentos ya no son una seccion inline del scroll de la ficha:
> se agrego una barra de pestanas (`.fr-tabs`, mismos valores que `.fz-tabs` de Zonas) con **"Ficha del
> residente"** y **"Documentos (N)"** (contador). La pestana Documentos usa `<DocumentosPanel Fill="true">`
> (ocupa todo el alto del modal, como la pestana Documentos de Zonas). El lapiz/eliminar del header quedan
> gateados a la pestana Ficha. Mismo backend de adjuntos de Directorio (sin cambios de API ni BD); `<style>`
> inline (no toca CSS module ni `?v=`). Verificado en runtime (demo, residente Mariano): dos pestanas, panel
> Fill a alto completo, subida (por API) + render en visor, borrado por la X con contador 1->0. Rollback =
> redeploy del artefacto previo (no toca BD).
>
> **VERSION PREVIA = 0.0.246 (Web)** (sigue a 0.0.245, 2026-10-27). **SOLO CODIGO, SIN MIGRACION, SIN
> CONFIG.** API sin cambios (0.0.213). **Pestana "Documentos" homogenea tambien en Residentes**
> (`FichaResidenteModal`): la seccion "DOCUMENTOS (CEDULA, RUT, ETC.)" pasa de una lista simple al mismo canon
> compartido `DocumentosPanel` (miniaturas + visor inline con lightbox / iframe PDF, subida multiple). Reutiliza
> el backend de adjuntos de Directorio que YA existia (`GET/POST api/directorio/adjuntos/Persona/{id}`,
> `DELETE api/directorio/adjuntos/{id}`; una persona = un residente), asi que no hay migracion ni cambio de API.
> La subida/borrado quedan disponibles siempre en el panel (como en los otros modulos), no solo en modo edicion;
> el RBAC del endpoint (`Directorio` Crear/Eliminar) sigue protegiendo el servidor. Se quito CSS/ayudante muerto
> (`.fr-doc*`, `FmtTam`). Verificado en runtime (demo): render de PDF+imagen, cambio de visor, borrado con
> reselección, subida multiple por API. Rollback = redeploy del artefacto previo (no toca BD).
>
> **VERSION PREVIA = 0.0.245 (Web) + 0.0.213 (API)** (sigue a 0.0.244, 2026-10-27). **1 MIGRACION NUEVA
> (ADITIVA), SIN CONFIG.** **Pestana "Documentos" homogenea en Zonas, Unidades privadas y Equipos** (mismo
> canon PQRSD: columna de miniaturas + visor inline con lightbox para imagenes / iframe para PDF / tarjeta
> de archivo). Se extrajo un componente compartido `Components/Shared/DocumentosPanel.razor` (clases `.dap-*`,
> `<style>` global) que consumen los 3 modales (`FichaZonaModal`, `GestionarUnidadesModal`, `FichaEquipoModal`),
> para no triplicar el markup. **Zonas y Unidades = solo frontend** (sus endpoints de documentos ya existian).
> **Equipos = backend nuevo** (no existia): entidad `EquipoDocumento`, DbSet, model config (FK cascade +
> indices + HasQueryFilter) y **1 MIGRACION `20261028010516_AddEquipoDocumentos`** -- crea la tabla
> `equipo_documentos` con **RLS FORCE + policy `tenant_isolation` + GRANT a `propia_app`** (bloque RLS agregado
> a mano; EF no lo genera). APLICAR en prod con `dotnet ef database update` (owner `propia`). Aditiva, sin
> backup especial, aplicada en `propia_dev` el 2026-10-27 (OK de Alex). API: DTO `EquipoDocumentoDto` +
> `Documentos` en `EquipoFichaDto`; servicio `Agregar/EliminarEquipoDocumentoAsync`; endpoints
> `POST api/mi-copropiedad/equipos/{id}/documentos` (RequierePermiso MiCopropiedad.Crear, max 10MB) y
> `DELETE api/mi-copropiedad/equipos/documentos/{docId}` (MiCopropiedad.Eliminar). Blobs en
> `tenants/{tid}/equipos/{id}/docs/`. **Sin config nueva.** Verificado en runtime: los 3 modulos suben/ven/
> borran documentos (Equipos probado por API real + render en el navegador interno). Rollback = `Down`
> (dropea `equipo_documentos`; las otras 2 tablas de documentos no se tocan).
>
> **VERSION PREVIA = 0.0.244 (Web)** (sigue a 0.0.243, 2026-10-27). **SOLO CODIGO, SIN MIGRACION, SIN
> CONFIG.** API sin cambios (0.0.212). **Ola 3 (parte 3) de "totales por columna en grupos": Directorio**
> (`/directorio`), ambas sub-vistas (**Personas** y **Empresas**). Con esto el rollout queda COMPLETO en todos
> los modulos de vista-tabla. Directorio es padre (`DirectorioPanel`) + 2 sub-vistas con grid propio: el menu
> y el estado (`_aggDir`) viven en el padre y se pasan a cada hija por un nuevo parametro `Agg`; cada hija
> pinta su cabecera de grupo (antes un banner flex) como una fila en su mismo grid, con la franja al nivel del
> contenedor y el total alineado bajo cada columna. Numericas = solo campos propios Numero/Moneda/Porcentaje
> (las columnas de sistema del Directorio son texto; los campos propios se comparten entre ambas pestanas).
> **Bump `directorio.css?v=13`** en `App.razor`. Verificado en runtime: Personas (Contar 200 por grupo CC),
> Empresas (Contar 2 por grupo). Sin advertencias nuevas (baseline 45).
>
> **Resumen del rollout completo (0.0.240 -> 0.0.244, todo solo codigo):** la funcion "totales por columna en
> grupos" (Suma/Promedio/Minimo/Maximo/Contar, opt-in por la flecha de columna, persistido por modulo en
> localStorage) esta ahora en TODOS los modulos de vista-tabla: Unidades y Tareas (previos), Contratos, PQRSD,
> Equipos/Activos, Residentes, Polizas/Seguros, Zonas, Mascotas, Vehiculos, Usuarios, Roles y Directorio.
> Componentes compartidos tocados una sola vez: `TablaColMenu` (`ModoAgregado`/`AggOferta`), `TablaGruposRenderTr`
> y `TablaGruposRender` (`CabeceraTemplate` opcional, retrocompatible). Sin migracion ni config en toda la serie.
>
> **VERSION PREVIA = 0.0.243 (Web)** (sigue a 0.0.242, 2026-10-27). **SOLO CODIGO, SIN MIGRACION, SIN
> CONFIG.** API sin cambios (0.0.212). **Ola 3 (parte 2) de "totales por columna en grupos": Roles**
> (`/roles`). La tabla de Roles usa la tabla compartida `tbl` con agrupacion manual (un solo nivel, por
> Categoria o Tipo); la fila de grupo (antes un `<td colspan>` unico) pasa a pintar un total por columna. La
> unica columna numerica es "usuarios" (usuarios asignados), que suma por grupo; Contar aplica a cualquier
> columna. **Bump `roles.css?v=16`** en `App.razor`. Verificado en runtime: suma de usuarios por categoria
> (Administrativo 2, Personal Operativo 0, Portal Residente 244). **Directorio queda fuera de esta tanda**
> (pendiente de decision: es el de mayor esfuerzo y menor valor del rollout — padre + 2 sub-vistas
> heterogeneas con grid propio; un directorio de personas/empresas rara vez suma campos numericos).
>
> **VERSION PREVIA = 0.0.242 (Web)** (sigue a 0.0.241, 2026-10-27). **SOLO CODIGO, SIN MIGRACION, SIN
> CONFIG.** API sin cambios (0.0.212). **Ola 3 (parte 1) de "totales por columna en grupos"**: se extiende la
> funcion a **Zonas comunes** (`/zonas-comunes`), **Mascotas** (`/mascotas`), **Vehiculos** (`/vehiculos`) y
> **Usuarios** (`/usuarios`). Zonas/Mascotas/Vehiculos reusan el `CabeceraTemplate` del `TablaGruposRender`
> (sin cambios en el componente). Usuarios tiene arquitectura padre/hijo: el menu y el estado de agregado
> viven en `UsuariosPanel` y la cabecera se pasa al hijo `UsuariosTablaVista` por un nuevo parametro
> `CabeceraGrupo`. Numericas: Zonas (aforo, tarifa + custom); Mascotas/Vehiculos/Usuarios solo custom
> Numero/Moneda/Porcentaje (sus columnas de sistema son texto). **Bump `usuarios.css?v=23`** en `App.razor`.
> Verificado en runtime: Zonas (suma de aforo 20/60), Usuarios (Contar 1/1 por grupo). Sin advertencias nuevas
> (baseline 45). **Pendiente Ola 3 (parte 2): Directorio y Roles** (render no estandar: Directorio = padre + 2
> sub-vistas heterogeneas; Roles = tabla con agrupacion manual).
>
> **VERSION PREVIA = 0.0.241 (Web)** (sigue a 0.0.240, 2026-10-27). **SOLO CODIGO, SIN MIGRACION, SIN
> CONFIG.** API sin cambios (0.0.212). **Ola 2 de "totales por columna en grupos"**: se extiende la funcion
> (Suma/Promedio/Minimo/Maximo/Contar, opt-in desde la flecha de columna, persistido por modulo en
> localStorage) a **Equipos/Activos** (`/equipos-activos`), **Residentes** (`/residentes`) y **Polizas/Seguros**
> (`/seguros`). Estos usan render de grupo **div-grid** (no `<table>`): se agrega un `CabeceraTemplate` opcional
> y retrocompatible al componente compartido `TablaGruposRender` (los consumidores que no lo pasan siguen con
> el banner clasico); Seguros, que tiene su propio aplanado, lleva el nodo del grupo en su record `Fila`. La
> cabecera de grupo pasa a ser una fila en el MISMO grid que las filas, con la franja a nivel del contenedor
> (cubre toda la fila) y el total alineado bajo cada columna. "Contar (no vacios)" usa getters dedicados por
> clave de columna en Seguros (`TextoAggColS`), y `SortStr*` en Equipos/Residentes. Verificado en runtime:
> Equipos (suma de Cantidad 2/2 + Contar), Residentes (Contar 1/591), Seguros (VALOR $147M/$273M, cuotas 18/19).
> Sin advertencias nuevas (las de `EquiposActivosPanel:888`/`ResidentesPanel:639` son campos sin usar previos).
> Pendiente Ola 3: Zonas, Directorio, Mascotas, Vehiculos, Usuarios, Roles.
>
> **VERSION PREVIA = 0.0.240 (Web)** (base `main` HEAD `e2973c0b` = 0.0.239, 2026-10-27). **SOLO
> CODIGO, SIN MIGRACION, SIN CONFIG, SIN PAQUETES NUEVOS.** API sin cambios (sigue 0.0.212). Tanda de
> vista-tabla: (1) **decimales hasta 6** (antes 4) en el formato numerico de campos, en TODOS los modulos
> (CrearCampoModal/EditarCampoSistemaModal/ConfigCamposEntidad); (2) **totales por columna en grupos**
> (Suma/Promedio/Minimo/Maximo/Contar), opt-in desde la flecha de columna, persistido por modulo en
> localStorage (clave `propia_agregado_<modulo>`), rollout Unidades (piloto, +Contar/Min/Max) + **Ola 1
> Contratos y PQRSD** (Tareas ya lo tenia); componentes compartidos nuevos: `TablaColMenu.ModoAgregado`
> + `AggOferta`, `TablaGruposRenderTr.CabeceraTemplate` (opcional, retrocompatible); (3) **fix** de la
> franja del total de grupo que se cortaba en el primer total (ahora cubre toda la fila). **Bump
> `tabla-grupos.css?v=3`** en `App.razor`. Toda la persistencia es cliente (localStorage); nada toca BD.
> Verificado en runtime (demo). Detalle en `deploy/DEPLOY_HANDOFF_2026-10-27_v0.0.240.md`.
>
> **VERSION PREVIA = 0.0.239 (Web)** (`main` HEAD `1b63c211`, 2026-10-26). **MI TANDA (sesion UI) ES
> SOLO CODIGO, SIN MIGRACION NI CONFIG:** (1) campos **Funcion/calculado** ahora ofrecidos en el "+" de los 8
> modulos que no exponen relaciones (Vehiculos, Mascotas, Zonas, Equipos, Residentes, Directorio, Contratos,
> Polizas) via el nuevo parametro `OfreceFuncion` de `CrearCampoModal` (el computo ya estaba en backend);
> (2) tres **indicadores** en Unidades Privadas (`/distribucion`): total de unidades, apartamentos y suma de
> coeficientes (con `%`), mismo patron visual que los KPIs de Usuarios; (3) **Equipos (tabla): alineacion
> header/filas** (se quito `min-width:max-content`, pisos px + recorte de celdas) que corregia el valor que se
> iba a la derecha del "+"; (4) **logo del sidebar** a 68px/max-80px. Verificado en runtime + BD. Detalle en
> **ESTADO 2026-10-26** mas abajo.
>
> **CRITICO - el deploy de `main` HEAD NO es solo mi tanda:** entre la version antes documentada (0.0.212) y
> este HEAD hay ~35 commits que este banner aun no resumia. Lo mas importante para el deploy: el **merge de
> Folders v2** (modulo 2.15 Documentos, `ffd47588`) YA ESTA EN MAIN y trae **3 MIGRACIONES**:
> `20261022105420_AddFoldersDocumentos`, `20261022170331_AddFolderChatIndexacion` y
> **`20261023210234_DropExpedientesV1` (DESTRUCTIVA: elimina las tablas v1 de Expedientes)**. Aplicar con
> `dotnet ef database update` (owner `propia`), en ese orden. El resto del rango (homogenizacion de
> modales/fichas "la ficha abre en edicion", `ModalHeaderActions`, limpieza de modales huerfanos de
> Mantenimiento, tipo "Modulo contributivo" en Unidades) es SOLO CODIGO. **Esas migraciones de Folders y su
> ensayo las debe confirmar el responsable de ese epico** (ver `ESTADO 2026-10-22 Folders` y su doc per-deploy);
> la sesion UI que bumpeo a 0.0.238 no las probo. La **API sigue en 0.0.212**; 0.0.238 es la version del Web
> (la del pie de la app).
>
> **VERSION A DESPLEGAR = 0.0.212** (`main` HEAD `4a042760`, merge de CARGA 02 = la plantilla de carga sale en
> el orden de columnas configurado de cada tabla). **SOLO CODIGO, SIN MIGRACION, SIN CONFIG.** Afecta solo la
> generacion de la plantilla Excel (6 hojas): manda el `orden` guardado en `unidad_campos_config` por entidad;
> UNIDAD PRIVADA/NOMBRE fija de primera y PRINCIPAL pegada a AGRUPACION; los campos propios se intercalan donde
> el usuario los puso. El importador no cambia (lee por encabezado). Encima de 0.0.211, que SI trae la migracion
> `EquipoCarga_CoeficientesCincoDecimales` (ver banner de abajo): si prod venia de <=0.0.209, esa migracion
> sigue pendiente de aplicar.
>
> **VERSION PREVIA = 0.0.211** (`main` HEAD `b8130b39`, merge de `equipo/carga-plantilla` = CARGA 01,
> carga masiva de unidades; incluye tambien los ajustes del modal de Tareas/subtarea, solo codigo). **1 MIGRACION**
> `EquipoCarga_CoeficientesCincoDecimales`: `coeficiente_propiedad` y `modulo_contributivo_1..5` de
> `unidades_privadas` pasan de `numeric(7,4)` a `numeric(8,5)` (ampliacion, sin perdida de datos; aplicada en
> `propia_dev` el 2026-10-20). Va con cambios de codigo en el importador (anexo sin PRINCIPAL o con principal
> inexistente se rechaza y no se crea; vinculos de anexos nacen con "incluye en facturacion"), 5 decimales en
> la tabla de unidades y en el rebalanceo. Al desplegar: `dotnet ef database update` (o el SQL equivalente:
> `ALTER TABLE unidades_privadas ALTER COLUMN coeficiente_propiedad TYPE numeric(8,5)` y lo mismo para
> `modulo_contributivo_1..5`). Sin config nueva. **Paquete NuGet nuevo** en `Propia.Infrastructure`: `ExcelDataReader 3.7.0` (MIT), para que la carga
> masiva acepte tambien `.xls` (Excel 97-2003): `ExcelEntrada` detecta la firma binaria y convierte el .xls a un
> XLWorkbook en memoria; los importadores no cambian. `dotnet restore` normal; sin dependencias nativas.
>
> **ULTIMA ACTUALIZACION 2026-10-14: VERSION A DESPLEGAR = 0.0.147** (`main` HEAD `0969b57f`, rama
> `equipo/alex-header-zonas-equipos` ya mergeada a `main`). Tanda grande **SOLO CODIGO (SIN MIGRACION, SIN
> CONFIG)**: homogenizacion de la vista-tabla (Filtros y Agrupar con TODOS los campos incl. dinamicos en todos
> los modulos; Agrupar = Ordenar/Columnas) + expander del alta = crear+abrir ficha en todos (Usuarios, Roles,
> y ficha nueva en Vehiculos/Mascotas) + arreglos de Usuarios (alta con rol obligatorio, sin lapiz por fila,
> multi-rol en el modal) y Roles (editor sin "Semilla"). Detalle en `DEPLOY_HANDOFF_2026-10-14_v0.0.147.md`.
> No agrega migracion sobre 0.0.145 (`AddUsuarioTenantArchivado` sigue siendo la ultima).
>
> _(Historico 2026-10-13: VERSION 0.0.146 (`0e64eaf4`): fix subida de imagenes Identidad + drag&drop, solo codigo.)_
>
> _(Historico: VERSION 0.0.145 (`3c1e864a`): 1 MIGRACION `AddUsuarioTenantArchivado` + vista "Archivadas" del
> selector. Si prod NO tiene 0.0.145, aplicar esa migracion. 0.0.146/0.0.147 no agregan migracion.)_
>
> _Lo que sigue abajo es el detalle historico de tandas previas (0.0.146, 0.0.145, ...)._
>
> **(banner previo 0.0.146) VERSION A DESPLEGAR = 0.0.146** (`main` HEAD `0e64eaf4`, rama
> `equipo/alex-header-zonas-equipos` ya mergeada a `main`). Esta tanda (0.0.146) es **SOLO CODIGO (SIN
> MIGRACION, SIN CONFIG)**: arregla la **subida de imagenes** (logo/fachada) de la Identidad de la copropiedad
> + tarjeta **drag & drop**. Causa raiz del 401: `IdentidadForm` recibia el parametro `Jwt` como literal
> (`Jwt="_jwt"` sin `@`) y mandaba `Bearer _jwt` -> `IDX14100 no dots` -> 401. Detalle en la seccion
> **ESTADO 2026-10-13 Subida de imagenes Identidad** y en el doc `DEPLOY_HANDOFF_2026-10-13_v0.0.146.md`.
> Recomendado (aparte): activar storage **R2** en prod para que las imagenes subidas no se pierdan en cada
> redeploy (`LocalBlobStorage` usa filesystem efimero). Ver "Config" del doc per-deploy.
>
> **Tanda anterior (0.0.145, `3c1e864a`): 1 MIGRACION** + UI/servicio: vista **"Archivadas"** en el selector
> "Mis copropiedades" (archivar/activar copropiedades POR USUARIO). **Si prod NO tiene aun 0.0.145, APLICAR la
> migracion** `AddUsuarioTenantArchivado` (columna `archivado` en `usuarios_tenant` + recrea `get_tenants_for_persona`
> y agrega `set_usuario_tenant_archivado`). La tanda 0.0.146 NO agrega migracion sobre 0.0.145.
> Detalle en **ESTADO 2026-10-13 Selector: vista Archivadas** y en `DEPLOY_HANDOFF_2026-10-13_v0.0.145.md`.
>
> **OJO - prod ya esta en 0.0.141** (la migracion `AddOrganizacionLimiteCopropiedades` ya se aplico). Las
> tandas 0.0.142-0.0.144 (expander del alta + hotfix del limite) son UI/servicio SIN migracion. La UNICA
> migracion pendiente de 0.0.141 a 0.0.145 es `AddUsuarioTenantArchivado` (esta tanda). Si se despliega sobre
> una prod ANTERIOR a 0.0.141, aplicar ademas `AddOrganizacionLimiteCopropiedades` (ver doc 0.0.141).
> Todo lo anterior a 0.0.132 sigue vigente. El bloque "> Generado..." de abajo es HISTORICO (describe 0.0.99).
>
> Generado 2026-09-10, actualizado **2026-09-28 (TANDA 2: carga masiva single-tenant + ficha unidad + AGRUPAR compartido + hubs)**.
> Version a desplegar: **0.0.99** (csproj bumpeado). HEAD `main` (`0a9e147`) incluye ademas de la tanda 0.0.98:
> carga masiva sin columna COPROPIEDAD (single-tenant), desplegable de UNIDAD PRIVADA en Residentes, la ficha
> de unidad reestilizada (editor `.cpe-*`), el componente compartido de agrupacion `TablaAgruparMenu`, y la
> tarjeta de carga masiva movida arriba de las pestanas en los hubs.
> Prod (ultimo dato conocido, deploy 2026-09-24): **0.0.95** (`025676b`). Este deploy sube TODO lo acumulado
> desde ahi (0.0.96 Usuarios + 0.0.98 + esta tanda 0.0.99). Lo nuevo de ESTA tanda esta en la seccion
> **ESTADO 2026-09-28 TANDA 2**; la anterior en **ESTADO 2026-09-28 (SPLIT TAREAS)**.
> Repo: https://github.com/alexandercuartas665/PROPIA  ·  rama `main` (HEAD al desplegar).
> Companion: `DEPLOY_CHECKLIST.md` (misma carpeta) con el detalle version por version.
> Este archivo es el resumen operativo para la sesion de deploy. **Vive en el repo** (`deploy/`) y se
> copia a la carpeta de trabajo; asi no se pierde.

---

## ESTADO 2026-10-26 Funcion en el "+" + indicadores Unidades + alineacion Equipos + logo  [v0.0.239, SOLO CODIGO]

**SIN MIGRACION, SIN CONFIG.** Commits en `main` (HEAD `1b63c211`): `594819a6` (Funcion), `098bc64f`
(indicadores), `d498d5e2` (bump 0.0.238), `80a4c601` (alineacion Equipos), `1b63c211` (logo + bump 0.0.239).

### 1) Campos Funcion/calculado ofrecidos en el "+" de los 8 modulos sin relaciones
- `CrearCampoModal.razor` gana el parametro `OfreceFuncion` (default false). El tipo **Funcion/calculado**
  aparece al CREAR si `(TiposAvanzados || OfreceFuncion) && !EsEdicion`. Usuario/Directorio (tipos de
  RELACION) siguen gateados SOLO por `TiposAvanzados`, asi que esto NO expone relaciones donde no las habia.
- Se puso `OfreceFuncion="true"` en el "+" de: Vehiculos, Mascotas, Zonas, Equipos, Residentes, Directorio,
  Contratos (`Servicios.razor`) y Polizas (`Seguros.razor`). Antes, en esos 8 la unica via para crear un campo
  Funcion era el gestor viejo `ConfigCamposEntidad`. Los 4 que ya la ofrecian (Unidades/PQRSD/Tareas/Usuarios)
  no cambian. **El computo de la Funcion ya estaba soportado en backend** para todos; no hay cambio de API/BD.
- Verificado runtime (copropiedad demo): el "+" ofrece "Funcion / calculado" y NO "Relacion" en Mascotas
  (vinculado) y Polizas (no-vinculado); piloto Vehiculos con create + computo end-to-end.

### 2) Indicadores en Unidades Privadas (`/distribucion`, `DistribucionPanel.razor`)
- Tres tarjetas encima de la tabla, mismo patron visual que los KPIs de Usuarios (clases `.dst-kpi*`, tokens de
  `propia-tokens.css` -> dark mode cubierto; fuente/tamano identicos validados por `getComputedStyle`):
  **Unidades privadas** (`_unidades.Count`), **Apartamentos** (tipo base Apartamento, `tipo=1` sin tipo custom)
  y **Suma de coeficientes** (`sum(CoeficientePropiedad)` de TODAS las unidades, sin filtrar, con sufijo `%`).
- Verificado contra BD (tenant demo): 605 / 264 / 103.626% coinciden 1:1 con count/filter/sum sobre
  `unidades_privadas`. Solo lectura; no toca endpoints ni esquema.

### 3) Equipos (tabla): alineacion header vs filas (`EquiposActivosPanel.razor`, solo CSS)
- Bug: con un campo presente (p.ej. el campo Funcion `ValorTotalAdq`), su valor se pintaba a la DERECHA del
  "+", bajo ningun titulo. Causa: `.ea-listgrid { min-width: max-content }` se evalua por elemento; el header
  (labels cortos) quedaba mas angosto que las filas (infladas por `<select>` con texto largo), y las pistas
  `fr` se resolvian distinto -> celdas del cuerpo corridas.
- Fix: se quita `min-width:max-content`; cada pista flexible lleva piso en px (`minmax` en `ColWidth` y en el
  fallback de `.ea-listgrid`) y las celdas recortan (`.ea-cell`/`.ea-th-dyn` con `min-width:0` + `overflow:hidden`
  + ellipsis). Header y filas quedan al mismo ancho; scroll horizontal cuando no caben. Verificado: ambos a
  1012px, celda VALORTOTALADQ del header coincide 1:1 con las filas. **Solo Equipos** (no se tocaron los demas
  `*-listgrid`).

### 4) Logo del sidebar (`NavMenu.razor`, solo CSS)
- `.pnav-logo`: de 46px/max-170px a **68px/max-80px** (`object-fit:contain`, `display:block`). Cabe en
  `.pnav-brand` (74px) sin recorte. Verificado render 68x68, maxWidth 80px. El override de sidebar mini
  (30px/60px) no cambia.

**Deploy:** redeploy de `Propia.Web` normal (`dotnet publish`), sin pasos de BD para ESTA tanda. Las migraciones
de Folders del rango 0.0.213-0.0.238 son aparte (ver banner arriba).

---

## ESTADO 2026-10-22 Documentos v2 "Folders" - Ola 0 (modelo + API)  [RAMA `equipo/expedientes`, NO en main]

Evolucion del modulo 2.15 Expedientes/TRD al concepto "Folders y documentos" (maqueta `expedientes-v2`).
Ola 0 = backend solamente; la UI vieja (`Documentos.razor` -> `/api/expedientes`) sigue funcionando sin cambios.

### 1 MIGRACION NUEVA - ADITIVA CON COPIA DE DATOS - APLICAR EN PROD (owner `propia`, `dotnet ef database update`)
`20261022105420_AddFoldersDocumentos`: crea `folders` (arbol, `parent_id`), `folder_documentos`,
`folder_documento_versiones`, `folder_campo_definiciones` (metadatos tipados de folder o documento, check XOR),
`folder_documento_campo_valores` (EAV). Las 5 con RLS FORCE + policy + GRANT a `propia_app`.
**Copia los datos v1 conservando Ids**: serie -> folder raiz; subserie -> subfolder; expediente -> subfolder
(mismo Id, por eso `contrato_expedientes.expediente_id` y `polizas.expediente_id` siguen validos); tipologias ->
documentos (archivo, versiones; si habia archivo sin fila de version se sintetiza la v1); campos -> metadatos
Texto (los del expediente que duplicaban a los de la subserie no se copian: ya se heredan); `meta_json` -> valores.
Durante la copia se quita `FORCE` RLS a las 8 tablas v1 (el owner no es superuser en prod) y se restaura al final,
todo en la misma transaccion. **No borra las tablas v1** (se retiran en la Ola 4, cuando la UI v2 este en main).
Rollback = `Down` (dropea las 5 tablas nuevas; las v1 quedan intactas). Ensayada el 2026-10-22 sobre una copia
de `propia_dev` (`propia_folders_test`, borrada despues): 2 expedientes, 3 archivos, 5 versiones, 3 valores migrados.
Sin config nueva. Sin paquetes nuevos (`System.IO.Compression` para el ZIP).

### API nueva `api/folders` (`FoldersController` / `FoldersService`)
Arbol, tarjetas, detalle con ruta y campos heredados, CRUD de folders (mover sin ciclos; eliminar solo vacio),
documentos (casilla, cargar, versionar, archivar/restaurar, duplicar, descargar, preview, versiones), metadatos
(crear/editar/eliminar; valores con validacion por tipo y obligatorios que bloquean) y `GET {id}/zip` recursivo.
Limite 20 MB, extensiones PDF/imagen/Office/texto. RBAC: `Documentos` Crear/Editar/Eliminar; GET abiertos al tenant.
Tests: `FoldersFlowTests` (6) + `RlsCoverageTests` verdes.

### Ola 1 (misma rama, solo codigo, sin migracion): UI "Folders"
`Documentos.razor` pasa a la vista v2: tarjetas de folders raiz + modales "Configurar folders" (3 columnas, arbol de
profundidad arbitraria, documentos, metadatos con `CrearCampoModal`) y "Nuevo folder". La vista Expedientes/TRD
v1 desaparece de la UI (el API `/api/expedientes` sigue vivo hasta la Ola 4). Archivo central sin cambios.
Archivos nuevos: `Components/Shared/Folders/*.razor`, `wwwroot/css/modules/folders.css` (link en `App.razor`),
`propiaUI.modalFocus` en `propia-ui.js` (`?v=25`). Contratos y Seguros conectan ahora FOLDERS (`/api/folders/lista`;
`GobiernoYServicios` consulta `Folders`, mismos Ids que los expedientes migrados).

### Ola 2 (misma rama, solo codigo): explorador de folders
Clic en una tarjeta abre `ExploradorFoldersModal` (98vw x 95vh): estructura | documentos como miniaturas | visor |
captura de metadatos | chat ocultable, con separadores redimensionables (`propiaUI.explorerSplits`, anchos en
localStorage `propia.folders.colw`). Cargar / reemplazar (versiones) / duplicar / archivar-restaurar / ZIP desde la
UI contra `api/folders`. El chat responde con datos del folder (vista previa); la IA real entra en la Ola 3.
Archivos: `Components/Shared/Folders/{ExploradorFoldersModal,CargarDocumentoModal,FolderCampoInput}.razor`,
`FolderUi.cs`; `folders.css?v=2`; `propia-ui.js?v=26`.

### Ola 3 (misma rama): chat con IA real + 1 MIGRACION NUEVA - ADITIVA
`20261022170331_AddFolderChatIndexacion`: tablas `folder_chat_mensajes` (conversacion por copropiedad + folder raiz +
usuario) y `folder_documento_textos` (texto extraido del archivo actual, una fila por documento) con RLS FORCE +
policy + GRANT a `propia_app`. Sin datos que migrar. Rollback = `Down`.
`FolderChatService`: proveedor = el habilitado en Super Admin (`ai_provider_configs`, prioridad Claude > Gemini >
ChatGPT > DeepSeek; modelo de la config o el del catalogo), cuota del plan via `IAiUsageService` (se registra con
source `folders-chat`). Contexto = estructura del folder raiz + por documento nombre/archivo/metadatos (con su
descripcion) + texto extraido (txt/csv/md directo; docx/xlsx en local; PDF e imagenes via el extractor de IA de
Super Admin, `IAiDocumentExtractor`, modulo `folders`). Limites: 4.000 caracteres por documento, 48.000 de contexto,
12 indexaciones por turno, 16 turnos de historial. API: `GET/POST/DELETE api/folders/{raiz}/chat`,
`POST api/folders/documentos/{id}/indexar?forzar=`. Sin IA configurada el chat responde un aviso y no rompe.
**Config necesaria en prod**: un proveedor habilitado con API key en Super Admin > Servidores de IA (ya existe para
los agentes) y, para leer PDF/imagenes, el motor de extraccion de IA (el mismo que usa Seguros/OCR).

### Ola 4 (misma rama): cierre + 1 MIGRACION NUEVA - DESTRUCTIVA
`20261023210234_DropExpedientesV1`: elimina las 8 tablas v1 (`series_documentales`, `subseries_documentales`,
`subserie_tipologias`, `subserie_campos`, `expedientes`, `expediente_tipologias`, `expediente_tipologia_versiones`,
`expediente_campos`). Sus datos ya estan copiados en las tablas `folder*` por `AddFoldersDocumentos` (mismos Ids).
Orden en prod: backup -> `AddFoldersDocumentos` -> validar en la UI que los expedientes aparecen como folders ->
`AddFolderChatIndexacion` -> `DropExpedientesV1`. El codigo v1 (controller `api/expedientes`, servicio, entidades)
ya no existe en esta rama. Rollback real = backup previo (el `Down` recrea el esquema vacio).
**Resumen de las 3 migraciones de esta rama (en orden):** `20261022105420_AddFoldersDocumentos` (aditiva + copia),
`20261022170331_AddFolderChatIndexacion` (aditiva), `20261023210234_DropExpedientesV1` (destructiva).
**Nota de entorno (dev):** la API key del proveedor Gemini de agentes en `propia_dev` esta cifrada con una llave de
Data Protection que ya no existe en `data_protection_keys` (`a0384552-...`); vuelve a guardarla en Super Admin >
Servidores de IA. Mientras tanto el chat de Folders usa el motor de documentos (OCR Gemini), cuya clave si descifra.
**Menu lateral:** el catalogo base dice "Documentos" (`MenuCatalog.cs`); si una copropiedad ve "Expedientes" es su
menu personalizado (editable desde la app), no codigo.

## ESTADO 2026-10-13 Subida de imagenes Identidad (logo/fachada) + drag & drop  [VERSION ACTUAL 0.0.146]

Version **0.0.146**. Commit `0e64eaf4`. Rama `equipo/alex-header-zonas-equipos` (ya en `main`). **SIN migracion, SIN config obligatoria.**

### Que cambia
- **Fix del 401 al subir logo/fachada.** Causa raiz (Blazor): un parametro de componente tipo `string` pasado
  como `Attr="_jwt"` **sin `@`** se interpreta como el literal `"_jwt"`, no como el campo. `IdentidadForm`
  enviaba `Bearer _jwt` al API -> `IDX14100: JWT is not well formed, there are no dots` -> 401. Fix:
  `MiCopropiedadPanel.razor` `Jwt="@_jwt"` y `EditarIdentidadModal.razor` `Jwt="@Jwt"`.
- **Subida robusta + drag & drop**: `IdentidadForm.razor` bufferiza el `InputFile` a bytes (`ByteArrayContent`)
  antes del POST, y las tarjetas de Logo/Fachada aceptan arrastrar/soltar o clic, con preview y boton quitar.
- CSS `.pmm-drop*` en `propia-tokens.css`; bump `?v` del link en `App.razor`.

### Migracion / Config
- **Ninguna migracion.** La ultima sigue siendo `AddUsuarioTenantArchivado` (tanda 0.0.145).
- **Sin cambios de env obligatorios.** Recomendado (opcional, para persistir imagenes en prod): `Storage__Provider=R2`
  + `Storage__R2__{AccountId,AccessKeyId,SecretAccessKey,BucketName,PublicUrl}`. Sin R2 las subidas funcionan pero
  el filesystem de Railway es efimero y la imagen se pierde en el siguiente redeploy.

### Verificacion post-deploy
- `Configuracion Copropiedad` > Identidad > **Editar datos** > tarjeta **Logo**/**Fachada**: arrastrar/soltar o
  clic -> sube y muestra preview (sin "No se pudo subir la imagen"). **Guardar** -> recargar -> el logo persiste
  y la tarjeta del selector "Mis copropiedades" lo muestra.

### Verificado en dev (copropiedad demo)
POST `/api/uploads/imagen/logo` **200** (antes 401); blob escrito en `wwwroot/uploads/tenants/<tid>/logo.png`;
preview renderizada; al Guardar, `tenants.logo_url` persiste la URL subida. Datos de prueba revertidos.

### Rollback
Redeploy del artefacto 0.0.145. **No toca BD** (sin migracion en esta tanda).

---

## ESTADO 2026-10-13 Selector "Mis copropiedades": vista Archivadas (archivar/activar por usuario)

Version **0.0.145**. Commit `3c1e864a`. Rama `equipo/alex-header-zonas-equipos` (ya en `main`).

### 1 MIGRACION NUEVA - APLICAR EN PROD (owner `propia`, `dotnet ef database update`)
- **`20261013214928_AddUsuarioTenantArchivado`**:
  - Agrega columna `archivado boolean not null default false` a `usuarios_tenant` (TenantEntity; la RLS existente
    ya la cubre, no hay bloque RLS nuevo).
  - **Recrea la funcion SQL `get_tenants_for_persona(uuid)`** para devolver ademas `archivado` y `ultimo_acceso`
    (cambia la firma -> DROP + CREATE; SECURITY DEFINER, igual que antes).
  - **Crea la funcion `set_usuario_tenant_archivado(persona, tenant, archivado)`** (SECURITY DEFINER, row_security
    off) que archiva/activa el vinculo de UNA persona con UN tenant saltando la RLS de `usuarios_tenant`.
  Reversible (Down restaura la firma vieja de la funcion y quita la columna).

### Que habilita
En el selector "Mis copropiedades" (`CopropiedadSwitcher`, el modal que abre al tocar el nombre de la copropiedad
arriba):
- 3ra vista **"Archivadas"** (siempre en tabla) con contador en el toggle.
- **Icono de archivar** en las tarjetas (galeria) y **boton Archivar** en la vista Tabla; en Archivadas,
  **boton Activar** (desarchivar). Las archivadas NO salen en Galeria ni Tabla.
- Vista **Tabla**: quitada la columna **Rol**; ahora `Copropiedad | Codigo | Ultimo ingreso | accion`.
- **Archivar es POR USUARIO** (preferencia personal del selector; NO cambia el estado global de la copropiedad
  ni afecta a otros usuarios). "Ultimo ingreso" = `usuarios_tenant.ultimo_acceso` del propio usuario.

### Cambios de codigo (sin seeds ni config)
- `UsuarioTenant.cs` (+`Archivado`). Migracion arriba.
- `TenantInfo` (Application) / `TenantInfoDto` (Shared) += `Archivado`, `UltimoAcceso`; `AuthService.cs` lee las
  columnas nuevas de la funcion (GoogleSignInService solo lee 2 columnas -> no cambia).
- `MisCopropiedadesController.cs` (`PUT ~/api/mis-copropiedades/{tenantId}/archivar` y `/desarchivar`) +
  `IMisCopropiedadesService`/`MisCopropiedadesService.cs` (`ArchivarAsync`, via la funcion SECURITY DEFINER).
- `CopropiedadSwitcher.razor` (3 vistas + iconos + columnas + CSS).

### Verificacion (dev, demo admin con varias copropiedades, Web 5105)
Archivar [YUNQUE] desde la tabla -> sale de Tabla y Galeria, "Archivadas" pasa a 1; en Archivadas -> Activar ->
vuelve a la lista; galeria con icono de archivar en cada tarjeta; Tabla sin Rol y con Ultimo ingreso. Es por-usuario
(la funcion solo toca el vinculo de la persona autenticada). Sin datos de prueba (0 `archivado=true` al cerrar).

### Rollback
Redeploy del artefacto previo. La columna es aditiva e inerte para el codigo viejo; opcionalmente `database update`
a la migracion anterior (restaura la firma vieja de `get_tenants_for_persona`). OJO: el codigo viejo espera la
funcion con 4 columnas, asi que si se hace rollback del CODIGO hay que revertir TAMBIEN la migracion (o la funcion
devolveria 6 columnas y AuthService leeria por indice 0-3 sin romper, pero mejor revertir ambos juntos).

---

## ESTADO 2026-10-13 Expander del alta (crear+abrir ficha) + hotfix del limite de copropiedades

Versiones **0.0.142 -> 0.0.144**. Commits `554195f5`, `4d99d7c1`, `12115655`. **UI + servicio, SIN migraciones.**
Deploy = redeploy del artefacto de `main`. Rollback = redeploy del artefacto previo.

### 1) Expander del alta: crear + abrir ficha (0.0.142/143)
El icono de expandir de la fila de alta (vista Tabla) ahora crea el registro con lo escrito y abre su
ficha/modal para completar, igual que Unidades. Modulos igualados: **Zonas Comunes, Equipos y Activos**
(`554195f5`), **Residentes, Seguros, Contratos** (`4d99d7c1`). Detalle: `<span>` muteado -> `<button>` que
llama al handler de crear-y-abrir (en Zonas/Equipos `CrearYAbrirFichaAsync` ahora tambien persiste los campos
dinamicos del alta y cierra la fila; en Residentes reusa el `abrir` que ya tenia `AgregarAsync`; en Seguros y
Contratos se agrego `bool abrir` que abre el modal tras crear). Pendientes (NO en esta tanda): Usuarios/Roles
(decision de producto) y Vehiculos/Mascotas (no tienen ficha de registro aun).

### 2) HOTFIX: el error del limite de copropiedades nombra la organizacion (0.0.144)
Reporte de prod: "amplie el limite en Super Admin pero el tenant sigue pegado en 1". El limite de copropiedades
es **por ORGANIZACION** y el mensaje no decia de cual, asi que no se sabia si se amplio la correcta (la
copropiedad nueva cuelga de la organizacion de la copropiedad que el usuario ADMINISTRA; si sus copropiedades
estan repartidas en varias orgs, puede ampliar una distinta a la que recibe la nueva). **No es un bug de
logica** (el override se lee y aplica; verificado). Cambios (`MisCopropiedadesService.CrearAsync`):
- El error ahora nombra la org: *"El limite de copropiedades de la organizacion 'X' es N y ya tiene M. Amplia
  el limite de ESA organizacion en la consola (Super Admin) o contacta a soporte."*
- Log (Information) con tenantActivo + orgDestino (id + nombre) + override + limite del plan + limite efectivo +
  conteo, para rastrear que organizacion evalua el enforcement.
Sin cambio de la regla (`override de la org ?? plan ?? 1`). **Sin migracion.**

### Verificacion (dev, Web 5105)
Expander: Seguros happy-path completo (crea poliza + abre modal, luego borrada); Zonas/Equipos crean + abren
ficha; Residentes/Contratos: el expander dispara el handler (validacion de minimos visible). Hotfix: con
override=1 en una org con 7 copropiedades, el create responde "...organizacion 'Administraciones del Valle SAS'
es 1 y ya tiene 7...". Sin datos de prueba.

---

## ESTADO 2026-10-13 Super Admin: limite de copropiedades por organizacion + ver plan

Version **0.0.141**. Commit `978950c2`. Rama `equipo/alex-header-zonas-equipos` (ya en `main`).

### 1 MIGRACION NUEVA - ADITIVA - APLICAR EN PROD (owner `propia`, `dotnet ef database update`)
- **`20261013194852_AddOrganizacionLimiteCopropiedades`**: agrega 1 columna NULLABLE a `organizaciones`:
  `limite_copropiedades integer null`. **Sin backfill.** **Sin tabla nueva.** `organizaciones` es GLOBAL (sin
  RLS), asi que no hay bloque RLS. Reversible (DropColumn). Inerte para el codigo viejo (null = comportamiento
  actual).

### Que habilita
En `/admin/organizaciones` (consola A&D) el operador ahora puede:
- **Editar el limite de copropiedades de UNA organizacion** (override): un numero propio que tiene prioridad
  sobre el limite del plan; dejarlo vacio = usar el limite del plan (comportamiento de hoy). 0 = no puede crear mas.
- **Ver el plan** de cada organizacion (nombre del plan de su suscripcion activa) y el limite efectivo junto al
  conteo (`count / limite`, badge "propio" si hay override).

Enforcement (al crear copropiedad por el flujo tenant-admin, `MisCopropiedadesService`):
`limite = override de la org ?? limite del plan ?? (sin suscripcion -> 1)`. El override es el unico cambio de
prioridad; el resto de la logica no cambia.

### Cambios de codigo (todo backend + UI, sin seeds ni config)
- `Organizacion.cs` (+`LimiteCopropiedades int?`). Migracion arriba.
- `SuperAdmin/Dtos.cs`: `OrganizacionDto` += `LimiteOverride`, `PlanNombre`, `LimitePlan`, `LimiteEfectivo`
  (params opcionales -> compatible con los clientes/tests que lo deserializan); nuevo `CambiarLimiteOrganizacionRequest`.
- `ISuperAdminService`/`SuperAdminService.cs`: `ListOrganizacionesAsync` trae plan + limites; nuevo
  `CambiarLimiteCopropiedadesAsync` (con log de auditoria `CHANGE_LIMITE_ORGANIZACION`).
- `AdminController.cs`: `PUT ~/api/admin/organizaciones/{orgId}/limite` (policy SuperAdmin).
- `MisCopropiedadesService.cs`: enforcement con override.
- `Organizaciones.razor`: columna Plan + celda limite editable + modal.

### Verificacion (hecha en dev, founder super-admin, Web 5105)
GET organizaciones trae plan + limites; PUT override=10 persiste (BD) y efectivo=10 (gana sobre el plan),
revertir a null vuelve al plan; **enforcement end-to-end**: override=1 en una org con 7 copropiedades ->
el create por el flujo tenant-admin rechaza 400 "El limite permitido es 1 ... ya tienes 7"; UI muestra la
columna Plan y, tras editar, la fila pasa a "0 / 5 propio". Datos de prueba borrados (0 overrides en BD).

### Rollback
Redeploy del artefacto previo. La migracion es aditiva e inerte para el codigo viejo; opcionalmente
`database update` a la migracion anterior para quitar la columna (no necesario).

---

## ESTADO 2026-10-13 Homogeneidad alta inline + fila recien creada VIOLETA + limpieza CSS

Versiones **0.0.133 -> 0.0.140**. Rama `equipo/alex-header-zonas-equipos` (ya en `main`, HEAD `8c9b6fff`).
**SIN migraciones. SIN cambios de backend/servicio. SIN config/seeds nuevos. 100% UI (.razor + .css).**
Deploy: redeploy del artefacto de `main`. Rollback: redeploy del artefacto previo (no hay esquema que revertir).

Que incluye (todas las vistas-tabla quedan homogeneas en el alta inline y en el resaltado de recien creado):

1. **Roles - bandeja homogenizada (0.0.133, commit `e192630d`).** El modal/bandeja de Roles y Permisos usa
   ahora los controles canonicos: `FiltroDinamico` + Ordenar + Agrupar + menu de columna (incl. ver/crear
   categorias desde la columna Categoria). La matriz de permisos pasa de "." a checkbox deshabilitado
   (simetrica). `roles.css ?v=15`.
2. **Alta inline al canon VIOLETA (0.0.134-0.0.138).** El estilo de la fila de alta se unifico en TODOS los
   modulos: input "fantasma" (`border:1px solid transparent` + fondo transparente, `padding:10px 12px`,
   `font:13px`, radius 8) con borde+glow violeta al foco (`1.5px var(--propia-brand)` + `0 0 0 3px
   rgba(109,79,227,.14)`); fila de alta abierta con fondo `brand-soft-2`; hint "Tab/Esc" presente.
   - Corregidos: Vehiculos, Mascotas, Residentes, Contratos, Mantenimiento, Tareas, Seguros, Roles
     (commit `fcfcfd66`), Zonas y Equipos -padding/font que faltaban- (`f368c88a`).
   - Unidades: el foco pasa del anillo `::after` inset (recuadro flotante) a **borde sobre el input** como
     el resto (`b45174de`), y la celda de alta gana aire vertical (`padding:7px 4px`) para que el glow no se
     recorte (`a06cd16b`).
3. **Fila recien creada VIOLETA en los 11 modulos vista-tabla (0.0.139, commit `790cd41a`).** El resaltado
   de "recien creado" pasa de VERDE (`ok-soft`/`#34C759`) a VIOLETA (`var(--propia-brand-soft-2)` +
   `inset 3px var(--propia-brand)`). Modulos: Unidades, Vehiculos, Mascotas, Residentes (fila+tarjeta),
   Seguros, Equipos, Zonas, Directorio, Mantenimiento, PQRSD, Contratos. El verde queda SOLO para
   toasts/mensajes de exito (po-flash.ok, ctr-toast), que NO se tocaron.
4. **Limpieza CSS muerto en Unidades (0.0.140, commit `8c9b6fff`).** Se quitaron ~85 selectores `.dst-*`
   muertos (144 lineas) del `<style>` inline de `DistribucionPanel.razor`, residuo de la migracion al canon
   `.tbl`. Verificado sin uso en todo el repo; sin efecto funcional ni visual.

**CSS cache-bust (en `App.razor`, viajan con el artefacto):** `tabla-base.css ?v=10`, `servicios.css ?v=19`,
`mantenimiento.css ?v=5`, `directorio.css ?v=12`, `pqrs.css ?v=23`, `roles.css ?v=15`, `tareas.css ?v=28`.
El resto del alta es inline en el `.razor` (sin `?v`). Al servir detras de CDN/proxy, purgar cache de esos CSS
o confiar en el `?v` (recomendado: el `?v` ya fuerza la recarga).

**Verificacion (hecha en dev, demo, Web 5105):** build sin errores; alta inline validada modulo por modulo
(input transparente + glow violeta + hint); fila recien creada validada en los 11 modulos (escaneo de
stylesheets -> todas en `brand-soft-2`+`brand`, 0 reglas verdes residuales; render real en Zonas
`rgb(245,241,254)`+`rgb(109,79,227)`); Unidades tras la limpieza CSS renderiza igual (tabla+282 registros,
menu Columnas, vista Tarjetas, alta+hint, footer). Llaves/comentarios del `<style>` balanceados.

**Pendiente conocido, NO en esta tanda (diagnostico, no deploy):** en `DistribucionPanel.razor` queda un token
CSS roto cosmetico (`.cg-row-warn td { background: var(--propia-bg-card)af2; }`) y codigo C# muerto (ruta de
import CSV huerfana + banner `_impResult` inalcanzable). No afectan el deploy; se corrigen aparte.

---

## ESTADO 2026-10-13 Columnas mover+ancho (10 modulos) + retiro PQRSD legacy

Versiones 0.0.126-0.0.132. **Sin migraciones.** Todo UI salvo UN cambio de backend (constante):
- `MiCopropiedadService.Unidades.cs` -> `EntidadesCampoConfig` += `comunicacion, listanegra, asamblea,
  lineawhatsapp, cartera, intervencion, porteria_visitantes, porteria_movimientos, porteria_vehiculos,
  porteria_correspondencia` (whitelist del endpoint generico `unidades-config`; la tabla ya es generica, sin
  migracion). (`programacion` ya estaba.) Sin esto, esos modulos dan 400 al guardar orden/ancho de columnas.
- UI: reorder-drag + resize de columnas en Seguros, Tareas (vista Tabla), Comunicaciones, Lista negra,
  Asambleas, Lineas WhatsApp, Cartera, Porteria (4 tablas) y Mantenimiento (Programacion + Intervenciones).
  Persisten en unidades-config por entidad (Seguros=poliza, Tareas=config del tablero, y las entidades
  nuevas de arriba). Porteria y Mantenimiento enrutan sus varias tablas por prefijo del data-clave. Tareas:
  fix de `GuardarColCfgT` que borraba el ancho al reordenar/ocultar.
- **Retirada** la lista PQRSD legacy (`Pqrs.razor`, rutas `/pqrs-list` y `/pqrsd-list`): estaba huerfana, la
  reemplaza `PqrsKanban.razor` (`/pqrs`,`/pqrsd`). Rollback: redeploy del artefacto previo.
- **Programa COMPLETO:** toda vista-tabla de datos del tenant tiene mover-columna + ancho. (La tabla de
  Planes/plantillas de Mantenimiento y la normalizacion grande al `.tbl` son otro plan, fuera de esto.)

---

## ESTADO 2026-10-12 E5.1 Mascotas·tipo: agregar tipos por copropiedad

Version 0.0.125. Rama `equipo/alex-header-zonas-equipos`.

### 1 MIGRACION NUEVA — ADITIVA — APLICAR EN PROD (owner `propia`, `dotnet ef database update`)
- `20261012230340_AddMascotaTipoCustom`: agrega 1 columna NULLABLE a `unidad_mascotas`:
  `tipo_custom text null`. **Sin backfill** (las filas existentes siguen usando el enum `tipo`).
  **Sin tabla nueva** (la RLS existente de `unidad_mascotas` ya cubre la columna). Reversible (DropColumn).

Qué habilita: la copropiedad puede AGREGAR tipos de mascota propios (igual que `tipos_unidad_custom`).
El select de Tipo muestra las semillas A&D (`mascota.tipo`) + las propias (guardadas en `unidad_campos_config`
entidad=`mascotas`, clave=`tipo`, que escribe el modal canónico AutoServido). Una fila con tipo propio guarda
`tipo = Otro (4)` + `tipo_custom = <etiqueta>`; al volver a una semilla, `tipo_custom` se limpia. El enum sigue
intacto como semilla/respaldo, así que Portería/reservas/reportes que leen el enum NO cambian. La plantilla
Excel ofrece las propias en el desplegable (export) y el import resuelve una etiqueta no-semilla como tipo
propio (en vez de colapsar a Perro). Sin catálogo nuevo (reusa `mascota.tipo`). Rollback: redeploy del
artefacto previo + `database update` a la migración anterior si se desea quitar la columna (no es necesario,
es aditiva e inerte para el código viejo). Es la primera ola del enum-agregar (E5.1); E5.2–E5.4 pendientes.

---

## ESTADO 2026-10-12 E1b/Usuarios/Tareas/Mantenimiento: editores de lista + Mantenimiento al catalogo

Version 0.0.124. **Sin migraciones nuevas** (todo UI + seeding de catalogo). El `CatalogoListasSeeder`
(arranque) siembra 5 listas nuevas de Mantenimiento: `mantenimiento.periodicidad` (9), `mantenimiento.prioridad`
(4), `mantenimiento.intervtipo` (2), `mantenimiento.intervestado` (5), `mantenimiento.intervprioridad` (4).
Son ENUMS PUROS: A&D renombra los labels en /admin/catalogos y se reflejan en Mantenimiento; el VALOR sigue
siendo el enum, asi que el scheduler (periodicidad) y el workflow (estado) NO cambian.

UI (sin migracion ni catalogo nuevo): "Editar campo" muestra la lista en Vehiculos/Mascotas/Equipos/Zonas/
Directorio(sexo); Usuarios(cargo) agrega opciones por copropiedad (texto, sin migracion); Tareas
(etapa/prioridad/etiquetas) usa el modal canonico por-tablero (lista del tablero + agregar + gestor de
color/orden). Rollback: redeploy del artefacto previo; el seeder es idempotente y no destructivo.

---

## ESTADO 2026-10-12 PQRSD: tipo (plazo) + estado (columnas Kanban) al catalogo A&D — global + override (LEER PRIMERO)

Rama `equipo/alex-header-zonas-equipos`. E2 COMPLETO: las listas PQRSD **tipo** (plazo legal Ley 1755) y
**estado** (columnas del tablero) pasan al catalogo global A&D. Diseno SEGURO: **el motor de plazos/workflow
NO se toca** (sigue leyendo el enum + las tablas por copropiedad); A&D manda el valor base global (plazo del
tipo; nombre/color del estado) y se PROPAGA a cada copropiedad por el seeder, salvo override.

### 2 MIGRACIONES NUEVAS — ADITIVAS — APLICAR EN PROD (owner `propia`, `dotnet ef database update`)
- `20261012182837_PqrsdTipoOverridePlazo`: `pqrsd_tipos` += `override_plazo` (bool, default false) +
  **backfill** que marca override=true los tipos base cuyo `dias_habiles` difiere del plazo de fabrica
  (PqrsdCatalogo.PlazosBase) de su `legal`. No destructiva.
- `20261012195911_PqrsdEstadoOverrideVisual`: `pqrsd_estados` += `override_visual` (bool, default false) +
  **backfill** que marca override=true las columnas base cuyo nombre/color difiere del de fabrica. No destructiva.

### CONFIG/ARRANQUE — seeder de catalogo (idempotente)
- `CatalogoListasSeeder` siembra 2 listas globales nuevas: `pqrsd.tipo` (clave `e:<TipoPqrsd>`, Meta
  `{plazoDias,diasInconformidad,nivelUrgencia,legal}`) y `pqrsd.estado` (clave `e:<EstadoPqrsd>`, color +
  Meta `{semanticaLegal,terminal,orden}`), paridad con PlazosBase / EstadosBase.
- `PqrsdService.AsegurarCatalogoBaseAsync` / `AsegurarTableroBaseAsync` PROPAGAN (perezoso) el plazo del tipo
  y el nombre/color de la columna base desde el catalogo a las copropiedades sin override. Inicio identico.

### Que cambia para el usuario
- A&D ya puede editar en `/admin/catalogos`: el **plazo** de los tipos (columna "Plazo (dias)" en `pqrsd.tipo`)
  y el **nombre/color** de las columnas base (`pqrsd.estado`). Al guardar, aplica a las copropiedades que no
  lo hayan personalizado. La **semantica legal/terminal** de las columnas es backbone (no editable).
- En `/pqrsd`, el "Editar campo" de las columnas **Tipo** y **Etapa** ya muestra la lista + agregar.
- Verificado en dev: propagacion A&D->copropiedad OK (plazo y color); override respetado; "Editar campo"
  muestra y agrega; integracion PQRSD+Calendario 28/28 (motor legal sin regresion).

---

## ESTADO 2026-10-12 CONTRATOS: edicion completa + listas Forma de pago / Tipo de servicio (LEER PRIMERO)

Rama `equipo/alex-header-zonas-equipos`. En /contratos: todos los campos editables (celda + fila de
alta), y **Forma de pago** y **Tipo de servicio** pasan a ser LISTAS del catalogo global A&D.

### 1 MIGRACION NUEVA — ADITIVA — APLICAR EN PROD (owner `propia`, `dotnet ef database update`)
- `20261012165334_ContratosFormaPagoYTipoServicio`: agrega 2 columnas nullable a `contratos_servicio`
  (`forma_pago` text, `tipo_servicio_texto` text) + **backfill** de `tipo_servicio_texto` desde el enum
  `tipo` (etiquetas: 1=Aseo ... 13=Otros). Sin RLS nueva (la tabla ya tiene RLS). No destructiva: el
  codigo viejo ignora las columnas. `forma_pago` queda NULL (concepto nuevo = metodo de pago); el N de
  cuotas sigue en `forma_pago_cuotas`.
- Aplicada en **dev** (OK de Alex 2026-10-12).

### CONFIG/ARRANQUE — seeder de catalogo (idempotente, ya existente)
El `CatalogoListasSeeder` (corre al arrancar) ahora siembra 2 listas globales nuevas: `contrato.formapago`
(Efectivo/Transferencia/PSE/Cheque/Tarjeta/Debito automatico/Otro) y `contrato.tiposervicio` (paridad con
el enum). Idempotente: solo inserta lo que falta; A&D las edita en `/admin/catalogos`.

### Que cambia para el usuario
- "Forma de pago" ya no es "N de cuotas": es un metodo de pago (lista editable). El N de cuotas queda como
  columna aparte "N de cuotas" (no visible por defecto).
- "Tipo de servicio" es editable desde A&D (antes enum fijo).
- Valor, Pago mensual, Forma de pago, Tipo de servicio, N de cuotas: editables en la celda y en el alta
  (antes salian "-"). La fila de alta captura TODOS los campos visibles (sistema + propios + Etapa).
- "Vencimiento" sigue automatico (derivado por fecha), por decision.

---

## ESTADO 2026-10-09 DEPLOY 0.0.113 (LEER PRIMERO)

Rama `equipo/alex-header-zonas-equipos` -> mergear a `main` (ya en 0.0.112, HEAD `ff8e7502`).
Version **0.0.113** (desde 0.0.112). **25 commits.** Detalle completo en
**`deploy/DEPLOY_HANDOFF_2026-10-09_v0.0.113.md`** (leer ese). Resumen:

- **Directorio: etiquetas como LISTA DE CATALOGO** (1 MIGRACION `20261008030714_DirectorioEtiquetasListaCatalogo`):
  las etiquetas pasan de FK al catalogo rico `etiquetas_catalogo` a un VALOR de texto respaldado por la lista
  global `directorio.etiqueta` + custom por copropiedad. La migracion AGREGA `valor`, hace BACKFILL desde el
  nombre y luego DROPEA FK/columna `etiqueta_id` (parcialmente destructiva; `etiquetas_catalogo` se conserva).
  `directorio_etiquetas` ya tiene RLS FORCE (sin cambio de RLS). `CatalogoListasSeeder` siembra la lista nueva.
- **Homogenizacion vista-tabla (migration-free):** cierre completo con Unidades como referencia -- guardado
  invisible + quitar cesto, CTA `.row-add--sm` + FAB, columna de seleccion/accion masiva, 1a columna rigurosa
  (checkbox 38px pegado al borde, expander 26x26 icono pelado, padding sel/exp `0 4px`), header 47px, campos
  custom inline en Unidades, codigo de unidad en violeta. Replicado a todos los modulos vista-tabla.
- **Tareas (migration-free):** la vista Tabla ya NO lleva fila de alta predispuesta -> "Agregar tarea" ABRE la
  fila (estilo Tablero); y se quitaron los mini indicadores (KPIs) de Tareas y Tableros.

### 1 MIGRACION NUEVA — parcialmente destructiva — APLICAR EN PROD (owner `propia`, `dotnet ef database update`)
- `20261008030714_DirectorioEtiquetasListaCatalogo` — AGREGA `directorio_etiquetas.valor`, BACKFILL desde
  `etiquetas_catalogo.nombre`, luego DROP FK + indices + columna `etiqueta_id`; nuevo indice unico
  `(vinculo_id, valor)`. **Aplicada y verificada en dev.**
- **ORDEN:** desplegar el codigo 0.0.113 PRIMERO y aplicar la migracion INMEDIATAMENTE despues. Es un rename de
  columna (viejo lee `etiqueta_id`, nuevo lee `valor`): hay una ventana breve de desajuste cualquiera sea el
  orden, pero afecta SOLO a las etiquetas del Directorio; el resto de la app no depende de esta migracion.

### Cache-busting (ya en App.razor): servir
`tabla-base.css?v=7`, `tareas.css?v=20`, `pqrs.css?v=21`, `servicios.css?v=15`, `directorio.css?v=11`,
`usuarios.css?v=21`, `roles.css?v=14`, `porteria.css?v=3`, `propia-ui.js?v=23`.

### Pendiente (NO en este deploy)
Replicar el "boton abre la linea" (alta estilo Tablero) al resto de modulos vista-tabla, para no desalinear el
canon (Tareas es la referencia). Decidir en proxima tanda.

---

## ESTADO 2026-10-07 DEPLOY 0.0.112 (LEER PRIMERO)

Rama `equipo/alex-header-zonas-equipos` -> mergear a `main`. Version **0.0.112** (desde 0.0.111).
Detalle completo en **`deploy/DEPLOY_HANDOFF_2026-10-07_v0.0.112.md`** (leer ese). Resumen:

- **B2b - rol activo por sesion** (SIN migracion): el JWT lleva `rol_id`; el RBAC autoriza con el rol activo;
  `POST /connect/switch-rol`; gate "elige tu rol" para multi-rol. Aditivo (tokens viejos/mono-rol = sin cambio).
- **Unidades: eliminado el concepto Torre** (1 MIGRACION DESTRUCTIVA): la unidad se identifica solo por su
  codigo (`numero`); se quitaron entidad/tabla/columna/FK `Torre`, CRUD `/torres`, tools MCP de torres, y
  `TorreNombre`/`TorreId` de los DTOs. **+ validacion de codigo duplicado** (sin mayus/tildes, con mensaje).
- **Cosmetico**: codigo de unidad en violeta de marca en todos los modulos (CSS/markup, sin migracion).
  Cache-bust en `App.razor`: `pqrs.css?v=17`, `porteria.css?v=2`.

### 1 MIGRACION NUEVA — DESTRUCTIVA — APLICAR EN PROD (owner `propia`, `dotnet ef database update`)
- `20261007214045_RemoveTorreDeUnidades` — **hornea** el codigo en `numero` (`<torreShort>-<numero>`,
  desambigua duplicados por tenant) y luego **DROP** FK + tabla `torres` + columna `torre_id`. Irreversible
  (el Down recrea la estructura vacia pero los codigos ya horneados no se des-hornean).
- **ORDEN CRITICO:** desplegar el codigo 0.0.112 PRIMERO (no consulta `torres`), y recien DESPUES aplicar la
  migracion. Si se aplica antes de que el codigo nuevo este vivo, el codigo viejo (0.0.111) da 500 (consulta
  `torres`/`torre_id`). `database update` aplica tambien `20261007135437` si prod venia por detras de 0.0.111.

---

## ESTADO 2026-10-06 DEPLOY 0.0.103 (LEER PRIMERO)

Rama `equipo/alex-header-zonas-equipos` -> **mergeada a `main`** (HEAD `0d110c5`). Version **0.0.103**.
Este deploy acumula: **catalogo global de listas (Olas 1-4A)**, **alta inline de Unidades completa**, y la
**vista-tabla reordenar/redimensionar columnas** (Contratos + PQRSD). Detalle del catalogo:
`deploy/PLAN_CATALOGOS_SUPER_ADMIN.md`.

### 2 MIGRACIONES NUEVAS — APLICAR EN PROD (owner `propia`, `dotnet ef database update`)
1. `20261006120327_ContratoTipoCategoriaAString` — `contrato_servicios.tipo_contrato` y `.categoria` de
   `integer` (enum) a `text`, mapeando los valores existentes (int -> etiqueta) con un `CASE`. Habilita que
   "Tipo de contrato"/"Categoria" sean listas editables por copropiedad. Rollback real = redeploy anterior
   (su `Down` vuelve a int; texto nuevo sin mapeo quedaria NULL).
2. `20261006164654_AddCatalogoOpcion` — tabla GLOBAL `catalogo_opciones` (sin tenant_id, **sin RLS** a
   proposito: tabla de plataforma; `RlsCoverageTests` solo mira tablas con tenant_id, asi que no aplica) +
   GRANT a `propia_app`. Rollback = redeploy anterior, sin revertir esquema (la tabla queda sin uso).

### CONFIG/ARRANQUE NUEVO — seeder idempotente
`CatalogoListasSeeder.EnsureAsync` corre en CADA arranque (Web y Api), SIEMPRE. Siembra las 12 listas de
fabrica (unidad.tipo/estado, contrato.tipocontrato/categoria, vehiculo.tipovehiculo, mascota.tipo,
equipo.categoria/tipo/estado, zona.categoria/estado, persona.sexo) desde los enums/semillas. Solo inserta
lo que falta; NO pisa lo que A&D edite. No-op tras la primera vez. Va en try/catch: si falla, loguea y NO
bloquea el arranque. **La migracion debe estar aplicada ANTES de arrancar** (si no, loguea warning y al
proximo arranque siembra).

### Que cambia para el usuario
- **Consola A&D -> nueva pagina `/admin/catalogos`** (pestana Plataforma): editar labels/orden/color/activo
  y agregar opciones de las 12 listas, para TODAS las copropiedades, sin tocar codigo. La clave no se edita
  (los datos apuntan a ella); las semilla no se borran, se desactivan.
- Cada modulo (Unidades, Contratos, Vehiculos, Mascotas, Equipos, Zonas, Personas-sexo) **lee sus labels/
  opciones del catalogo**, componiendo el override por copropiedad encima. Sin catalogo, usa el fallback de
  codigo (comportamiento identico al actual), asi que es seguro aun si el seeder no corrio.
- **Alta inline de Unidades** ahora captura estado, habitaciones, banos, parqueaderos, paga admin, cuota,
  observaciones y modulos contributivos (antes "En la ficha"). Sin migracion (el request ya los soportaba).
- **Contratos y PQRSD**: reordenar (arrastrar header) + redimensionar columnas (tirador + doble-clic).

### Pendiente (NO en este deploy): PQRSD-unify (Ola 4 Tranche B) — proxima sesion.

## ESTADO 2026-10-02 ROLES REFINO VISUAL + MENU (LEER PRIMERO)

Rama `equipo/alex-roles-refino` mergeada a `main` (commits `95da282` Roles refino + `43172f3` EditarCampoSistema).
**NO trae migraciones nuevas** (el esquema de Roles ya entro con la migracion de 2026-09-29 OLA A de abajo).
Es UI + un cambio de DATO en la tabla global `menu_overrides`.

**UI (auto-deploy desde main):**
- Roles y Permisos (`RolesPanel.razor`): matriz muestra SOLO acciones validas por modulo (MP-04/MP-06; el
  resto pinta "."); ficha del rol como MODAL (`.usr-modal-roles`); bandeja migrada al frame canonico `.tbl`
  (llena el ancho, header sticky, footer); barra Filtros/Buscar/Ordenar/Agrupar; badge base "Sistema".
- EditarCampoSistema (`EditarCampoSistemaModal.razor` + `DistribucionPanel.razor`): campos de sistema tipo
  lista muestran opciones rigidas (solo lectura) + permiten agregar propias.
- CSS bumps: `usuarios.css?v=9`, `roles.css?v=4`, (App.razor).
- Menu base (`MenuCatalog.cs`): `mi-usuarios` "Usuarios y roles" -> "Usuarios"; nuevo item `mi-roles`
  "Roles" -> `/roles`.

**ACCION MANUAL EN PROD (correr tu, Alex): mover "Roles" a la seccion Configuracion del menu.**
El menu real lo arma `menu_overrides` (tabla GLOBAL, sin tenant_id). En dev ya se aplico. En prod, el
`parent_key` de la seccion "Configuracion" puede tener OTRO id (es un custom-sec-*). Pasos:
1. Ver el id real de la seccion Configuracion en prod:
   `SELECT node_key,label FROM menu_overrides WHERE label ILIKE 'config%' AND is_custom=true;`
2. Con ese `<ID_CONFIG>`, correr (ajusta `sort_order` si choca con otro item de esa seccion):
   ```sql
   BEGIN;
   UPDATE menu_overrides SET label='Usuarios', updated_at=now() WHERE node_key='mi-usuarios';
   UPDATE menu_overrides SET sort_order=6, updated_at=now() WHERE node_key='mi-directorio';
   INSERT INTO menu_overrides (id, node_key, label, parent_key, sort_order, created_at, is_custom, hidden)
   VALUES (gen_random_uuid(), 'mi-roles', NULL, '<ID_CONFIG>', 5, now(), false, false);
   COMMIT;
   ```
   (En dev el id fue `custom-sec-3954c8fdc250481f9197e077ba2c6893` y quedo Usuarios(4) -> Roles(5) -> Terceros(6).)
   Si NO se corre: "Roles" igual funciona y aparece, pero bajo "Mi Copropiedad" (el catalogo base), no en Configuracion.

---

## ESTADO 2026-09-29 MODULO 2.5 v2.0 OLA A (matriz de roles por copropiedad + bandeja - LEER PRIMERO)

Rama `equipo/atlas-usuarios-roles-25`. **Trae UNA migracion nueva ADITIVA que SI hay que aplicar en
prod**: `20260929212521_Modulo25V2_RolCategoria_MatrizPorTenant` (owner `propia`, `dotnet ef database update`).
- Agrega `roles_copropiedad.categoria` (int, default 1 = Administrativo).
- Crea tabla `rol_permisos_tenant` (override de la matriz de permisos POR COPROPIEDAD) con **RLS FORCE +
  policy `tenant_isolation` (USING+WITH CHECK) + GRANT a propia_app**. Fix critico: antes editar la
  matriz de un rol base (global) mutaba la matriz de TODAS las copropiedades; ahora cada una guarda su
  override y lee la matriz efectiva (default global + override del tenant). Cubierta por `RlsCoverageTests`.
- Puramente aditiva y reversible (Down). **NO re-siembra roles**: el re-seed de los 7 roles base + el
  remapeo de usuarios (Propietario/Residente->Portal Residente, Operario->Personal Operativo, etc.) es
  el **D2**, que va en una migracion de DATOS SEPARADA posterior (con OK de Alex antes de aplicar).
- UI: bandeja de Roles nueva en `/roles` (`RolesPanel.razor` + `css/modules/roles.css?v=1`, ya linkeado
  en `App.razor`); el modal viejo de roles en Usuarios se elimino (el boton "Roles y permisos" navega a
  `/roles`). Sin config nueva.

## ESTADO 2026-09-29 MODULO 2.5 v2.0 D2 (re-seed de roles base + remapeo de usuarios)

Rama `equipo/atlas-roles-d2` (sale de Ola A). **Migracion de DATOS** (no toca esquema):
`20260930021045_Modulo25V2_D2_ReseedRolesBase`. Debe correr DESPUES de la migracion de Ola A.
- **Correr como owner `propia`** (`dotnet ef database update`). `propia` es superuser/BYPASSRLS, asi que
  el remap alcanza todas las copropiedades. **NO aplicar como `propia_app`**: con RLS FORCE y sin
  `app.tenant_id` la policy filtraria a 0 filas y el remap se saltaria en silencio.
- Que hace (todo en 1 transaccion, con aserciones `DO` que ABORTAN si queda cualquier huerfano):
  deja **7 roles base** (Administrador, Consejero, Coordinador, Asistente [categoria Administrativo];
  Personal Operativo [ex-Operario, Operativo]; Portal Residente [NUEVO, fusiona Propietario+Residente,
  categoria Portal Residente]; Terceros [NUEVO]); **borra** Propietario, Residente, Inmobiliaria,
  Revisor Fiscal, Vigilante; **remapea** usuarios+invitaciones de Propietario/Residente a Portal
  Residente; Portal Residente HEREDA la matriz de Propietario (10 celdas, decision de Alex); Contador
  pasa a **personalizado** del tenant de su usuario; facetas de siembra Propietario+Residente se
  fusionan por tenant en Portal Residente.
- **Destructiva y NO reversible** (Down = no-op deliberado: el merge Propietario+Residente pierde la
  distincion). **Antes de aplicar en prod: BACKUP.**
- **DEV: APLICADO y verificado 2026-09-29** (7 roles base OK; 244 usuarios en Portal Residente; Contador
  tipo=Personalizado tenant-scoped; matriz Portal Residente=10 celdas; facetas="1,2"; **0 huerfanos** en
  usuarios_tenant / usuario_invitaciones / rol_permisos / rol_permisos_tenant). **PROD: PENDIENTE del
  dale de Alex.**

---

## ESTADO 2026-09-28 TANDA 2 (carga masiva single-tenant + ficha + AGRUPAR - LEER PRIMERO)

Tanda posterior a 0.0.98. Todo esta en `main` (HEAD `0a9e147`). **Es 100% migration-free: NO agrega ni una
migracion vs 0.0.98** (las 3 migraciones aditivas siguen siendo las de la seccion "ESTADO 2026-09-28 (SPLIT
TAREAS)"; si prod ya recibio 0.0.98 no hay nada que aplicar). Cambios (6 commits, `191393c..0a9e147`):

1. **Carga masiva SINGLE-TENANT** (`7e59b65`): la plantilla Excel ya **NO emite la columna COPROPIEDAD** en
   ninguna de las 7 hojas. Cada archivo carga en **una sola copropiedad: la activa**. El importador
   (`UnidadesCargaImportService`) enruta las filas sin COPROPIEDAD a la copropiedad activa con **UPSERT**
   (grupo interno "SoloVacias"); la logica de grupos multi-copropiedad no se toco. La fila de ejemplo se
   marca con el sentinel `EJEMPLO (borrar fila)` en una celda de texto y el importador la ignora en cualquier
   hoja. **COMPAT: los archivos VIEJOS que aun traigan COPROPIEDAD (incl. "Todas las copropiedades") se
   siguen procesando** sin cambios. Migration-free.
2. **Residentes: desplegable de UNIDAD PRIVADA** (`7e59b65`): en las hojas PERSONAS/VEHICULOS/MASCOTAS la
   columna UNIDAD PRIVADA trae un desplegable (`LISTA_UNIDADES`) con los codigos TORRE-NUMERO de las unidades
   ya cargadas. En onboarding (sin unidades) queda como texto libre. Los campos dinamicos siguen saliendo
   como columnas `[Label]` por cada campo VISIBLE de la copropiedad (verificado en runtime). Migration-free.
3. **Ficha de unidad reestilizada** (`6e5b411`, merge de `equipo/atlas-ficha-unidad`): `GestionarUnidadesModal`
   pasa a editor `.cpe-*` de 4 pestanas. **Cache-busting: servir `unidades-modal.css?v=4`** (ya bumpeado en
   `Components/App.razor`). Migration-free.
4. **Descarga de plantilla solo en el hub** (`03e3a4c`): se quito el boton "Descargar plantilla" de la vista
   Unidades standalone (`/distribucion`); la descarga vive UNICAMENTE en los hubs (`HubCargaMasiva`). Se
   conserva "Cargar plantilla" en standalone. Migration-free.
5. **AGRUPAR centralizado** (`a6790d7`, merge de `equipo/atlas-orden-agrupar`): nuevo componente compartido
   **`Components/Shared/TablaAgruparMenu.razor`** (gemelo de `TablaOrdenarMenu`, agrupacion N-niveles, solo
   tokens `--propia-*`), cableado en **Directorio** como modulo de referencia. Sin CSS nuevo (estilos inline
   en el componente). Migration-free. (El rollout al resto de modulos, incluido Unidades, sigue en curso.)
6. **Carga masiva ARRIBA de las pestanas** (`0a9e147`): la tarjeta `HubCargaMasiva` se movio de debajo de
   `.cfg-tabs` a encima (entre titulo y pestanas) en los dos hubs (`ConfiguracionCopropiedad` scope config y
   `ResidentesHub` scope residentes). Solo reorden de markup. Migration-free.

**Verificacion post-deploy TANDA 2:**
- [ ] **Carga masiva:** descargar la plantilla del hub -> **NINGUNA hoja pide COPROPIEDAD**; en Residentes la
      columna UNIDAD PRIVADA trae desplegable con las unidades; los campos dinamicos visibles salen como `[Label]`.
      Importar OK (cae en la copropiedad activa). Reimportar un archivo VIEJO con COPROPIEDAD -> tambien OK.
- [ ] **Hubs:** la tarjeta "Carga masiva por plantilla" (Descargar/Cargar) aparece ARRIBA de las pestanas en
      `/configuracion-copropiedad` y `/residentes-hub`.
- [ ] **Unidades standalone** (`/distribucion`): solo boton "Cargar plantilla" (sin "Descargar").
- [ ] **Ficha de unidad:** abrir una unidad -> editor de 4 pestanas `.cpe-*` con estilos correctos (verificar
      que se sirve `unidades-modal.css?v=4`, sin CSS viejo cacheado).
- [ ] **Agrupar (Directorio):** el boton "Agrupar" abre el menu compartido (`AGRUPAR POR` / `Agregar agrupacion`).

---

## ESTADO 2026-09-28 (SPLIT TAREAS + ORTOGRAFIA A-F - LEER PRIMERO)

Tanda posterior al handoff del 09-24. Todo esta en `main` (HEAD `fbfea4c`). Trae: menu de columna estilo
Airtable en las bandejas, cierre de la reorg de hubs, **descripcion de campo** (1 migracion aditiva),
**confirmacion in-app** (adios `confirm()` nativo), **split del modulo Tareas** (1 migracion aditiva),
**Contratos/Seguros -> tarea al tablero General al vencer**, y la **auditoria ortografica completa (tildes),
Olas A-F**. Las secciones 0-7 de mas abajo siguen vigentes TAL CUAL. **Solo 2 migraciones, ambas aditivas
y sin RLS nueva; el resto es migration-free.**

### CON MIGRACION (3 nuevas vs el handoff 09-24, TODAS ADITIVAS, sin RLS nueva)
El `ef database update` (seccion 2) aplica solo lo que falte. Vs `20260924143830_AddUsuarioCampos`:
1. **`20260926023023_AddDescripcionCamposDefinicion`** - agrega `descripcion text NULL` a las 7 tablas
   `*_campos_definiciones` (unidad/persona/vehiculo/mascota/tercero/zona/usuario). Solo columnas; no toca
   RLS (esas tablas ya la tienen). `Down()` dropea las columnas.
2. **`20260927131731_AddTableroEsGeneral`** - agrega `es_general boolean NOT NULL default false` a
   `tableros`. Marca el tablero "General" unico del nuevo modulo Tareas. Aditiva; `Down()` dropea la columna.
3. **`20260928161525_AddDescripcionCampoConfig`** - agrega `descripcion text NULL` a `unidad_campos_config`
   (config de campos de SISTEMA por copropiedad; la usa el nuevo editor focalizado "Editar campo" para
   guardar la descripcion de un campo fijo). Aditiva; `Down()` dropea la columna.

Ninguna crea tablas -> **no suma a las 8 tablas sin RLS conocidas** (seccion 6); `RlsCoverageTests` sigue
igual. Comprobacion:
```sql
select count(*) from information_schema.columns
 where table_name like '%_campos_definiciones' and column_name='descripcion';   -- debe dar 7
select column_name from information_schema.columns
 where table_name='tableros' and column_name='es_general';                      -- debe existir
select column_name from information_schema.columns
 where table_name='unidad_campos_config' and column_name='descripcion';         -- debe existir
```

### "Editar campo" en columnas de sistema = editor focalizado (UI, migration 3 arriba)
Antes, "Editar campo" del menu de columna solo salia en campos propios; sobre un campo fijo no hacia nada
util. Ahora sale siempre: en campo propio abre el editor del campo; en campo de SISTEMA abre un editor
focalizado (alias/descripcion/visibilidad; el tipo es fijo). Aplica a Distribucion, Residentes, Mascotas,
Vehiculos, Zonas, Equipos y Directorio, y tambien a PQRSD, Seguros y Servicios. Tambien se homogenizo el
boton "+ Agregar registro" en Directorio y Usuarios. Todo migration-free salvo la columna `descripcion`
(migracion 3).

### Fix ancho de tabla en Directorio y Usuarios (UI, migration-free)
Esas 2 tablas quedaban mas angostas que las demas por un contenedor de pagina con padding horizontal
extra (`.dir-page` 34px, `.usr-wrap` 28px) que Distribucion/Mascotas no tienen. Se quito ese padding para
que usen el ancho total. **Cache-busting:** el deploy debe servir `directorio.css?v=4` y `usuarios.css?v=6`
(ya bumpeados en `Components/App.razor`).

### CopropiedadSwitcher milimetrico (UI, migration-free)
Integrado por fast-forward de `equipo/atlas-copropiedad-switcher` (rama de ATLAS, re-revisada por VIGIA):
el selector de copropiedad (modal galeria/tabla + creacion en formulario unico) calca la maqueta
`modal-copropiedad.html` con tokens claro/oscuro. **1 solo archivo** (`CopropiedadSwitcher.razor`), SIN
backend, SIN migracion. Decision de producto: se muestran las imagenes reales del logo (sin fallback de
avatar). Verificado en runtime lado a lado con la maqueta.

### SPLIT DEL MODULO TAREAS (cambio de comportamiento - leer)
- **`/tareas`** ahora es el **tablero General unico**: siempre abre listo para recibir actividades y trae la
  columna **ORIGEN** (que modulo emitio la tarea: PQRSD, Contratos, Seguros, Mantenimiento, o "Propia" si es
  manual). El **selector de tablero destino de PQRSD desaparecio**: toda tarea de modulo cae en el General
  marcada con su Origen (inmutable, lo fija el modulo emisor).
- **`/tableros`** es el **modulo nuevo de proyectos** (los tableros multi-columna de antes), EXCLUYE el
  General.
- **Migracion de datos (lazy, sin script):** al abrir Tareas por primera vez tras el deploy, el tablero
  default existente se marca `es_general=true` (get-or-create idempotente en `AsegurarTableroGeneralAsync`).
  Las tareas historicas se quedan donde estan (Origen = Propia); solo las nuevas de modulos caen al General.
- **MENU (ver 0.7): es DATA.** El codigo agrega al catalogo los items **"Tareas"** (`/tareas`) y **"Tableros"**
  (`/tableros`) bajo Operacion; pero la ORGANIZACION del menu vive en `menu_overrides`. Reimportar
  `menu-propia.json` en Super Admin si se quiere la organizacion afinada (sin el import, ambos son
  alcanzables por su ruta desde el catalogo del codigo).

### CONTRATOS/SEGUROS -> TAREA AL GENERAL (migration-free)
- El job `ContratosVencimientoJob` (2 corridas/dia), ademas de la alerta de vencimiento, ahora **emite UNA
  tarea de seguimiento al tablero General** cuando un contrato/poliza entra en "por vencer": Origen =
  **CONTRATOS** / **SEGUROS**. Idempotente (no duplica si ya hay una tarea no eliminada para esa entidad).
- **DE PASO SE ARREGLO** el bug preexistente que estaba en la seccion 6 de este handoff: el job hacia
  `if (contratos.Count == 0) continue;` y **nunca alertaba polizas** en copropiedades sin contratos con
  fecha fin. Ahora recorre ambos. (El `catch` por tenant ya logueaba desde antes.)

### ORTOGRAFIA (tildes) - Olas A-F, con BACKFILLS SELF-HEALING (sin paso manual)
Se corrigieron tildes en UI, catalogos y plantillas. Lo relevante para deploy:
- **Backfills idempotentes por tenant (NO son migracion, NO hay script manual):** los catalogos que ya
  estaban sembrados en ASCII se renombran solos la primera vez que cada tenant abre el modulo
  (`ExecuteUpdateAsync` con `WHERE nombre_viejo`, tenant-scoped; tras el rename es UPDATE de 0 filas).
  Cubre: estado PQRSD "En gestion"->"En gestion" con tilde; estados de Cartera (Notificacion/Pre-juridico/
  Juridico); rubros de Presupuesto; TRD de Documentos (series/subseries/tipologias); cargo EquipoOrg
  "Asistente de Facturacion". **Respetan ediciones del usuario** (solo tocan la fila que conserva el ASCII
  por defecto). No hay nada que ejecutar a mano.
- **Carga masiva ahora TOLERA acentos:** los encabezados de la plantilla pasaron a llevar tilde
  (TELEFONO->con tilde, AREA, MATRICULA, etc.), pero el importador (`UnidadesCargaImportService`) compara
  insensible a acentos, asi que **un archivo viejo con encabezados en ASCII sigue importando igual**. No
  rompe plantillas guardadas por los clientes.
- **Carga masiva ahora es single-tenant (0.0.98):** la plantilla ya no pide COPROPIEDAD; todo carga en la
  copropiedad activa. Los archivos viejos con COPROPIEDAD se siguen procesando (ver 0.2). Residentes gana
  desplegable de UNIDAD PRIVADA con las unidades ya cargadas.
- El resto (labels/placeholders/`.razor`, mensajes de toast, `<option>`) es texto visible, migration-free.

### Verificacion post-deploy especifica de esta tanda
- [ ] **Migraciones:** correr los 2 `select` de arriba (descripcion x7, es_general en tableros).
- [ ] **Tareas:** `/tareas` abre el General con columna **ORIGEN**; generar un PQRSD nuevo -> aparece una
      tarea en el General con Origen **PQRSD**. `/tableros` lista proyectos SIN el General.
- [ ] **Contratos/Seguros:** (si hay datos por vencer) tras la corrida del job, el General muestra tareas con
      Origen Contratos/Seguros. Una copropiedad SIN contratos ya recibe alertas de **polizas** (bug 09-24
      resuelto).
- [ ] **Ortografia:** kanban PQRSD muestra la columna **"En gestion" con tilde**; Cartera/Presupuesto/TRD de
      Documentos con tildes; formularios y menus con tildes, sin caracteres rotos (mojibake).
- [ ] **Carga masiva (single-tenant + compat):** descargar la plantilla -> **NINGUNA hoja pide COPROPIEDAD**;
      en Residentes la columna UNIDAD PRIVADA trae desplegable con las unidades cargadas. Importar OK (cae en
      la copropiedad activa). **Compat:** reimportar un archivo VIEJO con COPROPIEDAD (y/o encabezados ASCII)
      -> tambien importa sin errores.
- [ ] **Menu:** reimportar `menu-propia.json` si se quiere Tareas/Tableros en su ubicacion afinada (ver 0.7).

---

## ESTADO 2026-09-24 (VISTA TABLA + CAMPOS EN USUARIOS - LEER PRIMERO)

Esta tanda cierra la homogeneizacion de la **vista tabla** en los modulos de administracion y agrega
**campos dinamicos a Usuarios**. Las secciones 0-7 de abajo siguen vigentes TAL CUAL. **Casi todo es
migration-free y ya esta en `main`; lo unico con migracion es Usuarios (1 migracion aditiva).**

### YA EN `main` (migration-free, reusa columnas/config existentes)
Commits `0ba2cc0` (paquete vista tabla) y `63aa46e` (correcciones de auditoria):
- **Vista tabla estandar completa** en Residentes, Mascotas, Vehiculos, Usuarios y Directorio
  (Personas/Empresas): seleccion + accion masiva, alta inline, menu **Columnas**, ordenar, **arrastrar/
  mover columnas** (pointer-drag JS, no DnD nativo), **ancho + autoajuste**, menu **clic-derecho** (sin
  "Duplicar"), footer `TablaPager`. Componentes compartidos nuevos: `CrearCampoModal`, `TablaCtxMenu`.
- **Directorio - columnas dinamicas**: reusa el subsistema `personas` (compartido con Residentes); el
  valor vive sobre el **vinculo activo principal** de la persona/empresa (coincide con Residentes). Borrado
  masivo = **inactivar vinculo** (se expuso `VinculoId` en los DTOs). Personas y Empresas comparten el
  mismo set de campos.
- **Formula/tipos avanzados habilitados en Zonas y Equipos** (antes inalcanzables): su `ConfigCamposEntidad`
  ahora pasa `TiposAvanzados="true"`. Reusa el EAV existente (`zona_campos_*` / `equipo_campos_*`). **Sin
  migracion.**
- **Usuarios - acciones de la vista tabla**: revocar masivo + **columnas configurables server-side**. Esto
  reusa `unidad_campos_config` con `entidad='usuario'`: se **amplio la whitelist** `EntidadesCampoConfig`
  en codigo (aditivo, **sin migracion**; la tabla ya es generica Entidad+CampoClave).
- **Limpieza CSS**: se eliminaron assets muertos y ausentes en la maqueta (`wwwroot/lib/` bootstrap por
  defecto, `assets/libs/fontawesome`, `assets/libs/lucide`, `assets/css/styles-rtl.css`). **Cache-busting:
  el deploy debe servir `usuarios.css?v=3` y `propia-ui.js?v=14`** (ya bumpeados en `Components/App.razor`).
- **Correcciones de auditoria vs maqueta**: valor CSS malformado en Distribucion (`.cg-summary.warn`),
  backdrop del modal y menu contextual 1:1 con la maqueta, inputs de alta punteados en Mascotas/Vehiculos.
- Verificado en runtime (5105) sobre la copropiedad demo; datos de prueba borrados. Build 0 errores.

### CON MIGRACION - Usuarios campos dinamicos (NUEVO, 1 migracion aditiva)
> **Estado:** codigo completo y compilando (0 errores), **commiteado y en el lote de este deploy (0.0.96)**.
> AVISO IMPORTANTE: la migracion **no se pudo aplicar en dev** (el modo automatico bloqueo la BD compartida),
> asi que **el deploy es la primera vez que corre `AddUsuarioCampos` y la primera vez que se ejercita el EAV
> de Usuarios en runtime**. Por eso la verificacion de humo de Usuarios (crear campo + editar valor + F5)
> es **paso #1 obligatorio post-deploy**; si algo falla, el `Down()` de la migracion dropea solo esas 2
> tablas y no afecta nada mas (ver seccion Rollback).

- **Que agrega**: paridad total de la vista tabla en Usuarios: crear campo ("+"/modal), gestor de campos,
  **Formula/tipos avanzados**, y **celda editable por tipo**. Unico ausente (como en todos): "Duplicar".
- **Migracion `20260924143830_AddUsuarioCampos`** (aditiva): 2 tablas nuevas
  `usuario_campos_definiciones` y `usuario_campos_valores`, **cada una con ENABLE + FORCE ROW LEVEL
  SECURITY + policy `tenant_isolation` + GRANT a `propia_app`** (patron identico a Equipo/Zona). No toca
  ninguna tabla existente. El registro del valor es el **`usuarios_tenant.id`** (membresia del usuario en
  la copropiedad), no el usuario global.
- **Endpoints nuevos** (en `MiCopropiedadController`, gateados con `MI_COPROPIEDAD`): `usuarios-campos`
  (GET/POST/PUT/DELETE) y `usuarios-campos-valores` (GET, PUT `/{registroId}/{definicionId}`).
- **Sin pasos manuales de datos**: los campos nacen vacios; cada copropiedad crea los suyos (igual que en
  los demas modulos). El gestor de Usuarios usa `SinConfigSistema` para NO chocar con el menu Columnas.

### Migraciones de esta tanda (2 nuevas vs origin/main 025676b)
El `ef database update` de la seccion 2 aplica solo lo que falte (compara `__EFMigrationsHistory`). Vs el
punto de prod (`025676b` = 0.0.95) quedan **2 aditivas**:
1. `20260915135206_EquipoAtlas_AddTableroCamposConfig` - viene con la integracion de atlas-tareas (estaba
   en rama en el handoff del 09-15, ahora integrada a main). Tabla `tablero_campos_config`.
2. `20260924143830_AddUsuarioCampos` - las 2 tablas de campos de Usuarios (ver arriba), con RLS.
- Verificacion rapida de que quedaron aplicadas:
```sql
select relname, relrowsecurity, relforcerowsecurity
from pg_class where relname in ('usuario_campos_definiciones','usuario_campos_valores','tablero_campos_config');
-- las 3 deben existir; las de usuario_* con relrowsecurity=t y relforcerowsecurity=t
```
- `RlsCoverageTests`: las 2 tablas nuevas ya llevan RLS con el patron estandar, asi que **no** deben sumar
  a las 8 tablas sin RLS conocidas (seccion 6). Correr la suite de integracion para reconfirmar.

### Verificacion post-deploy especifica de esta tanda
- [ ] **Usuarios (tras aplicar la migracion):** en `/usuarios` (vista Tabla) el boton **Campos** abre el
      gestor; el "+" crea una columna; crear un campo de cada tipo + una **Formula** y editar un valor sobre
      un usuario; **F5 persiste** (valor guardado sobre `usuarios_tenant.id`). Sin la migracion, "Configurar
      campos" dara 500 `column/table does not exist`.
- [ ] **Zonas y Equipos:** el selector de tipo del gestor ya lista **Formula/Usuario/Directorio**.
- [ ] **Directorio:** columnas dinamicas visibles en Personas y Empresas; el valor coincide con Residentes
      para la unidad principal; "Quitar del directorio" (masivo) inactiva el vinculo sin borrar la persona.
- [ ] **CSS:** con `?v=3`/`?v=14` servidos, iconos (flaticon) intactos tras quitar fontawesome/lucide.

---

## ESTADO 2026-09-15 (POST-INTEGRACION B - LEER PRIMERO)

**Desplegable HOY = `origin/main` @ `40ab5da`, version `0.0.95`.** Se integro el paquete B
(`feature/hub-config`) a main. Las secciones 0-7 de abajo siguen vigentes TAL CUAL (migraciones, config,
permisos, RLS); esta seccion dice QUE sumo B y que sigue faltando. **B es 100% migration-free.**

### Lo que ENTRO en main con B (todo SIN migracion, reusa columnas/config existentes)
- **Hub "Configuracion Copropiedad"** (`/configuracion-copropiedad`): 9 pestañas (General, Unidades,
  Residentes, Mascotas, Vehiculos, Zonas, Equipos, Usuarios, Directorio) que reusan cada modulo COMPLETO
  via patron *Panel + parametro Embedded. Item de sidebar nuevo (MenuCatalog, en codigo). Rutas sueltas
  y accesos previos INTACTOS. Verificado runtime (las 9 pestañas rinden el modulo completo).
- **Fase 2 replicada a Vehiculos, Mascotas y Residentes**: los 3 tipos (Formula/Usuario/Directorio) ahora
  tambien en esos modulos (antes solo en Unidades). Reusan los catalogos EAV existentes (unidad_placas /
  unidad_mascotas / unidad_personas + sus campos). Residentes ademas: toggle Tabla/Tarjetas.
- **Gestor de campos (ConfigCamposEntidad, componente COMPARTIDO -> lo heredan las 9 superficies):**
  - **Formula MULTI-PASO**: la formula pasa de 1 operacion a una lista de PASOS (op + operandos: campo
    Numero/Moneda, constante, o resultado de un paso anterior) + operaciones binarias Resta/Division/
    Multiplicacion. Retrocompatible: las formulas de 1-op ya guardadas computan IDENTICO. Test de
    regresion 11/11 verde. Verificado runtime (formula de 2 pasos = Coef-10, F5 persiste).
  - **Selector de tipo amigable**: dropdown con categorias + descripcion por tipo.
  - **Crear == Editar**: al editar, el tipo se agrupa igual que al crear; los avanzados salen bloqueados
    (candado + tooltip) porque no se convierten sin recrear.
- **Fix del modal del gestor**: el CSS `.cg-*` del modal se movio a `config-campos.css` (global) para que
  el gestor FLOTE en cualquier superficie/pestaña (antes vivia inline en Distribucion y caia inline fuera).
- **Deploy docs** (este handoff + checklists) actualizados.

Pendiente #4 (ortografia de nombres/valores) DIFERIDO por decision de Alex (revision por-modulo despues).

### Migraciones: SIN NOVEDAD (B no agrega ninguna)
La ultima migracion en `main` sigue siendo `20260913005909_AddCostoEstimadoProgramacionTarea`. B es
migration-free. El set pendiente vs prod 0.0.67 es EXACTAMENTE el de la **seccion 2** (9 aditivas). Si
prod ya recibio 0.0.93/0.0.94, **no queda ninguna migracion pendiente** y 0.0.95 seria solo-codigo.
(Nota: entre 0.0.93 y 0.0.95 hubo un bump intermedio 0.0.94 "para deploy"; ni ese ni B tocaron el esquema.)

### CRITICO - LO QUE **NO** ESTA EN MAIN (aun en ramas)
- **Fase 2 en Seguros/Contratos (SELLO)** y **Mantenimiento (YUNQUE)** - en curso; rebasean sobre este
  main (heredan el gestor corregido) y luego se integran. Migration-free (reusan el componente).
- **Ronda de ajustes de homogeneidad**: ATLAS (Tareas, orden + T-11 + T-09) **CON migracion
  `tablero_campos_config`** (rama `equipo/atlas-tareas`, no aplicada), FARO (PQRSD), YUNQUE (color/forma).
- **Directorio / Usuarios con campos propios (EAV nuevo, CON migracion)** - solo planificado, sin codigo.

### Verificacion de lo que SI va (B)
- Build Release del Web sobre el main mergeado: **0 errores**.
- Runtime (5105): hub 9 pestañas OK; Fase 2 en Veh/Mas/Residentes OK (columnas, captura, F5, cross-tenant);
  formula multi-paso OK (2 pasos, computo correcto, F5); modal del gestor FLOTA en todas. Datos de prueba borrados.
- Tests: gestor de formula 11/11 (unit); Fase 2 integracion (Unidades 7/7, Veh/Mas 10/10, Residentes 5/5 Release).
- Baseline de integracion: **224 verdes / 8-9 rojos PREEXISTENTES** (deuda conocida). Correr `dotnet test`
  en el commit exacto antes de desplegar para reconfirmar.

**Paso post-deploy del MENU (ver 0.7): el hub agrega el item "Configuracion Copropiedad" desde el catalogo
del CODIGO (no requiere import), pero la ORGANIZACION del menu sigue siendo data (menu_overrides). Importar
el JSON como siempre si se quiere la organizacion afinada.**

---

## 0. LO CRITICO (leer antes de aplicar)

### 0.1 Hay una migracion de SEGURIDAD

Un test nuevo descubrio que **26 de las 230 tablas con `tenant_id` no tenian RLS EN ABSOLUTO**
(`relrowsecurity = false` y cero politicas) - no que les faltara el FORCE. El `HasQueryFilter` de EF las
seguia filtrando, asi que **no habia fuga por la via normal de la aplicacion**, pero les faltaba la red de
seguridad final: una consulta cruda que olvidara el filtro no tenia nada que la detuviera. Las mas
sensibles eran **Directorio** y **Seguros**.

`AddRlsTablasFaltantes` protege las **18 que son TenantEntity**.

**Riesgo revisado antes de aplicarla** (FORCE RLS ata tambien al dueno de la tabla, y un lector sin tenant
de sesion pasaria de "ve todo" a "ve cero filas" **en silencio, sin error**): ninguna de las 18 se lee con
`IgnoreQueryFilters`, SQL crudo ni Dapper; ningun seeder ni controller de SuperAdmin las toca; y los dos
caminos que corren fuera de un request normal fijan el tenant antes de leer. En dev quedo verificado en el
navegador y la suite de integracion no tuvo regresiones.

**Aun asi, la verificacion #1 post-deploy es entrar a Directorio, Seguros y Contratos y confirmar que
listan datos.** Si alguno queda vacio, revertir con el `Down()` de esa migracion.

**Regla nueva permanente:** cualquier migracion futura que rellene datos en esas 18 tablas debe fijar
`app.tenant_id` primero, o vera cero filas.

### 0.2 Cambio de comportamiento en la plantilla de carga

La hoja UNIDADES ahora emite **solo las columnas de los campos VISIBLES**. Si una copropiedad oculta un
campo en "Configurar", ese campo **deja de salir en la plantilla**. Es lo pedido, pero si alguien reporta
que "desaparecio una columna de la plantilla", la causa es esa.

`UNIDAD PRIVADA` y `PRINCIPAL` estan blindadas y salen siempre.

**Cambio 0.0.98: la plantilla ya NO trae la columna `COPROPIEDAD`** (en ninguna de las 7 hojas). Cada
archivo carga en **UNA sola copropiedad: la activa**. El importador enruta las filas (sin COPROPIEDAD) a la
copropiedad activa con **UPSERT** (grupo interno "SoloVacias"); la logica de grupos multi-copropiedad no se
toco, solo se retiro la columna del generador. **Compat: los archivos VIEJOS que aun traigan COPROPIEDAD se
siguen procesando** (el importador reconoce la columna y el valor "Todas las copropiedades"). La fila de
ejemplo se marca con el sentinel `EJEMPLO (borrar fila)` en una celda de texto (UNIDAD PRIVADA / NOMBRE /
PLACA) y el importador la ignora en cualquier hoja.

**Plantilla de Residentes: la columna `UNIDAD PRIVADA` de PERSONAS/VEHICULOS/MASCOTAS trae un desplegable**
con los codigos de las unidades ya cargadas (TORRE-NUMERO, o Numero suelto). En onboarding (aun sin unidades)
la columna queda como texto libre y se referencia contra la hoja UNIDADES del mismo archivo.

### 0.3 NO hay que crear ni confirmar ningun campo en prod

Los **5 modulos contributivos** son **columnas del sistema**: aparecen solos en todas las copropiedades en
cuanto corre la migracion. **No hay seed, ni script, ni paso manual de creacion.**

Lo unico manual es que **nacen ocultos**: cada copropiedad que los quiera usar los activa en
**Unidades > Configurar** con el icono del ojo. Igual para las 4 pestanas nuevas: los campos del SISTEMA
de Personas/Vehiculos/Mascotas/Terceros salen solos; los campos PROPIOS los crea cada copropiedad.

Consecuencia practica: **tras el deploy nadie vera nada nuevo en la tabla de unidades hasta que active los
campos.** Eso es lo esperado, no un fallo.

### 0.4 PERMISOS: quien puede usar el gestor de campos

**Esto ya mordio en produccion** (403 al activar un campo). Las escrituras de "Configurar" exigen
`MI_COPROPIEDAD / Editar`, con bypass solo si el rol es **EXACTAMENTE** `Administrador`. Y la matriz
`rol_permisos` esta sembrada **escasa**:

| Rol | Permisos MI_COPROPIEDAD habilitados |
|---|---|
| **Administrador** | 6 |
| Consejero, Propietario | 1 (solo ver) |
| Asistente, Contador, Coordinador, Operario, Residente, Vigilante, Inmobiliaria, Revisor Fiscal | **0** |

O sea: **un 403 al activar un campo es lo ESPERADO para casi cualquier rol**, no un bug.

**Caso traicionero:** un Administrador de la copropiedad A que **cambia a la copropiedad B sin tener
vinculo alli** pierde el bypass (`GetRolActorAsync` devuelve null) y todo escribir da 403, aunque la
cabecera le siga mostrando "Admin". Comprobar:

```sql
select ut.rol, ut.estado, t.nombre
from usuarios_tenant ut
  join tenants t on t.id = ut.tenant_id
  join personas p on p.id = ut.persona_id
where lower(p.email) = 'EMAIL_DEL_USUARIO';
```

Si para esa copropiedad no hay fila, o el rol no es exactamente `Administrador`, ahi esta la causa.

**Decision pendiente:** si se espera que un Coordinador o un Asistente configure campos, hay que
habilitarles `MI_COPROPIEDAD / Editar` en la matriz por defecto. Hoy no pueden.

### 0.7 EL MENU LATERAL ES DATA, NO CODIGO (paso manual post-deploy)

La reorganizacion del menu (grupo **Configuracion** con Unidades Privadas, Residentes, Mascotas,
Vehiculos, Equipos y Zonas, Usuarios, Directorio, Lineas WhatsApp, Lista negra; y **Mi Copropiedad**
reducido a "Mi copropiedad") **NO viaja por git**. Vive en la tabla global `menu_overrides` y se
gestiona desde **Super Admin > Configuracion de menu**. Un `git push` NO la lleva a prod.

**Paso post-deploy (una sola vez):**
1. Desplegar el codigo PRIMERO (para que existan en el catalogo las rutas nuevas `/vehiculos` y
   `/mascotas`; si se importa antes, esas dos entradas se descartan en silencio al guardar).
2. En prod: **Super Admin > Configuracion de menu > Importar JSON** (el `menu-propia.json` que exporta
   Alex desde dev) **> Guardar cambios**.
3. El import **reemplaza** TODO el menu de prod (borra los overrides viejos y reinserta los del JSON):
   asi desaparece el "Residentes" dummy (`/proximamente`) y queda el modulo real, sin pasos extra.

**Sin este import, el deploy NO falla:** los modulos `/vehiculos` y `/mascotas` son alcanzables igual
(salen en su ubicacion por defecto, Mi Copropiedad, desde el catalogo del codigo). Lo que el import
cambia es solo la ORGANIZACION del menu. El cache del menu es en memoria (TTL 10 min) y se invalida
solo al Guardar, asi que el cambio se ve al instante tras importar.

### 0.6 IMAGENES: las de las copropiedades son URLs EXTERNAS

En prod las imagenes de copropiedad estaban guardadas como rutas del almacenamiento LOCAL
(`/uploads/tenants/...`). En Railway el disco es **efimero**: se borra en cada redeploy, asi que daban 404
y se veian rotas. Se limpiaron esas 8 URLs muertas (4 logos + 3 fachadas + una cadena vacia) y se pusieron
**URLs externas** (Unsplash, licencia libre) que no dependen del disco y sobreviven a los redeploys.

**Para que se vean hace falta el fix de 0.0.92** (ver seccion 1): hasta ese deploy siguen rotas, porque el
bug estaba en el codigo, no en el dato.

**Pendiente de infraestructura:** confirmar `Storage__Provider=R2` y las `R2__*` en Railway. Mientras no
este, **cualquier imagen que suba un usuario por la app se volvera a perder en el siguiente deploy**.

```bash
railway variables | grep -iE "Storage__Provider|R2__"
```

Nota: las fotos actuales son de archivo, genericas. Conviene reemplazarlas por fotos reales de cada
copropiedad cuando se tengan.

### 0.5 Si "Configurar" parece no responder

Desde 0.0.91 el panel **avisa y dice la causa**:
- **500** con `column ... does not exist` -> **faltan migraciones** (seccion 2).
- **403** -> **permisos** (seccion 0.4). El mensaje distingue si es el rol, el permiso, o que el usuario
  no esta vinculado a esa copropiedad.

(Hasta 0.0.89 se quedaba mudo: un entorno sin migrar o sin permisos era indistinguible de un boton roto.
Ese fue justamente el sintoma que costo diagnosticar.)

---

## 1. Que se despliega (0.0.68 -> 0.0.93, acumulado sobre prod 0.0.67)

- **0.0.93 - Vehiculos y Mascotas son modulos propios; el panel Campos de Unidades queda solo con la
  unidad; y llega el lote del equipo (Tareas + Mantenimiento).** Detalle:
  - **Vehiculos (`/vehiculos`) y Mascotas (`/mascotas`)** salen de la ficha de la unidad a modulos
    independientes con la vista tabla estandar y su propio panel de Campos. **Sin migracion**: reusan
    `unidad_placas` y `unidad_mascotas` y sus catalogos de campos, que ya existian. Backend nuevo: GET
    agregado por copropiedad y **PUT** de placa/mascota (antes solo habia alta y baja; la edicion en
    linea los necesita), gateado con `MI_COPROPIEDAD/Editar`.
  - **Panel "Campos" de Unidades = solo la unidad.** Se unifico la entrada (se fue el boton "Configurar"
    duplicado) y salieron las pestanas Personas/Vehiculos/Mascotas/Terceros. Personas se configura en su
    modulo (`/residentes`); Vehiculos/Mascotas en los suyos. Terceros configuraba las empleadas de la
    unidad (no el Directorio) y quedaba confuso: se quito.
  - **Carga por Excel (afinada sobre 0.0.88):** un campo VISIBLE ahora si sale como columna en la
    plantilla y se importa (faltaban 9 de sistema); TIPO propio de la copropiedad se ofrece y se importa,
    y ya no se reescribe a "Apartamento" en silencio; las listas de los desplegables pasaron a una hoja
    oculta (sin tope de longitud). **Sin migracion.**
  - **Lote del equipo (con migracion, ver seccion 2):** Tareas T-01..T-05 (permisos en 41 endpoints,
    validacion de pertenencia al tenant, un solo camino de cambio de estado, aislamiento), permiso
    Operario-crear-tareas, e indices `tenant_id` en las tablas de tableros; Mantenimiento M-01 (job
    diario del preventivo, idempotente, sin columna nueva).
  - **Menu lateral reorganizado -> es DATA, ver 0.7.** No viaja por git; se importa en Super Admin.
- **0.0.92 - Una imagen EXTERNA ya no se rompe al resolver su URL.** Ninguno de los dos proveedores de
  almacenamiento podia guardar una imagen alojada fuera de la plataforma: `ResolveUrl` asumia que toda URL
  absoluta era un blob propio con host viejo y la reescribia (Local le arrancaba el host y devolvia solo el
  path; R2 colgaba el path de su propio endpoint). En ambos casos -> 404 e imagen rota. Ahora solo se
  reescribe lo propio (path con `/uploads/`, o host del bucket/dominio publico en R2); el resto queda
  intacto. **Es el fix que hace visibles las imagenes de copropiedad (ver 0.6).** Solo codigo.
- **0.0.91 - El 403 de Configurar ya dice por que y que hacer.** El backend distingue la causa en `reason`
  (`permiso_insuficiente`, `rol_insuficiente`, `sin_persona`) y no se estaba leyendo. Ver 0.4 y 0.5.
- **0.0.90 - Un guardado fallido de configuracion ya no se queda mudo.** Ver 0.5.
- **0.0.89 - 5 modulos contributivos + plantilla filtrada por visibilidad.**
  `unidades_privadas` +`modulo_contributivo_1..5` `numeric(7,4)` NULL (misma precision que
  `coeficiente_propiedad`). En el gestor de campos salen debajo de Coeficiente y **ocultos por defecto**.
  El importador los lee; celda vacia no pisa el valor existente. Ver 0.2 para la plantilla.
  **Sin validacion de suma al 100%** -> seccion 6.
- **0.0.88 - Carga por Excel: los campos dinamicos ya se guardan.** La plantilla emitia las columnas
  `[Label]` pero **el importador no las leia**: el dato se descartaba sin un solo error. Ahora se guardan
  en las 6 hojas con catalogo (UNIDADES, PERSONAS, VEHICULOS, MASCOTAS, ZONAS COMUNES, EQUIPOS), al crear
  y al actualizar. Una columna `[X]` desconocida avisa una vez por hoja.
- **0.0.87/0.0.86 - SEGURIDAD: RLS en 18 tablas.** Ver 0.1.
- **0.0.85/0.0.84 - Gestor de campos Fase 3 + listas con semilla.** Las 4 pestanas del panel "Configurar"
  (Personas, Vehiculos, Mascotas, Terceros) ya son funcionales: campos propios por entidad + los campos
  del sistema de cada ficha con alias, visibilidad, ubicacion y -donde la columna real lo permite- tipo y
  formato. **La seccion TERCEROS no existia en la ficha** (el backend estaba completo pero no se
  renderizaba en ninguna parte): se le construyo pestana y seccion. Los tres estados de fabrica ya son
  semilla de verdad (candado, solo se ocultan), igual que los tipos base. La lista se administra en un
  popup. Al ocultar una opcion, las unidades que ya la usan la conservan. Migraciones
  `AddCamposEntidadesVinculadasUnidad` (8 tablas con su RLS) y `AddEntidadAUnidadCampoConfig`.
- **0.0.87 - Etapa B:** tipo de dato cambiable (solo entre tipos compatibles con la columna real) y
  formato (decimales, separador de miles, moneda, maximo de caracteres).
- **0.0.86 - Todos los campos como columnas + drag & drop.** La "ubicacion" del panel ES el orden de la
  tabla. Orden y visibilidad pasan de `localStorage` por usuario a configuracion **por copropiedad**.
- **0.0.83 - Carga de unidades: anexos idempotentes.**
- **0.0.82 - Configurar unidades = administrador de campos.** Migracion `AddUnidadCampoConfig`.
- **0.0.81 - Tipos de unidad configurables** (+tipo base "Cajeros"). Migracion `AddUnidadTipoCustom`.
- **0.0.74-0.0.80 - Modulo Mantenimiento reelaborado** (Programacion en tabla con alta inline, Tercero +
  Contrato opcionales, sin Cron, Calendario Mes/Semana, fix del flotante del SelectorPersona por CSP).
- **0.0.69/0.0.71 - Campos dinamicos tipados en Zonas y Equipos.**
- **0.0.68 - Unidades: campos dinamicos como columnas + Estado dropdown.**
- **0.0.64 - PQRSD alertas de plazo.**
- Incluye lo de 0.0.63-0.0.67 que no haya llegado a prod (Contratos IA, hotfix 403 OCR, etc.).

## 2. Migraciones a aplicar (17 pendientes vs prod, todas ADITIVAS)

```bash
cd src/Propia.Api
dotnet ef database update --project ../Propia.Infrastructure --startup-project .
```

`ef database update` aplica SOLO las que falten (compara `__EFMigrationsHistory`). Las **9 nuevas de esta
tanda**, en orden:

1. `20260910005638_AddUnidadCampoConfig` - tabla nueva `unidad_campos_config` con RLS FORCE + policy + GRANT.
2. `20260910112409_AddUnidadCampoConfigTipoFormatoOrden` - +`tipo` int null, +`formato` text null,
   +`oculto` bool default false, +`orden` int null.
3. `20260910120849_AddCamposEntidadesVinculadasUnidad` - **8 tablas nuevas** (persona/vehiculo/mascota/
   tercero x definiciones+valores), **cada una con ENABLE + FORCE + policy tenant_isolation + GRANT**.
4. `20260910132959_AddEntidadAUnidadCampoConfig` - +`entidad` varchar(20) NOT NULL default `'unidad'`;
   DROP del unico `(tenant_id, campo_clave)` y CREATE de `(tenant_id, entidad, campo_clave)`.
5. `20260910140101_AddRlsTablasFaltantes` - **SEGURIDAD**, ver 0.1. Solo SQL, sin cambios de esquema.
6. `20260910202226_AddModulosContributivosUnidad` - `unidades_privadas` +`modulo_contributivo_1..5`
   numeric(7,4) NULL.

7. `20260910235018_EquipoAtlas_AddIndicesTenantIdTareas` - **solo indices**: 6 CREATE INDEX de
   `tenant_id` en `tableros`, `tablero_usuarios`, `tablero_campos`, `tarea_campo_valores`,
   `tarea_adjuntos`, `tarea_subtareas`. No cambia esquema ni datos.
8. `20260912205405_AddPermisoCrearTareasOperario` - seed en la tabla global `rol_permisos`: da al rol
   Operario el permiso Crear sobre TAREAS. Solo datos (idempotente).
9. `20260913005909_AddCostoEstimadoProgramacionTarea` - `programacion_tareas` +`costo_estimado`
   numeric(14,2) NULL. Modulo Mantenimiento (YUNQUE): costo estimado de una programacion. Aditiva, sin
   datos ni indices; el `Down()` hace DROP de la columna.

(Las anteriores 0.0.68-0.0.83 estan listadas en `DEPLOY_CHECKLIST.md`.)

**Comprobacion rapida de que quedaron aplicadas:**

```sql
select column_name from information_schema.columns
where table_name = 'unidad_campos_config' order by 1;
-- deben estar: alias, campo_clave, entidad, formato, id, oculto, opciones, orden, tenant_id, ...

select count(*) from information_schema.columns
where table_name = 'unidades_privadas' and column_name like 'modulo_contributivo%';  -- debe dar 5
```

> Ninguna borra datos. La 4 recrea un indice unico; la 5 no toca esquema.

## 3. REGLA CRITICA (no romper el login otra vez)

- **Criptografia se queda en 9.x.** NO subir `Microsoft.AspNetCore.Cryptography.KeyDerivation` ni
  `DataProtection` a 10.x. Mezclar 10.x rompio el login en 0.0.66.
- Build de Railway con restore fresh `-warnaserror`: verificar 0 NU1603/NU1605.
- **CSP:** el sitio corre sin `unsafe-eval` (solo `wasm-unsafe-eval`). No introducir JS que dependa de
  `eval()`; los helpers van en `wwwroot/js/*.js` (subir `?v=` en `Components/App.razor` al tocarlos).
  El drag & drop de esta tanda usa eventos nativos de Blazor, sin JS.

## 4. Variables de entorno obligatorias en prod

`Jwt__SigningKey` (>=32 chars real), `Propia__WebhookToken`, `Meta__AppSecret`,
`Storage__Provider=R2` + `R2__*`, `ConnectionStrings__*` (owner + app con RLS).
Recomendadas: `Metrics__ScrapeToken`, `ForwardedHeaders__KnownNetworks__0` (CIDR del proxy Railway).

## 5. Post-deploy (verificacion minima)

- [ ] Login OK; el footer muestra `v0.0.93`.
- [ ] **Menu (ver 0.7):** tras importar el JSON en Super Admin y Guardar, el grupo **Configuracion**
      muestra Unidades Privadas, Residentes, Mascotas, Vehiculos, Equipos y Zonas...; y **Mi Copropiedad**
      queda solo con "Mi copropiedad". Entrar a `/vehiculos` y `/mascotas` y confirmar que listan.
- [ ] **Imagenes de copropiedad:** el selector "Mis copropiedades" muestra la foto de cada una, no el
      icono roto. Si sale rota, revisar que el `src` renderizado conserve el host completo (era el bug
      de 0.0.92).
- [ ] **PRIMERO (RLS):** entrar a **Directorio**, **Seguros** y **Contratos** y confirmar que **listan
      datos**. Si alguno sale vacio -> revisar logs por `row-level security` / `42501` y revertir el
      `Down()` de `AddRlsTablasFaltantes`. (En dev: Directorio 200 filas, Seguros 7, Contratos 21.)
- [ ] Las 18 tablas quedan con `relrowsecurity` y `relforcerowsecurity` en `true` y >=1 politica; en toda
      la BD deben quedar solo **8** tablas de tenant sin RLS (las de la seccion 6).
- [ ] **Probar "Configurar" con un usuario cuyo rol sea EXACTAMENTE `Administrador` en esa copropiedad**
      (ver 0.4): con otro rol el 403 es lo esperado, no un fallo.
- [ ] **Si no responde:** el aviso rojo dice la causa. 500 con `column ... does not exist` = faltan
      migraciones (seccion 2). 403 = permisos (seccion 0.4).
- [ ] **Modulos contributivos:** en Unidades > Configurar salen "Modulo Contributivo 1..5" debajo de
      Coeficiente y **ocultos**; activar uno lo saca como columna y guarda decimales (ej. 12.3456).
- [ ] **Plantilla filtrada:** con un modulo activo, la plantilla trae SOLO ese (no los otros 4); ocultar
      un campo (ej. Matricula) lo saca de la plantilla y la fila de EJEMPLO sigue alineada.
      `COPROPIEDAD`, `UNIDAD PRIVADA` y `PRINCIPAL` deben salir SIEMPRE.
- [ ] **Configurar > pestanas Personas/Vehiculos/Mascotas/Terceros:** cada una lista sus campos del
      sistema; crear un campo propio (ej. "Numero de chip" en Mascotas) y comprobar que aparece en esa
      seccion de la **ficha** y guarda su valor. La ficha debe tener pestana **Terceros**.
- [ ] **Listas:** "Gestionar lista" abre un **popup** encima del modal. Los tres estados de fabrica y los
      tipos base salen con candado y **sin eliminar**, solo el ojo. Ocultar una opcion la saca de los
      selectores pero una unidad que ya la usaba la **sigue mostrando en su fila**.
- [ ] **Columnas:** mostrar/ocultar cualquier campo como columna y reubicarlo arrastrando; el orden se
      mantiene al recargar **y lo ven todos los usuarios de la copropiedad** (ya no es por usuario).
- [ ] **Carga por Excel con campos dinamicos:** con un campo propio configurado, descargar la plantilla,
      verificar la columna `[Nombre del campo]`, llenarla, cargar y confirmar el valor en la ficha. Una
      columna `[X]` inventada debe dar **un** aviso por hoja.
- [ ] `/mantenimiento`: solo tabs Programacion y Calendario; alta inline; el panel del SelectorPersona
      FLOTA sin deformar la fila; Calendario Mes/Semana.
- [ ] `/health` = 200; cabeceras de seguridad presentes; un rol no-admin recibe 403 en una escritura gateada.

## 6. Pendientes que necesitan DECISION (no bloquean el deploy)

- **Permisos del gestor de campos.** Hoy solo el rol `Administrador` puede configurar campos. Decidir si
  Coordinador/Asistente deben poder, y en ese caso habilitarles `MI_COPROPIEDAD / Editar` en la matriz por
  defecto (hoy la siembra les deja 0 permisos en ese modulo). Ver 0.4.
- **8 tablas de tenant siguen sin RLS** y el test de cobertura **falla a proposito** listandolas: 6 con
  `tenant_id` NULLABLE (`calendario_eventos`, `notificaciones`, `login_audit_events`, `sistema_logs`,
  `usuario_sesiones`, `document_extraction_logs`) y 2 de Capa 1 cruzadas por diseno
  (`org_colaborador_copropiedades`, `panel_snapshot_copropiedades`). Decidir por cada una: politica que
  tolere `NULL`, o exencion documentada en el test.
- **Modulos contributivos sin validacion de suma.** Son campos libres. El dominio YA tiene
  `TipoCoeficiente` + `UnidadCoeficiente` con la regla RN-02 (cada tipo debe sumar 100%) y sus endpoints
  (`tipos-coeficiente`, `unidades/{id}/coeficientes`), pero **sin ninguna UI que los consuma**. Decidir si
  los modulos migran a ese modelo (permite N modulos y valida la suma) o se quedan como columnas planas.
- **TERCEROS = dos conceptos con el mismo nombre.** El catalogo de la pestana cuelga de `UnidadEmpleada`
  (empleadas de servicio de una unidad); la hoja TERCEROS de la plantilla carga terceros del **Directorio**
  (crea Empresa/Persona global + vinculo, sin columna UNIDAD PRIVADA). Por eso esa hoja quedo sin columnas
  dinamicas. Decidir: renombrar la pestana a "Empleadas/Personal" y dar al Directorio su propio catalogo,
  o que la hoja TERCEROS cree `UnidadEmpleada`.
- **Campos de sistema de Personas incompletos en el panel:** la hoja PERSONAS de la plantilla tiene `SEXO`,
  `FECHA NACIMIENTO` y `PROFESION`, que no aparecen en la pestana de configuracion porque viven en la tabla
  global `personas`, no en `UnidadPersona`.
- **Visibilidad/orden de los campos de SISTEMA de las fichas vinculadas:** se guardan pero todavia no
  cambian el render de la ficha. Los campos **propios** si se ordenan y ocultan.
- **Separador de miles:** se guarda y se aplican decimales y maximo de caracteres, pero el separador no se
  pinta dentro de celdas editables (reparsear "1.234" es donde se corrompen coeficientes y cuotas).
- **Dato malo (revisar en prod):** en dev habia 1 unidad con `tipo = 0`, fuera del enum `TipoUnidad` (que
  empieza en 1). Antes se mostraba como "Apartamento" y cualquier edicion la habria guardado asi; ahora se
  ve `(sin definir: 0)`. Buscar en prod:
  `select id, numero, tipo from unidades_privadas where tipo not between 1 and 20;`
- **Bug preexistente en `ContratosVencimientoJob`:** el `continue` de la linea 45
  (`if (contratos.Count == 0) continue;`) salta al siguiente tenant, asi que el bloque de alertas de
  **polizas** (linea 83) nunca corre para una copropiedad sin contratos con fecha fin -> esa copropiedad
  **no recibe alertas de vencimiento de seguros**. Ademas la linea 115 (`catch { errores++; }`) se traga
  toda excepcion por tenant sin log.

## 7. Rollback

Si algo falla: redeploy del artefacto **0.0.67** (ultimo bueno en prod). Las migraciones son aditivas, no
hace falta revertir esquema para volver (las columnas/tablas nuevas quedan sin uso).

**Excepcion:** si el problema es RLS (modulos que salen vacios), correr el `Down()` de
`AddRlsTablasFaltantes`, que desactiva la RLS de esas 18 tablas y las deja como estaban.
