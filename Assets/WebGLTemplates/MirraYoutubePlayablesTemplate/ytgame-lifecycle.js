// Early platform events for Google's Unity wrapper. Game progress is owned by C#.
(function (root) {
    'use strict';
    function createLifecycle(sdk, options) {
        options = options || {};
        const schedule = options.setTimeout || setTimeout;
        const cancel = options.clearTimeout || clearTimeout;
        let instance, runtime, paused = false, audio = false, first = false, ready = false;
        let readyWanted = false, pending = [], resumeWaiters = [];
        const handlers = {};
        let languagePromise;
        function direct(method, value) {
            instance.SendMessage('YTGameWrapper', method, value == null ? '' : String(value));
        }
        function send(method, value) {
            if (!instance || paused) pending.push([method, value]);
            else direct(method, value);
        }
        function drain() {
            while (instance && !paused && pending.length) direct(...pending.shift());
        }
        function once(action, timeout, complete) {
            let done = false;
            const timer = schedule(() => finish('timeout'), timeout);
            function finish(status, value) {
                if (done) return;
                done = true; cancel(timer); complete(status, value);
            }
            // Start synchronously: a pause-time flush must precede the pause boundary.
            try { Promise.resolve(action()).then(v => finish('success', v), () => finish('failed')); }
            catch (_) { finish('failed'); }
        }
        function readySignal() {
            readyWanted = true;
            if (ready || paused) return;
            if (options.onReady) options.onReady();
            sdk.game.gameReady(); ready = true;
        }
        function initialize() {
            if (!sdk || !sdk.game || !sdk.system) throw new Error('YouTube SDK is unavailable.');
            sdk.system.onAudioEnabledChange(value => {
                audio = !!value;
                if (handlers.audio) send('ReceiveOnAudioEnabledChange', String(audio));
            });
            audio = !!sdk.system.isAudioEnabled();
            sdk.system.onPause(() => {
                if (paused) return;
                // C# flushes progress and pauses audio before the main loop is stopped.
                if (instance && handlers.pause) direct('ReceiveOnPauseEvent', '');
                paused = true;
                if (runtime) runtime.pause();
            });
            sdk.system.onResume(() => {
                if (!paused) return;
                paused = false;
                if (instance) {
                    if (handlers.audio) direct('ReceiveOnAudioEnabledChange', String(audio));
                    if (handlers.resume) direct('ReceiveOnResumeEvent', '');
                }
                if (runtime) runtime.resume();
                drain();
                while (resumeWaiters.length) resumeWaiters.shift()();
                if (readyWanted) readySignal();
            });
            if (!first) { sdk.game.firstFrameReady(); first = true; }
        }
        function normalizeSave(raw) {
            if (raw == null || raw === '') return JSON.stringify({version: 2, entries: []});
            if (typeof raw !== 'string') throw new Error('Invalid cloud response');
            const doc = JSON.parse(raw);
            if (!doc || typeof doc !== 'object' || Array.isArray(doc)) throw new Error('Invalid save');
            if ('version' in doc && doc.version !== 1 && doc.version !== 2) throw new Error('Unknown save version');
            let entries;
            if (Array.isArray(doc.entries)) entries = doc.entries;
            else {
                const values = doc.version === 1 ? doc.values : doc;
                if (!values || typeof values !== 'object' || Array.isArray(values) || doc.version === 2)
                    throw new Error('Invalid save schema');
                entries = Object.keys(values).map(key => ({key, value: values[key]}));
            }
            const seen = new Set();
            entries = entries.map(entry => {
                if (!entry || typeof entry.key !== 'string' || !entry.key || seen.has(entry.key) ||
                    !['string', 'number', 'boolean'].includes(typeof entry.value) ||
                    (typeof entry.value === 'number' && !Number.isFinite(entry.value))) throw new Error('Invalid entry');
                seen.add(entry.key);
                return {key: entry.key, value: String(entry.value)};
            });
            return JSON.stringify({version: 2, entries});
        }
        return {
            initialize, send, once, normalizeSave,
            getLanguage: function () {
                if (!languagePromise) languagePromise = new Promise(resolve =>
                    once(() => sdk.system.getLanguage(), 12000, (status, tag) =>
                        resolve(status === 'success' && typeof tag === 'string' ? tag : 'en')));
                return languagePromise;
            },
            get paused() { return paused; }, get audioEnabled() { return audio; },
            get inPlayables() { return sdk.IN_PLAYABLES_ENV === true; },
            ready: readySignal,
            firstFrame: function () { if (!first) { sdk.game.firstFrameReady(); first = true; } },
            register: function (kind) { handlers[kind] = true; },
            attachRuntime: function (next) { runtime = next; if (paused) runtime.pause(); },
            attachUnity: function (next) {
                instance = next;
                if (handlers.audio) direct('ReceiveOnAudioEnabledChange', String(audio));
                if (paused && handlers.pause) direct('ReceiveOnPauseEvent', '');
                drain();
            },
            whenResumed: function () { return paused ? new Promise(resolve => resumeWaiters.push(resolve)) : Promise.resolve(); },
            status: function (message) { if (options.status) options.status(message); }
        };
    }
    if (typeof module !== 'undefined' && module.exports) module.exports = createLifecycle;
    else root.createYTGameLifecycle = createLifecycle;
})(typeof window !== 'undefined' ? window : globalThis);
