/**
 * Copyright 2023 Google LLC
 * 
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 * 
 *     https://www.apache.org/licenses/LICENSE-2.0
 * 
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

// Based on Google's Unity wrapper and the BUSES callback fixes.
// Local additions: early lifecycle queue, bounded Promise results, strict rewards and save acknowledgments.
mergeInto(LibraryManager.library, {
    $YTGameSendMessage: function (objectName, methodName, value) {
        window.YTGameLifecycle.send(methodName, value);
    },
    YTGameAttachRuntime__deps: ['emscripten_pause_main_loop', 'emscripten_resume_main_loop'],
    YTGameAttachRuntime: function () {
        window.YTGameLifecycle.attachRuntime({
            pause: function () { _emscripten_pause_main_loop(); },
            resume: function () { _emscripten_resume_main_loop(); }
        });
    },
    GetYTGameSDKVersion: function () {
        var version = String(ytgame.SDK_VERSION || '');
        var length = lengthBytesUTF8(version) + 1, buffer = _malloc(length);
        stringToUTF8(version, buffer, length); return buffer;
    },
    SendYTGameScore: function (score) { ytgame.engagement.sendScore({value: score}); },
    SendFirstFrameReady: function () { window.YTGameLifecycle.firstFrame(); },
    SendGameReady: function () { window.YTGameLifecycle.ready(); },
    LoadGameData__deps: ['$YTGameSendMessage'],
    LoadGameData: function () {
        var life = window.YTGameLifecycle;
        life.once(function () { return ytgame.game.loadData(); }, 12000, function (status, data) {
            if (status === 'success') {
                try { YTGameSendMessage('YTGameWrapper', 'ReceiveOnLoadSaveEvent', life.normalizeSave(data)); return; }
                catch (_) { /* Corruption is a failed load, never an empty cloud save. */ }
            }
            YTGameSendMessage('YTGameWrapper', 'ReceiveOnLoadSaveFailed', '');
        });
    },
    SaveGameData__deps: ['$YTGameSendMessage'],
    SaveGameData: function (pointer) {
        var json = UTF8ToString(pointer);
        window.YTGameLifecycle.once(function () { return ytgame.game.saveData(json); }, 12000, function (status) {
            YTGameSendMessage('YTGameWrapper', 'ReceiveOnSaveResult', status);
        });
    },
    GetGameLanguage__deps: ['$YTGameSendMessage'],
    GetGameLanguage: function () {
        window.YTGameLifecycle.getLanguage().then(function (tag) {
            YTGameSendMessage('YTGameWrapper', 'ReceiveOnLanguage', tag);
        });
    },
    SetGameStatus: function (pointer) { window.YTGameLifecycle.status(UTF8ToString(pointer)); },
    SendGameError: function (pointer) { console.error(UTF8ToString(pointer)); ytgame.health.logError(); },
    SendGameWarning: function (pointer) { console.warn(UTF8ToString(pointer)); ytgame.health.logWarning(); },
    CheckIsAudioEnabled: function () { return window.YTGameLifecycle.audioEnabled; },
    SetCallbackOnAudioEnabledChange: function () { window.YTGameLifecycle.register('audio'); },
    SetCallbackOnGamePause: function () { window.YTGameLifecycle.register('pause'); },
    SetCallbackOnGameResume: function () { window.YTGameLifecycle.register('resume'); },
    IsGamePaused: function () { return window.YTGameLifecycle.paused; },
    CheckInPlayablesEnv: function () { return window.YTGameLifecycle.inPlayables; },
    RequestAnInterstitialAd__deps: ['$YTGameSendMessage'],
    RequestAnInterstitialAd: function () {
        window.YTGameLifecycle.once(function () { return ytgame.ads.requestInterstitialAd(); }, 120000, function () {
            // Settling does not prove an impression. Only release the caller's UI.
            YTGameSendMessage('YTGameWrapper', 'ReceiveOnInterstitialAdEvent', 'false');
        });
    },
    RequestAnRewardedAd__deps: ['$YTGameSendMessage'],
    RequestAnRewardedAd: function (pointer) {
        var rewardId = UTF8ToString(pointer);
        window.YTGameLifecycle.once(function () { return ytgame.ads.requestRewardedAd(rewardId); }, 120000,
            function (status, earned) {
                YTGameSendMessage('YTGameWrapper', 'ReceiveOnRewardedAdEvent', status === 'success' && earned === true ? 'true' : 'false');
            });
    }
});
