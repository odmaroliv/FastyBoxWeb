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
                }
            },
            spacing: {
                '72': '18rem',
                '84': '21rem',
                '96': '24rem',
            },
            animation: {
                'bounce-slow': 'bounce 3s infinite',
            }
        },
    },
    plugins: [
        require('@tailwindcss/forms'),
    ],
}