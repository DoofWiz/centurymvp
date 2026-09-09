// The browser's own keyboard is the enemy of a WASD + L-Ctrl control scheme: Ctrl+W in a tab
// closes it mid-battle. Installed once from Century.App.BrowserKeyGuard at boot (WebGL only).
//
// What a page may swallow, this swallows: every Ctrl/Cmd combination the browser allows a
// page to cancel, plus the bare keys the game uses that would otherwise scroll or refocus
// the page (Space, Tab, Backspace, arrows, quick-find keys, F5...). What a page may NOT
// swallow in a tab (Ctrl+W / Ctrl+T / Ctrl+N) becomes swallowable in FULLSCREEN through the
// Keyboard Lock API, so the fullscreen button is the true safety. preventDefault only cancels
// the browser's action — the events still reach Unity untouched.
mergeInto(LibraryManager.library, {
  CenturyInstallBrowserKeyGuard: function () {
    if (window.__centuryKeyGuard) return;
    window.__centuryKeyGuard = true;

    var guarded = ['Space', 'Tab', 'Backspace', 'Quote', 'Slash',
                   'F1', 'F3', 'F5', 'F6', 'F7', 'F10',
                   'ArrowUp', 'ArrowDown', 'ArrowLeft', 'ArrowRight'];

    window.addEventListener('keydown', function (e) {
      var t = e.target;
      if (t && (t.tagName === 'INPUT' || t.tagName === 'TEXTAREA' || t.isContentEditable)) return;

      if (e.ctrlKey || e.metaKey
          || (e.altKey && (e.code === 'ArrowLeft' || e.code === 'ArrowRight'))
          || guarded.indexOf(e.code) !== -1) {
        e.preventDefault();
      }
    }, { capture: true });

    // Fullscreen: lock the whole keyboard so even Ctrl+W stays in the game. Holding Esc
    // still exits — the browser's own failsafe, by design.
    var syncLock = function () {
      if (!navigator.keyboard || !navigator.keyboard.lock) return;
      if (document.fullscreenElement) navigator.keyboard.lock()['catch'](function () {});
      else if (navigator.keyboard.unlock) navigator.keyboard.unlock();
    };
    document.addEventListener('fullscreenchange', syncLock);
    document.addEventListener('webkitfullscreenchange', syncLock);
  }
});
