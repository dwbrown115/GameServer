// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

// Function to set a cookie
function setCookie(name, value, days) {
    let expires = "";
    if (days) {
        let date = new Date();
        date.setTime(date.getTime() + (days * 24 * 60 * 60 * 1000));
        expires = "; expires=" + date.toUTCString();
    }
    document.cookie = name + "=" + (value || "") + expires + "; path=/";
}

// Function to get a cookie
function getCookie(name) {
    let nameEQ = name + "=";
    let ca = document.cookie.split(';');
    for (let i = 0; i < ca.length; i++) {
        let c = ca[i];
        while (c.charAt(0) === ' ') c = c.substring(1, c.length);
        if (c.indexOf(nameEQ) === 0) return c.substring(nameEQ.length, c.length);
    }
    return null;
}

// Function to apply the theme
function applyTheme() {
    const htmlElement = document.documentElement; // Get the <html> element
    const darkModePreference = getCookie('darkMode');

    // Default to dark mode if no preference or 'enabled'
    if (darkModePreference === 'enabled' || darkModePreference === null) {
        htmlElement.setAttribute('data-bs-theme', 'dark');
        setCookie('darkMode', 'enabled', 365); // Ensure cookie is set for a year
    } else {
        htmlElement.setAttribute('data-bs-theme', 'light');
    }
}

// Function to toggle dark mode
function toggleDarkMode() {
    const htmlElement = document.documentElement; // Get the <html> element
    if (htmlElement.getAttribute('data-bs-theme') === 'dark') {
        htmlElement.setAttribute('data-bs-theme', 'light');
        setCookie('darkMode', 'disabled', 365);
    } else {
        htmlElement.setAttribute('data-bs-theme', 'dark');
        setCookie('darkMode', 'enabled', 365);
    }
}

// Apply theme on page load
document.addEventListener('DOMContentLoaded', applyTheme);

// Add event listener for the dark mode toggle button
document.addEventListener('DOMContentLoaded', () => {
    const darkModeToggle = document.getElementById('darkModeToggle');
    if (darkModeToggle) {
        darkModeToggle.addEventListener('click', toggleDarkMode);
    }
});

// Update the toggle switch state based on the current theme
document.addEventListener('DOMContentLoaded', () => {
    const darkModeToggle = document.getElementById('darkModeToggle');
    if (darkModeToggle) {
        darkModeToggle.checked = (document.documentElement.getAttribute('data-bs-theme') === 'dark');
    }
});

// Add a media query listener for system theme changes
window.matchMedia('(prefers-color-scheme: dark)').addEventListener('change', event => {
    // If the user has not explicitly set a preference, follow system theme
    if (getCookie('darkMode') === null) {
        if (event.matches) {
            document.documentElement.setAttribute('data-bs-theme', 'dark');
        } else {
            document.documentElement.setAttribute('data-bs-theme', 'light');
        }
        const darkModeToggle = document.getElementById('darkModeToggle');
        if (darkModeToggle) {
            darkModeToggle.checked = (document.documentElement.getAttribute('data-bs-theme') === 'dark');
        }
    }
});