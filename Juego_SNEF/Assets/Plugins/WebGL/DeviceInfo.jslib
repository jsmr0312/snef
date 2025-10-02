mergeInto(LibraryManager.library, {
  IsMobileBrowser: function() {
    try {
      var ua = navigator.userAgent || navigator.vendor || window.opera || "";
      var isMobileUA =
        /android|iphone|ipad|ipod|iemobile|blackberry|opera mini|mobile/i.test(ua);

      var hasTouch = (navigator.maxTouchPoints && navigator.maxTouchPoints > 0);

      var coarse = false;
      try { coarse = window.matchMedia && window.matchMedia("(pointer: coarse)").matches; } catch(e){}

      return (isMobileUA || hasTouch || coarse) ? 1 : 0;
    } catch(e) {
      return 0;
    }
  }
});
