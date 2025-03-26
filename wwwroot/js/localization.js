// localization.js
window.localizationFunctions = {
    setCulture: function (culture) {
        // Save to localStorage
        localStorage.setItem('culture', culture);

        // Set cookie for server-side detection
        var expiry = new Date();
        expiry.setFullYear(expiry.getFullYear() + 1);
        document.cookie = `c=${culture}|uic=${culture}; path=/; expires=${expiry.toUTCString()}`;

        // Update HTML lang attribute
        document.documentElement.setAttribute('lang', culture);

        return true;
    },

    getCurrentCulture: function () {
        return localStorage.getItem('culture') ||
            document.documentElement.getAttribute('lang') ||
            'es';
    }
};