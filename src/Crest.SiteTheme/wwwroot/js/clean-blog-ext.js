// Crest.Menu's activate-links.js is a plain function now (window.activateLinks), not a
// jQuery plugin; calling it as one threw "activateLinks is not a function" on every page.
(function () {
    var nav = document.getElementById('mainNav');
    if (nav && typeof window.activateLinks === 'function') {
        window.activateLinks(nav);
    }
})();
