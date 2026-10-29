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

// ==================================================
// Begin YouTube Playables integration section 
// ==================================================

using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Scripting;

namespace YTGameSDK
{
    // This code is designed to help communication between YT Game JS SDK and Unity.
    [Preserve]
    public class YTGameWrapper : MonoBehaviour
    {
        // connect external call
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern string GetYTGameSDKVersion();

        [DllImport("__Internal")]
        private static extern void SendYTGameScore(int score);
    
        [DllImport("__Internal")]
        private static extern void SendFirstFrameReady();
    
        [DllImport("__Internal")]
        private static extern void SendGameReady();
    
        [DllImport("__Internal")]
        private static extern void SaveGameData(string gameSaveData);
        [DllImport("__Internal")] private static extern void YTGameAttachRuntime();
        [DllImport("__Internal")] private static extern void GetGameLanguage();
        [DllImport("__Internal")] private static extern bool IsGamePaused();
        [DllImport("__Internal")] private static extern void SetGameStatus(string message);
    
        [DllImport("__Internal")]
        private static extern void LoadGameData();
    
        [DllImport("__Internal")]
        private static extern void SendGameError(string errorMessage);
    
        [DllImport("__Internal")]
        private static extern void SendGameWarning(string warningMessage);
    
        [DllImport("__Internal")]
        private static extern bool CheckIsAudioEnabled();
    
        [DllImport("__Internal")]
        private static extern void SetCallbackOnAudioEnabledChange();
    
        [DllImport("__Internal")]
        private static extern void SetCallbackOnGamePause();
    
        [DllImport("__Internal")]
        private static extern void SetCallbackOnGameResume();

        [DllImport("__Internal")]
        private static extern bool CheckInPlayablesEnv();
        
        [DllImport("__Internal")]
        private static extern void RequestAnInterstitialAd();
        
        [DllImport("__Internal")]
        private static extern void RequestAnRewardedAd(string rewardId);
#endif

        // Used to set callbacks for when the audio changes on YT's end
        public delegate void OnYTGameAudioChange(bool isAudioEnabled);
        protected OnYTGameAudioChange callbackOnYTGameAudioChange;

        // Used to set callbacks for when YT Game pause happens
        public delegate void OnYTGamePause();
        protected OnYTGamePause callbackOnYTGamePause;

        // Used to set callbacks for when YT Game resume happens
        public delegate void OnYTGameResume();
        protected OnYTGameResume callbackOnYTGameResume;

        // Used to set callbacks for when YT Game resume happens
        public delegate void OnYTGameLoadSave(string data);
        protected OnYTGameLoadSave callbackOnYTGameLoadSave;

        // Used to set callbacks for when YT Game rewarded ad completes
        public delegate void OnYTGameRewardedAd(bool earned);
        protected OnYTGameRewardedAd callbackOnYTGameRewardedAd;

        // Used to set callbacks when interstitial ad promise settles (shown or not).
        public delegate void OnYTGameInterstitialAd(bool shown);
        protected OnYTGameInterstitialAd callbackOnYTGameInterstitialAd;

        public bool DontDestroyOnSceneChange = true;
        private Action<string> saveCallback, languageCallback;
        private bool adPending;
        public bool IsAdPending => adPending;

        private void Awake()
        {
            // JS SendMessage targets the GameObject name "YTGameWrapper".
            if (gameObject.name != "YTGameWrapper")
                gameObject.name = "YTGameWrapper";

            if (DontDestroyOnSceneChange) GameObject.DontDestroyOnLoad(this.gameObject);
#if UNITY_WEBGL && !UNITY_EDITOR
            YTGameAttachRuntime();
#endif
        }

        #region YouTube Game SDK
        // Receive The YouTube Playables SDK version.
        public string GetTheYTGameSDKVersion()
        {
            string theSDKVersion = "";
#if UNITY_WEBGL && !UNITY_EDITOR
            theSDKVersion = GetYTGameSDKVersion();
#endif
            return theSDKVersion;
        }

        // The score object the game sends to YouTube.
        // The score value expressed as an integer.
        public void SendGameScore(int score)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            SendYTGameScore(score);
#endif
        }

        // Notifies YouTube that the game�s first frame is ready to be shown.
        // Note: The game MUST call this API. Otherwise, the game isn�t shown on YouTube.
        public void SendGameFirstFrameReady()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            SendFirstFrameReady();
#endif
        }

        // Notifies YouTube that the game is ready for players to interact with.
        public void SendGameIsReady()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            SendGameReady();
