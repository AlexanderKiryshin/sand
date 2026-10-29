const fs = require('node:fs');
const path = require('node:path');
const zlib = require('node:zlib');
const root = 'G:/codex/sand-yandex-payments/sand';
const output = path.join(__dirname, 'new-build-local-mock');
if (fs.existsSync(output)) throw new Error('Mock copy already exists; inspect it before replacing');
fs.cpSync(path.join(root, 'Builds/YandexPayments'), output, {recursive:true});
const framework = path.join(output, 'Build/YandexPayments.framework.js.br');
let code = zlib.brotliDecompressSync(fs.readFileSync(framework)).toString();
const old = 'Module.createMirraSDK=async r=>{Module.mirraSDK=await K.createInstance(r)}';
if (!code.includes(old)) throw new Error('Unexpected SDK bootstrap');
code = code.replace(old, 'Module.createMirraSDK=async r=>{r.framework.forcePlatformType="YandexGames";Module.mirraSDK=await K.createInstance(r)}');
fs.writeFileSync(framework, zlib.brotliCompressSync(Buffer.from(code)));
const catalog = JSON.parse(fs.readFileSync(path.join(root,'tasks/iap-catalog.json'),'utf8')).products;
const sdk = `// Local deterministic Yandex SDK double. Never distributed with the release.
const MOCK_CATALOG = ${JSON.stringify(catalog.map(p=>({id:p.id,price:String(p.price_rub),priceCurrencyCode:'TST'})))};
window.ym = () => {};
const MOCK_KEY = 'sand-yandex-payments-audit-v1';
const clone = x => JSON.parse(JSON.stringify(x));
const state = JSON.parse(localStorage.getItem(MOCK_KEY) || '{"cloud":{},"receipts":[],"next":0}');
const save = () => localStorage.setItem(MOCK_KEY,JSON.stringify(state));
const player = {
  async getData(){return clone(state.cloud);},
  async setData(data){Object.assign(state.cloud,clone(data));save();console.log('MOCK_SAVE_CONFIRMED',Object.keys(state.cloud['sand-payment-receipts-v1']?.receipts||{}).length);},
  isAuthorized:()=>true,getName:()=> 'Local test',getUniqueID:()=> 'local-test',getPhoto:()=>''
};
const payments = {
  async getCatalog(){return MOCK_CATALOG.map(p=>({...p,getPriceCurrencyImage:()=>'/currency-mock.png'}));},
  async getPurchases(){return clone(state.receipts);},
  async purchase({id}){const receipt={productID:id,purchaseToken:'mock-'+(++state.next)};state.receipts.push(receipt);save();console.log('MOCK_PURCHASE',id);return clone(receipt);},
  async consumePurchase(token){if(!state.cloud['sand-payment-receipts-v1']?.receipts[token])throw new Error('consume before committed reward');state.receipts=state.receipts.filter(r=>r.purchaseToken!==token);save();console.log('MOCK_CONSUME_AFTER_SAVE',token);}
};
const sdk = {
  environment:{app:{id:'local-audit'},i18n:{lang:'ru'},browser:{lang:'ru'}},
  async getPlayer(){return player;},async getPayments(){return payments;},async getFlags(){return{};},
  features:{LoadingAPI:{ready(){console.log('MOCK_READY');}},GameplayAPI:{start(){},stop(){}}},
  adv:{showFullscreenAdv({callbacks}){callbacks.onOpen?.();callbacks.onClose?.(true);},showRewardedVideo({callbacks}){callbacks.onOpen?.();callbacks.onRewarded?.();callbacks.onClose?.();}},
  auth:{async openAuthDialog(){}},on(){},off(){},
  feedback:{async canReview(){return {value:false};}},shortcut:{async canShowPrompt(){return {canShow:false};}}
};
window.YaGames={async init(){console.log('LOCAL_YANDEX_MOCK_ACTIVE');return sdk;}};
`;
fs.writeFileSync(path.join(output,'sdk.js'),sdk);
let html=fs.readFileSync(path.join(output,'index.html'),'utf8');
html=html.replace('</body>','<div style="position:fixed;bottom:0;left:0;background:#fff;color:#24363d;font:11px monospace;z-index:100">LOCAL YANDEX MOCK · TST · NO REAL PAYMENTS</div></body>');
fs.writeFileSync(path.join(output,'index.html'),html);
console.log(output);
