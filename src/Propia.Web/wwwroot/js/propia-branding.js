/* PROPIA - marca de plataforma (white-label).
 * Lee GET {apiBase}/branding (publico) y aplica el logo/icono configurado por el Super Admin
 * a los elementos marcados con data-brand-logo / data-brand-icon, mas el favicon.
 *
 * Diseno: las imagenes ya traen el asset PropIA por defecto como src estatico, asi que si la
 * marca no esta configurada (o el fetch falla) NO hay flash ni imagen rota: solo se sobrescribe
 * cuando la marca apunta a un logo distinto. Funciona en el login (sin sesion) y en todo el sistema,
 * independiente del render mode de Blazor.
 */
(function () {
    var CACHE_KEY = 'propia_branding';

    function apiBase() {
        try {
            var m = document.querySelector('meta[name="propia-api-base"]');
            return (m && m.content) ? m.content : '';
        } catch (e) { return ''; }
    }

    function aplicar(b) {
        if (!b) return;
        try {
            if (b.loginLogoUrl) {
                document.querySelectorAll('[data-brand-logo]').forEach(function (el) {
                    if (el.getAttribute('src') !== b.loginLogoUrl) el.setAttribute('src', b.loginLogoUrl);
                });
            }
            if (b.iconUrl) {
                document.querySelectorAll('[data-brand-icon]').forEach(function (el) {
                    if (el.getAttribute('src') !== b.iconUrl) el.setAttribute('src', b.iconUrl);
                });
                document.querySelectorAll('link[rel="icon"], link[rel="apple-touch-icon"]').forEach(function (el) {
                    el.setAttribute('href', b.iconUrl);
                });
            }
            if (b.platformName) {
                document.querySelectorAll('[data-brand-name]').forEach(function (el) {
                    el.textContent = b.platformName;
                });
            }
        } catch (e) { }
    }

    function aplicarDesdeCache() {
        try {
            var raw = sessionStorage.getItem(CACHE_KEY);
            if (raw) aplicar(JSON.parse(raw));
        } catch (e) { }
    }

    async function refrescar() {
        try {
            var resp = await fetch(apiBase() + '/branding', { headers: { 'Accept': 'application/json' } });
            if (!resp.ok) return;
            var b = await resp.json();
            try { sessionStorage.setItem(CACHE_KEY, JSON.stringify(b)); } catch (e) { }
            aplicar(b);
        } catch (e) { /* sin red / CORS: nos quedamos con el asset por defecto */ }
    }

    // 1) Pintar de inmediato lo cacheado (cero parpadeo entre navegaciones).
    aplicarDesdeCache();
    // 2) Refrescar contra el API.
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', refrescar);
    } else {
        refrescar();
    }

    // 3) Tras navegaciones "enhanced" de Blazor el DOM se reemplaza: re-aplicar la marca cacheada.
    function reg() {
        if (window.Blazor && window.Blazor.addEventListener) {
            window.Blazor.addEventListener('enhancedload', aplicarDesdeCache);
        } else {
            setTimeout(reg, 100);
        }
    }
    reg();

    window.propiaBranding = { refresh: refrescar, apply: aplicar };
})();
