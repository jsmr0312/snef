mergeInto(LibraryManager.library, {
  IsMobile: function () {
    var ua = "";
    try { ua = (navigator.userAgent || navigator.vendor || window.opera || "").toLowerCase(); } catch (e) {}
    var looksMobile = /android|iphone|ipad|ipod|windows phone|iemobile|mobile/.test(ua);
    return looksMobile ? 1 : 0;
  },

  // Alias para compatibilidad con otros scripts
  IsMobileBrowser: function () {
    var ua = "";
    try { ua = (navigator.userAgent || navigator.vendor || window.opera || "").toLowerCase(); } catch (e) {}
    var looksMobile = /android|iphone|ipad|ipod|windows phone|iemobile|mobile/.test(ua);
    return looksMobile ? 1 : 0;
  }
});
