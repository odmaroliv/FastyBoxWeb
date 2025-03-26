/** @type {import('tailwindcss').Config} */
module.exports = {
    content: [
        './Pages/**/*.{cshtml,razor}',
        './Shared/**/*.{cshtml,razor}',
        './Components/**/*.{cshtml,razor}'
    ],
    darkMode: 'class',
    theme: {
        extend: {
            colors: {
                primary: {
                    50: '#f0f9ff',
                    100: '#e0f2fe',
                    200: '#bae6fd',
                    300: '#7dd3fc',
                    400: '#38bdf8',
                    500: '#0ea5e9',
                    600: '#0284c7',
                    700: '#0369a1',
                    800: '#075985',
                    900: '#0c4a6e',
                },
                // Colores específicos para el modo oscuro
                dark: {
                    bg: '#121827',         // Fondo principal
                    card: '#1e293b',       // Fondo de tarjetas/elementos
                    border: '#334155',     // Color de bordes
                    text: '#f8fafc',       // Texto principal
                    textSecondary: '#cbd5e1', // Texto secundario
                    accent: '#3b82f6',     // Color de acento (azul)
                    hover: '#2c3e50',      // Color de hover
                    success: '#064e3b',    // Verde oscuro para éxito
                    error: '#7f1d1d',      // Rojo oscuro para errores
                    warning: '#78350f',    // Naranja oscuro para advertencias
                    info: '#1e3a8a'        // Azul oscuro para información
                }
            },
            spacing: {
                '72': '18rem',
                '84': '21rem',
                '96': '24rem',
            },
            animation: {
                'bounce-slow': 'bounce 3s infinite',
            },
            // Sombras específicas para modo oscuro
            boxShadow: {
                'dark-sm': '0 1px 2px 0 rgba(0, 0, 0, 0.05)',
                'dark': '0 1px 3px 0 rgba(0, 0, 0, 0.1), 0 1px 2px 0 rgba(0, 0, 0, 0.06)',
                'dark-md': '0 4px 6px -1px rgba(0, 0, 0, 0.1), 0 2px 4px -1px rgba(0, 0, 0, 0.06)',
                'dark-lg': '0 10px 15px -3px rgba(0, 0, 0, 0.1), 0 4px 6px -2px rgba(0, 0, 0, 0.05)',
                'dark-xl': '0 20px 25px -5px rgba(0, 0, 0, 0.1), 0 10px 10px -5px rgba(0, 0, 0, 0.04)',
                'dark-2xl': '0 25px 50px -12px rgba(0, 0, 0, 0.25)',
                'dark-inner': 'inset 0 2px 4px 0 rgba(0, 0, 0, 0.06)',
            },
            // Transiciones
            transitionProperty: {
                'colors': 'color, background-color, border-color, text-decoration-color, fill, stroke',
            },
            // Variantes para formularios en modo oscuro
            backgroundColor: {
                'dark-input': '#1a2234',
                'dark-input-focus': '#2c3a57',
            },
            borderColor: {
                'dark-input': '#334155',
                'dark-input-focus': '#475569',
            },
            textColor: {
                'dark-input': '#f1f5f9',
                'dark-input-placeholder': '#94a3b8',
            }
        },
    },
    plugins: [
        require('@tailwindcss/forms'),
    ],
}