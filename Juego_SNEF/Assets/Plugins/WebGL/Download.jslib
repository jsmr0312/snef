mergeInto(LibraryManager.library, {
  DownloadBase64File: function (base64Ptr, filenamePtr, mimeTypePtr) {
    var b64 = UTF8ToString(base64Ptr);
    var filename = UTF8ToString(filenamePtr);
    var mime = UTF8ToString(mimeTypePtr);

    // Decodificar base64 -> Blob
    var byteChars = atob(b64);
    var byteNums = new Array(byteChars.length);
    for (var i = 0; i < byteChars.length; i++) byteNums[i] = byteChars.charCodeAt(i);
    var byteArray = new Uint8Array(byteNums);
    var blob = new Blob([byteArray], { type: mime });

    // Forzar descarga
    var url = URL.createObjectURL(blob);
    var a = document.createElement('a');
    a.href = url;
    a.download = filename;
    document.body.appendChild(a);
    a.click();
    setTimeout(function () {
      document.body.removeChild(a);
      URL.revokeObjectURL(url);
    }, 0);
  }
});
