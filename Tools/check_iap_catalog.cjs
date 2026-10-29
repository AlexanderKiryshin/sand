// Validate the release catalog against production IDs/rewards and the requested price changes.
const fs = require('node:fs');
const path = require('node:path');
const assert = require('node:assert/strict');
const root = path.resolve(__dirname, '..');
const read = file => fs.readFileSync(path.join(root, file), 'utf8');
const json = file => JSON.parse(read(file));
const catalog = json('tasks/iap-catalog.json');
const shop = read('Assets/Scripts/UI/RewardedShop.cs');
const hud = read('Assets/Scripts/UI/SandEconomyHud.cs');
const boosterSource = read('Assets/Scripts/Boosters/BoosterCatalog.cs');
const products = new Map(catalog.products.map(product => [product.id, product]));
const constant = (name, source = shop) => {
  const match = source.match(new RegExp(`public const int ${name} = (\\d+);`));
  assert.ok(match, `Missing runtime constant ${name}`);
  return Number(match[1]);
};
assert.equal(catalog.currency, 'RUB');
assert.equal(products.size, 47);
assert.equal(catalog.products.length, products.size, 'Duplicate product IDs');
for (const id of [...catalog.retired_product_ids, ...catalog.legacy_restore_only_ids])
  assert.ok(!products.has(id), `Retired product remains on sale: ${id}`);
const theme = json('tasks/theme-iap-products.json');
const styles = json('tasks/style-bundles/products.json');
const boosters = json('tasks/booster-shop/products.json');
const resourceIds = [...shop.matchAll(/private const string \w+Tag = "([^"]+)";/g)].map(match => match[1]);
const expectedIds = [...resourceIds, ...boosters.products.map(p => p.id), ...theme.products.map(p => p.tag), ...styles.products.map(p => p.id)];
assert.deepEqual([...products.keys()].sort(), expectedIds.sort(), 'Catalog and runtime products disagree');
const rate = catalog.economy_price_basis.exchange_rate;
assert.ok(Number.isInteger(rate.coins) && rate.coins > 0);
assert.ok(Number.isInteger(rate.price_rub) && rate.price_rub > 0);
const coinPurchase = catalog.economy_price_basis.booster_coin_purchase;
assert.equal(coinPurchase.coins, constant('BuyPriceCoins', boosterSource));
assert.equal(coinPurchase.charges, constant('AdGrantCharges', boosterSource));
const coinHandler = hud.slice(hud.indexOf('private void BuyWithCoins()'), hud.indexOf('private void BuyWithAd()'));
assert.ok(coinHandler.includes('TryToSpendCoins(BoosterCatalog.BuyPriceCoins)'));
assert.ok(coinHandler.includes('GrantCharges(pendingBuyType, BoosterCatalog.AdGrantCharges)'));
const gold = [['gold_800', 'GoldSmallAmount', 800, 0], ['gold_1500', 'GoldMediumAmount', 1500, 10], ['gold_3000', 'GoldLargeAmount', 3000, 25]];
const discountedGoldIds = new Set(gold.filter(entry => entry[3] > 0).map(entry => entry[0]));
for (const [id, field, amount, discount] of gold) {
  assert.equal(constant(field), amount);
  assert.deepEqual(products.get(id).rewards, {coins: amount});
  assert.equal(products.get(id).discount_percent || 0, discount);
  assert.equal(products.get(id).price_rub * rate.coins * 100, amount * rate.price_rub * (100 - discount), `Gold discount differs: ${id}`);
  assert.ok(hud.includes('"Gold " + RewardedShop.' + field), `Shop card is detached from payout: ${id}`);
}
const prices = {resource_bundle: 130, conveyor: 200, casings: 100, background: 200, background_bundle: 300, style_bundle: 300};
for (const product of products.values()) {
  assert.ok(Number.isInteger(product.price_rub) && product.price_rub > 0);
  if (!discountedGoldIds.has(product.id))
    assert.equal(product.price_rub % 10, 0, `Non-discounted price must be a multiple of 10: ${product.id}`);
  if (product.category in prices) assert.equal(product.price_rub, prices[product.category], product.id);
}
for (const entry of boosters.products) {
  const type = {Magnet: 'magnet', Transparent: 'fill', Shuffle: 'shuffle'}[entry.booster];
  assert.deepEqual(products.get(entry.id).rewards, {[type]: constant('SingleBoosterPackCount')});
  const pack = products.get(entry.id);
  assert.equal(pack.price_rub * rate.coins * coinPurchase.charges,
    pack.rewards[type] * coinPurchase.coins * rate.price_rub,
    `Direct booster price differs from the base coin rate: ${entry.id}`);
  for (const [goldId, , , discount] of gold) {
    const goldPack = products.get(goldId);
    assert.equal(pack.price_rub * goldPack.rewards.coins * coinPurchase.charges * (100 - discount),
      pack.rewards[type] * coinPurchase.coins * goldPack.price_rub * 100,
      `Effective booster discount differs via ${goldId}: ${entry.id}`);
  }
}
for (const [id, coins, charges] of [['premium_kit_75', 'SmallBundleGoldAmount', 'SmallBundleBoosterBonus'], ['removeAd', 'RemoveAdsGoldAmount', 'RemoveAdsBoosterBonus']]) {
  const reward = products.get(id).rewards;
  assert.equal(reward.coins, constant(coins));
  for (const type of ['magnet', 'fill', 'shuffle']) assert.equal(reward[type], constant(charges));
}
assert.equal(products.get('removeAd').price_rub, catalog.remove_ads_price_basis.booster_pack_ids.reduce((total, id) => total + products.get(id).price_rub, 0));
assert.equal(products.get('removeAd').rewards.no_ads, true);
assert.equal(products.get('removeAd').consumable, false, 'Remove ads must be a one-time purchase');
for (const bundle of theme.products.filter(p => p.category === 'background_bundle')) {
  const item = products.get(bundle.tag);
  assert.deepEqual(item.rewards.cosmetics.map(c => c.theme_index), bundle.background_ids);
  assert.equal(item.price_rub, bundle.background_ids.length * prices.background * 0.5, 'Background bundle lost its 50% discount');
}
for (const bundle of styles.products)
  assert.deepEqual(products.get(bundle.id).rewards.cosmetics, bundle.items.map(item => ({part: item.part, theme_index: item.theme})));
console.log(`IAP_CATALOG_CHECKS_PASS: 47 products, gold discounts exactly 10% and 25%, other prices divisible by 10, all 3 booster packs retain base-rate prices and inherit gold discounts when bought with coins, removeAd = ${products.get('removeAd').price_rub} RUB, bundle contents and 50% background discount.`);
