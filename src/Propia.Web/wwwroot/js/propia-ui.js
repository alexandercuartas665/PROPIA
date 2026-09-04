// =============================================================================
// PROPIA UI - control del theme (claro/oscuro) y sidebar.
// Inspirado en la plantilla NexLink (assets/js/main.js + appSettings.js).
// Persistencia: localStorage como cache + endpoint /api/preferences/theme
// para sincronizar entre dispositivos del mismo usuario.
// =============================================================================

(function () {
    'use strict';

    var docEl = document.documentElement;
    var STORAGE_KEY = 'propia_theme';

    // ---------- Theme ----------

    function getStoredTheme() {
        try { return localStorage.getItem(STORAGE_KEY); } catch (e) { return null; }
    }

    function setStoredTheme(theme) {
        try { localStorage.setItem(STORAGE_KEY, theme); } catch (e) { /* ignore */ }
    }

    function getPreferredTheme() {
        var stored = getStoredTheme();
        if (stored === 'dark' || stored === 'light') return stored;
        // D-01: sin preferencia guardada -> 'light' (no se hereda el tema del SO).
        return 'light';
    }

    function applyTheme(theme) {
        docEl.setAttribute('data-bs-theme', theme);
        var btns = document.querySelectorAll('.theme-btn');
        for (var i = 0; i < btns.length; i++) {
            if (theme === 'dark') btns[i].classList.add('active');
            else btns[i].classList.remove('active');
        }
    }

    // Persistencia en backend (si hay sesion). No bloqueante - localStorage es el cache local.
    function persistThemeServer(theme) {
        try {
            var jwt = sessionStorage.getItem('propia_jwt');
            if (!jwt) return;
            // El Web tiene IHttpClientFactory hacia propia-api, pero desde JS hacemos fetch directo
            // a la API. La URL la inferimos del meta "propia:api-base" si existe, sino llamamos
            // al mismo origen y dejamos que el proxy resuelva.
            var meta = document.querySelector('meta[name="propia-api-base"]');
            var base = meta ? meta.getAttribute('content') : '';
            var url = (base || '') + '/api/preferencias/ui-theme';
            fetch(url, {
                method: 'PUT',
                headers: { 'Content-Type': 'application/json', 'Authorization': 'Bearer ' + jwt },
                body: JSON.stringify({ theme: theme })
            }).catch(function () { /* ignorar - localStorage es el fallback */ });
        } catch (e) { /* ignorar */ }
    }

    // Sincronizar desde backend al cargar (si hay sesion)
    function pullThemeFromServer() {
        try {
            var jwt = sessionStorage.getItem('propia_jwt');
            if (!jwt) return;
            var meta = document.querySelector('meta[name="propia-api-base"]');
            var base = meta ? meta.getAttribute('content') : '';
            var url = (base || '') + '/api/preferencias/ui-theme';
            fetch(url, { headers: { 'Authorization': 'Bearer ' + jwt } })
                .then(function (r) { return r.ok ? r.json() : null; })
                .then(function (data) {
                    if (data && (data.theme === 'light' || data.theme === 'dark')) {
                        applyTheme(data.theme);
                        setStoredTheme(data.theme);
                    }
                })
                .catch(function () { /* ignorar */ });
        } catch (e) { /* ignorar */ }
    }

    // API expuesta a Blazor: leer/setear/togglear el theme
    window.propiaUI = window.propiaUI || {};
    window.propiaUI.getTheme = function () {
        return docEl.getAttribute('data-bs-theme') || getPreferredTheme();
    };
    window.propiaUI.setTheme = function (theme) {
        if (theme !== 'dark' && theme !== 'light') return;
        applyTheme(theme);
        setStoredTheme(theme);
        persistThemeServer(theme);
    };
    window.propiaUI.toggleTheme = function () {
        var current = window.propiaUI.getTheme();
        var next = current === 'dark' ? 'light' : 'dark';
        window.propiaUI.setTheme(next);
        return next;
    };
    window.propiaUI.syncThemeFromServer = pullThemeFromServer;

    // ---------- Sidebar toggle (mobile/responsive) ----------

    // Aplica el "hueco" que el contenido y el header dejan para el sidebar de columna unica.
    // Se hace con estilos INLINE !important porque la plantilla NexLink trae reglas :has()
    // por-breakpoint (80px entre 1200-1480, etc.) que, en este motor, le ganan de forma anomala
    // a nuestras reglas :has()/planas del <style> del layout. El inline !important gana a todo.
    var SIDEBAR_W = 276;     // debe coincidir con --app-menubar-tabs del MainLayout
    var SIDEBAR_MINI = 72;   // riel de iconos en modo mini (debe coincidir con --pnav-w mini)
    function syncSidebarLayout() {
        try {
            var mode = docEl.getAttribute('data-app-sidebar');
            var mobile = window.innerWidth < 1200;
            var off = mobile ? '0px' : (mode === 'mini' ? (SIDEBAR_MINI + 'px') : (SIDEBAR_W + 'px'));
            var w = document.querySelector('.app-wrapper');
            var h = document.querySelector('.app-header');
            if (w) w.style.setProperty('margin-left', off, 'important');
            if (h) h.style.setProperty('padding-left', off, 'important');
        } catch (e) { }
    }

    // Re-aplica el estado guardado del sidebar (mini/full) al <html>. Idempotente: se llama en
    // cada rebind (incluido tras navegacion enhanced de Blazor) para no quedar desincronizado.
    function restoreSidebar() {
        try {
            var toggler = document.querySelector('.app-toggler');
            var saved = localStorage.getItem('propia_sidebar_mode');
            if (saved === 'mini' && window.innerWidth >= 1200) {
                docEl.setAttribute('data-app-sidebar', 'mini');
                if (toggler) toggler.classList.add('active');
            } else {
                docEl.setAttribute('data-app-sidebar', 'full');
                if (toggler) toggler.classList.remove('active');
            }
        } catch (e) { }
        syncSidebarLayout();
    }

    var _sidebarDelegated = false;
    // Toggle del sidebar via DELEGACION en document: un unico listener global que sobrevive a
    // cualquier reemplazo del DOM (navegacion enhanced de Blazor) y NO se puede duplicar. Esto
    // evita que el boton "abrir menu" se "congele" tras entrar a un modulo.
    function bindSidebarToggle() {
        if (_sidebarDelegated) return;
        _sidebarDelegated = true;
        document.addEventListener('click', function (e) {
            var toggler = e.target.closest ? e.target.closest('.app-toggler') : null;
            if (!toggler) return;
            e.preventDefault();
            var menubar = document.querySelector('.app-menubar-tabs');
            if (window.innerWidth >= 1200) {
                // Desktop: oculta/muestra todo el sidebar (modo "mini" = fuera de pantalla)
                var current = docEl.getAttribute('data-app-sidebar');
                var next = current === 'mini' ? 'full' : 'mini';
                docEl.setAttribute('data-app-sidebar', next);
                toggler.classList.toggle('active', next === 'mini');
                try { localStorage.setItem('propia_sidebar_mode', next); } catch (e2) { }
                syncSidebarLayout();
            } else {
                // Mobile/tablet: muestra/oculta el cajon
                if (menubar) menubar.classList.toggle('open');
                toggler.classList.toggle('active');
            }
        });
        // Reajustar el hueco del contenido cuando cambia el ancho de ventana (cruces de breakpoint).
        window.addEventListener('resize', function () { syncSidebarLayout(); });
    }

    var _searchHotkeyBound = false;
    // Atajo Cmd/Ctrl+K -> abre el buscador global del topbar (clic sobre [data-propia-search]).
    // Delegado en document e idempotente (sobrevive a la navegacion enhanced de Blazor).
    function bindSearchHotkey() {
        if (_searchHotkeyBound) return;
        _searchHotkeyBound = true;
        document.addEventListener('keydown', function (e) {
            var k = (e.key || '').toLowerCase();
            if (k === 'k' && (e.metaKey || e.ctrlKey)) {
                var trigger = document.querySelector('[data-propia-search]');
                if (trigger) { e.preventDefault(); trigger.click(); }
            }
        });
    }

    function bindThemeButtons() {
        var btns = document.querySelectorAll('.theme-btn');
        for (var i = 0; i < btns.length; i++) {
            if (btns[i].dataset.bound === '1') continue;
            btns[i].dataset.bound = '1';
            btns[i].addEventListener('click', function (e) {
                e.preventDefault();
                window.propiaUI.toggleTheme();
            });
        }
    }

    // ---------- Tabs del rail (sin Bootstrap) ----------
    // Click en .app-navbar-tabs .menu-link cambia tab-pane activo.

    function bindRailTabs() {
        // BUG FIX (validacion MCP 16/05/2026): el comportamiento previo guardaba el ultimo
        // tab clickeado en sessionStorage.propia_rail_tab y lo RESTAURABA en cada carga,
        // sobrescribiendo el tab activo que el server-side pone via URL en NavMenu.razor.
        // Ahora el server-side gobierna: la URL determina el tab activo. El click en el
        // rail solo permite "preview" visual de otro grupo sin navegar (el usuario clickea
        // un NavLink del panel para navegar realmente, y el tab se reajusta segun la URL
        // resultante en el siguiente render). Limpiamos cualquier valor previo para no
        // dejar la regresion latente.
        try { sessionStorage.removeItem('propia_rail_tab'); } catch (e) { }

        var links = document.querySelectorAll('.app-navbar-tabs .menu-link[data-tab-target]');
        for (var i = 0; i < links.length; i++) {
            if (links[i].dataset.bound === '1') continue;
            links[i].dataset.bound = '1';
            links[i].addEventListener('click', function (e) {
                e.preventDefault();
                var target = this.getAttribute('data-tab-target');
                if (!target) return;
                var prevActiveLink = document.querySelector('.app-navbar-tabs .menu-link.active');
                if (prevActiveLink) prevActiveLink.classList.remove('active');
                this.classList.add('active');
                var panes = document.querySelectorAll('.tab-content .tab-pane');
                for (var j = 0; j < panes.length; j++) {
                    panes[j].classList.remove('show');
                    panes[j].classList.remove('active');
                }
                var pane = document.getElementById(target);
                if (pane) {
                    pane.classList.add('show');
                    pane.classList.add('active');
                }
                // No persistimos en sessionStorage: la siguiente navegacion debe usar
                // la URL como fuente de verdad (NavMenu.razor ResolveTab(Nav.Uri)).
            });
        }
    }

    // ---------- Inicializacion ----------

    function init() {
        applyTheme(getPreferredTheme());
        bindSidebarToggle();
        bindSearchHotkey();
        restoreSidebar();
        bindThemeButtons();
        bindRailTabs();
        syncAuthCta();
        bindLogout();
        pullThemeFromServer();  // si hay sesion, sincroniza desde backend (override local)
    }

    // Aplicar theme ASAP (antes de DOMContentLoaded para evitar flash)
    applyTheme(getPreferredTheme());

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }

    // Blazor "enhanced navigation" reemplaza el body sin recargar la pagina y NO dispara
    // DOMContentLoaded. Por eso el header (CTA Ingresar/Salir) y el sidebar quedaban
    // desincronizados tras cada navegacion (se veia "Ingresar" como si se deslogueara).
    // Re-sincronizamos en cada enhancedload.
    document.addEventListener('enhancedload', function () {
        try { window.propiaUI.rebind(); } catch (e) { }
    });

    // Sincroniza el CTA del header (Ingresar vs nombre copropiedad + Salir) con el
    // estado de sesion en sessionStorage. Es JS puro porque MainLayout es SSR puro
    // y no hidrata interactivamente con Blazor.
    function syncAuthCta() {
        try {
            var jwt = sessionStorage.getItem('propia_jwt');
            var nombre = sessionStorage.getItem('propia_copropiedad_nombre') || '';
            // Sincronizamos el data attribute global asi el CSS pre-resuelve el CTA
            // incluso en navegaciones SPA donde el script inline solo corre una vez.
            try { document.documentElement.setAttribute('data-propia-auth', jwt ? 'in' : 'out'); } catch (e) { }
            var cta = document.querySelector('[data-propia-cta]');
            if (!cta) return;
            var login = cta.querySelector('[data-propia-cta-login]');
            var logout = cta.querySelector('[data-propia-cta-logout]');
            var copropiedadEl = cta.querySelector('[data-propia-cta-copropiedad]');
            var copropiedadNombre = cta.querySelector('[data-propia-cta-copropiedad-nombre]');
            if (jwt) {
                if (login) login.style.display = 'none';
                if (logout) logout.style.display = '';
                if (copropiedadEl) {
                    copropiedadEl.style.display = nombre ? '' : 'none';
                    if (copropiedadNombre && nombre) copropiedadNombre.textContent = nombre;
                }
            } else {
                if (login) login.style.display = '';
                if (logout) logout.style.display = 'none';
                if (copropiedadEl) copropiedadEl.style.display = 'none';
            }
        } catch (e) { /* sin sessionStorage = solo CTA Ingresar */ }
    }

    function bindLogout() {
        var btn = document.querySelector('[data-propia-cta-logout]');
        if (!btn || btn._propiaLogoutBound) return;
        btn._propiaLogoutBound = true;
        btn.addEventListener('click', function (e) {
            e.preventDefault();
            try {
                if (window.propiaAuth && window.propiaAuth.clear) {
                    window.propiaAuth.clear();
                } else {
                    sessionStorage.removeItem('propia_jwt');
                    sessionStorage.removeItem('propia_tenant_id');
                    sessionStorage.removeItem('propia_copropiedad_nombre');
                    localStorage.removeItem('propia_jwt');
                    localStorage.removeItem('propia_tenant_id');
                    localStorage.removeItem('propia_copropiedad_nombre');
                }
            } catch (err) { }
            window.location.href = '/login';
        });
    }

    // Blazor: cada vez que se renderiza una pagina (Server-side interactive)
    // los bindings deben re-aplicarse porque algunos elementos pueden recrearse.
    window.propiaUI.rebind = function () {
        bindSidebarToggle();
        bindSearchHotkey();
        restoreSidebar();
        bindThemeButtons();
        bindRailTabs();
        syncAuthCta();
        bindLogout();
    };

    // Expuesto para que login.razor lo llame justo despues de setItem('propia_jwt').
    window.propiaUI.refreshAuthCta = syncAuthCta;

    // Toolbar de formato (estilo WhatsApp) para textareas: envuelve la seleccion con
    // los marcadores (before/after), o los inserta en el cursor si no hay seleccion.
    // Luego dispara 'input' para que el @bind de Blazor capture el nuevo valor.
    window.propiaUI.wrapField = function (id, before, after) {
        var el = document.getElementById(id);
        if (!el) return;
        var start = el.selectionStart, end = el.selectionEnd, v = el.value;
        var sel = v.substring(start, end);
        var ins = before + (sel || '') + (after || '');
        el.value = v.substring(0, start) + ins + v.substring(end);
        var pos = sel ? start + ins.length : start + before.length;
        el.focus();
        try { el.setSelectionRange(pos, pos); } catch (e) { }
        el.dispatchEvent(new Event('input', { bubbles: true }));
    };

    // Enfoca el primer elemento que coincida con el selector. Lo usan las filas de alta
    // inline que se recrean con @key tras crear (Blazor pierde el foco al recrear el DOM).
    window.propiaUI.focusBySelector = function (selector) {
        try { var el = document.querySelector(selector); if (el) el.focus(); } catch (e) { }
    };

    // Scrollea un contenedor (por selector) hasta el fondo. Lo usa el chat de actividad de Tareas
    // para dejar visible el ultimo mensaje al abrir la tarjeta o tras enviar/adjuntar.
    // Reintenta unas veces porque el contenido (imagenes/PDF embebidos) cambia de alto al cargar
    // asincronicamente, y un unico scroll quedaria a mitad.
    window.propiaUI.scrollBottom = function (selector) {
        var delays = [0, 60, 180, 400, 800];
        delays.forEach(function (d) {
            setTimeout(function () {
                try { var el = document.querySelector(selector); if (el) el.scrollTop = el.scrollHeight; } catch (e) { }
            }, d);
        });
    };

    // Helper para descargar bytes desde base64 (modulo 2.15 Documentos).
    window.propiaUI.downloadBase64 = function (filename, mime, base64) {
        var byteCharacters = atob(base64);
        var byteNumbers = new Array(byteCharacters.length);
        for (var i = 0; i < byteCharacters.length; i++) {
            byteNumbers[i] = byteCharacters.charCodeAt(i);
        }
        var byteArray = new Uint8Array(byteNumbers);
        var blob = new Blob([byteArray], { type: mime || 'application/octet-stream' });
        var url = URL.createObjectURL(blob);
        var a = document.createElement('a');
        a.href = url;
        a.download = filename || 'documento';
        document.body.appendChild(a);
        a.click();
        setTimeout(function () { URL.revokeObjectURL(url); document.body.removeChild(a); }, 100);
    };

    // -------------------------------------------------------------------------
    // Scroll horizontal superior espejo (vista Lista de Tareas): una barra arriba
    // sincronizada con el scroll real de la tabla, para no tener que bajar hasta
    // el pie para desplazarse cuando hay muchas columnas.
    // -------------------------------------------------------------------------
    window.propiaUI.syncTopScroll = function (topId, innerId, listId) {
        var top = document.getElementById(topId);
        var inner = document.getElementById(innerId);
        var list = document.getElementById(listId);
        if (!top || !inner || !list) return;

        function syncWidth() { inner.style.width = list.scrollWidth + 'px'; }

        if (top.__ptSynced) { syncWidth(); return; }
        top.__ptSynced = true;

        var lock = false;
        top.addEventListener('scroll', function () {
            if (lock) return; lock = true; list.scrollLeft = top.scrollLeft; lock = false;
        });
        list.addEventListener('scroll', function () {
            if (lock) return; lock = true; top.scrollLeft = list.scrollLeft; lock = false;
        });

        syncWidth();
        if (window.ResizeObserver) {
            var ro = new ResizeObserver(function () { syncWidth(); });
            ro.observe(list);
            if (list.firstElementChild) ro.observe(list.firstElementChild);
        }
        window.addEventListener('resize', syncWidth);
    };

    // -------------------------------------------------------------------------
    // Tooltip del rail de iconos (titulo + descripcion de lo que hace el modulo).
    //
    // No se usa el de Bootstrap: su init vive en assets/js/main.js, que esta app no
    // carga, y ademas solo admite texto plano. Tampoco sirve un tooltip CSS dentro
    // del <li>: el rail es un contenedor con scroll (simplebar) y lo recortaria.
    // Por eso el globo se crea UNA vez en el <body> y se posiciona a mano.
    //
    // Delegacion en document: los items del rail los pinta Blazor y se re-renderizan,
    // asi que enganchar un listener a cada uno se perderia en el siguiente render.
    // -------------------------------------------------------------------------
    var tipEl = null;

    function asegurarTip() {
        if (tipEl && document.body.contains(tipEl)) return tipEl;
        tipEl = document.createElement('div');
        tipEl.className = 'propia-railtip';
        tipEl.innerHTML = '<div class="propia-railtip-t"></div><div class="propia-railtip-d"></div>';
        document.body.appendChild(tipEl);
        return tipEl;
    }

    function mostrarTip(target) {
        var titulo = target.getAttribute('data-tip-title');
        if (!titulo) return;
        var el = asegurarTip();
        el.querySelector('.propia-railtip-t').textContent = titulo;
        el.querySelector('.propia-railtip-d').textContent = target.getAttribute('data-tip-desc') || '';

        // Se muestra invisible primero para poder medir el alto real y centrarlo.
        el.style.visibility = 'hidden';
        el.classList.add('is-on');

        var r = target.getBoundingClientRect();
        var top = r.top + (r.height / 2) - (el.offsetHeight / 2);
        // Que no se salga por arriba ni por abajo de la ventana.
        top = Math.max(8, Math.min(top, window.innerHeight - el.offsetHeight - 8));
        el.style.top = top + 'px';
        el.style.left = (r.right + 12) + 'px';
        el.style.visibility = '';
    }

    function ocultarTip() {
        if (tipEl) tipEl.classList.remove('is-on');
    }

    document.addEventListener('mouseover', function (e) {
        var t = e.target && e.target.closest ? e.target.closest('[data-tip-title]') : null;
        if (t) mostrarTip(t);
    });
    document.addEventListener('mouseout', function (e) {
        var t = e.target && e.target.closest ? e.target.closest('[data-tip-title]') : null;
        if (t) ocultarTip();
    });
    // Al hacer click se navega: el globo no debe quedar flotando sobre la pantalla nueva.
    document.addEventListener('click', ocultarTip);
    window.addEventListener('scroll', ocultarTip, true);
})();

// Alto del viewport, para que los popover anclados decidan si abren hacia abajo o arriba.
window.propiaViewportH = function () { return window.innerHeight || document.documentElement.clientHeight || 0; };

// Sincroniza una barra de scroll horizontal "fantasma" (arriba) con el contenedor real de la
// tabla (abajo). Idempotente: se puede llamar en cada render sin duplicar listeners.
window.propiaSyncScroll = function (topSel, bodySel) {
    try {
        var top = document.querySelector(topSel);
        var body = document.querySelector(bodySel);
        if (!top || !body) return;
        var inner = top.firstElementChild;
        var tabla = body.querySelector('table');
        var w = (tabla ? tabla.scrollWidth : body.scrollWidth);
        if (inner) inner.style.width = w + 'px';
        var syncing = false;
        top.onscroll = function () { if (syncing) return; syncing = true; body.scrollLeft = top.scrollLeft; syncing = false; };
        body.onscroll = function () { if (syncing) return; syncing = true; top.scrollLeft = body.scrollLeft; syncing = false; };
    } catch (e) { /* no-op */ }
};
