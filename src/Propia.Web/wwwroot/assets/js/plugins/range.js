document.addEventListener('DOMContentLoaded', () => {
    const rangeInput = document.getElementById('customRange4');
    const rangeOutput = document.getElementById('rangeValue');

    if (!rangeInput || !rangeOutput) return;

    // Initial value
    rangeOutput.textContent = rangeInput.value;

    rangeInput.addEventListener('input', () => {
        rangeOutput.textContent = rangeInput.value;
    });
});
