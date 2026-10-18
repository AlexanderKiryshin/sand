const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const root = path.resolve(__dirname, '..');
const sdk = fs.readdirSync(path.join(root, 'Library/PackageCache')).find(n => n.startsWith('com.romanlee17.mirrasdk5@'));
assert.ok(sdk, 'Installed SDK is required');
const ctx = vm.createContext({ Module: {}, UTF8ToString: v => v, LibraryManager: { library: {} }, mergeInto: Object.assign });
for (const feature of ['ads', 'payments', 'data', 'language', 'analytics']) {
    vm.runInContext(fs.readFileSync(path.join(root, 'Library/PackageCache', sdk, 'Runtime/MirraWeb/Plugins', `mirraSDK_${feature}_library.jslib`), 'utf8'), ctx);
}
vm.runInContext(fs.readFileSync(path.join(root, 'Assets/Plugins/WebGL/SandSdkReadiness.jslib'), 'utf8'), ctx);
const api = ctx.LibraryManager.library;
assert.throws(() => api.mirraSDK_ads_isRewardedReady(), /Cannot read properties of undefined \(reading 'ads'\)/);
assert.equal(api.sand_sdk_isBridgeReady(), 0);
delete ctx.Module;
assert.equal(api.sand_sdk_isBridgeReady(), 0);
const calls = [];
const fullSdk = {
    ads: { isRewardedReady: true, isInterstitialReady: true, invokeRewarded: () => calls.push('rewarded'), invokeInterstitial: () => calls.push('interstitial') },
    payments: { getProductPrice: () => 10, getProductCurrency: () => 'RUB', isProductPurchased: () => false, purchase: () => calls.push('purchase'), updatePurchases: () => {}, purchases: [] },
    data: { get: (_, value) => value, set: () => calls.push('data'), hasKey: () => false, save: () => calls.push('save') },
    analytics: { gameIsReady: () => calls.push('ready'), gameplayStart: () => calls.push('start'), gameplayStop: () => calls.push('stop') },
    language: { current: 1 }
};
ctx.Module = { allocateString: value => value, invokeMonoPCallback: () => {} };
for (const feature of Object.keys(fullSdk)) {
    ctx.Module.mirraSDK = { ...fullSdk, [feature]: undefined };
    assert.equal(api.sand_sdk_isBridgeReady(), 0, `Missing ${feature} must block SDK calls`);
    if (api.sand_sdk_isBridgeReady()) api.mirraSDK_ads_invokeRewarded(1, 2, 3);
}
assert.equal(calls.length, 0);
ctx.Module.mirraSDK = { ...fullSdk, ads: {} };
assert.equal(api.sand_sdk_isBridgeReady(), 0, 'Incomplete API must not pass readiness');
ctx.Module.mirraSDK = fullSdk;
assert.equal(api.sand_sdk_isBridgeReady(), 1);
assert.equal(api.mirraSDK_ads_isRewardedReady(), true);
assert.equal(api.mirraSDK_ads_isInterstitialReady(), true);
api.mirraSDK_ads_invokeRewarded(1, 2, 3);
api.mirraSDK_ads_invokeInterstitial(1, 2, 3);
assert.equal(api.mirraSDK_language_current(), 1);
assert.equal(api.mirraSDK_data_getInt('key', 4), 4);
api.mirraSDK_data_setInt('key', 4, false);
api.mirraSDK_data_save();
assert.equal(api.mirraSDK_payments_getProductPrice('product'), 10);
api.mirraSDK_payments_purchase(1, 'product', 2, 3);
api.mirraSDK_analytics_gameIsReady();
api.mirraSDK_analytics_gameplayStart(1);
api.mirraSDK_analytics_gameplayStop(1);
assert.deepEqual(calls, ['rewarded', 'interstitial', 'data', 'save', 'purchase', 'ready', 'start', 'stop']);
console.log('SDK_BRIDGE_READINESS_CHECKS_PASS: exact crash reproduced; missing/partial modules blocked; installed SDK calls work when ready');
