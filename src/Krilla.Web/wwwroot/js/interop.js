// The app's JavaScript: theme persistence, the footer's payload/RAM figures, and the blob-URL
// plumbing the PDF preview and download need. Everything else is C#.

// Every access is wrapped, because localStorage THROWS rather than returning null when the
// browser is refusing site data — a third-party-storage block, or Safari in private mode. This is
// a static page whose only stored state is a theme name, and an unguarded read runs inside the
// layout's OnInitializedAsync, so the whole app failed to start over a preference it can perfectly
// well do without. Best-effort in both directions: a reader who blocks storage gets the default
// theme and a toggle that works for the life of the page.
window.statePreference = {
    get: function (key) {
        try {
            return localStorage.getItem(key);
        } catch {
            return null;
        }
    },
    set: function (key, value) {
        try {
            localStorage.setItem(key, value);
        } catch {
        }
    },
    remove: function (key) {
        try {
            localStorage.removeItem(key);
        } catch {
        }
    }
};

// A converted PDF is bytes in WebAssembly memory. To show it in an <iframe> or hand it to the
// browser's downloader it has to become a URL, which means a Blob and an object URL. Those are
// held by the browser until explicitly revoked, so `release` is not optional housekeeping — a
// page whose purpose is repeated conversion would otherwise accumulate every PDF it ever made.
window.pdfBlob = {
    create: function (bytes) {
        const blob = new Blob([new Uint8Array(bytes)], { type: 'application/pdf' });
        return URL.createObjectURL(blob);
    },
    release: function (url) {
        if (url) {
            URL.revokeObjectURL(url);
        }
    },
    download: function (url, name) {
        const link = document.createElement('a');
        link.href = url;
        link.download = name;
        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);
    }
};

window.appInfo = {
    userAgent: function () {
        return navigator.userAgent;
    },
    // Totals the app's boot download. Waits for the load event (and web fonts) so every
    // framework/asset request has finished first, then sums Resource Timing: encodedBodySize is
    // the compressed bytes over the wire, decodedBodySize the uncompressed bytes.
    downloadSize: async function () {
        if (document.readyState !== 'complete') {
            await new Promise(resolve => window.addEventListener('load', resolve, { once: true }));
        }
        try {
            await document.fonts.ready;
        } catch {
        }

        let zipped = 0;
        let unzipped = 0;
        const add = entry => {
            zipped += entry.encodedBodySize || 0;
            unzipped += entry.decodedBodySize || 0;
        };
        performance.getEntriesByType('navigation').forEach(add);
        performance.getEntriesByType('resource').forEach(add);
        return { zipped, unzipped };
    },
    // Approximate RAM the app occupies. The managed heap and krilla's own allocations both live
    // in WebAssembly linear memory, so the WASM buffer size is the real footprint; fall back to
    // Chromium's JS heap when the runtime handle isn't exposed, and 0 when neither is available
    // (so the caller can hide the figure).
    ramBytes: function () {
        try {
            const buffer = globalThis.getDotnetRuntime?.(0)?.Module?.HEAP8?.buffer;
            if (buffer) {
                return buffer.byteLength;
            }
        } catch {
        }
        return performance.memory?.usedJSHeapSize ?? 0;
    }
};

window.themeManager = {
    applyTheme: function (themeName) {
        document.documentElement.setAttribute('data-theme', themeName.toLowerCase());
    },
    // The theme a reader gets before they have chosen one. Defaulting to light regardless left a
    // reader whose system is in dark mode staring at a white page until they found the toggle,
    // which is the one group that has already stated a preference. Named to match ThemeType, so
    // the C# side can parse it straight back.
    preferredTheme: function () {
        return window.matchMedia?.('(prefers-color-scheme: dark)').matches ? 'Dark' : 'Light';
    },
    // Runs from index.html BEFORE Blazor boots, so the first paint is already the right theme
    // rather than a flash of light that corrects itself once the runtime has downloaded.
    // ThemePreferenceService resolves the same answer the same way, so the two agree.
    initializeTheme: function () {
        let saved = null;
        try {
            saved = localStorage.getItem('selectedTheme');
        } catch {
        }
        window.themeManager.applyTheme(saved || window.themeManager.preferredTheme());
    }
};
