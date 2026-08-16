window.hermesDropdown = {
  initClickOutside: function () {
    if (window._hermesDropdownInitialized) {
      return;
    }
    window._hermesDropdownInitialized = true;

    document.addEventListener('pointerdown', function (event) {
      var openDetails = document.querySelectorAll('details.news-ms[open]');
      openDetails.forEach(function (details) {
        if (!details.contains(event.target)) {
          details.removeAttribute('open');
        }
      });
    });

    document.addEventListener('keydown', function (event) {
      if (event.key === 'Escape') {
        var openDetails = document.querySelectorAll('details.news-ms[open]');
        openDetails.forEach(function (details) {
          details.removeAttribute('open');
        });
      }
    });
  }
};
