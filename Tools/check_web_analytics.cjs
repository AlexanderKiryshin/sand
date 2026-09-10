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
assert.equal(scripts.filter(s => s.src === 'https://mc.yandex.ru/metrika/tag.js').length, 1);
fs.writeFileSync(root + '/tasks/web-analytics-bridge-validation.md', 'PASS: official GameAnalytics WebGL progression/design bridges and Your Games Metrica initialization/queued goal preserve level 300 and event parameters. SDK/browser doubles; no production requests or credentials.\n');
console.log('WEB_ANALYTICS_BRIDGES_PASS');
