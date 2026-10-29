// ZIP loading from BUSES MirraYoutubePlayablesTemplate, with local JSZip and Sand's UI.
const unityApp = {
    fetchAndUnzip: async function (url, filename) {
        await YTGameLifecycle.whenResumed();
        const response = await fetch(url);
        if (!response.ok) throw new Error('Download failed: ' + url);
        const buffer = await response.arrayBuffer();
        await YTGameLifecycle.whenResumed();
        const archive = await JSZip.loadAsync(buffer);
        const entry = archive.file(filename);
        if (!entry) throw new Error('Missing ZIP entry: ' + filename);
        const content = await entry.async('arraybuffer');
        return URL.createObjectURL(new Blob([content], {type: filename.endsWith('.wasm') ? 'application/wasm' : 'application/octet-stream'}));
    },
    start: async function (settings) {
        await YTGameLifecycle.whenResumed();
        const urls = await Promise.all([
            unityApp.fetchAndUnzip(settings.dataUrl, settings.dataName),
            unityApp.fetchAndUnzip(settings.codeUrl, settings.codeName)
        ]);
        await YTGameLifecycle.whenResumed();
        await new Promise((resolve, reject) => {
            const script = document.createElement('script');
            script.src = settings.loaderUrl; script.onload = resolve; script.onerror = reject;
            document.body.appendChild(script);
        });
        await YTGameLifecycle.whenResumed();
        try {
            const instance = await createUnityInstance(document.getElementById('unity-canvas'), {
                dataUrl: urls[0], codeUrl: urls[1], frameworkUrl: settings.frameworkUrl,
                companyName: settings.companyName, productName: settings.productName,
                productVersion: settings.productVersion, streamingAssetsUrl: 'StreamingAssets',
                devicePixelRatio: Math.min(window.devicePixelRatio || 1, 2), matchWebGLToCanvasSize: true
            }, value => { document.getElementById('progress').value = value; });
            window.unityGameInstance = instance;
            YTGameLifecycle.attachUnity(instance);
        } finally { urls.forEach(url => URL.revokeObjectURL(url)); }
    }
};
