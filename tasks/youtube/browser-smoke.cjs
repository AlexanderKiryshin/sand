'use strict';
// Exercises the exported Unity player with a deterministic SDK fixture. The fixture is
// intercepted by Playwright, never copied into the release bundle or sent to YouTube.
const { chromium } = require('playwright');
const http = require('node:http');
const fs = require('node:fs');
const path = require('node:path');
const assert = require('node:assert/strict');
const root = path.resolve(process.argv[2] || 'Builds/YouTube/2026-10-28-buses-r3');
const output = path.resolve('tasks/youtube');
const report = { bundle: root, integration: 'Google YTGameWrapper / BUSES / Mirra YouTube preset / ZIP loader', environment: 'Local Edge / WebGL2 / software GPU; simulated YouTube SDK', assertions: [], errors: [], requests: [], writes: [] };
let browser, server;
let cloud = JSON.stringify({version:1,values:{Coins:123,CoinsInPreviosLevel:123,sand_progress_version:3,sand_progress_highest_unlocked:0,sand_progress_highest_completed:-1,sand_progress_current_level:0}});
function check(condition, message) {assert.ok(condition,message);report.assertions.push(message);console.log('PASS '+message);}
const fixture = `
window.__ytTest={events:{},ready:0,first:0,ads:[],adFinished:0,loads:0,mode:'true',cloud:null,balance:()=>{const d=__ytTest.cloud;return d&&d.entries?Number(d.entries.find(e=>e.key==='Coins')?.value||0):Number(d?.values?.Coins||0);}};
window.ytgame={IN_PLAYABLES_ENV:true,
 system:{isAudioEnabled:()=>false,getLanguage:()=>Promise.resolve('en-US'),
 onAudioEnabledChange:cb=>__ytTest.events.audio=cb,onPause:cb=>__ytTest.events.pause=cb,onResume:cb=>__ytTest.events.resume=cb},
 game:{firstFrameReady:()=>__ytTest.first++,gameReady:()=>__ytTest.ready++,loadData:()=>{__ytTest.loads++;return window.testLoadCloud().then(data=>{__ytTest.cloud=JSON.parse(data);return data;});},saveData:data=>{__ytTest.cloud=JSON.parse(data);__ytTest.pendingSaves=(__ytTest.pendingSaves||0)+1;return window.testSaveCloud(data).finally(()=>__ytTest.pendingSaves--);}},
 ads:{requestRewardedAd:id=>{__ytTest.ads.push(id);if(__ytTest.mode==='error')return Promise.reject('unavailable');if(__ytTest.mode==='held'){__ytTest.events.pause();return new Promise(resolve=>__ytTest.resolveAd=resolve);}return Promise.resolve(__ytTest.mode==='true');},requestInterstitialAd:()=>{__ytTest.ads.push('interstitial');return Promise.resolve();}}};
`;
(async()=>{
    server=http.createServer((req,res)=>{
        const target=path.resolve(root,'.'+decodeURIComponent(new URL(req.url,'http://localhost').pathname));
        const file=target===root?path.join(root,'index.html'):target;
        if(!file.startsWith(root+path.sep)||!fs.existsSync(file)||!fs.statSync(file).isFile()){res.writeHead(404);res.end();return;}
        const extension=path.extname(file);
        res.setHeader('Content-Type',({'.html':'text/html','.js':'text/javascript','.wasm':'application/wasm','.json':'application/json'})[extension]||'application/octet-stream');
        res.setHeader('Content-Length',fs.statSync(file).size);fs.createReadStream(file).pipe(res);
    });
    await new Promise(resolve=>server.listen(0,'127.0.0.1',resolve));
    browser=await chromium.launch({channel:'msedge',headless:true,args:['--use-angle=swiftshader','--enable-unsafe-swiftshader']});
    if (!process.env.SAND_TOUCH_ONLY) {
    const context=await browser.newContext({viewport:{width:1280,height:900}});
    const page=await context.newPage();
    await page.exposeFunction('testLoadCloud',()=>cloud);
    await page.exposeFunction('testSaveCloud',data=>{cloud=data;report.writes.push(JSON.parse(data));});
    await page.addInitScript(()=>{
        window.__draws=0;window.__gains=[];
        window.__analysers=[];
        const connect=AudioNode.prototype.connect;
        AudioNode.prototype.connect=function(destination,...args){
            if(destination instanceof AudioDestinationNode){
                let analyser=this.context.__testAnalyser;
                if(!analyser){analyser=this.context.createAnalyser();analyser.fftSize=2048;this.context.__testAnalyser=analyser;window.__analysers.push(analyser);connect.call(analyser,destination);}
                return connect.call(this,analyser,...args);
            }
            return connect.call(this,destination,...args);
        };
        window.__outputRms=()=>Math.max(0,...window.__analysers.map(analyser=>{const samples=new Float32Array(analyser.fftSize);analyser.getFloatTimeDomainData(samples);return Math.sqrt(samples.reduce((sum,v)=>sum+v*v,0)/samples.length);}));

        for(const type of [window.WebGLRenderingContext,window.WebGL2RenderingContext])if(type)
            for(const name of ['drawArrays','drawElements']){const original=type.prototype[name];type.prototype[name]=function(...args){window.__draws++;return original.apply(this,args);};}
        const type=window.AudioContext||window.webkitAudioContext;
        if(type){const original=type.prototype.createGain;type.prototype.createGain=function(...args){const gain=original.apply(this,args);window.__gains.push(gain);return gain;};}
    });
    await page.route('https://www.youtube.com/game_api/v1',route=>route.fulfill({contentType:'application/javascript',body:fixture}));
    page.on('pageerror',error=>report.errors.push(error.message));
    page.on('console',message=>{if(message.type()==='error')report.errors.push(message.text());});
    page.on('request',request=>report.requests.push(request.url()));
    const started=Date.now();
    await page.goto('http://127.0.0.1:'+server.address().port+'/',{waitUntil:'domcontentloaded'});
    await page.waitForFunction(()=>window.__ytTest&&__ytTest.ready===1,{},{timeout:180000});
    report.readyMilliseconds=Date.now()-started;
    await page.evaluate(()=>{const original=unityGameInstance.SendMessage.bind(unityGameInstance);unityGameInstance.SendMessage=(object,method,value)=>{original(object,method,value);if(method==='ReceiveOnRewardedAdEvent')__ytTest.adFinished++;};});
    check(await page.evaluate(()=>__ytTest.loads===1),'Exactly one cloud load: Google wrapper owns saves, Mirra does not duplicate it');
    check(await page.locator('#loading').evaluate(el=>getComputedStyle(el).display==='none'),'Loading screen removed before gameReady');
    check(await page.evaluate(()=>__ytTest.first===1&&__ytTest.ready===1),'Exactly one firstFrameReady and gameReady');
    check(await page.evaluate(()=>__ytTest.balance()===123),'Existing cloud balance survives scene initialization');
    await page.screenshot({path:path.join(output,'desktop.png')});
    report.audioMuted=await page.evaluate(()=>__gains.map(g=>g.gain.value));
    if(process.env.SAND_AUDIO_ONLY){
        await page.evaluate(async()=>{for(const context of new Set(__gains.map(g=>g.context)))await context.resume();});
        await new Promise(resolve=>setTimeout(resolve,300));
        check(await page.evaluate(()=>__analysers.length>0&&__outputRms()<1e-7),'Initial SDK mute silences measured WebAudio output');
        await page.evaluate(()=>__ytTest.events.audio(true));
        await page.waitForFunction(()=>__outputRms()>1e-5,{},{timeout:10000});
        check(true,'SDK unmute produces measured game audio');
        await page.evaluate(()=>__ytTest.events.pause());
        await new Promise(resolve=>setTimeout(resolve,300));
        check(await page.evaluate(()=>__outputRms()<1e-7),'SDK pause silences measured audio');
        await page.evaluate(()=>{__ytTest.events.audio(false);__ytTest.events.audio(true);});
        await new Promise(resolve=>setTimeout(resolve,300));
        check(await page.evaluate(()=>__outputRms()<1e-7),'Unmute while paused cannot restart audio');
        await page.evaluate(()=>__ytTest.events.resume());
        await page.waitForFunction(()=>__outputRms()>1e-5,{},{timeout:10000});
        check(true,'SDK resume restores enabled music');
        await page.evaluate(()=>{__ytTest.events.audio(false);__ytTest.events.pause();__ytTest.events.resume();});
        await new Promise(resolve=>setTimeout(resolve,300));
        check(await page.evaluate(()=>__outputRms()<1e-7),'Mirra resume preserves the current SDK mute');
        report.audioRms=await page.evaluate(()=>__outputRms());
        check(report.errors.length===0,'Audio checks have no browser or Unity errors');
        return;
    }

    await page.evaluate(()=>__ytTest.events.pause());
    const pausedDraws=await page.evaluate(()=>__draws);
    await new Promise(resolve=>setTimeout(resolve,300));
    check(await page.evaluate(()=>__draws)===pausedDraws,'Platform pause stops Unity rendering including unscaled animations');
    await page.evaluate(()=>__ytTest.events.audio(true));
    check(await page.evaluate(()=>YTGameLifecycle.paused),'Unmute preserves platform pause');
    await page.evaluate(()=>__ytTest.events.resume());
    await page.waitForFunction(count=>__draws>count,pausedDraws,{timeout:10000});
    check(true,'Platform resume restarts Unity rendering');
    await page.evaluate(()=>__ytTest.events.audio(false));
    await page.setViewportSize({width:390,height:844});
    await page.screenshot({path:path.join(output,'portrait.png')});
    check(await page.evaluate(()=>__ytTest.balance()===123),'Portrait resize preserves cloud-backed state');
    await page.setViewportSize({width:844,height:390});
    await page.screenshot({path:path.join(output,'landscape.png')});
    check(await page.evaluate(()=>__ytTest.balance()===123),'Landscape resize preserves state');
    await page.setViewportSize({width:900,height:900});
    await page.screenshot({path:path.join(output,'square.png')});
    await page.setViewportSize({width:1280,height:900});
    await page.mouse.click(835,26);
    await page.screenshot({path:path.join(output,'shop.png')});
    await page.evaluate(()=>__ytTest.mode='false');
    await page.mouse.click(786,228);
    await page.waitForFunction(()=>__ytTest.ads.length===1);
    await page.waitForFunction(()=>__ytTest.adFinished===__ytTest.ads.length);
    await page.evaluate(()=>new Promise(resolve=>requestAnimationFrame(()=>requestAnimationFrame(resolve))));
    check(await page.evaluate(()=>__ytTest.balance()===123),'Cancelled rewarded ad grants no coins');
    await page.evaluate(()=>__ytTest.mode='error');
    await page.mouse.click(786,228);
    await page.waitForFunction(()=>__ytTest.ads.length===2);
    await page.waitForFunction(()=>__ytTest.adFinished===__ytTest.ads.length);
    await page.evaluate(()=>new Promise(resolve=>requestAnimationFrame(()=>requestAnimationFrame(resolve))));
    check(await page.evaluate(()=>__ytTest.balance()===123),'Rejected rewarded ad releases the button without a reward');
    await page.evaluate(()=>__ytTest.mode='true');
    await page.mouse.click(786,228);
    await page.waitForFunction(()=>__ytTest.balance()===223);
    check(true,'Confirmed rewarded ad grants exactly 100 coins through the shop');
    await page.evaluate(()=>__ytTest.mode='held');
    await page.mouse.click(786,228);
    await page.waitForFunction(()=>YTGameLifecycle.paused);
    await page.evaluate(()=>{__ytTest.events.audio(true);__ytTest.resolveAd(true);});
    await new Promise(resolve=>setTimeout(resolve,200));
    check(await page.evaluate(()=>YTGameLifecycle.paused&&__ytTest.balance()===223),'Reward callback does not resume a platform pause or update gameplay during it');
    await page.evaluate(()=>__ytTest.events.resume());
    await page.waitForFunction(()=>__ytTest.balance()===323);
    await page.screenshot({path:path.join(output,'shop-after-ad.png')});
    check(await page.evaluate(()=>__ytTest.ads.every(id=>id==='shop_coins_100')),'Reward placements use a stable reward ID');
    await page.keyboard.press('Escape');
    // Exercise the actual CoinManager API, not a test-only player component.
    await page.evaluate(()=>unityGameInstance.SendMessage('Polish','AddCoinsOnComplete'));
    await page.waitForFunction(()=>__ytTest.balance()===373,{},{timeout:10000});
    await page.waitForFunction(()=>!__ytTest.pendingSaves,{},{timeout:10000});
    check(Number(JSON.parse(cloud).entries.find(e=>e.key==='Coins').value)===373,'Material coin change is acknowledged by cloud save');
    await page.reload({waitUntil:'domcontentloaded'});
    await page.waitForFunction(()=>window.__ytTest&&__ytTest.ready===1,{},{timeout:120000});
    check(await page.evaluate(()=>__ytTest.balance()===373),'Reload restores acknowledged progress');
    report.jsHeap=await page.evaluate(()=>performance.memory?{used:performance.memory.usedJSHeapSize,total:performance.memory.totalJSHeapSize}:null);
    const forbidden=report.requests.filter(url=>!url.startsWith('http://127.0.0.1:')&&!url.startsWith('blob:http://127.0.0.1:')&&url!=='https://www.youtube.com/game_api/v1');
    check(forbidden.length===0,'No external requests except the YouTube SDK');
    check(report.errors.length===0,'No browser or Unity errors');
    }
    const mobile=await browser.newContext({viewport:{width:390,height:844},hasTouch:true,isMobile:true});
    const touch=await mobile.newPage();
    await touch.exposeFunction('testLoadCloud',()=>cloud);
    await touch.exposeFunction('testSaveCloud',data=>{cloud=data;report.writes.push(JSON.parse(data));});
    await touch.route('https://www.youtube.com/game_api/v1',route=>route.fulfill({contentType:'application/javascript',body:fixture}));
    touch.on('pageerror',error=>report.errors.push(error.message));
    await touch.goto('http://127.0.0.1:'+server.address().port+'/',{waitUntil:'domcontentloaded'});
    await touch.waitForFunction(()=>window.__ytTest&&__ytTest.ready===1,{},{timeout:120000});
    await touch.screenshot({path:path.join(output,'mobile-before-touch.png')});
    await touch.touchscreen.tap(118,507);
    await new Promise(resolve=>setTimeout(resolve,500));
    await touch.screenshot({path:path.join(output,'portrait-touch.png')});
    check(report.errors.length===0,'Touch-capable mobile viewport starts and accepts input without errors');
})().catch(error=>{report.failure=error.stack;console.error(error);process.exitCode=1;}).finally(async()=>{
    fs.writeFileSync(path.join(output,process.env.SAND_AUDIO_ONLY?'audio-results.json':process.env.SAND_TOUCH_ONLY?'touch-results.json':'browser-results.json'),JSON.stringify(report,null,2));
    if(browser)await browser.close();if(server)await new Promise(resolve=>server.close(resolve));
});
