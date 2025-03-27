
window.setupClickListener = function (dotnetHelper) {
    document.addEventListener('click', function (event) {
        // Verificar si el clic fue en elementos del menú que no deberían cerrarlo
        let targetElement = event.target;
        for (let i = 0; i < 5 && targetElement != null; i++) {
            if (targetElement.hasAttribute('data-menu-exception')) {
                console.log("Click on menu exception element, keeping menu open");
                return;
            }
            targetElement = targetElement.parentElement;
        }

        // Verificar si el clic fue en un botón de menú
        targetElement = event.target;
        let isMenuToggle = false;
        for (let i = 0; i < 5 && targetElement != null; i++) {
            if (targetElement.hasAttribute('data-menu-button')) {
                isMenuToggle = true;
                break;
            }
            targetElement = targetElement.parentElement;
        }

        targetElement = event.target;
        let isInsideMenuContainer = false;
        for (let i = 0; i < 10 && targetElement != null; i++) {
            if (targetElement.hasAttribute('data-menu-container')) {
                isInsideMenuContainer = true;
                break;
            }
            targetElement = targetElement.parentElement;
        }

        // Solo cerramos si no es un toggle y no está dentro de un contenedor 
        // o es un elemento dentro del menú que debe cerrarlo
        if (!isMenuToggle && (!isInsideMenuContainer ||
            (isInsideMenuContainer && !event.target.closest('[data-menu-exception]')))) {

            // Verificar que dotnetHelper aún no haya sido desechado
            if (dotnetHelper) {
                // Intentar invocar el método solo si dotnetHelper está disponible
                try {
                    if (dotnetHelper.invokeMethodAsync) {
                        dotnetHelper.invokeMethodAsync('CloseMenu').catch(error => {
                            console.error('Error invocando CloseMenu:', error);
                        });
                    }
                } catch (error) {
                    console.error('Error invoking CloseMenu:', error);
                }
            }
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
            e.preventDefault();
            window.location.href = href; // Forzar recarga completa
            return;
        }

        // Si llegamos aquí, es un enlace interno que debe usar la navegación de Blazor
        // El evento será manejado por Blazor, no hacemos nada especial
    });
});

// Función para alternar el tema
window.toggleTheme = function (isDark) {
    console.log("Toggling theme to:", isDark ? "dark" : "light"); // Añade log para debug
    if (isDark) {
        document.documentElement.classList.add('dark');
        localStorage.setItem('theme', 'dark');
    } else {
        document.documentElement.classList.remove('dark');
        localStorage.setItem('theme', 'light');
    }
    return true; // Añade un valor de retorno para confirmar
};

window.redirectToStripeCheckout = function (sessionUrl) {
    window.location.href = sessionUrl;
}