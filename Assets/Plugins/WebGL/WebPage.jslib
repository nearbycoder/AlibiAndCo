// Tells the page (Assets/WebGLTemplates/Alibi) which of its touch buttons fit the moment:
// "title", "back", "board", "board zoomed" or "none" (see WebPage.Tell).
mergeInto(LibraryManager.library, {
  AlibiTouchState: function (ptr) {
    var state = UTF8ToString(ptr);
    window.alibiTouchStateNow = state;
    if (typeof window.alibiTouchState === 'function') window.alibiTouchState(state);
  }
});
