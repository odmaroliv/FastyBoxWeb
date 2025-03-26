// Revisa si el click fue en un elemento interactivo y solo llama CloseMenu si el usuario clickea fuera.
window.setupClickListener = function (dotnetHelper) {
    document.addEventListener('click', function (event) {
        var targetTag = event.target.tagName.toLowerCase();
        // Evita interferir en botones, enlaces y otros elementos interactivos.
        if (['button', 'a', 'input', 'select', 'textarea'].includes(targetTag)) {
            return;
        }
        dotnetHelper.invokeMethodAsync('CloseMenu');
    });
};

