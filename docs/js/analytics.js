// Analytics and the cookie consent banner for deskauth.com. See privacy.html.
//
// - Cloudflare Web Analytics uses no cookies and stores nothing on the visitor's device, so it runs
//   for every visit once CLOUDFLARE_TOKEN is set (Cloudflare > Analytics & Logs > Web Analytics).
// - Google Analytics sets cookies, so it only loads after the visitor accepts in the consent banner.
//   A browser's Global Privacy Control signal counts as declining.
//
// Both only run on the public site, so local copies and GitHub previews are not counted.
(function () {
  var GA_MEASUREMENT_ID = 'G-WQHNE1ETW3';
  var CLOUDFLARE_TOKEN = '';
  var SITE_HOST = 'deskauth.com';
  var CONSENT_KEY = 'deskauth-analytics-consent';

  if (location.hostname !== SITE_HOST) {
    return;
  }

  var googleLoaded = false;
  var banner = null;

  function readConsent() {
    try {
      return localStorage.getItem(CONSENT_KEY);
    } catch (e) {
      return null;
    }
  }

  function writeConsent(value) {
    try {
      localStorage.setItem(CONSENT_KEY, value);
    } catch (e) {
      // storage blocked: the choice applies to this page only
    }
  }

  function loadScript(src, attributes) {
    var script = document.createElement('script');
    script.async = true;
    script.src = src;
    for (var name in attributes) {
      script.setAttribute(name, attributes[name]);
    }
    document.head.appendChild(script);
  }

  function loadCloudflare() {
    if (CLOUDFLARE_TOKEN) {
      loadScript('https://static.cloudflareinsights.com/beacon.min.js', {
        'data-cf-beacon': JSON.stringify({ token: CLOUDFLARE_TOKEN })
      });
    }
  }

  function loadGoogleAnalytics() {
    if (googleLoaded || !GA_MEASUREMENT_ID) {
      return;
    }
    googleLoaded = true;
    window.dataLayer = window.dataLayer || [];
    window.gtag = function () {
      window.dataLayer.push(arguments);
    };
    // analytics only: no advertising storage or personalization
    window.gtag('consent', 'default', {
      analytics_storage: 'granted',
      ad_storage: 'denied',
      ad_user_data: 'denied',
      ad_personalization: 'denied'
    });
    window.gtag('js', new Date());
    window.gtag('config', GA_MEASUREMENT_ID);
    loadScript('https://www.googletagmanager.com/gtag/js?id=' + encodeURIComponent(GA_MEASUREMENT_ID), {});
  }

  // Google Analytics cookies are _ga and _ga_<id>, set on the site's domain
  function removeGoogleCookies() {
    document.cookie.split(';').forEach(function (cookie) {
      var name = cookie.split('=')[0].trim();
      if (name === '_ga' || name.indexOf('_ga_') === 0) {
        var expired = name + '=; expires=Thu, 01 Jan 1970 00:00:00 GMT; path=/';
        document.cookie = expired;
        document.cookie = expired + '; domain=' + SITE_HOST;
        document.cookie = expired + '; domain=.' + SITE_HOST;
      }
    });
  }

  function choose(value) {
    writeConsent(value);
    hideBanner();
    if (value === 'granted') {
      loadGoogleAnalytics();
    } else {
      removeGoogleCookies();
      if (googleLoaded) {
        // stop the already loaded script from setting cookies again
        location.reload();
      }
    }
  }

  function element(tag, className, text) {
    var el = document.createElement(tag);
    if (className) {
      el.className = className;
    }
    if (text) {
      el.textContent = text;
    }
    return el;
  }

  function showBanner() {
    if (banner) {
      banner.hidden = false;
      reserveSpace();
      return;
    }
    banner = element('section', 'consent');
    banner.setAttribute('aria-label', 'Cookie consent');

    var text = element('p', 'consent-text',
      'May we use Google Analytics cookies to see how people find and use this website? ' +
      'They are not used for advertising. The DeskAuth app never tracks you. ');
    var link = element('a', null, 'Privacy notice');
    link.href = 'privacy.html';
    text.appendChild(link);

    var buttons = element('div', 'consent-buttons');
    var accept = element('button', 'button', 'Accept');
    accept.type = 'button';
    accept.addEventListener('click', function () { choose('granted'); });
    var decline = element('button', 'button', 'Decline');
    decline.type = 'button';
    decline.addEventListener('click', function () { choose('denied'); });
    buttons.appendChild(accept);
    buttons.appendChild(decline);

    banner.appendChild(text);
    banner.appendChild(buttons);
    document.body.appendChild(banner);
    reserveSpace();
  }

  // room below the page so the banner doesn't cover the footer
  function reserveSpace() {
    document.body.style.paddingBottom = (banner.offsetHeight + 32) + 'px';
  }

  function hideBanner() {
    if (banner) {
      banner.hidden = true;
      document.body.style.paddingBottom = '';
    }
  }

  function start() {
    loadCloudflare();

    var consent = readConsent();
    if (consent === 'granted') {
      loadGoogleAnalytics();
    } else if (consent !== 'denied' && !navigator.globalPrivacyControl) {
      showBanner();
    }

    // "Cookie settings" links reopen the banner; without this script they go to the privacy notice
    var settings = document.querySelectorAll('[data-cookie-settings]');
    for (var i = 0; i < settings.length; i++) {
      settings[i].addEventListener('click', function (event) {
        event.preventDefault();
        showBanner();
      });
    }
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', start);
  } else {
    start();
  }
})();
