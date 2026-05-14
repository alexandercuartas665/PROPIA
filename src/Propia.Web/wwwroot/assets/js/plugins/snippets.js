document.addEventListener('DOMContentLoaded', () => {
    const exampleModal = document.getElementById('exampleModal');
    if (!exampleModal) return;

    exampleModal.addEventListener('show.bs.modal', (event) => {
        const button = event.relatedTarget;
        if (!button) return;

        const recipient = button.getAttribute('data-bs-whatever') || '';

        const modalTitle = exampleModal.querySelector('.modal-title');
        const modalBodyInput = exampleModal.querySelector('.modal-body input');

        if (modalTitle) {
            modalTitle.textContent = `New message to ${recipient}`;
        }

        if (modalBodyInput) {
            modalBodyInput.value = recipient;
        }
    });
});
