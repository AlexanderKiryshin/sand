const fs = require('node:fs');
const path = require('node:path');
const zlib = require('node:zlib');
const dir = path.join(__dirname, 'build18/Build');
const file = fs.readdirSync(dir).find(n => n.endsWith('.framework.js.br'));
const source = zlib.brotliDecompressSync(fs.readFileSync(path.join(dir, file))).toString();
fs.writeFileSync(path.join(__dirname, 'build18-framework.js'), source);
const urls = [...new Set(source.match(/https?:[^\s"'<>]+/g) || [])];
console.log(JSON.stringify({framework: file, sdkUrls: urls.filter(u => /mirra|yandex|sdk/.test(u))}, null, 2));
for (const term of ['mirraSDK_payments_getProductCurrency', 'mirraSDK_payments_purchase', 'mirraSDK_main', 'mirraSDKConfig']) {
 const index = source.indexOf(term);
 if (index >= 0) console.log(source.slice(Math.max(0,index-100),index+700));
}
