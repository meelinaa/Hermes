window.hermesAuth = {
  signOutAndReload: function (loginPath) {
    var path = loginPath || '/login';
    var host = window.location.hostname;
    document.cookie.split(';').forEach(function (part) {
      var cookie = part.trim();
      if (!cookie) return;
      var eq = cookie.indexOf('=');
      var name = (eq === -1 ? cookie : cookie.substring(0, eq)).trim();
      if (!name) return;
      document.cookie = name + '=;expires=Thu, 01 Jan 1970 00:00:00 GMT;path=/';
      document.cookie = name + '=;expires=Thu, 01 Jan 1970 00:00:00 GMT;path=/;domain=' + host;
    });
    window.location.replace(path);
  }
};
