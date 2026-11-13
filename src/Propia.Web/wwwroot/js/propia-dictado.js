/* Dictado (voz a texto del navegador) en cajas de texto multilinea.
   Se engancha solo a cada <textarea> que cumpla SELECTOR (piloto: data-dictado). Pone un microfono redondo en la
   esquina inferior derecha de la caja; al pulsarlo escucha (Web Speech API, es-CO) e inserta cada frase en el cursor,
   con espacio y mayuscula, respetando maxlength, y dispara input/change para que Blazor (@bind) lo vea.
   Comandos: "coma", "punto", "dos puntos", "punto y coma", "nueva linea", "punto y aparte".
   El boton vive en <body> (position:fixed) y sigue a la caja: no toca el DOM que maneja Blazor.
   Sin soporte del navegador (Firefox) no aparece. Sin eval (CSP). */
window.propiaDictado = (function () {
    var SR = window.SpeechRecognition || window.webkitSpeechRecognition;
    var SELECTOR = 'textarea[data-dictado]:not([data-sin-dictado])';
    var LANG = 'es-CO';
    // Chrome en Android/iPad, en modo continuo, reenvia en cada evento las frases ya reconocidas (cada palabra salia
    // repetida). En tabletas y celulares se reconoce frase por frase y se reinicia solo tras cada pausa.
    var MOVIL = /Android|iPhone|iPad|iPod/i.test(navigator.userAgent) || (navigator.platform === 'MacIntel' && navigator.maxTouchPoints > 1);
    var MIC = '<svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round"><rect x="9" y="2.5" width="6" height="12" rx="3"/><path d="M5 11a7 7 0 0 0 14 0M12 18v3.5"/></svg>';
    var CONTENIDO = '<span class="pd-ic">' + MIC + '</span>'
        + '<span class="pd-ondas" aria-hidden="true"><i></i><i></i><i></i><i></i><i></i></span>'
        + '<span class="pd-txt">Escuchando</span><span class="pd-stop" aria-hidden="true"></span>';
    var items = [];          // { ta, btn }
    var activo = null;       // item que esta escuchando
    var rec = null, burbuja = null, halo = null, programado = false, vozTimer = null, flashTimer = null;
    // "Mejorar redaccion" (agente Corrector de redaccion del Super Admin): se ofrece al detener el dictado.
    var panel = null, panelIt = null, panelTimer = null, pendiente = null;
    var CHISPA = '<svg width="15" height="15" viewBox="0 0 24 24" fill="currentColor" aria-hidden="true"><path d="M11 2.5l1.9 5.1 5.1 1.9-5.1 1.9L11 16.5l-1.9-5.1-5.1-1.9 5.1-1.9z"/><path d="M18.5 14l.9 2.4 2.4.9-2.4.9-.9 2.4-.9-2.4-2.4-.9 2.4-.9z"/></svg>';
    var sesion = '';         // texto final ya insertado en la sesion de reconocimiento actual (solo movil)

    function iniciar() {
        if (!SR || !document.body) return;
        halo = document.createElement('div');
        halo.className = 'pd-halo';
        halo.setAttribute('aria-hidden', 'true');
        document.body.appendChild(halo);
        panel = document.createElement('div');
        panel.className = 'pd-panel';
        panel.setAttribute('role', 'dialog');
        panel.addEventListener('mousedown', function (e) { if (e.target.closest('button')) e.preventDefault(); });
        panel.addEventListener('click', accionPanel);
        document.body.appendChild(panel);
        document.addEventListener('input', function (e) {
            // Si el usuario sigue escribiendo, la invitacion a mejorar se retira (la propuesta abierta se respeta).
            // (la espera "Mejorando..." no se cierra: el teclado del celular manda un input al tocar el boton).
            if (e.isTrusted && panelIt && e.target === panelIt.ta && panel.classList.contains('pd-oferta')
                && !panel.classList.contains('is-cargando')) cerrarPanel();
        }, true);
        burbuja = document.createElement('div');
        burbuja.className = 'pd-burbuja';
        burbuja.setAttribute('role', 'status');
        burbuja.setAttribute('aria-live', 'polite');
        document.body.appendChild(burbuja);
        new MutationObserver(programar).observe(document.documentElement, { childList: true, subtree: true, attributes: true, attributeFilter: ['data-dictado', 'data-sin-dictado', 'disabled', 'readonly', 'style', 'class'] });
        window.addEventListener('scroll', programar, true);
        window.addEventListener('resize', programar);
        if (window.visualViewport) {   // teclado virtual que abre/cierra y zoom con los dedos
            window.visualViewport.addEventListener('resize', programar);
            window.visualViewport.addEventListener('scroll', programar);
        }
        document.addEventListener('input', programar, true);
        document.addEventListener('mouseup', programar, true);   // al redimensionar la caja con el tirador
        programar();
    }

    function programar() { if (programado) return; programado = true; setTimeout(function () { programado = false; sincronizar(); }, 50); }

    // La navegacion mejorada de Blazor (ir de una pagina a otra sin recargar) reconstruye el <body> y quita lo que no es
    // suyo: el halo, el panel, la burbuja y los botones quedaban desconectados (la propuesta llegaba y no se veia).
    function enDom(el) { if (el && !el.isConnected && document.body) document.body.appendChild(el); }
    function reponer() {
        enDom(halo); enDom(panel); enDom(burbuja);
        items.forEach(function (it) { enDom(it.mej); enDom(it.btn); });
    }

    function sincronizar() {
        reponer();
        var tas = Array.prototype.slice.call(document.querySelectorAll(SELECTOR));
        // Altas
        tas.forEach(function (ta) {
            if (items.some(function (i) { return i.ta === ta; })) return;
            var b = document.createElement('button');
            b.type = 'button';
            b.className = 'pd-mic';
            b.innerHTML = CONTENIDO;
            b.title = 'Dictar con la voz';
            b.setAttribute('aria-label', 'Dictar con la voz');
            b.addEventListener('mousedown', function (e) { e.preventDefault(); });   // no quitar el foco de la caja
            var m = document.createElement('button');
            m.type = 'button';
            m.className = 'pd-mej';
            m.innerHTML = CHISPA;
            m.title = 'Mejorar redacción con IA';
            m.setAttribute('aria-label', 'Mejorar redacción con IA');
            m.addEventListener('mousedown', function (e) { e.preventDefault(); });
            var it = { ta: ta, btn: b, mej: m };
            b.addEventListener('click', function () { alternar(it); });
            m.addEventListener('click', function () { if (activo === it) detener(); mejorar(it); });
            document.body.appendChild(m);
            document.body.appendChild(b);
            items.push(it);
        });
        // Bajas (la caja salio del DOM o perdio el atributo)
        items = items.filter(function (it) {
            if (tas.indexOf(it.ta) >= 0) return true;
            if (activo === it) detener();
            if (panelIt === it) cerrarPanel();
            it.btn.remove();
            it.mej.remove();
            return false;
        });
        items.forEach(posicionar);
        if (activo) posicionarBurbuja(activo);
        if (panelIt) posicionarPanel(panelIt);
    }

    // Visible solo si la esquina de la caja se ve dentro de todos sus contenedores con scroll (modales, paneles).
    function visible(ta, x, y) {
        if (ta.disabled || ta.readOnly) return false;
        var r = ta.getBoundingClientRect();
        if (r.width < 60 || r.height < 30) return false;
        for (var el = ta.parentElement; el && el !== document.body; el = el.parentElement) {
            var cs = getComputedStyle(el);
            if (cs.display === 'none' || cs.visibility === 'hidden') return false;
            if (/(auto|scroll|hidden|clip)/.test(cs.overflow + cs.overflowY + cs.overflowX)) {
                var c = el.getBoundingClientRect();
                if (x < c.left || x > c.right || y < c.top || y > c.bottom) return false;
            }
        }
        return x >= 0 && y >= 0 && x <= window.innerWidth && y <= window.innerHeight;
    }

    function posicionar(it) {
        var r = it.ta.getBoundingClientRect();
        // Esquina inferior derecha, a la izquierda del tirador de tamano (y de la barra de scroll si la hay).
        var scrollbar = it.ta.offsetWidth - it.ta.clientWidth;
        var x = r.right - 34 - Math.max(scrollbar, 14), y = r.bottom - 34;
        if (!visible(it.ta, x + 14, y + 14)) { it.btn.style.display = 'none'; it.mej.style.display = 'none'; return; }
        it.btn.style.display = 'flex';
        // Anclado por la derecha: al escuchar se estira hacia la izquierda como pildora.
        it.btn.style.right = (window.innerWidth - (x + 28)) + 'px';
        it.btn.style.top = y + 'px';
        // Por encima de la caja (y de su modal), sin pasar por encima de otros overlays mas altos.
        var z = zIndexDe(it.ta);
        it.btn.style.zIndex = String(z + 2);
        // Boton "Mejorar redaccion": encima del microfono (o a su izquierda si la caja es baja). Solo con sesion, con
        // suficiente texto y cuando no se esta dictando en esta caja.
        var conTexto = it.ta.value.trim().length >= 15 && !!token();
        if (conTexto && activo !== it && panelIt !== it) {
            var alta = r.height >= 80;
            it.mej.style.display = 'flex';
            it.mej.style.right = (window.innerWidth - (x + 28) + (alta ? 0 : 34)) + 'px';
            it.mej.style.top = (y - (alta ? 34 : 0)) + 'px';
            it.mej.style.zIndex = String(z + 2);
        } else it.mej.style.display = 'none';
        if (activo === it) {
            // Halo con degradado que gira alrededor de la caja que esta escuchando.
            var cs = getComputedStyle(it.ta);
            halo.style.left = (r.left - 3) + 'px';
            halo.style.top = (r.top - 3) + 'px';
            halo.style.width = (r.width + 6) + 'px';
            halo.style.height = (r.height + 6) + 'px';
            halo.style.borderRadius = 'calc(' + (cs.borderTopLeftRadius || '8px') + ' + 3px)';
            halo.style.zIndex = String(z + 1);
        }
    }

    function zIndexDe(el) {
        var z = 0;
        for (; el && el !== document.body; el = el.parentElement) {
            var v = parseInt(getComputedStyle(el).zIndex, 10);
            if (!isNaN(v) && v > z) z = v;
        }
        return Math.max(z, 10);
    }

    function posicionarBurbuja(it) {
        if (it.btn.style.display === 'none') { ocultarBurbuja(); return; }
        // La pildora mide 118px al escuchar (el rect aun puede estar en plena transicion).
        var b = it.btn.getBoundingClientRect();
        if (burbuja.style.display !== 'block') {
            burbuja.style.display = 'block';
            setTimeout(function () { burbuja.classList.add('is-vis'); }, 10);
        }
        burbuja.style.zIndex = it.btn.style.zIndex;
        var ancho = Math.min(320, window.innerWidth - 24);
        burbuja.style.maxWidth = ancho + 'px';
        burbuja.style.left = Math.max(12, b.right - ancho) + 'px';
        burbuja.style.width = ancho + 'px';
        burbuja.style.top = (b.top - 8) + 'px';
    }

    function mensaje(txt, tipo) {
        var vis = burbuja.classList.contains('is-vis');
        burbuja.className = 'pd-burbuja' + (tipo ? ' is-' + tipo : '') + (vis ? ' is-vis' : '');
        burbuja.textContent = txt;
    }

    function ocultarBurbuja() {
        burbuja.classList.remove('is-vis');
        setTimeout(function () { if (!burbuja.classList.contains('is-vis')) burbuja.style.display = 'none'; }, 200);
    }

    // Las ondas se avivan mientras llega voz y se calman en silencio.
    function hayVoz(it) {
        it.btn.classList.add('is-voz');
        clearTimeout(vozTimer);
        vozTimer = setTimeout(function () { it.btn.classList.remove('is-voz'); }, 900);
    }

    function alternar(it) {
        if (activo === it) { detener(); ofrecerMejora(it); return; }
        if (activo) detener();
        cerrarPanel();
        empezar(it);
    }

    // ===================== Mejorar redaccion =====================
    var ESC = { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' };
    function esc(t) { return String(t).replace(/[&<>"']/g, function (c) { return ESC[c]; }); }
    function token() { try { return localStorage.getItem('propia_jwt'); } catch (e) { return null; } }

    function ofrecerMejora(it) {
        // Solo con sesion (las paginas publicas no tienen agente) y con algo que valga la pena corregir.
        if (!token() || it.ta.value.trim().length < 15) return;
        it.mej.style.display = 'none';   // el chip ocupa ese lugar mientras se ofrece
        abrirPanel(it, 'pd-oferta',
            '<button type="button" class="pd-of-btn" data-a="mejorar">' + CHISPA + '<span>Mejorar redacción</span></button>'
            + '<button type="button" class="pd-of-x" data-a="cerrar" title="No, gracias" aria-label="No, gracias">&times;</button>', 15000);
    }

    function abrirPanel(it, clase, html, autocierre) {
        reponer();
        clearTimeout(panelTimer);
        panel.className = 'pd-panel ' + clase;
        panel.innerHTML = html;
        panel.style.display = clase.indexOf('pd-oferta') >= 0 ? 'flex' : 'block';
        panelIt = it;
        posicionarPanel(it);
        setTimeout(function () { panel.classList.add('is-vis'); }, 10);
        if (autocierre) panelTimer = setTimeout(cerrarPanel, autocierre);
    }

    function cerrarPanel() {
        clearTimeout(panelTimer);
        if (!panel) return;
        panel.classList.remove('is-vis');
        var it = panelIt;
        panelIt = null;
        if (it) posicionar(it);
        setTimeout(function () { if (!panel.classList.contains('is-vis')) panel.style.display = 'none'; }, 180);
    }

    // Celular/tableta: el teclado virtual mueve la zona visible y el anclaje a la caja deja el panel tapado o fuera de
    // pantalla. Ahi el panel es una hoja inferior pegada a lo que se ve (visualViewport), justo encima del teclado.
    function enHoja() { return MOVIL || window.innerWidth < 640; }

    function posicionarPanel(it) {
        if (enHoja()) {
            panel.classList.add('pd-hoja');
            panel.style.visibility = 'visible';
            panel.style.zIndex = String(zIndexDe(it.ta) + 3);
            var vv = window.visualViewport;
            var vTop = vv ? vv.offsetTop : 0, vLeft = vv ? vv.offsetLeft : 0;
            var vAlto = vv ? vv.height : window.innerHeight, vAncho = vv ? vv.width : window.innerWidth;
            var w = Math.min(vAncho - 20, 560);
            panel.style.width = w + 'px';
            panel.style.right = 'auto';
            panel.style.left = (vLeft + (vAncho - w) / 2) + 'px';
            var h = panel.offsetHeight || 60;
            panel.style.top = Math.max(vTop + 8, vTop + vAlto - h - 12) + 'px';
            return;
        }
        panel.classList.remove('pd-hoja');
        var r = it.ta.getBoundingClientRect();
        if (!visible(it.ta, r.right - 30, r.bottom - 20)) { panel.style.visibility = 'hidden'; return; }
        panel.style.visibility = 'visible';
        panel.style.zIndex = String(zIndexDe(it.ta) + 3);
        if (panel.classList.contains('pd-prop')) {
            var ancho = Math.min(Math.max(r.width - 16, 280), 560, window.innerWidth - 24);
            panel.style.width = ancho + 'px';
            panel.style.left = Math.max(12, Math.min(r.right - 8 - ancho, window.innerWidth - ancho - 12)) + 'px';
            panel.style.right = 'auto';
            // Encima de la parte baja de la caja; si no cabe arriba, debajo de la caja.
            var alto = panel.offsetHeight || 260;
            var arriba = r.bottom - 8 - alto;
            panel.style.top = (arriba >= 8 ? arriba : Math.min(r.bottom + 8, window.innerHeight - alto - 8)) + 'px';
        } else {
            panel.style.width = 'auto';
            panel.style.left = 'auto';
            panel.style.right = (window.innerWidth - (r.right - 22)) + 'px';
            panel.style.top = (r.bottom - 76) + 'px';
        }
    }

    function accionPanel(e) {
        var b = e.target.closest('[data-a]');
        if (!b || !panelIt) return;
        var it = panelIt, a = b.getAttribute('data-a');
        if (a === 'cerrar' || a === 'mantener') { cerrarPanel(); return; }
        if (a === 'mejorar') { mejorar(it); return; }
        if (a === 'usar' && pendiente && pendiente.it === it) {
            fijarValor(it.ta, pendiente.mejorado);
            destello(it);
            abrirPanel(it, 'pd-oferta is-ok', CHISPA + '<span>Texto mejorado</span><button type="button" class="pd-of-link" data-a="deshacer">Deshacer</button>', 8000);
            return;
        }
        if (a === 'deshacer' && pendiente && pendiente.it === it) {
            if (it.ta.value === pendiente.mejorado) fijarValor(it.ta, pendiente.original);
            pendiente = null;
            cerrarPanel();
        }
    }

    function normal(t) { return String(t || '').replace(/\s+/g, ' ').trim(); }

    // Mientras el corrector trabaja: el halo gira alrededor de la caja y la caja se atenua (se nota de inmediato).
    function esperando(it, on) {
        if (on) {
            destello(it, true);
            it.ta.classList.add('pd-trabajando');
        } else {
            it.ta.classList.remove('pd-trabajando');
            if (!activo) { halo.classList.remove('is-on', 'is-flash', 'is-espera'); setTimeout(function () { if (!activo && !halo.classList.contains('is-on')) halo.style.display = 'none'; }, 260); }
        }
    }

    function mejorar(it) {
        var original = it.ta.value;
        if (original.trim().length < 15) return;
        it.mej.style.display = 'none';
        var contexto = it.ta.getAttribute('data-dictado-contexto') || it.ta.getAttribute('placeholder') || '';
        abrirPanel(it, 'pd-oferta is-cargando', CHISPA + '<span>Mejorando la redacción...</span>');
        panel.classList.add('is-vis');   // sin esperar la transicion: que se vea ya
        esperando(it, true);
        var ctrl = window.AbortController ? new AbortController() : null;
        var limite = setTimeout(function () { if (ctrl) ctrl.abort(); }, 30000);
        var t0 = Date.now();
        fetch('/api/texto/mejorar', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json', 'Authorization': 'Bearer ' + token() },
            body: JSON.stringify({ texto: original, contexto: contexto }),
            signal: ctrl ? ctrl.signal : undefined
        }).then(function (resp) {
            return resp.json().catch(function () { return {}; }).then(function (j) { return { ok: resp.ok, status: resp.status, j: j }; });
        }).then(function (res) {
            clearTimeout(limite);
            esperando(it, false);
            if (window.console) console.info('[dictado] mejorar redaccion: HTTP ' + res.status + ' en ' + (Date.now() - t0) + ' ms');
            if (panelIt !== it) return;   // el usuario cerro la espera o cambio de caja
            if (!res.ok || !res.j.texto) {
                var msg = res.status === 401 ? 'Tu sesión expiró: recarga la página.' : (res.j.error || 'No se pudo mejorar el texto.');
                abrirPanel(it, 'pd-oferta is-err', '<span>' + esc(msg) + '</span>', 6000);
                return;
            }
            if (normal(it.ta.value) !== normal(original)) {
                abrirPanel(it, 'pd-oferta is-err', '<span>El texto cambió mientras el corrector trabajaba. Toca ✨ otra vez.</span>', 6000);
                return;
            }
            pendiente = { it: it, original: original, mejorado: res.j.texto };
            abrirPanel(it, 'pd-prop',
                '<div class="pd-prop-h">' + CHISPA + '<span>Propuesta del corrector</span><button type="button" class="pd-of-x" data-a="cerrar" aria-label="Cerrar">&times;</button></div>'
                + '<div class="pd-prop-txt">' + esc(res.j.texto) + '</div>'
                + '<div class="pd-prop-nota">Revísala: el corrector mejora la forma, no cambia los hechos.</div>'
                + '<div class="pd-prop-acts"><button type="button" class="pd-prop-no" data-a="mantener">Mantener el mío</button>'
                + '<button type="button" class="pd-prop-si" data-a="usar">Usar este texto</button></div>');
            setTimeout(function () { if (panelIt === it) posicionarPanel(it); }, 30);
        }).catch(function (err) {
            clearTimeout(limite);
            esperando(it, false);
            var msg = err && err.name === 'AbortError' ? 'El corrector tardó demasiado. Intenta de nuevo.' : 'No se pudo conectar con el corrector.';
            if (panelIt === it) abrirPanel(it, 'pd-oferta is-err', '<span>' + msg + '</span>', 6000);
        });
    }

    function fijarValor(ta, valor) {
        Object.getOwnPropertyDescriptor(HTMLTextAreaElement.prototype, 'value').set.call(ta, valor);
        ta.dispatchEvent(new Event('input', { bubbles: true }));
        ta.dispatchEvent(new Event('change', { bubbles: true }));
    }

    // Halo de un instante alrededor de la caja (el texto cambio).
    function destello(it, mantener) {
        reponer();
        var r = it.ta.getBoundingClientRect(), cs = getComputedStyle(it.ta);
        halo.style.left = (r.left - 3) + 'px'; halo.style.top = (r.top - 3) + 'px';
        halo.style.width = (r.width + 6) + 'px'; halo.style.height = (r.height + 6) + 'px';
        halo.style.borderRadius = 'calc(' + (cs.borderTopLeftRadius || '8px') + ' + 3px)';
        halo.style.zIndex = String(zIndexDe(it.ta) + 1);
        halo.style.display = 'block';
        if (mantener) { halo.classList.remove('is-flash'); setTimeout(function () { halo.classList.add('is-on', 'is-espera'); }, 10); return; }
        setTimeout(function () { halo.classList.add('is-on', 'is-flash'); }, 10);
        setTimeout(function () { if (!activo) { halo.classList.remove('is-on', 'is-flash'); setTimeout(function () { if (!activo) halo.style.display = 'none'; }, 260); } }, 900);
    }

    function empezar(it) {
        reponer();
        activo = it;
        it.btn.classList.add('is-on');
        it.btn.title = 'Detener el dictado';
        it.btn.setAttribute('aria-label', 'Detener el dictado');
        it.ta.focus();
        it.mej.style.display = 'none';
        posicionar(it);
        halo.style.display = 'block';
        setTimeout(function () { halo.classList.add('is-on'); }, 10);
        mensaje('Escuchando... habla con naturalidad. Di "coma", "punto" o "nueva línea" para puntuar.', null);
        posicionarBurbuja(it);
        rec = new SR();
        rec.lang = LANG;
        rec.continuous = !MOVIL;
        rec.interimResults = true;
        rec.onstart = function () { sesion = ''; };
        rec.onspeechstart = function () { hayVoz(it); };
        rec.onresult = function (e) {
            var parcial = '';
            if (MOVIL) {
                // Una frase por sesion: el ultimo resultado trae la frase completa; si llega repetida o ampliada,
                // solo se inserta lo nuevo.
                var ult = e.results[e.results.length - 1], t = ult[0].transcript.trim();
                if (ult.isFinal) {
                    var nuevo = t.toLowerCase().indexOf(sesion.toLowerCase()) === 0 ? t.slice(sesion.length) : (t.toLowerCase() === sesion.toLowerCase() ? '' : t);
                    if (nuevo.trim()) insertar(it.ta, nuevo);
                    sesion = t;
                } else parcial = t;
            } else {
                for (var i = e.resultIndex; i < e.results.length; i++) {
                    var tx = e.results[i][0].transcript;
                    if (e.results[i].isFinal) insertar(it.ta, tx);
                    else parcial += tx;
                }
            }
            hayVoz(it);
            mensaje(parcial ? parcial.trim() + '...' : 'Escuchando...', null);
        };
        rec.onerror = function (e) {
            if (e.error === 'no-speech' || e.error === 'aborted') return;
            var txt = e.error === 'not-allowed' || e.error === 'service-not-allowed'
                ? 'El navegador no dio permiso para usar el micrófono. Actívalo en el candado de la barra de direcciones.'
                : e.error === 'audio-capture' ? 'No se encontró un micrófono.'
                : e.error === 'network' ? 'El dictado del navegador necesita conexión a internet.'
                : 'El dictado se detuvo (' + e.error + ').';
            detener(txt);
        };
        // Chrome corta tras unos segundos de silencio: si el usuario no lo detuvo, se reanuda.
        rec.onend = function () { if (activo === it && rec) { try { rec.start(); } catch (x) { detener(); } } };
        try { rec.start(); } catch (x) { detener('No se pudo iniciar el dictado.'); }
    }

    function detener(error) {
        var it = activo;
        activo = null;
        if (rec) { var r = rec; rec = null; r.onend = null; try { r.stop(); } catch (x) { } }
        if (it) {
            it.btn.classList.remove('is-on', 'is-voz');
            it.btn.title = 'Dictar con la voz';
            it.btn.setAttribute('aria-label', 'Dictar con la voz');
            posicionar(it);
        }
        if (halo) {
            halo.classList.remove('is-on', 'is-flash');
            setTimeout(function () { if (!activo) halo.style.display = 'none'; }, 260);
        }
        if (error && it) {
            mensaje(error, 'err');
            posicionarBurbuja(it);
            setTimeout(function () { if (!activo) ocultarBurbuja(); }, 5000);
        } else if (burbuja) ocultarBurbuja();
    }

    // Comandos de puntuacion dictados ("punto" solo al final de la frase para no romper "el punto de encuentro").
    function puntuar(t) {
        t = ' ' + t.trim() + ' ';
        t = t.replace(/\s+punto y aparte\s+/gi, '.\n')
             .replace(/\s+(nueva l[ií]nea|nuevo p[aá]rrafo)\s+/gi, '\n')
             .replace(/\s+punto y coma\s+/gi, '; ')
             .replace(/\s+dos puntos\s+/gi, ': ')
             .replace(/\s+coma\s+/gi, ', ')
             .replace(/\s+punto(\s+final)?\s*$/i, '.');
        return t.replace(/[ \t]+\n/g, '\n').replace(/\n[ \t]+/g, '\n').trim();
    }

    function insertar(ta, texto) {
        var t = puntuar(texto);
        if (!t) return;
        var v = ta.value, ini = ta.selectionStart == null ? v.length : ta.selectionStart, fin = ta.selectionEnd == null ? ini : ta.selectionEnd;
        var antes = v.slice(0, ini), despues = v.slice(fin);
        var inicioFrase = antes.trim() === '' || /[.!?¿¡:\n]\s*$/.test(antes);
        if (inicioFrase) t = t.charAt(0).toUpperCase() + t.slice(1);
        var sep = antes === '' || /[\s\n]$/.test(antes) || /^[.,;:]/.test(t) ? '' : ' ';
        var nuevo = antes + sep + t + (despues && !/^[\s.,;:!?]/.test(despues) ? ' ' : '');
        var max = ta.maxLength > 0 ? ta.maxLength : Infinity;
        if ((nuevo + despues).length > max) {
            nuevo = nuevo.slice(0, Math.max(antes.length, max - despues.length));
            mensaje('Se alcanzó el máximo de caracteres de esta caja.', 'err');
        }
        var setter = Object.getOwnPropertyDescriptor(HTMLTextAreaElement.prototype, 'value').set;
        setter.call(ta, nuevo + despues);
        ta.selectionStart = ta.selectionEnd = nuevo.length;
        ta.dispatchEvent(new Event('input', { bubbles: true }));
        ta.dispatchEvent(new Event('change', { bubbles: true }));
        // Destello del halo: la frase quedo escrita.
        if (halo && activo && activo.ta === ta) {
            halo.classList.remove('is-flash');
            void halo.offsetWidth;
            halo.classList.add('is-flash');
            clearTimeout(flashTimer);
            flashTimer = setTimeout(function () { halo.classList.remove('is-flash'); }, 600);
        }
    }

    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', iniciar); else iniciar();
    return { soportado: !!SR, detener: function () { detener(); }, refrescar: programar };
})();
