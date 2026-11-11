/* =============================================================================
   PROPIA - Carga de documentos con IA (.iai-*): interop de los componentes Shared/IaIngesta.
   Port de la replica (replica/js/rp-ia-ingesta.js: RP.iaIngesta.wireDrop y resaltar; PROPUESTAS.md seccion 4).
     wireDrop(el)                   arrastrar archivos sobre TODO el modal muestra el velo "Suelta para que la IA
                                    los lea"; al soltar, los archivos pasan al <input type=file data-iai-input>
                                    visible del modal (el InputFile de Blazor) y se dispara 'change': Blazor los
                                    recibe como si se hubieran elegido. Sin input habilitado (otra pestana, lectura
                                    en curso) no se intercepta nada.
     resaltar(el, claves, dudosas)  ilumina EN SECUENCIA (cada 110 ms) los campos [data-iai="clave"] que lleno la
                                    IA; los dudosos con iai-fill--rev.
   ============================================================================= */
window.propiaIai = (function () {
    'use strict';

    function inputDe(el) { return el.querySelector('input[type=file][data-iai-input]:not([disabled])'); }

    function wireDrop(el) {
        if (!el || el.__iaiDrop) return; el.__iaiDrop = true;
        var velo = null, n = 0;
        function tieneArchivos(e) { var t = e.dataTransfer && e.dataTransfer.types; return t && [].indexOf.call(t, 'Files') >= 0 && !!inputDe(el); }
        function quitar() { n = 0; if (velo) { velo.remove(); velo = null; } }
        el.addEventListener('dragenter', function (e) {
            if (!tieneArchivos(e)) return; e.preventDefault(); n++;
            if (!velo) {
                velo = document.createElement('div');
                velo.className = 'iai-velo';
                velo.innerHTML = '<div><i class="fi fi-rr-cloud-upload"></i><b>Suelta para que la IA los lea</b><span>PDF de contratos o pólizas, uno o varios</span></div>';
                el.appendChild(velo);
            }
        });
        el.addEventListener('dragover', function (e) { if (tieneArchivos(e)) e.preventDefault(); });
        el.addEventListener('dragleave', function (e) { if (!tieneArchivos(e)) return; if (--n <= 0) quitar(); });
        el.addEventListener('drop', function (e) {
            if (!tieneArchivos(e)) return; e.preventDefault(); quitar();
            var input = inputDe(el), fs = [].slice.call(e.dataTransfer.files || []);
            if (!input || !fs.length) return;
            var dt = new DataTransfer();
            (input.multiple ? fs : fs.slice(0, 1)).forEach(function (f) { dt.items.add(f); });
            input.files = dt.files;
            input.dispatchEvent(new Event('change', { bubbles: true }));
        });
    }

    function resaltar(el, claves, dudosas) {
        if (!el) return;
        var n = 0;
        (claves || []).forEach(function (k) {
            var c = el.querySelector('[data-iai="' + k + '"]'); if (!c) return;
            var dud = (dudosas || []).indexOf(k) >= 0;
            c.classList.remove('iai-fill', 'iai-fill--rev'); void c.offsetWidth;
            c.style.animationDelay = (n++ * 110) + 'ms';
            c.classList.add(dud ? 'iai-fill--rev' : 'iai-fill');
        });
    }

    return { wireDrop: wireDrop, resaltar: resaltar };
})();
