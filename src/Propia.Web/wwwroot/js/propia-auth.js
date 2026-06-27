/* PROPIA - capa de sesion del cliente.
 * - Persiste el JWT y datos del tenant en localStorage (sobreviven al cierre de la pestaña).
 * - Hidrata sessionStorage al cargar la pagina para que el codigo legacy de las paginas
 *   (que lee sessionStorage directamente) siga funcionando sin reescribir.
 * - Expone propiaAuth.save/clear/get/refresh para nuevos puntos de uso.
 * - propiaAuth.refresh llama POST /connect/refresh con el JWT actual y actualiza los stores.
 * - guardSession redirige a /login si NO hay sesion o si el JWT esta VENCIDO y no se puede
 *   refrescar. propiaAuth.startAutoRefresh mantiene la sesion viva (refresh deslizante) mientras
 *   el tab este abierto, evitando que las paginas muestren modulos vacios por un 401 silencioso.
 */
(function () {
    var KEYS = [
        'propia_jwt',
        'propia_tenant_id',
        'propia_copropiedad_nombre',
        'propia_admin_jwt'
    ];

    function hidratarDesdeLocal() {
        try {
            KEYS.forEach(function (k) {
                if (!sessionStorage.getItem(k)) {
                    var v = localStorage.getItem(k);
                    if (v) sessionStorage.setItem(k, v);
                }
            });
        } catch (e) { }
    }

    function escribirEnAmbos(k, v) {
        try {
            if (v === null || v === undefined || v === '') {
                sessionStorage.removeItem(k);
                localStorage.removeItem(k);
            } else {
                sessionStorage.setItem(k, v);
                localStorage.setItem(k, v);
            }
        } catch (e) { }
    }

    function leerCualquiera(k) {
        try {
            return sessionStorage.getItem(k) || localStorage.getItem(k);
        } catch (e) { return null; }
    }

    function apiBase() {
        try {
            var m = document.querySelector('meta[name="propia-api-base"]');
            return (m && m.content) ? m.content : '';
        } catch (e) { return ''; }
    }

    /** Segundos hasta el exp del JWT dado (negativo si ya vencio), o null si no se puede parsear. */
    function secsLeft(token) {
        if (!token) return null;
        try {
            var parts = token.split('.');
            if (parts.length !== 3) return null;
            var payload = JSON.parse(atob(parts[1].replace(/-/g, '+').replace(/_/g, '/')));
            if (!payload.exp) return null;
            return payload.exp - Math.floor(Date.now() / 1000);
        } catch (e) { return null; }
    }

    // Hidratar inmediatamente para que la guard de App.razor lea la sesion persistida.
    hidratarDesdeLocal();

    var autoRefreshTimer = null;

    window.propiaAuth = {
        /** Guarda JWT + tenant + nombre de copropiedad en ambos stores. null/undefined = no toca esa key.
         *  Para limpiar usa propiaAuth.clear(). */
        save: function (token, tenantId, coproNombre) {
            if (token != null) escribirEnAmbos('propia_jwt', token);
            if (tenantId != null) escribirEnAmbos('propia_tenant_id', tenantId);
            if (coproNombre != null) escribirEnAmbos('propia_copropiedad_nombre', coproNombre);
            try { if (token != null) document.documentElement.setAttribute('data-propia-auth', token ? 'in' : 'out'); } catch (e) { }
        },
        /** Variante admin: guarda en propia_admin_jwt. */
        saveAdmin: function (token) {
            escribirEnAmbos('propia_admin_jwt', token);
        },
        /** Limpia todo. */
        clear: function () {
            KEYS.forEach(function (k) { escribirEnAmbos(k, ''); });
            try { document.documentElement.setAttribute('data-propia-auth', 'out'); } catch (e) { }
        },
        /** Lee de sessionStorage primero, luego localStorage. */
        get: function (k) { return leerCualquiera(k); },
        /** Llama POST /connect/refresh con el JWT actual; actualiza stores si OK. Devuelve nuevo token o null. */
        refresh: async function () {
            var token = leerCualquiera('propia_jwt');
            if (!token) return null;
            try {
                var resp = await fetch(apiBase() + '/connect/refresh', {
                    method: 'POST',
                    headers: { 'Authorization': 'Bearer ' + token, 'Accept': 'application/json' }
                });
                if (!resp.ok) return null;
                var data = await resp.json();
                if (!data || !data.accessToken) return null;
                window.propiaAuth.save(
                    data.accessToken,
                    data.activeTenantId || undefined,
                    undefined);
                return data.accessToken;
            } catch (e) { return null; }
        },
        /** Devuelve el numero de segundos hasta exp del JWT actual, o null si no se puede parsear. */
        expiresIn: function () {
            return secsLeft(leerCualquiera('propia_jwt'));
        },
        /** True si hay JWT de cliente y ya vencio (con 5s de skew). */
        isExpired: function () {
            var s = secsLeft(leerCualquiera('propia_jwt'));
            return s !== null && s <= 5;
        },
        /** Mantiene la sesion viva: refresca el JWT de cliente ~2 min antes de su exp mientras el
         *  tab este abierto. Si el token ya vencio y el refresh falla (sliding window agotado),
         *  limpia la sesion y redirige a /login. Idempotente: si ya hay timer corriendo, no crea otro. */
        startAutoRefresh: function () {
            if (autoRefreshTimer) return;
            autoRefreshTimer = setInterval(async function () {
                try {
                    var left = window.propiaAuth.expiresIn();
                    if (left === null) return; // no hay JWT de cliente (p.ej. sesion admin)
                    if (left >= 120) return;   // todavia hay margen
                    var nuevo = await window.propiaAuth.refresh();
                    if (!nuevo && left <= 0) {
                        // Vencido y el sliding window ya no permite refresh -> cerrar sesion.
                        clearInterval(autoRefreshTimer); autoRefreshTimer = null;
                        window.propiaAuth.clear();
                        var path = (window.location.pathname || '/').toLowerCase();
                        if (!rutaEsPublica(path)) redirectLogin();
                    }
                } catch (e) { /* nunca romper por el auto-refresh */ }
            }, 60000); // cada 60s
        }
    };

    /* Guard de ruta: si la pagina actual requiere sesion y no hay JWT (o el JWT esta vencido y no
     * se puede refrescar), redirige a /login con returnUrl. Evita que las paginas de Capa 2
     * muestren modulos vacios o el banner "Sesion expirada" cuando la sesion ya no sirve. */
    function rutaEsPublica(path) {
        var publics = ['/', '/login', '/registro', '/signin-google', '/operador/info', '/_probe'];
        // /onboarding y /onboarding/continuar requieren JWT temporal del registro: NO publicas.
        if (publics.indexOf(path) !== -1) return true;
        if (path.indexOf('/invitacion/') === 0) return true; // links de invitacion publicos
        if (path.indexOf('/c/') === 0) return true;           // comunicados publicos
        if (path.indexOf('/_dev/') === 0) return true;        // paginas de desarrollo
        return false;
    }
    function redirectLogin() {
        var qs = window.location.pathname + (window.location.search || '');
        if (!qs || qs === '/') qs = (window.location.pathname || '/').toLowerCase();
        window.location.replace('/login?returnUrl=' + encodeURIComponent(qs));
    }
    async function guardSession() {
        try {
            var path = (window.location.pathname || '/').toLowerCase();
            if (rutaEsPublica(path)) return;
            var jwt = leerCualquiera('propia_jwt');
            var adminJwt = leerCualquiera('propia_admin_jwt');
            // Sin ninguna sesion (cliente ni super admin): a login de inmediato.
            if (!jwt && !adminJwt) { redirectLogin(); return; }
            // JWT de cliente VENCIDO: intentar refresh sliding; si falla, limpiar y a login.
            // (El acceso fino y las rutas de super admin con propia_admin_jwt los resuelve el API.)
            if (jwt && secsLeft(jwt) !== null && secsLeft(jwt) <= 5) {
                var nuevo = await window.propiaAuth.refresh();
                if (!nuevo) {
                    // Si solo habia sesion de cliente vencida, cerrar y redirigir.
                    if (!adminJwt) { window.propiaAuth.clear(); redirectLogin(); return; }
                }
            }
            // Sesion viva: arrancar el refresh deslizante para que no expire con el tab abierto.
            window.propiaAuth.startAutoRefresh();
        } catch (e) { /* nunca romper la pagina por el guard */ }
    }
    guardSession();
})();
