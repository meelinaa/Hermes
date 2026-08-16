window.hermesTheme = {
  init: function () {
    var saved = localStorage.getItem('hermes.theme') || 'system';
    this.apply(saved);
    try {
      var mediaQuery = window.matchMedia('(prefers-color-scheme: dark)');
      if (mediaQuery && mediaQuery.addEventListener) {
        mediaQuery.addEventListener('change', function () {
          if ((localStorage.getItem('hermes.theme') || 'system') === 'system') {
            window.hermesTheme.apply('system');
          }
        });
      }
    } catch (e) {
    }
  },
  apply: function (theme) {
    var effectiveTheme = theme;
    if (theme === 'system') {
      effectiveTheme = (window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches) ? 'dark' : 'light';
    }
    document.documentElement.setAttribute('data-theme', effectiveTheme);
    document.documentElement.setAttribute('data-theme-setting', theme);
    return effectiveTheme;
  },
  setTheme: function (theme) {
    localStorage.setItem('hermes.theme', theme);
    return this.apply(theme);
  },
  getTheme: function () {
    return localStorage.getItem('hermes.theme') || 'system';
  }
};

window.hermesTheme.init();
