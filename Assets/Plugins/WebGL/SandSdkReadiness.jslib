mergeInto(LibraryManager.library, {
    sand_sdk_isBridgeReady: function () {
        // SDK C# flags alone cannot guarantee that this particular Unity JS Module has its bridge.
        try {
            if (typeof Module === 'undefined' || !Module.mirraSDK) return 0;
            var sdk = Module.mirraSDK;
            var required = {
                ads: ['invokeRewarded', 'invokeInterstitial'],
                payments: ['getProductPrice', 'getProductCurrency', 'isProductPurchased', 'purchase', 'updatePurchases'],
                data: ['get', 'set', 'hasKey', 'save'],
                analytics: ['gameIsReady', 'gameplayStart', 'gameplayStop']
            };
            if (!sdk.language || typeof sdk.language.current === 'undefined') return 0;
            for (var name in required) {
                if (!sdk[name]) return 0;
                for (var i = 0; i < required[name].length; i++)
                    if (typeof sdk[name][required[name][i]] !== 'function') return 0;
            }
            return 1;
        } catch (_) {
            return 0;
        }
    }
});