#endif
        }

        // Local extension to Google's wrapper: acknowledge the Promise, never a sync status.
        public void SendGameSaveData(string json, Action<string> callback)
        {
            if (saveCallback != null) { callback("failed"); return; }
            saveCallback = callback;
#if UNITY_WEBGL && !UNITY_EDITOR
            SaveGameData(json);
#else
            ReceiveOnSaveResult("failed");
#endif
        }
        [Preserve] public void ReceiveOnSaveResult(string status)
        {
            var callback = saveCallback; saveCallback = null; callback?.Invoke(status);
        }
        public void LoadLanguage(Action<string> callback)
        {
            languageCallback = callback;
#if UNITY_WEBGL && !UNITY_EDITOR
            GetGameLanguage();
#else
            ReceiveOnLanguage("en");
#endif
        }
        [Preserve] public void ReceiveOnLanguage(string language)
        { var callback = languageCallback; languageCallback = null; callback?.Invoke(language); }
        public bool IsPlatformPaused()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return IsGamePaused();
#else
            return false;
#endif
        }
        public void ShowStatus(string message)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            SetGameStatus(message);
#endif
        }

        // Loads game data from the YouTube cloud in the form of a serialized string.
        // @return a Promise that completes when loading succeeded and rejects with an SdkError when failed.
        // Also sets a callback function for your loaded save data
        public void LoadGameSaveData(OnYTGameLoadSave onLoadSaveCallback)
        {
            callbackOnYTGameLoadSave = onLoadSaveCallback;

#if UNITY_WEBGL && !UNITY_EDITOR
            if (!CheckInPlayablesEnv())
            {
                ReceiveOnLoadSaveFailed("");
                return;
            }
            LoadGameData();
#else
            // Editor / non-WebGL: mirror GameProgressStore's PlayerPrefs backup so F5-like
            // domain reloads and Play Mode restarts still restore progress.
            var local = PlayerPrefs.GetString("YT_PLAYABLES_SAVE", string.Empty);
            StartCoroutine(InvokeLoadSaveNextFrame(local));
#endif
        }

        private IEnumerator InvokeLoadSaveNextFrame(string data)
        {
            yield return null;
            ReceiveOnLoadSaveEvent(data ?? string.Empty);
        }

        // After triggering load game save data, this callback will be triggered from YT JS.
        // Note: The game must call LoadGameSaveData before this will trigger
        [Preserve] public void ReceiveOnLoadSaveEvent(string data)
        {
            var callback = callbackOnYTGameLoadSave; callbackOnYTGameLoadSave = null;
            callback?.Invoke(data ?? string.Empty);
        }

        // Called from JS when loadData rejects. Invokes callback with null so callers can
        // finish waiting without treating the result as an authoritative empty cloud save.
        [Preserve] public void ReceiveOnLoadSaveFailed(string unused)
        {
            var callback = callbackOnYTGameLoadSave; callbackOnYTGameLoadSave = null; callback?.Invoke(null);
        }

        // Logs an error to YouTube.Aggregate information will be accessible on the YouTube Playables health page.
        // Note: This API is best-effort and rate-limited which can result in data loss.
        public void SendYTGameError(string errorMessage)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            SendGameError(errorMessage);
#endif
        }

        // Logs a warning to YouTube.Aggregate information will be accessible on the YouTube Playables health webpage.
        // Note: This API is best-effort and rate-limited which can result in data loss.
        public void SendYTGameWarning(string warningMessage)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            SendGameWarning(warningMessage);
