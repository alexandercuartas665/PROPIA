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
        toggler.addEventListener('click', function () {
            menubar.classList.toggle('open');
            toggler.classList.toggle('active');
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
                try { sessionStorage.setItem('propia_rail_tab', target); } catch (e) { }
            });
        }
        // Restaurar tab activo entre navegaciones
        try {
            var saved = sessionStorage.getItem('propia_rail_tab');
            if (saved) {
                var link = document.querySelector('.app-navbar-tabs .menu-link[data-tab-target="' + saved + '"]');
                if (link) link.click();
            }
        } catch (e) { }
    }

    // ---------- Inicializacion ----------

    function init() {
        applyTheme(getPreferredTheme());
        bindSidebarToggle();
        bindThemeButtons();
        bindRailTabs();
        pullThemeFromServer();  // si hay sesion, sincroniza desde backend (override local)
    }

    // Aplicar theme ASAP (antes de DOMContentLoaded para evitar flash)
    applyTheme(getPreferredTheme());

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }

    // Blazor: cada vez que se renderiza una pagina (Server-side interactive)
    // los bindings deben re-aplicarse porque algunos elementos pueden recrearse.
    window.propiaUI.rebind = function () {
        bindSidebarToggle();
        bindThemeButtons();
        bindRailTabs();
    };
})();
