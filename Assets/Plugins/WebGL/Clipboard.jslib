// Copy text to the page's clipboard (Unity's own clipboard doesn't reach the browser's).
// Called from Clipboard.Copy while a button press is being handled, so the browser sees it as
// part of that click. The last text and how it went are kept on window for Tools/webtest.mjs.
mergeInto(LibraryManager.library, {
  AlibiCopyText: function (ptr) {
    var text = UTF8ToString(ptr);
    window.alibiCopied = text;
    window.alibiCopyStatus = 'pending';
    function fallback() {
      try {
        var area = document.createElement('textarea');
        area.value = text;
        area.setAttribute('readonly', '');
        area.style.position = 'fixed';
        area.style.opacity = '0';
        document.body.appendChild(area);
        area.select();
        var ok = document.execCommand('copy');
        document.body.removeChild(area);
        window.alibiCopyStatus = ok ? 'copied (execCommand)' : 'failed';
      } catch (e) {
        window.alibiCopyStatus = 'failed: ' + e;
      }
    }
    if (navigator.clipboard && navigator.clipboard.writeText) {
      navigator.clipboard.writeText(text).then(function () { window.alibiCopyStatus = 'copied'; }, fallback);
    } else {
      fallback();
    }
  }
});
