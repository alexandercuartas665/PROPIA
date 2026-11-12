/* Sala virtual de asambleas (ola 5): cliente LiveKit. El servidor de PROPIA solo firma el token; el audio y el video viajan
   navegador <-> LiveKit. El mosaico lo pinta este modulo dentro de un contenedor vacio que Blazor no toca; a .NET solo
   suben eventos (participantes, chat, palabra, avisos). Manos, palabra, chat y "silenciar a todos" van por el canal de datos.
   Sin eval (CSP). La libreria se carga bajo demanda desde jsdelivr con version fija. */
window.propiaSala = (function () {
    var LK_URL = 'https://cdn.jsdelivr.net/npm/livekit-client@2.5.7/dist/livekit-client.umd.js';
    var MAX_TILES = 24;
    var cargando = null, room = null, ref = null, grid = null, audios = null, timer = null;
    var tiles = {}, manos = {}, palabra = null, moderador = false;

    function cargar() {
        if (window.LivekitClient) return Promise.resolve();
        if (cargando) return cargando;
        cargando = new Promise(function (ok, err) {
            var s = document.createElement('script');
            s.src = LK_URL; s.crossOrigin = 'anonymous';
            s.onload = function () { ok(); };
            s.onerror = function () { cargando = null; err(new Error('No se pudo cargar el cliente de video.')); };
            document.head.appendChild(s);
        });
        return cargando;
    }
    function ini(n) { return String(n || '?').trim().split(/\s+/).slice(0, 2).map(function (x) { return x.charAt(0); }).join('').toUpperCase(); }
    function nombreDe(p) { return p.name || p.identity; }
    function esMod(p) { try { return !!JSON.parse(p.metadata || '{}').moderador; } catch (e) { return false; } }
    function invocar(m) { if (!ref) return; var a = Array.prototype.slice.call(arguments, 1); try { ref.invokeMethodAsync.apply(ref, [m].concat(a)).catch(function () { }); } catch (e) { } }
    function enviar(obj, destinos) {
        if (!room) return;
        try {
            var data = new TextEncoder().encode(JSON.stringify(obj));
            room.localParticipant.publishData(data, destinos ? { reliable: true, destinationIdentities: destinos } : { reliable: true });
        } catch (e) { }
    }
    function pub(p, fuente) {
        var LK = window.LivekitClient, r = null;
        p.trackPublications.forEach(function (t) { if (t.source === fuente) r = t; });
        return r && LK ? r : null;
    }
    function todos() {
        if (!room) return [];
        var r = [room.localParticipant];
        room.remoteParticipants.forEach(function (p) { r.push(p); });
        return r;
    }
    function estado(p) {
        var LK = window.LivekitClient, S = LK.Track.Source;
        var mic = pub(p, S.Microphone), cam = pub(p, S.Camera), pan = pub(p, S.ScreenShare);
        return {
            id: p.identity, nombre: nombreDe(p), yo: p === room.localParticipant, mod: esMod(p),
            mic: !!(mic && !mic.isMuted), cam: !!(cam && !cam.isMuted && cam.track), pantalla: !!(pan && pan.track),
            habla: !!p.isSpeaking, mano: !!manos[p.identity]
        };
    }

    // ---------- mosaico ----------
    function programar() { if (timer) return; timer = setTimeout(function () { timer = null; pintar(); }, 60); }
    function tileDe(clave) {
        var t = tiles[clave];
        if (t) return t;
        var el = document.createElement('div'); el.className = 'asm-tile';
        var av = document.createElement('div'); av.className = 'asm-tile-av';
        var video = document.createElement('video'); video.className = 'asm-tile-video'; video.autoplay = true; video.playsInline = true; video.muted = true; video.style.display = 'none';
        var mano = document.createElement('span'); mano.className = 'asm-tile-mano'; mano.title = 'Pidió la palabra'; mano.innerHTML = '<i class="fi fi-rr-hand"></i>'; mano.style.display = 'none';
        var pie = document.createElement('div'); pie.className = 'asm-tile-pie';
        var ic = document.createElement('i'); var lb = document.createElement('span');
        pie.appendChild(ic); pie.appendChild(lb);
        el.appendChild(av); el.appendChild(video); el.appendChild(mano); el.appendChild(pie);
        t = tiles[clave] = { el: el, av: av, video: video, mano: mano, ic: ic, lb: lb, track: null };
        return t;
    }
    function ponerTrack(t, track, espejo, contener) {
        if (t.track !== track) {
            if (t.track) { try { t.track.detach(t.video); } catch (e) { } }
            t.track = track;
            if (track) track.attach(t.video);
        }
        t.video.style.display = track ? '' : 'none';
        t.av.style.display = track ? 'none' : '';
        t.video.style.transform = espejo ? '' : 'none';
        t.video.style.objectFit = contener ? 'contain' : '';
    }
    function pintar() {
        if (!room || !grid) return;
        var LK = window.LivekitClient, S = LK.Track.Source;
        var ps = todos(), lista = ps.map(estado);
        // Prioridad del mosaico: yo, pantallas compartidas, quien habla, camaras encendidas, el resto.
        var orden = ps.map(function (p, i) { return { p: p, e: lista[i] }; });
        orden.sort(function (a, b) {
            function peso(x) { return (x.e.yo ? 1000 : 0) + (x.e.pantalla ? 500 : 0) + (x.e.habla ? 200 : 0) + (x.e.mano ? 100 : 0) + (x.e.cam ? 50 : 0) + (x.e.mod ? 20 : 0); }
            return peso(b) - peso(a);
        });
        var vivos = {}, nodos = [];
        orden.slice(0, MAX_TILES).forEach(function (o) {
            var p = o.p, e = o.e;
            if (e.pantalla) {
                var kp = p.identity + '#pantalla', tp = tileDe(kp); vivos[kp] = 1;
                ponerTrack(tp, pub(p, S.ScreenShare).track, false, true);
                tp.el.className = 'asm-tile asm-tile--pantalla';
                tp.av.textContent = ini(e.nombre); tp.ic.className = 'fi fi-rr-screen'; tp.lb.textContent = 'Pantalla de ' + e.nombre;
                nodos.push(tp.el);
            }
            var t = tileDe(p.identity); vivos[p.identity] = 1;
            var cam = pub(p, S.Camera);
            ponerTrack(t, e.cam ? cam.track : null, e.yo, false);
            t.el.className = 'asm-tile' + (e.habla ? ' is-habla' : '') + (e.yo ? ' is-yo' : '') + (palabra === p.identity ? ' is-palabra' : '');
            t.av.textContent = ini(e.nombre);
            t.ic.className = 'fi fi-rr-' + (e.mic ? 'microphone' : 'microphone-slash is-off');
            t.lb.textContent = (e.yo ? 'Tú' : e.nombre) + (e.mod ? ' · Moderador' : '');
            t.mano.style.display = e.mano ? '' : 'none';
            nodos.push(t.el);
        });
        if (ps.length > MAX_TILES) {
            var tm = tileDe('#mas'); vivos['#mas'] = 1;
            tm.el.className = 'asm-tile asm-tile--mas'; tm.av.textContent = '+' + (ps.length - MAX_TILES); tm.ic.className = ''; tm.lb.textContent = 'más en la sala';
            nodos.push(tm.el);
        }
        Object.keys(tiles).forEach(function (k) {
            if (!vivos[k]) { var t = tiles[k]; if (t.track) { try { t.track.detach(t.video); } catch (e) { } } if (t.el.parentNode) t.el.parentNode.removeChild(t.el); delete tiles[k]; }
        });
        nodos.forEach(function (n, i) { if (grid.children[i] !== n) grid.insertBefore(n, grid.children[i] || null); });
        invocar('SalaParticipantes', JSON.stringify(lista));
    }

    // ---------- eventos ----------
    function alDato(payload, participante) {
        var m; try { m = JSON.parse(new TextDecoder().decode(payload)); } catch (e) { return; }
        if (!m || !participante) return;
        var de = participante.identity, nom = nombreDe(participante), deMod = esMod(participante);
        if (m.t === 'mano') { if (m.on) manos[de] = 1; else delete manos[de]; programar(); }
        else if (m.t === 'chat' && typeof m.texto === 'string') invocar('SalaChat', nom, m.texto.slice(0, 1000), false);
        else if (m.t === 'palabra' && deMod) {
            palabra = m.a || null;
            if (palabra && manos[palabra]) delete manos[palabra];
            if (room && palabra === room.localParticipant.identity) enviar({ t: 'mano', on: false });
            programar(); invocar('SalaPalabra', palabra || '');
        }
        else if (m.t === 'silenciar' && deMod && room && !moderador) {
            room.localParticipant.setMicrophoneEnabled(false).then(programar).catch(function () { });
            invocar('SalaAviso', 'El moderador silenció los micrófonos.'); invocar('SalaMicrofono', false);
        }
    }
    function cablear(LK) {
        var E = LK.RoomEvent;
        [E.ParticipantConnected, E.ParticipantDisconnected, E.TrackSubscribed, E.TrackUnsubscribed, E.TrackMuted, E.TrackUnmuted,
            E.LocalTrackPublished, E.LocalTrackUnpublished, E.ActiveSpeakersChanged, E.ParticipantNameChanged].forEach(function (ev) { room.on(ev, programar); });
        room.on(E.ParticipantConnected, function (p) {
            // Quien entra tarde no vio las manos levantadas: se le reenvia la propia y la palabra vigente.
            if (manos[room.localParticipant.identity]) enviar({ t: 'mano', on: true }, [p.identity]);
            if (moderador && palabra) enviar({ t: 'palabra', a: palabra }, [p.identity]);
        });
        room.on(E.ParticipantDisconnected, function (p) { delete manos[p.identity]; if (palabra === p.identity) { palabra = null; invocar('SalaPalabra', ''); } });
        room.on(E.TrackSubscribed, function (track) {
            if (track.kind === 'audio') { var a = track.attach(); a.setAttribute('data-sala-audio', '1'); audios.appendChild(a); }
        });
        room.on(E.TrackUnsubscribed, function (track) { if (track.kind === 'audio') track.detach().forEach(function (el) { if (el.parentNode) el.parentNode.removeChild(el); }); });
        room.on(E.DataReceived, alDato);
        room.on(E.Reconnecting, function () { invocar('SalaEstado', 'reconectando'); });
        room.on(E.Reconnected, function () { invocar('SalaEstado', 'conectado'); programar(); });
        room.on(E.Disconnected, function (motivo) { limpiar(); invocar('SalaEstado', 'desconectado:' + (motivo == null ? '' : motivo)); });
        room.on(E.AudioPlaybackStatusChanged, function () { if (room && !room.canPlaybackAudio) invocar('SalaAviso', 'Toca "Activar audio" para escuchar la sala.'); });
    }
    function limpiar() {
        Object.keys(tiles).forEach(function (k) { var t = tiles[k]; if (t.track) { try { t.track.detach(t.video); } catch (e) { } } if (t.el.parentNode) t.el.parentNode.removeChild(t.el); });
        tiles = {}; manos = {}; palabra = null;
        if (audios && audios.parentNode) audios.parentNode.removeChild(audios);
        audios = null; room = null; grid = null;
    }

    return {
        /* Conecta y publica. Devuelve 'ok', 'ok:sin-microfono' / 'ok:sin-camara' (sin permiso) o 'error:<mensaje>'. */
        conectar: async function (dotnetRef, gridId, url, token, conMic, conCam) {
            try {
                await cargar();
                if (room) { try { await room.disconnect(); } catch (e) { } limpiar(); }
                var LK = window.LivekitClient;
                ref = dotnetRef; grid = document.getElementById(gridId);
                audios = document.createElement('div'); audios.style.display = 'none'; document.body.appendChild(audios);
                room = new LK.Room({ adaptiveStream: true, dynacast: true });
                cablear(LK);
                await room.connect(url, token);
                moderador = esMod(room.localParticipant);
                var aviso = 'ok';
                try { await room.localParticipant.setMicrophoneEnabled(!!conMic); } catch (e) { aviso = 'ok:sin-microfono'; }
                if (conCam) { try { await room.localParticipant.setCameraEnabled(true); } catch (e) { if (aviso === 'ok') aviso = 'ok:sin-camara'; } }
                try { await room.startAudio(); } catch (e) { }
                pintar();
                return aviso;
            } catch (e) {
                if (room) { try { await room.disconnect(); } catch (e2) { } }
                limpiar();
                return 'error:' + (e && e.message ? e.message : e);
            }
        },
        /* Re-ubica el mosaico si Blazor volvio a crear el contenedor (cambio de pestana). */
        montar: function (gridId) {
            var g = document.getElementById(gridId);
            if (!g || !room) return false;
            if (g !== grid) { grid = g; Object.keys(tiles).forEach(function (k) { grid.appendChild(tiles[k].el); }); }
            pintar(); return true;
        },
        conectado: function () { return !!room; },
        microfono: async function (on) { if (!room) return false; try { await room.localParticipant.setMicrophoneEnabled(!!on); } catch (e) { return false; } programar(); return room.localParticipant.isMicrophoneEnabled; },
        camara: async function (on) { if (!room) return false; try { await room.localParticipant.setCameraEnabled(!!on); } catch (e) { return false; } programar(); return room.localParticipant.isCameraEnabled; },
        pantalla: async function (on) { if (!room) return false; try { await room.localParticipant.setScreenShareEnabled(!!on, { audio: false }); } catch (e) { return false; } programar(); return room.localParticipant.isScreenShareEnabled; },
        mano: function (on) { if (!room) return; if (on) manos[room.localParticipant.identity] = 1; else delete manos[room.localParticipant.identity]; enviar({ t: 'mano', on: !!on }); programar(); },
        chat: function (texto) { if (!room || !texto) return false; enviar({ t: 'chat', texto: String(texto).slice(0, 1000) }); return true; },
        darPalabra: function (identidad) { if (!room || !moderador) return; palabra = identidad || null; if (palabra) delete manos[palabra]; enviar({ t: 'palabra', a: palabra }); programar(); invocar('SalaPalabra', palabra || ''); },
        silenciarTodos: function () { if (room && moderador) enviar({ t: 'silenciar' }); },
        activarAudio: async function () { if (room) { try { await room.startAudio(); } catch (e) { } } },
        salir: async function () { if (!room) return; var r = room; try { await r.disconnect(); } catch (e) { } limpiar(); },
        soltar: function () { ref = null; }
    };
})();
