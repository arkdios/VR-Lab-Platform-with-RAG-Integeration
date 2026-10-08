mergeInto(LibraryManager.library, {
  // Saves text as a file through the browser's download mechanism.
  DownloadTextFile: function (fileNamePtr, mimeTypePtr, contentPtr) {
    var fileName = UTF8ToString(fileNamePtr);
    var mimeType = UTF8ToString(mimeTypePtr);
    var content = UTF8ToString(contentPtr);

    var blob = new Blob([content], { type: mimeType });
    var url = URL.createObjectURL(blob);
    var link = document.createElement('a');
    link.href = url;
    link.download = fileName;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    setTimeout(function () { URL.revokeObjectURL(url); }, 1000);
  },

  // Returns navigator.userAgent as a string Unity can read.
  GetUserAgentString: function () {
    var text = navigator.userAgent;
    var size = lengthBytesUTF8(text) + 1;
    var buffer = _malloc(size);
    stringToUTF8(text, buffer, size);
    return buffer;
  }
});