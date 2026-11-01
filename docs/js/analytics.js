// Google Analytics 4 for deskauth.com.
//
// Analytics is off until MEASUREMENT_ID is set to the site's Google Analytics measurement ID
// (Admin > Data streams > the web stream, "G-" followed by letters and numbers). It only runs
// on the public site, so local copies and GitHub previews are not counted.
(function () {
  var MEASUREMENT_ID = '';
  var SITE_HOST = 'deskauth.com';

  if (!MEASUREMENT_ID || location.hostname !== SITE_HOST) {
    return;
  }

  var script = document.createElement('script');
  script.async = true;
  script.src = 'https://www.googletagmanager.com/gtag/js?id=' + encodeURIComponent(MEASUREMENT_ID);
  document.head.appendChild(script);

  window.dataLayer = window.dataLayer || [];
  window.gtag = function () {
    window.dataLayer.push(arguments);
  };
  window.gtag('js', new Date());
  window.gtag('config', MEASUREMENT_ID);
})();
