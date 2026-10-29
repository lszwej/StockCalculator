(() => {
    'use strict';

    const storageKey = 'stockcalculator-theme';
    const darkTheme = 'dark';
    const lightTheme = 'light';

    const readStoredTheme = () => {
        try {
            const theme = localStorage.getItem(storageKey);
            return theme === darkTheme || theme === lightTheme ? theme : null;
        } catch {
            return null;
        }
    };

    const storeTheme = theme => {
        try {
            localStorage.setItem(storageKey, theme);
        } catch {
            // Storage unavailable (private mode / blocked) — theme still applies for the current page.
        }
    };

    const systemTheme = () =>
        window.matchMedia('(prefers-color-scheme: dark)').matches ? darkTheme : lightTheme;

    const currentTheme = () => readStoredTheme() ?? systemTheme();

    const applyTheme = theme => {
        document.documentElement.setAttribute('data-bs-theme', theme);

        document.querySelectorAll('[data-theme-value]').forEach(toggle => {
            toggle.setAttribute('aria-pressed', String(toggle.dataset.themeValue === theme));
        });
    };

    applyTheme(currentTheme());

    document.addEventListener('DOMContentLoaded', () => applyTheme(currentTheme()));

    document.addEventListener('click', event => {
        const toggle = event.target instanceof Element ? event.target.closest('[data-theme-value]') : null;
        const selectedTheme = toggle?.dataset.themeValue;
        if (selectedTheme !== darkTheme && selectedTheme !== lightTheme) {
            return;
        }

        storeTheme(selectedTheme);
        applyTheme(selectedTheme);
    });

    window.matchMedia('(prefers-color-scheme: dark)').addEventListener('change', () => {
        if (readStoredTheme() === null) {
            applyTheme(systemTheme());
        }
    });
})();
