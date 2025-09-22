mergeInto(LibraryManager.library, {
  LS_GetItem: function (keyPtr) {
    var key = UTF8ToString(keyPtr);
    var val = localStorage.getItem(key);

    if (!val) return 0;

    var lengthBytes = lengthBytesUTF8(val) + 1;
    var buffer = _malloc(lengthBytes);
    stringToUTF8(val, buffer, lengthBytes);
    return buffer;
  }
});
