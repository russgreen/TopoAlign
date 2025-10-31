(function () {
    if (window.__gaInit) return;
    window.__gaInit = true;

    var MEASUREMENT_ID = 'G-25CF6K81EE';

    // Prepare gtag queue
    window.dataLayer = window.dataLayer || [];
    window.gtag = window.gtag || function () { window.dataLayer.push(arguments); };

    gtag('js', new Date());

    // Load GA library
    var s = document.createElement('script');
    s.async = true;
    s.src = 'https://www.googletagmanager.com/gtag/js?id=' + encodeURIComponent(MEASUREMENT_ID);
    document.head.appendChild(s);

    // Configure
    gtag('config', MEASUREMENT_ID);
})();