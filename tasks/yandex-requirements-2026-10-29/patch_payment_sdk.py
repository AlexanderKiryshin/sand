from pathlib import Path
import json
import uuid

stage = Path('E:/myProjects/sand/Temp/YandexPaymentsChanges')
plugins = stage / 'Packages/com.romanlee17.mirrasdk5/Runtime/MirraWeb/Plugins'
path = plugins / 'mirraSDK.jspre'
source = path.read_text(encoding='utf-8')
if 'Module.createSandYandexPayments' not in source:
    marker = source.index('YaGames.init')
    start = source.rfind(';var ', 0, marker)
    end = source.index(';var ', marker)
    part = source[start:end]
    assert part.count('o.createInstance(r.Payments,t.SDK)') == 1
    part = part.replace('o.createInstance(r.Payments,t.SDK)', 'o.createInstance(r.Payments,t.SDK,t.data)')
    payment_start = part.index('this.Payments=class extends S')
    brace = part.index('{', payment_start)
    depth = 1
    payment_end = brace + 1
    while depth:
        if part[payment_end] == '{': depth += 1
        elif part[payment_end] == '}': depth -= 1
        payment_end += 1
    part = part[:payment_start] + 'this.Payments=class extends S{constructor(e,data){super();return Module.createSandYandexPayments(this,e,data)}}' + part[payment_end:]
    source = source[:start] + part + source[end:]
    path.write_text(source, encoding='utf-8')
path = plugins / 'mirraSDK_payments_library.jslib'
source = path.read_text(encoding='utf-8')
start = source.index('    mirraSDK_payments_updatePurchases:')
source = source[:start] + '''    mirraSDK_payments_updatePurchases: function (senderId, onSuccessPtr) {
        const deliver = purchases => {
            const json = JSON.stringify({Purchases: purchases});
            Module.invokeMonoPCallback(senderId, onSuccessPtr, json);
        };
        (async () => {
            if (typeof Module.mirraSDK.payments.restorePurchases === 'function') {
                await Module.mirraSDK.payments.restorePurchases(deliver);
            } else {
                await Module.mirraSDK.payments.updatePurchases();
                deliver(Module.mirraSDK.payments.purchases);
            }
        })().catch(error => console.warn('Purchase restoration remains pending:', error));
    },

    sand_payments_currency_image: function (productTagUTF8) {
        const payments = Module.mirraSDK && Module.mirraSDK.payments;
        const url = payments && typeof payments.getProductCurrencyImage === 'function'
            ? payments.getProductCurrencyImage(UTF8ToString(productTagUTF8)) : '';
        return Module.allocateString(url || '');
    },
};
mergeInto(LibraryManager.library, mirraSDK_payments_library);
'''
path.write_text(source, encoding='utf-8')
manifest = stage / 'Packages/manifest.json'
value = json.loads(manifest.read_text(encoding='utf-8'))
value['dependencies']['com.romanlee17.mirrasdk5'] = 'file:com.romanlee17.mirrasdk5'
manifest.write_text(json.dumps(value, indent=2) + '\n', encoding='utf-8')
lock = stage / 'Packages/packages-lock.json'
value = json.loads(lock.read_text(encoding='utf-8'))
entry = value['dependencies']['com.romanlee17.mirrasdk5']
entry.update(version='file:com.romanlee17.mirrasdk5', source='embedded')
entry.pop('hash', None)
lock.write_text(json.dumps(value, indent=2) + '\n', encoding='utf-8')
for path in [plugins / 'SandYandexPayments.jspre']:
    template = (plugins / 'mirraSDK.jspre.meta').read_text(encoding='utf-8')
    import re
    template = re.sub(r'guid: \w+', 'guid: ' + uuid.uuid4().hex, template)
    path.with_name(path.name + '.meta').write_text(template, encoding='utf-8')
print('Patched pinned SDK: Yandex provider, currency image bridge, restore acknowledgement.')
