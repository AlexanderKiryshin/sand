// Execute the installed WebGL bridges with deterministic SDK/browser doubles.
const fs = require('node:fs');
const vm = require('node:vm');
const assert = require('node:assert/strict');
const root = require('node:path').resolve(__dirname, '..');
const calls = [];
const ga = { LibraryManager: {library: {}}, UTF8ToString: value => value,
  mergeInto: Object.assign, autoAddDeps: () => {},
  gameanalytics: { GameAnalytics: new Proxy({}, {get: (_, name) => (...args) => calls.push([name, ...args])}) } };
vm.runInNewContext(fs.readFileSync(root + '/Packages/com.gameanalytics.sdk/Runtime/WebGL/GameAnalyticsUnity.jslib', 'utf8'), ga);
const fields = {level: 300, level_id: 'level_300', moves: 17, duration_seconds: 41.25};
for (const status of [1, 2, 3]) ga.LibraryManager.library.addProgressionEvent(status, 'campaign', 'level_300', '', JSON.stringify(fields), false);
ga.LibraryManager.library.addDesignEvent('gameplay:bonus_used', JSON.stringify({...fields, bonusType: 'magnet'}), false);
assert.equal(calls.length, 4);
assert.equal(calls[1][0], 'addProgressionEvent');
assert.equal(calls[1][2], 'campaign');
assert.equal(calls[1][3], 'level_300');
assert.equal(calls[1][5].level, 300);
assert.equal(calls[3][2].bonusType, 'magnet');
const browser = {LibraryManager: {library: {}}, UTF8ToString: value => value, mergeInto: Object.assign, console};
browser.window = browser;
const scripts = [{src: 'game.loader.js', parentNode: {insertBefore: node => scripts.push(node)}}];
browser.document = {scripts, createElement: tag => ({tag}), getElementsByTagName: () => scripts,
  head: {appendChild: node => {scripts.push(node); if (node.textContent) vm.runInContext(node.textContent, context);}}, body: {appendChild: () => {}}};
const context = vm.createContext(browser);
vm.runInContext(fs.readFileSync(root + '/Assets/PluginYourGames/Modules/Metrica/Plugins/YandexMetrica.jslib', 'utf8'), context);
browser.LibraryManager.library.InitYandexMetrica('12345678');
browser.LibraryManager.library.YandexMetricaSend2_js('level_completed', JSON.stringify(fields));
assert.equal(browser.yandexMetricaCounterId, 12345678);
assert.equal(browser.ym.a[0][1], 'init');
assert.equal(browser.ym.a[1][1], 'reachGoal');
assert.equal(browser.ym.a[1][2], 'level_completed');
assert.equal(browser.ym.a[1][3].level, 300);
for (const adType of ['interstitial', 'rewarded']) {
  const eventName = adType + '_shown';
  browser.LibraryManager.library.YandexMetricaSend2_js(eventName, JSON.stringify({
    ...fields, adType, placement: adType === 'rewarded' ? 'magnet' : 'interstitial', result: 'shown'
  }));
  const goal = browser.ym.a[browser.ym.a.length - 1];
  assert.equal(goal[1], 'reachGoal');
  assert.equal(goal[2], eventName);
  assert.equal(goal[3].level, 300);
  assert.equal(goal[3].adType, adType);
  assert.equal(goal[3].result, 'shown');
  assert.equal(goal[3].placement, adType === 'rewarded' ? 'magnet' : 'interstitial');
}
for (const [product, eventName] of [
  ['beginerKit_150', 'purchase_beginner_kit'], ['premium_kit_75', 'purchase_premium_kit'],
  ['gold_300', 'purchase_gold_300'], ['gold_700', 'purchase_gold_700'],
  ['gold_1200', 'purchase_gold_1200'], ['removeAd', 'purchase_remove_ads']
]) {
  for (const name of ['buy_inapp', eventName]) {
    browser.LibraryManager.library.YandexMetricaSend2_js(name, JSON.stringify({...fields, product_id: product, result: 'success'}));
    const goal = browser.ym.a[browser.ym.a.length - 1];
    assert.equal(goal[2], name);
    assert.equal(goal[3].product_id, product);
    assert.equal(goal[3].level, 300);
  }
}
assert.equal(scripts.filter(s => s.src === 'https://mc.yandex.ru/metrika/tag.js').length, 1);
for (let level = 1; level <= 30; level++) {
  for (const outcome of ['started', 'completed']) {
    const name = 'level_' + String(level).padStart(3, '0') + '_' + outcome;
    browser.LibraryManager.library.YandexMetricaSend2_js(name, JSON.stringify({...fields, level}));
    const goal = browser.ym.a[browser.ym.a.length - 1];
    assert.equal(goal[2], name);
    assert.equal(goal[3].level, level);
  }
}
for (const bonus of ['magnet', 'transparent', 'shuffle']) {
  browser.LibraryManager.library.YandexMetricaSend2_js('booster_' + bonus + '_used', JSON.stringify({
    ...fields, booster_type: bonus, level_booster: 'level_300:' + bonus
  }));
  const goal = browser.ym.a[browser.ym.a.length - 1];
  assert.equal(goal[3].level, 300);
  assert.equal(goal[3].level_booster, 'level_300:' + bonus);
}
for (const name of ['level_restarted', 'conveyor_expanded']) {
  browser.LibraryManager.library.YandexMetricaSend2_js(name, JSON.stringify(fields));
  const goal = browser.ym.a[browser.ym.a.length - 1];
  assert.equal(goal[2], name);
  assert.equal(goal[3].level, 300);
}
for (const name of ['cosmetic_acquired', 'cosmetic_selected']) {
  const data = {...fields, cosmetic_id: 'theme_olive_marks', cosmetic_category: 'background', cosmetic_index: 4};
  if (name === 'cosmetic_acquired') Object.assign(data, {acquisition: 'coins', product_id: 'sand_theme_ocean'});
  browser.LibraryManager.library.YandexMetricaSend2_js(name, JSON.stringify(data));
  const goal = browser.ym.a[browser.ym.a.length - 1];
  assert.equal(goal[2], name);
  assert.equal(goal[3].cosmetic_id, 'theme_olive_marks');
  assert.equal(goal[3].cosmetic_category, 'background');
  assert.equal(goal[3].cosmetic_index, 4);
  assert.equal(goal[3].level, 300);
  if (name === 'cosmetic_acquired') assert.equal(goal[3].acquisition, 'coins');
}
fs.writeFileSync(root + '/tasks/web-analytics-bridge-validation.md', 'PASS: official GameAnalytics WebGL progression/design bridges and Your Games Metrica initialization/queued goals preserve level 300, completion, interstitial/rewarded impressions, placement and cosmetic acquisition/selection parameters. SDK/browser doubles; no production requests or credentials.\n');
console.log('WEB_ANALYTICS_BRIDGES_PASS');
