// Helper para usar TinyMCE (editor enriquecido) desde Blazor via JS interop.
// Build self-hosted (GPL) cargado por CDN; license_key:'gpl' evita el aviso de licencia.
// Aspecto tipo "documento/carta": la hoja blanca centrada sobre un lienzo gris.
window.propiaTiny = {
    async init(selector, initialHtml) {
        if (!window.tinymce) return false;
        const id = selector.replace('#', '');
        try { const prev = tinymce.get(id); if (prev) prev.remove(); } catch (e) { }
        try {
            await tinymce.init({
                selector: selector,
                menubar: 'edit view format table',
                height: 560,
                plugins: 'lists link autolink table image charmap code fullscreen hr searchreplace wordcount visualblocks',
                toolbar: 'undo redo | blocks fontfamily fontsize | bold italic underline strikethrough | forecolor backcolor | ' +
                    'alignleft aligncenter alignright alignjustify | bullist numlist outdent indent | ' +
                    'link image table hr blockquote | removeformat | code fullscreen',
                toolbar_mode: 'wrap',
                branding: false,
                promotion: false,
                license_key: 'gpl',
                font_size_formats: '10px 11px 12px 14px 16px 18px 24px 30px 36px',
                // Hoja tipo carta (blanca, con margenes y sombra) sobre lienzo gris.
                content_style:
                    "html{background:#eceff3;} " +
                    "body{background:#fff; max-width:760px; margin:26px auto; padding:64px 72px; " +
                    "box-shadow:0 3px 16px rgba(27,42,58,.14); border-radius:2px; " +
                    "font-family:Georgia,'Times New Roman',serif; font-size:15px; line-height:1.6; color:#1B2A3A; min-height:900px;}",
                setup: function (ed) {
                    ed.on('init', function () { if (initialHtml) ed.setContent(initialHtml); });
                }
            });
            return true;
        } catch (e) { console.error('tiny init', e); return false; }
    },
    setContent(selector, html) {
        const id = selector.replace('#', '');
        try { const ed = window.tinymce && tinymce.get(id); if (ed) ed.setContent(html || ''); } catch (e) { }
    },
    insert(selector, text) {
        const id = selector.replace('#', '');
        try { const ed = window.tinymce && tinymce.get(id); if (ed) ed.insertContent(text); } catch (e) { }
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
