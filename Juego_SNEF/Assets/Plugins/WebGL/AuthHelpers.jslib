mergeInto(LibraryManager.library, {

  // ---------- localStorage ----------
  __GetLocalStorageItem: function (keyPtr) {
    try {
      var key = UTF8ToString(keyPtr);
      var v = localStorage.getItem(key) || "";
      var toPtr = (typeof stringToNewUTF8 === 'function') ? stringToNewUTF8
                : (typeof allocateUTF8   === 'function') ? allocateUTF8
                : function (s) { var n = lengthBytesUTF8(s) + 1; var p = _malloc(n); stringToUTF8(s, p, n); return p; };
      return toPtr(v);
    } catch (e) {
      var toPtr = (typeof stringToNewUTF8 === 'function') ? stringToNewUTF8
                : (typeof allocateUTF8   === 'function') ? allocateUTF8
                : function (s) { var n = lengthBytesUTF8(s) + 1; var p = _malloc(n); stringToUTF8(s, p, n); return p; };
      return toPtr("");
    }
  },

  __GetLocalStorageKeyCount: function () {
    try { return localStorage.length | 0; } catch (e) { return 0; }
  },

  __GetLocalStorageKeyAt: function (index) {
    try {
      var k = localStorage.key(index | 0) || "";
      var toPtr = (typeof stringToNewUTF8 === 'function') ? stringToNewUTF8
                : (typeof allocateUTF8   === 'function') ? allocateUTF8
                : function (s) { var n = lengthBytesUTF8(s) + 1; var p = _malloc(n); stringToUTF8(s, p, n); return p; };
      return toPtr(k);
    } catch (e) {
      var toPtr = (typeof stringToNewUTF8 === 'function') ? stringToNewUTF8
                : (typeof allocateUTF8   === 'function') ? allocateUTF8
                : function (s) { var n = lengthBytesUTF8(s) + 1; var p = _malloc(n); stringToUTF8(s, p, n); return p; };
      return toPtr("");
    }
  },

  __RequestTokenRefresh: function () {
  try {
    if (window.parent) {
      // Pide al host (tu app React) que haga el refresh
      window.parent.postMessage({ type: 'token.refresh.request' }, '*');
    }
  } catch (e) {}
},


  // ---------- sessionStorage ----------
  __GetSessionStorageItem: function (keyPtr) {
    try {
      var key = UTF8ToString(keyPtr);
      var v = sessionStorage.getItem(key) || "";
      var toPtr = (typeof stringToNewUTF8 === 'function') ? stringToNewUTF8
                : (typeof allocateUTF8   === 'function') ? allocateUTF8
                : function (s) { var n = lengthBytesUTF8(s) + 1; var p = _malloc(n); stringToUTF8(s, p, n); return p; };
      return toPtr(v);
    } catch (e) {
      var toPtr = (typeof stringToNewUTF8 === 'function') ? stringToNewUTF8
                : (typeof allocateUTF8   === 'function') ? allocateUTF8
                : function (s) { var n = lengthBytesUTF8(s) + 1; var p = _malloc(n); stringToUTF8(s, p, n); return p; };
      return toPtr("");
    }
  },

  __GetSessionStorageKeyCount: function () {
    try { return sessionStorage.length | 0; } catch (e) { return 0; }
  },

  __GetSessionStorageKeyAt: function (index) {
    try {
      var k = sessionStorage.key(index | 0) || "";
      var toPtr = (typeof stringToNewUTF8 === 'function') ? stringToNewUTF8
                : (typeof allocateUTF8   === 'function') ? allocateUTF8
                : function (s) { var n = lengthBytesUTF8(s) + 1; var p = _malloc(n); stringToUTF8(s, p, n); return p; };
      return toPtr(k);
    } catch (e) {
      var toPtr = (typeof stringToNewUTF8 === 'function') ? stringToNewUTF8
                : (typeof allocateUTF8   === 'function') ? allocateUTF8
                : function (s) { var n = lengthBytesUTF8(s) + 1; var p = _malloc(n); stringToUTF8(s, p, n); return p; };
      return toPtr("");
    }
  },

  // ---------- escucha de postMessage desde el padre ----------
  __SubscribeTokenMessages: function () {
    if (typeof window === "undefined") return;

    function deliver(t) {
      try {
        var inst =
          window.unityInstance ||
          (typeof Module !== "undefined" && (Module.unityInstance || Module.UnityInstance)) ||
          null;
        var tok = (t == null) ? "" : String(t);
        if (inst && inst.SendMessage) inst.SendMessage("WebBridge", "ReceiveToken", tok);
        else window.__pendingUnityToken = tok; // Unity aún no listo
      } catch (_) {}
    }

    window.addEventListener("message", function (e) {
      var d = e && e.data;
      if (!d) return;
      if (d.type === "unity.token" || d.type === "token.update") {
        deliver(d.value);
      }
    });

    if (window.__pendingUnityToken) {
      deliver(window.__pendingUnityToken);
      window.__pendingUnityToken = null;
    }
  }
});
