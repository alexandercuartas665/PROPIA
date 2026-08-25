// Helper minimo para usar TinyMCE (editor enriquecido) desde Blazor via JS interop.
// Build self-hosted (GPL) cargado por CDN; license_key:'gpl' evita el aviso de licencia.
window.propiaTiny = {
    async init(selector, initialHtml) {
        if (!window.tinymce) return false;
        const id = selector.replace('#', '');
        try { const prev = tinymce.get(id); if (prev) prev.remove(); } catch (e) { }
        try {
            await tinymce.init({
                selector: selector,
                menubar: false,
                height: 340,
                plugins: 'lists link autolink table',
                toolbar: 'undo redo | blocks | bold italic underline | bullist numlist | link table | removeformat',
                branding: false,
                promotion: false,
                license_key: 'gpl',
                content_style: "body{font-family:Inter,system-ui,Arial,sans-serif;font-size:14px;color:#1B2A3A;}",
                setup: function (ed) {
                    ed.on('init', function () { if (initialHtml) ed.setContent(initialHtml); });
                }
            });
            return true;
        } catch (e) { console.error('tiny init', e); return false; }
    },
    getContent(selector) {
        const id = selector.replace('#', '');
        try { const ed = window.tinymce && tinymce.get(id); return ed ? ed.getContent() : ''; }
        catch (e) { return ''; }
    },
    destroy(selector) {
        const id = selector.replace('#', '');
        try { const ed = window.tinymce && tinymce.get(id); if (ed) ed.remove(); } catch (e) { }
    }
};
