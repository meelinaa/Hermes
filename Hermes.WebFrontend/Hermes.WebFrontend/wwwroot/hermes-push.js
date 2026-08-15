// Hermes Web Push & Browser Notifications Interop
window.hermesPush = {
  getPermission: function () {
    if (!('Notification' in window)) {
      return 'unsupported';
    }
    return Notification.permission;
  },

  requestPermission: async function () {
    if (!('Notification' in window)) {
      return 'unsupported';
    }
    try {
      const permission = await Notification.requestPermission();
      return permission;
    } catch (e) {
      console.warn('Failed to request notification permission:', e);
      return 'denied';
    }
  },

  sendNotification: function (title, body, icon) {
    if (!('Notification' in window) || Notification.permission !== 'granted') {
      return false;
    }

    try {
      new Notification(title, {
        body: body || '',
        icon: icon || '/favicon.png'
      });
      return true;
    } catch (e) {
      console.warn('Failed to trigger notification:', e);
      return false;
    }
  }
};
