// propia-pdfthumb.js
// Genera una miniatura (dataURL) de la primera pagina de un PDF, o devuelve la imagen misma
// si el archivo es imagen. pdf.js se carga de forma PEREZOSA (solo la primera vez que se pide
// una miniatura de PDF), desde el asset local (offline, sin CDN en runtime).
window.propiaPdfThumb = (function () {
    var pdfjsReady = null;

    function ensurePdfjs() {
        if (pdfjsReady) return pdfjsReady;
        pdfjsReady = new Promise(function (resolve, reject) {
            if (window.pdfjsLib) { resolve(window.pdfjsLib); return; }
            var s = document.createElement('script');
            s.src = '/assets/libs/pdfjs/pdf.min.js';
            s.onload = function () {
                try {
                    window.pdfjsLib.GlobalWorkerOptions.workerSrc = '/assets/libs/pdfjs/pdf.worker.min.js';
                    resolve(window.pdfjsLib);
                } catch (e) { reject(e); }
            };
            s.onerror = function () { reject(new Error('No se pudo cargar pdf.js')); };
            document.head.appendChild(s);
        });
        return pdfjsReady;
    }

    function b64ToBytes(b64) {
        var bin = atob(b64);
        var len = bin.length;
        var bytes = new Uint8Array(len);
        for (var i = 0; i < len; i++) bytes[i] = bin.charCodeAt(i);
        return bytes;
    }

    // base64: contenido del archivo en base64 (sin prefijo data:)
    // mime: tipo declarado del archivo
    // maxW: ancho objetivo de la miniatura en px (alto se deriva del aspecto de la pagina)
    // Devuelve un dataURL (jpeg) o null si no se pudo (el .razor cae al icono).
    async function render(base64, mime, maxW) {
        maxW = maxW || 120;
        try {
            if (mime && mime.indexOf('image/') === 0) {
                // La propia imagen sirve de miniatura.
                return 'data:' + mime + ';base64,' + base64;
            }
            var pdfjs = await ensurePdfjs();
            var bytes = b64ToBytes(base64);
            var doc = await pdfjs.getDocument({ data: bytes }).promise;
            var page = await doc.getPage(1);
            var vp0 = page.getViewport({ scale: 1 });
            var scale = maxW / vp0.width;
            var vp = page.getViewport({ scale: scale });
            var canvas = document.createElement('canvas');
            canvas.width = Math.ceil(vp.width);
            canvas.height = Math.ceil(vp.height);
            var ctx = canvas.getContext('2d');
            ctx.fillStyle = '#ffffff';
            ctx.fillRect(0, 0, canvas.width, canvas.height);
            await page.render({ canvasContext: ctx, viewport: vp }).promise;
            var url = canvas.toDataURL('image/jpeg', 0.72);
            try { page.cleanup(); doc.cleanup(); doc.destroy(); } catch (e) { }
            return url;
        } catch (e) {
            if (window.console) console.warn('propiaPdfThumb: no se pudo generar miniatura', e);
            return null;
        }
    }

    // Extrae el TEXTO de la capa de texto del PDF (pdf.js, local, SIN OCR de IA). Devuelve null si no
    // es PDF o fallo; cadena vacia si el PDF no trae texto (escaneado como imagen).
    async function extraerTexto(base64, mime) {
        try {
            if (mime && mime.indexOf('image/') === 0) { return null; }   // imagen: no hay capa de texto
            var pdfjs = await ensurePdfjs();
            var bytes = b64ToBytes(base64);
            var doc = await pdfjs.getDocument({ data: bytes }).promise;
            var maxP = Math.min(doc.numPages, 15);
            var partes = [];
            for (var p = 1; p <= maxP; p++) {
                var page = await doc.getPage(p);
                var tc = await page.getTextContent();
                var linea = tc.items.map(function (it) { return it.str; }).join(' ');
                if (linea) { partes.push(linea); }
                try { page.cleanup(); } catch (e) { }
            }
            try { doc.cleanup(); doc.destroy(); } catch (e) { }
            var txt = partes.join('\n').replace(/[ \t]+/g, ' ').trim();
            return txt.length > 60000 ? txt.slice(0, 60000) : txt;
        } catch (e) {
            if (window.console) console.warn('propiaPdfThumb: no se pudo extraer texto', e);
            return null;
        }
    }

    // Paginas a leer de un PDF largo: las primeras (encabezado, clausulas) y las 2 ULTIMAS (firmas, anexos).
    function paginasALeer(total, max) {
        if (total <= max) { var t = []; for (var i = 1; i <= total; i++) t.push(i); return t; }
        var out = [];
        for (var p = 1; p <= max - 2; p++) out.push(p);
        out.push(total - 1, total);
        return out;
    }

    // Texto + total de paginas (Agente Documental: el servidor mide si el texto es "pobre" = pocas palabras por
    // pagina y entonces lee con vision). Lee las primeras 13 y las 2 ultimas paginas. null si falla.
    async function extraerTextoInfo(base64, mime) {
        try {
            if (mime && mime.indexOf('image/') === 0) { return null; }
            var pdfjs = await ensurePdfjs();
            var doc = await pdfjs.getDocument({ data: b64ToBytes(base64) }).promise;
            var total = doc.numPages, partes = [];
            var lista = paginasALeer(total, 15);
            for (var i = 0; i < lista.length; i++) {
                var page = await doc.getPage(lista[i]);
                var tc = await page.getTextContent();
                var linea = tc.items.map(function (it) { return it.str; }).join(' ');
                if (linea) { partes.push(linea); }
                try { page.cleanup(); } catch (e) { }
            }
            try { doc.cleanup(); doc.destroy(); } catch (e) { }
            var txt = partes.join('\n').replace(/[ \t]+/g, ' ').trim();
            return { texto: txt.length > 60000 ? txt.slice(0, 60000) : txt, paginas: total };
        } catch (e) {
            if (window.console) console.warn('propiaPdfThumb: no se pudo extraer texto', e);
            return null;
        }
    }

    // Rasteriza cada pagina del PDF a JPEG base64 (SIN el prefijo "data:...") para mandarlas a un modelo de
    // vision (DeepSeek). scale 2.0 ~= 150 DPI. Tope de paginas para acotar payload/tokens: en un PDF largo van
    // las primeras y las 2 ultimas (firmas/anexos). Devuelve { paginas: [b64...], total: <paginas del PDF> }
    // para avisar si se leyo parcial. {paginas:[],total:0} si falla.
    async function rasterizarPaginas(base64, maxPaginas) {
        maxPaginas = maxPaginas || 12;
        try {
            var pdfjs = await ensurePdfjs();
            var bytes = b64ToBytes(base64);
            var doc = await pdfjs.getDocument({ data: bytes }).promise;
            var total = doc.numPages;
            var lista = paginasALeer(total, maxPaginas);
            var out = [];
            for (var ix = 0; ix < lista.length; ix++) {
                var p = lista[ix];
                var page = await doc.getPage(p);
                var vp = page.getViewport({ scale: 2.0 });
                var canvas = document.createElement('canvas');
                canvas.width = Math.ceil(vp.width); canvas.height = Math.ceil(vp.height);
                var ctx = canvas.getContext('2d');
                ctx.fillStyle = '#ffffff'; ctx.fillRect(0, 0, canvas.width, canvas.height);
                await page.render({ canvasContext: ctx, viewport: vp }).promise;
                var dataUrl = canvas.toDataURL('image/jpeg', 0.72);
                var comma = dataUrl.indexOf(',');
                out.push(comma >= 0 ? dataUrl.slice(comma + 1) : dataUrl);   // solo el base64, sin "data:"
                try { page.cleanup(); } catch (e) { }
            }
            try { doc.cleanup(); doc.destroy(); } catch (e) { }
            return { paginas: out, total: total };
        } catch (e) { if (window.console) console.warn('propiaPdfThumb: no se pudo rasterizar', e); return { paginas: [], total: 0 }; }
    }

    return { render: render, extraerTexto: extraerTexto, extraerTextoInfo: extraerTextoInfo, rasterizarPaginas: rasterizarPaginas };
})();
