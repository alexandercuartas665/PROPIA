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
        return window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches
            ? 'dark' : 'light';
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

    function bindSidebarToggle() {
        var toggler = document.querySelector('.app-toggler');
        var menubar = document.querySelector('.app-menubar-tabs');
        if (!toggler || !menubar) return;
        if (toggler.dataset.bound === '1') return;
        toggler.dataset.bound = '1';
        // Restaurar estado guardado
        try {
            var saved = localStorage.getItem('propia_sidebar_mode');
            if (saved === 'mini' && window.innerWidth >= 1200) {
                docEl.setAttribute('data-app-sidebar', 'mini');
                toggler.classList.add('active');
            }
        } catch (e) { }
        toggler.addEventListener('click', function () {
            if (window.innerWidth >= 1200) {
                // Desktop: colapsa/expande el panel textual (modo "mini")
                var current = docEl.getAttribute('data-app-sidebar');
                var next = current === 'mini' ? 'full' : 'mini';
                docEl.setAttribute('data-app-sidebar', next);
                toggler.classList.toggle('active', next === 'mini');
                try { localStorage.setItem('propia_sidebar_mode', next); } catch (e) { }
            } else {
                // Mobile/tablet: muestra/oculta el cajon
                menubar.classList.toggle('open');
                toggler.classList.toggle('active');
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
                sessionStorage.removeItem('propia_jwt');
                sessionStorage.removeItem('propia_tenant_id');
                sessionStorage.removeItem('propia_copropiedad_nombre');
            } catch (err) { }
            window.location.href = '/login';
        });
    }

    // Blazor: cada vez que se renderiza una pagina (Server-side interactive)
    // los bindings deben re-aplicarse porque algunos elementos pueden recrearse.
    window.propiaUI.rebind = function () {
        bindSidebarToggle();
        bindThemeButtons();
        bindRailTabs();
        syncAuthCta();
        bindLogout();
    };

    // Expuesto para que login.razor lo llame justo despues de setItem('propia_jwt').
    window.propiaUI.refreshAuthCta = syncAuthCta;

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
})();
