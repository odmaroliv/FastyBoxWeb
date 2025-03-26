// Manejo de menús y clics fuera
window.setupClickListener = function (dotnetHelper) {
    document.addEventListener('click', function (event) {
        // Verificar si el clic fue fuera de elementos interactivos
        let targetElement = event.target;
        let isMenuOrButton = false;

        // Recorrer hasta 5 niveles hacia arriba en el DOM para verificar si es un menú o botón
        for (let i = 0; i < 5 && targetElement != null; i++) {
            if (targetElement.hasAttribute('data-menu-button') ||
                targetElement.hasAttribute('data-menu-container')) {
                isMenuOrButton = true;
                break;
            }
            targetElement = targetElement.parentElement;
        }

        if (!isMenuOrButton) {
            dotnetHelper.invokeMethodAsync('CloseMenu');
        }
    });
};

// Función para alternar el tema
window.toggleTheme = function (isDark) {
    if (isDark) {
        document.documentElement.classList.add('dark');
        localStorage.setItem('theme', 'dark');
    } else {
        document.documentElement.classList.remove('dark');
        localStorage.setItem('theme', 'light');
    }
};

// Inicializar tema al cargar la página
document.addEventListener('DOMContentLoaded', function () {
    if (localStorage.getItem('theme') === 'dark' ||
        (!localStorage.getItem('theme') && window.matchMedia('(prefers-color-scheme: dark)').matches)) {
        document.documentElement.classList.add('dark');
    } else {
        document.documentElement.classList.remove('dark');
    }
});

// Interoperabilidad con Stripe
window.initializeStripe = function (publishableKey) {
    window.stripe = Stripe(publishableKey);
    return true;
};

window.redirectToStripeCheckout = function (sessionId) {
    if (window.stripe) {
        window.stripe.redirectToCheckout({ sessionId: sessionId });
        return true;
    }
    return false;
};

// Función para copiar texto al portapapeles
window.copyToClipboard = function (text) {
    return navigator.clipboard.writeText(text)
        .then(() => true)
        .catch(() => {
            // Fallback para navegadores que no soportan Clipboard API
            const textArea = document.createElement('textarea');
            textArea.value = text;
            textArea.style.position = 'fixed';
            document.body.appendChild(textArea);
            textArea.focus();
            textArea.select();

            try {
                document.execCommand('copy');
                return true;
            } catch (err) {
                console.error('Error al copiar texto:', err);
                return false;
            } finally {
                document.body.removeChild(textArea);
            }
        });
};

// Arreglar problemas con los enlaces internos en Blazor
document.addEventListener('DOMContentLoaded', function () {
    // Interceptar clics en enlaces para usar la navegación de Blazor
    document.addEventListener('click', function (e) {
        // Buscar si el clic fue en un enlace o dentro de uno
        let target = e.target;
        while (target && target.tagName !== 'A') {
            target = target.parentElement;
        }

        // Si no es un enlace, salir
        if (!target) return;

        // Obtener href y comprobar si es un enlace interno
        const href = target.getAttribute('href');
        if (!href || href.startsWith('#') || href.startsWith('http') ||
            href.startsWith('mailto:') || href.startsWith('tel:') ||
            target.hasAttribute('download') || target.getAttribute('target') === '_blank') {
            return; // Permitir comportamiento normal para estos enlaces
        }

        // Verificar si es un enlace que debe forzar recarga
        if (target.hasAttribute('data-force-load')) {
            return; // Permitir comportamiento normal
        }

        // Si llegamos aquí, es un enlace interno que debe usar la navegación de Blazor
        // El evento será manejado por Blazor, no hacemos nada especial
    });
});