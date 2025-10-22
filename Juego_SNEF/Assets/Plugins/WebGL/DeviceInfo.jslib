mergeInto(LibraryManager.library, {
  IsMobile: function () {
    var ua = "";
    try { ua = (navigator.userAgent || navigator.vendor || window.opera || "").toLowerCase(); } catch (e) {}

    // iPadOS "Macintosh" con pantalla táctil
    var isiPadOSLike = (function(){
      try {
        return (navigator.platform === 'MacIntel' && navigator.maxTouchPoints > 1);
      } catch(e){ return false; }
    })();

    var isIOS = /iphone|ipod|ipad/.test(ua) || isiPadOSLike;
    var isAndroid = /android/.test(ua);
    var isMobileWord = /windows phone|iemobile|mobile/.test(ua);

    var looksMobile = isIOS || isAndroid || isMobileWord;
    return looksMobile ? 1 : 0;
  },

  // Alias
  IsMobileBrowser: function () {
    var ua = "";
    try { ua = (navigator.userAgent || navigator.vendor || window.opera || "").toLowerCase(); } catch (e) {}

    var isiPadOSLike = (function(){
      try {
        return (navigator.platform === 'MacIntel' && navigator.maxTouchPoints > 1);
      } catch(e){ return false; }
    })();

    var isIOS = /iphone|ipod|ipad/.test(ua) || isiPadOSLike;
    var isAndroid = /android/.test(ua);
    var isMobileWord = /windows phone|iemobile|mobile/.test(ua);

    var looksMobile = isIOS || isAndroid || isMobileWord;
    return looksMobile ? 1 : 0;
  }
});
