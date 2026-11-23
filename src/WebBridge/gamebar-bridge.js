(function (root) {
  'use strict';

  var STORAGE_CONFIG_KEY = 'ctc.generator.v1';
  var STORAGE_HASH_KEY = 'ctc.gamebar.hash.v1';
  var CHANNEL_NAME = 'ctc.gamebar.v1';
  var MODE_CLASSES = ['gb-pinned-clickthrough', 'gb-pinned-interactive', 'gb-unpinned'];

  function postToHost(win, payload) {
    var msg = typeof payload === 'string' ? payload : JSON.stringify(payload);
    try {
      if (win.chrome && win.chrome.webview && typeof win.chrome.webview.postMessage === 'function') {
        win.chrome.webview.postMessage(msg);
      } else if (win.external && typeof win.external.notify === 'function') {
        win.external.notify(msg);
      }
    } catch (_) { /* host unavailable outside Game Bar */ }
  }

  function injectOverlayStyles(doc) {
    if (!doc.head || doc.getElementById('ctc-gamebar-style')) return;
    var style = doc.createElement('style');
    style.id = 'ctc-gamebar-style';
    style.textContent = [
      'html, body { background: transparent !important; }',
      'html.gb-pinned-clickthrough, html.gb-pinned-clickthrough body {',
      '  background: transparent !important;',
      '  pointer-events: none !important;',
      '  overflow: hidden !important;',
      '}',
      'html.gb-pinned-clickthrough #ctc-auth-root { display: none !important; }',
      '#chat-container { opacity: var(--gb-chat-opacity, 1); }',
    ].join('\n');
    doc.head.appendChild(style);
  }

  function applyVisualState(doc, state) {
    if (!state || !doc.documentElement) return;
    var rootEl = doc.documentElement;
    MODE_CLASSES.forEach(function (cls) { rootEl.classList.remove(cls); });
    if (state.cssModeClass) rootEl.classList.add(state.cssModeClass);
    var chatOpacity = typeof state.chatOpacity === 'number' ? state.chatOpacity : 1;
    var effective = typeof state.effectiveOpacity === 'number' ? state.effectiveOpacity : chatOpacity;
    rootEl.style.setProperty('--gb-chat-opacity', String(chatOpacity));
    rootEl.style.setProperty('--gb-effective-opacity', String(effective));
    var authRoot = doc.getElementById('ctc-auth-root');
    if (authRoot) authRoot.style.display = state.hideChrome ? 'none' : '';
  }

  function applyUpdatedHash(win, storage, nextHash) {
    if (!nextHash || typeof nextHash !== 'string') return;
    try { storage && storage.setItem(STORAGE_HASH_KEY, nextHash); } catch (_) { /* ignore */ }
    if (win.location && win.location.hash !== nextHash) {
      win.location.hash = nextHash;
      if (typeof win.location.reload === 'function') win.location.reload();
    }
  }

  function readSavedConfig(storage) {
    try {
      var raw = storage && storage.getItem(STORAGE_CONFIG_KEY);
      return raw ? JSON.parse(raw) : {};
    } catch (_) {
      return {};
    }
  }

  function initGameBarBridge(opts) {
    var o = opts || {};
    var win = o.win || (typeof window !== 'undefined' ? window : null);
    var doc = o.doc || (typeof document !== 'undefined' ? document : null);
    var storage = o.storage || (win && win.localStorage) || null;
    if (!win || !doc) return null;

    var channel = typeof win.BroadcastChannel === 'function' ? new win.BroadcastChannel(CHANNEL_NAME) : null;
    var isOverlay = Boolean(doc.getElementById('chat-container'));
    var isSettings = Boolean(doc.getElementById('ctc-form-root'));

    function handleEnvelope(raw) {
      var msg = typeof raw === 'string' ? JSON.parse(raw) : raw;
      if (!msg || typeof msg !== 'object') return;
      if (msg.type === 'state:update') {
        applyVisualState(doc, msg.state);
        if (isOverlay && msg.hash && (!win.location || !win.location.hash)) {
          applyUpdatedHash(win, storage, msg.hash);
        }
      } else if (msg.type === 'config:updated' && isOverlay) {
        applyUpdatedHash(win, storage, msg.hash);
      } else if (msg.type === 'config:apply' && isOverlay && msg.hash) {
        applyUpdatedHash(win, storage, msg.hash);
      }
    }

    win.__ctcGameBarReceive = handleEnvelope;
    if (channel) {
      channel.onmessage = function (ev) { handleEnvelope(ev.data); };
    }

    if (isOverlay) {
      injectOverlayStyles(doc);
      var savedHash = storage && storage.getItem(STORAGE_HASH_KEY);
      if (savedHash && win.location && !win.location.hash) {
        win.location.hash = savedHash;
      }
      win.addEventListener && win.addEventListener('storage', function (ev) {
        if (ev.key === STORAGE_HASH_KEY && ev.newValue) applyUpdatedHash(win, storage, ev.newValue);
      });
      postToHost(win, { type: 'state:query' });
    }

    function sendApply(configOverride) {
      var cfg = configOverride || readSavedConfig(storage);
      var envelope = { type: 'config:apply', config: cfg };
      postToHost(win, envelope);
      channel && channel.postMessage(envelope);
    }

    if (isSettings) {
      var row = doc.querySelector('.ctc-btn-row');
      if (row && !doc.getElementById('ctc-gamebar-apply')) {
        var btn = doc.createElement('button');
        btn.type = 'button';
        btn.id = 'ctc-gamebar-apply';
        btn.className = 'ctc-btn ctc-btn-primary';
        btn.textContent = 'Apply to Game Bar Overlay';
        btn.addEventListener('click', function () { sendApply(); });
        row.insertBefore(btn, row.firstChild);
      }
      var scheduleSync = function () { win.setTimeout(function () { sendApply(); }, 50); };
      ['ctc-form-root', 'ctc-preset-select'].forEach(function (id) {
        var el = doc.getElementById(id);
        el && el.addEventListener('change', scheduleSync);
      });
      ['ctc-reset-btn', 'ctc-import-btn'].forEach(function (id) {
        var el = doc.getElementById(id);
        el && el.addEventListener('click', scheduleSync);
      });
    }

    return {
      handleEnvelope: handleEnvelope,
      sendApply: sendApply,
      destroy: function () { channel && channel.close && channel.close(); },
    };
  }

  root.CustomTwitchChatGameBarBridge = {
    STORAGE_CONFIG_KEY: STORAGE_CONFIG_KEY,
    STORAGE_HASH_KEY: STORAGE_HASH_KEY,
    CHANNEL_NAME: CHANNEL_NAME,
    initGameBarBridge: initGameBarBridge,
  };

  if (typeof window !== 'undefined' && typeof document !== 'undefined') {
    if (document.getElementById('chat-container') || document.getElementById('ctc-form-root') || document.readyState !== 'loading') {
      initGameBarBridge();
    } else {
      document.addEventListener('DOMContentLoaded', function () { initGameBarBridge(); });
    }
  }
}(typeof globalThis !== 'undefined' ? globalThis : this));
