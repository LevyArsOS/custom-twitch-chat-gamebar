(function (root) {
  'use strict';

  if (typeof root.globalThis === 'undefined') {
    root.globalThis = root;
  }
  if (typeof Object.hasOwn !== 'function') {
    Object.hasOwn = function (obj, prop) {
      return Object.prototype.hasOwnProperty.call(obj, prop);
    };
  }
  if (typeof Object.fromEntries !== 'function') {
    Object.fromEntries = function (entries) {
      var out = {};
      if (!entries) return out;
      var list = Array.from ? Array.from(entries) : entries;
      for (var i = 0; i < list.length; i += 1) {
        if (list[i]) out[list[i][0]] = list[i][1];
      }
      return out;
    };
  }
  if (typeof root.TextEncoder === 'undefined') {
    root.TextEncoder = function () {};
    root.TextEncoder.prototype.encode = function (str) {
      var utf8 = unescape(encodeURIComponent(String(str || '')));
      var arr = new Uint8Array(utf8.length);
      for (var i = 0; i < utf8.length; i += 1) arr[i] = utf8.charCodeAt(i);
      return arr;
    };
  }
  if (typeof root.TextDecoder === 'undefined') {
    root.TextDecoder = function () {};
    root.TextDecoder.prototype.decode = function (bytes) {
      var arr = bytes instanceof Uint8Array ? bytes : new Uint8Array(bytes || []);
      var bin = '';
      for (var i = 0; i < arr.length; i += 1) bin += String.fromCharCode(arr[i]);
      return decodeURIComponent(escape(bin));
    };
  }
  if (typeof Element !== 'undefined' && !Element.prototype.replaceChildren) {
    Element.prototype.replaceChildren = function () {
      while (this.firstChild) this.removeChild(this.firstChild);
      for (var i = 0; i < arguments.length; i += 1) {
        var node = arguments[i];
        this.appendChild(typeof node === 'string' ? document.createTextNode(node) : node);
      }
    };
  }
  [typeof Element !== 'undefined' && Element.prototype, typeof DocumentFragment !== 'undefined' && DocumentFragment.prototype].forEach(function (proto) {
    if (proto && !proto.append) {
      proto.append = function () {
        for (var i = 0; i < arguments.length; i += 1) {
          var item = arguments[i];
          this.appendChild(typeof item === 'string' ? document.createTextNode(item) : item);
        }
      };
    }
  });
  if (typeof Window !== 'undefined' && Window.prototype && Window.prototype.postMessage) {
    var origPost = Window.prototype.postMessage;
    Window.prototype.postMessage = function (data, targetOrigin, transfer) {
      var safeOrigin = (!targetOrigin || targetOrigin === 'null' || String(targetOrigin).indexOf('ms-appx') === 0)
        ? '*'
        : targetOrigin;
      return transfer ? origPost.call(this, data, safeOrigin, transfer) : origPost.call(this, data, safeOrigin);
    };
  }

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
      'html.gb-pinned-clickthrough #ctc-auth-root,',
      'html.gb-pinned-clickthrough #ctc-gb-status { display: none !important; }',
      '#chat-container { opacity: var(--gb-chat-opacity, 1); }',
      '#ctc-gb-status {',
      '  position: fixed; top: 8px; left: 8px; right: 8px; z-index: 9999;',
      '  padding: 6px 10px; border-radius: 6px; font: 600 12px/1.4 sans-serif;',
      '  background: rgba(20, 20, 28, 0.85); color: #e2e8f0; border: 1px solid rgba(145, 71, 255, 0.6);',
      '  pointer-events: none;',
      '}',
    ].join('\n');
    doc.head.appendChild(style);
  }

  function updateStatusBanner(doc, cfg) {
    var banner = doc.getElementById('ctc-gb-status');
    if (!banner && doc.body) {
      banner = doc.createElement('div');
      banner.id = 'ctc-gb-status';
      doc.body.appendChild(banner);
    }
    if (!banner) return;
    var chan = cfg && cfg.channel ? String(cfg.channel).trim() : '';
    banner.textContent = chan
      ? 'Twitch Chat: #' + chan + ' (Pin widget to hide this bar)'
      : 'No Twitch channel set. Click the Settings (gear) icon above to configure.';
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
      return raw ? JSON.parse(raw) : null;
    } catch (_) {
      return null;
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
    var isPreview = Boolean(win.location && String(win.location.search).indexOf('preview=1') !== -1);
    var lastRawConfig = storage ? storage.getItem(STORAGE_CONFIG_KEY) : null;

    function handleEnvelope(raw) {
      var msg = typeof raw === 'string' ? JSON.parse(raw) : raw;
      if (!msg || typeof msg !== 'object') return;
      if (msg.type === 'state:update') {
        applyVisualState(doc, msg.state);
        if (isOverlay && !isPreview && msg.hash && (!win.location || !win.location.hash)) {
          applyUpdatedHash(win, storage, msg.hash);
        }
      } else if ((msg.type === 'config:updated' || msg.type === 'config:apply') && isOverlay && !isPreview) {
        if (msg.config && storage) {
          var serialized = JSON.stringify(msg.config);
          if (serialized !== lastRawConfig) {
            lastRawConfig = serialized;
            try { storage.setItem(STORAGE_CONFIG_KEY, serialized); } catch (_) { /* ignore */ }
            if (typeof win.location.reload === 'function') win.location.reload();
            return;
          }
        }
        if (msg.hash) applyUpdatedHash(win, storage, msg.hash);
      }
    }

    win.__ctcGameBarReceive = handleEnvelope;
    if (channel) {
      channel.onmessage = function (ev) { handleEnvelope(ev.data); };
    }

    if (isOverlay && !isPreview) {
      injectOverlayStyles(doc);
      var savedCfg = readSavedConfig(storage);
      if (savedCfg) {
        win.__CTC_CONFIG__ = savedCfg;
      }
      var savedHash = storage && storage.getItem(STORAGE_HASH_KEY);
      if (savedHash && win.location && !win.location.hash) {
        win.location.hash = savedHash;
      }
      updateStatusBanner(doc, savedCfg);
      win.setInterval && win.setInterval(function () {
        var currentRaw = storage ? storage.getItem(STORAGE_CONFIG_KEY) : null;
        if (currentRaw && currentRaw !== lastRawConfig) {
          lastRawConfig = currentRaw;
          if (typeof win.location.reload === 'function') win.location.reload();
        }
      }, 600);
      postToHost(win, { type: 'state:query' });
    }

    function sendApply(configOverride) {
      var cfg = configOverride || readSavedConfig(storage) || {};
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
      var timer = null;
      var scheduleSync = function () {
        if (timer) win.clearTimeout(timer);
        timer = win.setTimeout(function () { sendApply(); }, 150);
      };
      ['ctc-form-root', 'ctc-preset-select'].forEach(function (id) {
        var el = doc.getElementById(id);
        if (el) {
          el.addEventListener('change', scheduleSync);
          el.addEventListener('input', scheduleSync);
        }
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
}(typeof globalThis !== 'undefined' ? globalThis : (typeof window !== 'undefined' ? window : this)));
