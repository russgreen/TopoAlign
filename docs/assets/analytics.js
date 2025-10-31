(function () {
    // Get the GA4 measurement ID from the script tag or fall back to default.
    var current = document.currentScript;
    var id = (current && current.dataset && current.dataset.gaId) || 'G-25CF6K81EE';
    if (!id) return;

    // Avoid double-initialization
    if (window.__gaInit) return;
    window.__gaInit = true;

    // Define dataLayer/gtag before loading the remote script so calls queue correctly.
    window.dataLayer = window.dataLayer || [];
    window.gtag = window.gtag || function () { window.dataLayer.push(arguments); };

    gtag('js', new Date());

    // Load the GA library asynchronously
    var s = document.createElement('script');
    s.async = true;
    s.src = 'https://www.googletagmanager.com/gtag/js?id=' + encodeURIComponent(id);
    document.head.appendChild(s);

    // Configure GA4
    gtag('config', id);
})();