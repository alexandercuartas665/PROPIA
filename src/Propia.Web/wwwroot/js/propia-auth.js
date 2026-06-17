/* PROPIA - capa de sesion del cliente.
 * - Persiste el JWT y datos del tenant en localStorage (sobreviven al cierre de la pestaña).
 * - Hidrata sessionStorage al cargar la pagina para que el codigo legacy de las paginas
 *   (que lee sessionStorage directamente) siga funcionando sin reescribir.
 * - Expone propiaAuth.save/clear/get/refresh para nuevos puntos de uso.
 * - propiaAuth.refresh llama POST /connect/refresh con el JWT actual y actualiza los stores.
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

    // Hidratar inmediatamente para que la guard de App.razor lea la sesion persistida.
    hidratarDesdeLocal();

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
            var token = leerCualquiera('propia_jwt');
            if (!token) return null;
            try {
                var parts = token.split('.');
                if (parts.length !== 3) return null;
                var payload = JSON.parse(atob(parts[1].replace(/-/g, '+').replace(/_/g, '/')));
                if (!payload.exp) return null;
                return payload.exp - Math.floor(Date.now() / 1000);
            } catch (e) { return null; }
        }
    };
})();