#endif
        }

        // Returns whether the game audio is enabled in the YouTube settings.
        // Note: The game SHOULD use this API to initialize the game audio state.
        public bool IsYTGameAudioEnabled()
        {
            bool isEnabled = true;
#if UNITY_WEBGL && !UNITY_EDITOR
            isEnabled = CheckIsAudioEnabled();
#endif
            return isEnabled;
        }

        // Sets a callback to be triggered when the audio settings change event is fired from YouTube.
        // Note: The game MUST use this API to update the game audio state.
        public void SetOnAudioEnabledChangeCallback(OnYTGameAudioChange audioCallback)
        {
            callbackOnYTGameAudioChange = audioCallback;
#if UNITY_WEBGL && !UNITY_EDITOR
            // Sets up the callback to receive when YT audio settings change 
            SetCallbackOnAudioEnabledChange();
#endif
        }

        // After setting a callback this is triggered from YT JS when receiving an audio event change.
        // Note: The game must call SetOnAudioEnabledChangeCallback before this to receive changes
        // @param isAudioEnabled whether the audio is enabled in the YouTube settings.
        // Receive callback from OnAudioEnabled in YT Game SDK JS
        [Preserve] public void ReceiveOnAudioEnabledChange(string isAudioEnabled)
        {
            bool isEnabled = (isAudioEnabled == "true" || isAudioEnabled == "True");
            callbackOnYTGameAudioChange?.Invoke(isEnabled);
        }

        // Sets a callback to be triggered when a pause game event is fired from YouTube.
        // Note: You have a short window to save any state before your game gets evicted.
        public void SetOnPauseCallback(OnYTGamePause onPauseCallback)
        {
            callbackOnYTGamePause = onPauseCallback;
#if UNITY_WEBGL && !UNITY_EDITOR
            // Sets up the callback to receive when YT pause happens 
            SetCallbackOnGamePause();
#endif
        }

        // After setting a callback this is triggered from YT JS when receiving an on Pause event.
        // Note: The game must call SetOnPauseCallback before this to receive changes
        [Preserve] public void ReceiveOnPauseEvent(string unused)
        {
            callbackOnYTGamePause?.Invoke();
        }

        // Sets a callback to be triggered when a resume game event is fired from YouTube.
        // @return a function to unset the callback.
        public void SetOnResumeCallback(OnYTGameResume onResumeCallback)
        {
            callbackOnYTGameResume = onResumeCallback;
#if UNITY_WEBGL && !UNITY_EDITOR
            // Sets up the callback to receive when YT pause happens 
            SetCallbackOnGameResume();
#endif
        }

        // After setting a callback this is triggered from YT JS when receiving an on Resume event.
        // Note: The game must call SetOnResumeCallback before this to receive changes
        [Preserve] public void ReceiveOnResumeEvent(string unused)
        {
            callbackOnYTGameResume?.Invoke();
        }

        // Returns whether the game is currently loaded in a proper Playables Environment.
        // Used to enusre calls like save data and send score are done in a Playables Env.
        public bool InPlayablesEnv()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return CheckInPlayablesEnv();
#elif UNITY_EDITOR
            // Editor is not a Playables host; pretend false so callers skip cloud-only paths.
            return false;
#else
            return false;
#endif
        }
        
        // Requests an interstitial. Completion is not evidence that an ad was displayed.
        public void RequestInterstitialAd(OnYTGameInterstitialAd callback = null)
        {
            if (adPending) { callback?.Invoke(false); return; }
            adPending = true;
            callbackOnYTGameInterstitialAd = callback;
#if UNITY_WEBGL && !UNITY_EDITOR
            RequestAnInterstitialAd();
#else
            // Editor / non-WebGL: settle immediately as "not shown".
            ReceiveOnInterstitialAdEvent("false");
#endif
        }

        // Called when the request settles. This callback must not be counted as an impression.
        [Preserve] public void ReceiveOnInterstitialAdEvent(string shownStr)
        {
            bool shown = (shownStr == "true" || shownStr == "True");
            var cb = callbackOnYTGameInterstitialAd;
            callbackOnYTGameInterstitialAd = null; adPending = false;
            cb?.Invoke(shown);
        }

        public void RequestRewardedAd(string rewardId, OnYTGameRewardedAd callback) {
            if (adPending) { callback?.Invoke(false); return; }
            adPending = true;
            callbackOnYTGameRewardedAd = callback;
#if UNITY_WEBGL && !UNITY_EDITOR
            RequestAnRewardedAd(rewardId);
#else
            // Fallback for editor
            ReceiveOnRewardedAdEvent("false");
#endif
        }

        [Preserve] public void ReceiveOnRewardedAdEvent(string rewardEarnedStr) {
            bool earned = (rewardEarnedStr == "true" || rewardEarnedStr == "True");
            var callback = callbackOnYTGameRewardedAd; callbackOnYTGameRewardedAd = null;
            adPending = false; callback?.Invoke(earned);
        }
        #endregion
    }
}
