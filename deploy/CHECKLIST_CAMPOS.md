# Lista de chequeo - Campos y modulos de unidad (0.0.93, refrescado 2026-09-15)

> Que revisar en prod despues del deploy 0.0.93, especifico del trabajo de CAMPOS y de los modulos
> Unidades / Residentes / Vehiculos / Mascotas. El detalle de migraciones, variables y menu esta en
> `HANDOFF_DEPLOY.md`. Marca cada punto en prod (copropiedad real, con un usuario Administrador).
>
> **Nuevo 2026-09-15 (Fase 2 REFERENCIA, solo en Unidades, sin migracion):** el gestor de Campos de
> Unidades ahora ofrece 3 tipos de campo mas (Formula, Usuario, Directorio). Ver seccion 1b. Aun NO estan
> en Vehiculos/Mascotas/Residentes (eso vive en ramas, no en main).

## 0. Antes de empezar

- [ ] Deploy de codigo aplicado y migraciones corridas (`ef database update`). Footer muestra `v0.0.93`.
- [ ] JSON del menu importado en Super Admin y **Guardado** (ver HANDOFF 0.7). Sin esto, Vehiculos y
      Mascotas salen igual pero en Mi Copropiedad, no en Configuracion.

## 1. Panel "Campos" de Unidades Privadas

- [ ] En Unidades Privadas, el boton **Campos** (barra de la tabla) abre el panel. Ya **no** existe un
      boton "Configurar" aparte en la cabecera.
- [ ] El panel abre directo a los campos de la **unidad**: **no** hay barra de pestanas
      (Personas/Vehiculos/Mascotas/Terceros ya no estan aqui).
- [ ] "Campos" tambien abre desde la vista **Tarjetas**, no solo desde Tabla.
- [ ] "Restablecer columnas" esta dentro del panel y funciona (ojo: reescribe orden y visibilidad de la
      copropiedad; probar en una copropiedad de prueba, no en una real con configuracion hecha).
- [ ] Activar/ocultar un campo del sistema (icono del ojo) se refleja en la tabla; el cambio es **por
      copropiedad** (lo ve toda la copropiedad, no solo el usuario).
- [ ] Renombrar (alias) un campo del sistema se refleja en el encabezado de la tabla.
- [ ] Crear un campo propio de la unidad -> aparece como columna nueva y su valor se guarda inline.

## 1b. Fase 2 REFERENCIA: tipos Formula / Usuario / Directorio (solo Unidades, 2026-09-15)

> Sin migracion (enum-only; `opciones` ya existia). Solo disponible en el gestor de Campos de **Unidades**.

- [ ] En Campos de Unidades, el selector **TIPO** del campo nuevo ofrece **Formula**, **Usuario** y
      **Directorio** ademas de los tipos previos.
- [ ] **Formula**: crear un campo Formula sobre campos numericos de la unidad (ej. Suma de coeficiente);
      la columna se calcula **en lectura** (read-only) y no se puede escribir su valor a mano.
- [ ] **Usuario**: crear un campo Usuario; su celda ofrece **solo usuarios de esta copropiedad**
      (no de otras); al elegir uno y recargar (F5), la celda muestra el **nombre**, no el Guid.
- [ ] **Directorio**: crear un campo Directorio; al elegir una persona vinculada y recargar, muestra el
      **nombre** resuelto.
- [ ] Un valor de Usuario/Directorio de OTRA copropiedad es rechazado (aislamiento por tenant).

## 2. Carga por Excel (plantilla + importacion)

- [ ] Descargar la plantilla: la hoja UNIDADES trae **solo las columnas de los campos VISIBLES** de esa
      copropiedad (mas COPROPIEDAD, UNIDAD PRIVADA y PRINCIPAL, que salen siempre).
- [ ] Un campo del sistema que se **active** vuelve a salir como columna en la plantilla.
- [ ] La columna **TIPO** ofrece los tipos del sistema (con su etiqueta: "Cuarto util", etc.) **y** los
      tipos propios de la copropiedad, si los hay.
- [ ] Llenar y cargar la plantilla: los valores caen en la unidad (incluidos campos propios `[Label]`).
- [ ] Recargar una plantilla a la que se le quito una columna o con una celda vacia **no borra** el dato
      que ya tenia la unidad (matricula, tipo, tipo propio, etc.).
- [ ] Un TIPO que no existe en la copropiedad da **error de esa fila** (no entra como "Apartamento").
- [ ] Las listas desplegables de la plantilla estan en una hoja oculta `PROPIA_LISTAS`; el archivo abre
      en Excel **sin pedir reparacion**.

## 3. Modulo Vehiculos (/vehiculos)

- [ ] Aparece en el menu (Configuracion, tras importar el JSON; o Mi Copropiedad por defecto).
- [ ] Lista **todos** los vehiculos de la copropiedad, con unidad y propietario.
- [ ] Alta inline al pie: elegir unidad + placa + tipo -> se crea y queda resaltado.
- [ ] Editar en linea la placa o el tipo -> se guarda (PUT). Antes esto no existia.
- [ ] Boton **Campos**: crear un campo propio del vehiculo -> aparece como columna y su valor se guarda.
- [ ] El expander de cada fila abre la unidad del vehiculo.

## 4. Modulo Mascotas (/mascotas)

- [ ] Igual que Vehiculos: lista todas las mascotas con unidad y propietario.
- [ ] Alta inline (unidad + nombre + tipo + raza), edicion en linea, y campos propios como columnas.

## 5. Residentes (/residentes) y su relacion con Personas

- [ ] "Personas" ya **no** se configura desde el panel de Unidades. Los campos de personas se gestionan
      en el modulo **Residentes** (menu "Columnas" de esa vista).
- [ ] Residentes lista todas las personas de las unidades (propietarios/residentes/...); el alta queda
      tambien en el Directorio.

## 6. Permisos (esto ya mordio antes en prod)

- [ ] Con un usuario **Administrador** exacto: puede abrir Campos, activar/ocultar y crear campos sin 403.
- [ ] Con un rol distinto sin `MI_COPROPIEDAD/Editar`: al intentar guardar sale un mensaje claro
      ("tu rol no tiene permiso de EDITAR..."), no un fallo mudo.

## 7. Si algo sale mal

- **500 con `column ... does not exist`** al abrir Unidades/Vehiculos/Mascotas -> faltan migraciones
  (HANDOFF seccion 2).
- **403 al activar/crear un campo** -> permisos (HANDOFF 0.4): el rol necesita `MI_COPROPIEDAD/Editar` o
  ser Administrador.
- **Vehiculos/Mascotas no aparecen en Configuracion** -> falta importar el JSON del menu (HANDOFF 0.7);
  igual son alcanzables por `/vehiculos` y `/mascotas`.
- **Una columna "desaparecio" de la plantilla** -> el campo esta oculto en Configurar; es el
  comportamiento esperado (HANDOFF 0.2).
