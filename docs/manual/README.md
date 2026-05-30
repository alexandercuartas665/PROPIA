# Manual del Administrador - PROPIA

Manual HTML auto-contenido (1 archivo HTML + 1 CSS + carpeta de capturas).
Estilo Stripe/Linear con sidebar fija y marca PROPIA.

## Estructura

```
docs/manual/
├── index.html          ← Manual completo (34 secciones)
├── README.md           ← Este archivo
├── assets/
│   └── styles.css      ← Estilos del manual
└── screenshots/        ← Carpeta para las capturas
    ├── 01_login.png
    ├── 02_google_oauth.png
    ├── 03_registro.png
    └── ...
```

## Como ver el manual

Doble clic en `index.html` (se abre en el navegador) o sirvelo con cualquier servidor estatico:

```bash
# Desde el repo
cd docs/manual
python -m http.server 8000      # http://localhost:8000
# o
npx serve .                      # http://localhost:3000
```

## Como agregar capturas

El HTML referencia 34 capturas con nombres fijos en `screenshots/`. Si el archivo no
existe, el manual muestra un placeholder con rayas diagonales (no se ve roto).

Para reemplazar un placeholder por la captura real:

1. Toma la captura desde el sistema PROPIA (Win+Shift+S en Windows, o tu herramienta favorita).
2. Recortala al area util (sin barra de tareas del sistema).
3. Guardala con el nombre exacto en `screenshots/`.
4. Recarga `index.html` en el navegador.

### Lista de capturas esperadas

| # | Archivo | Pantalla |
|---|---|---|
| 01 | `01_login.png` | `/login` |
| 02 | `02_google_oauth.png` | Selector de cuenta Google |
| 03 | `03_registro.png` | `/registro` |
| 04a | `04a_otp_email.png` | Bandeja de entrada con OTP |
| 04b | `04b_otp_input.png` | `/registro` paso 2 (input OTP) |
| 05 | `05_wizard.png` | `/onboarding/continuar` |
| 06 | `06_mi_copropiedad.png` | `/mi-copropiedad` vista general |
| 07 | `07_identidad.png` | Mi Copropiedad seccion 1 expandida |
| 08 | `08_distribucion.png` | Mi Copropiedad seccion 2 expandida |
| 09 | `09_equipo_trabajo.png` | Mi Copropiedad seccion 3 expandida |
| 10 | `10_gobierno.png` | Mi Copropiedad seccion 4 expandida |
| 11 | `11_servicios.png` | Mi Copropiedad seccion 5 expandida |
| 12 | `12_zonas.png` | Mi Copropiedad seccion 6 expandida |
| 13 | `13_equipos.png` | Mi Copropiedad seccion 7 expandida |
| 14 | `14_finanzas.png` | Mi Copropiedad seccion 8 expandida |
| 15 | `15_directorio.png` | `/directorio` |
| 16 | `16_usuarios.png` | `/usuarios` |
| 17 | `17_pqrsd.png` | `/pqrsd` |
| 18 | `18_cartera.png` | `/cartera` |
| 19 | `19_presupuesto.png` | `/presupuesto` |
| 20 | `20_tareas.png` | `/tareas` |
| 21 | `21_documentos.png` | `/documentos` |
| 22 | `22_asambleas.png` | `/asambleas` |
| 23 | `23_reservas.png` | `/reservas` |
| 24 | `24_comunicaciones.png` | `/comunicaciones` |
| 25 | `25_porteria.png` | `/porteria` |
| 26 | `26_mantenimiento.png` | `/mantenimiento` |
| 27 | `27_reportes.png` | `/reportes` |
| 28 | `28_dashboard.png` | `/dashboard-copropiedad` |
| 29 | `29_asistente_ia.png` | Panel del Asistente en Mi Copropiedad |
| 30 | `30_agentes_ia.png` | `/ia/agentes` |
| 31 | `31_lineas_whatsapp.png` | `/ia/lineas` |
| 32 | `32_cambiar_copropiedad.png` | Selector de copropiedad del header |
| 33 | `33_perfil.png` | `/cuenta/perfil` |

## Formato recomendado para capturas

- **Formato:** PNG (sin compresion lossy) o JPG de alta calidad
- **Ancho:** entre 1200 y 1600 px (se redimensiona en CSS)
- **Que NO incluir:** datos personales reales, datos financieros sensibles
- **Que SI incluir:** marca PROPIA visible, tema claro (consistencia visual)

Para esconder data sensible, usa la cuenta demo (`admin@demo.propia` / `PropiaDemo2026!`)
que tiene Edificio Portal del Sol con data ficticia.

## Personalizacion

- **Colores y tipografia** en `assets/styles.css` (variables CSS al inicio).
- **Estructura del menu** y orden de capitulos en `index.html` (los anchor links son
  `#identidad`, `#distribucion`, etc.).
- **Nuevas secciones:** copia un bloque `<section id="...">` existente y modifica.

## Distribucion

El manual es HTML estatico. Puedes:

- Subir `docs/manual/` a `https://app.propia.cubot.com.co/manual/`.
- Incluirlo como ruta dentro del app (`/admin/ayuda`).
- Empaquetar como ZIP y enviarlo por correo.
- Convertir a PDF usando Chrome (`Ctrl+P` &rarr; Guardar como PDF) — el CSS esta
  optimizado para impresion limpia.
