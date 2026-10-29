'use strict';
const assert=require('node:assert/strict'),fs=require('node:fs'),vm=require('node:vm');
const create=require('../../Assets/WebGLTemplates/MirraYoutubePlayablesTemplate/ytgame-lifecycle.js');
const tests=[];function test(name,run){tests.push([name,run]);}
const tick=()=>new Promise(resolve=>setImmediate(resolve));
function setup(){
 const events={},messages=[],timers=new Map();let id=0,ready=0,first=0,paused=0,resumed=0;
 const sdk={IN_PLAYABLES_ENV:true,game:{loadData:()=>Promise.resolve(''),saveData:()=>Promise.resolve(),firstFrameReady:()=>first++,gameReady:()=>ready++},system:{isAudioEnabled:()=>false,onAudioEnabledChange:cb=>events.audio=cb,onPause:cb=>events.pause=cb,onResume:cb=>events.resume=cb,getLanguage:()=>Promise.resolve('en-US')},ads:{requestRewardedAd:()=>Promise.resolve(true),requestInterstitialAd:()=>Promise.resolve()}};
 const life=create(sdk,{setTimeout:fn=>{timers.set(++id,fn);return id;},clearTimeout:id=>timers.delete(id)});
 life.initialize();life.register('audio');life.register('pause');life.register('resume');
 life.attachUnity({SendMessage:(object,method,value)=>messages.push([object,method,value])});life.attachRuntime({pause:()=>paused++,resume:()=>resumed++});
 const context={window:{YTGameLifecycle:life},ytgame:sdk,LibraryManager:{library:{}},mergeInto:(target,source)=>Object.assign(target,source),UTF8ToString:value=>value,console};
 vm.runInNewContext(fs.readFileSync('Assets/Plugins/UnityYTGameSDKLib.jslib','utf8'),context);
 const lib=context.LibraryManager.library;context.YTGameSendMessage=lib.$YTGameSendMessage;
 return {sdk,life,events,messages,lib,timers,expire:()=>{for(const fn of [...timers.values()])fn();},counts:()=>({ready,first,paused,resumed})};
}
test('Google wrapper exports load/save/audio/pause/ad entry points',async()=>{const x=setup();for(const name of ['LoadGameData','SaveGameData','CheckIsAudioEnabled','SetCallbackOnGamePause','RequestAnRewardedAd'])assert.equal(typeof x.lib[name],'function');});
test('Ready signals are sent once and wait for SDK resume',async()=>{const x=setup();x.events.pause();x.life.ready();assert.equal(x.counts().ready,0);x.events.resume();x.life.ready();assert.deepEqual(x.counts(),{ready:1,first:1,paused:1,resumed:1});});
test('Early audio and pause are subscribed before cloud loading',async()=>{const x=setup();x.events.audio(true);assert.equal(x.life.audioEnabled,true);x.events.pause();assert.equal(x.life.paused,true);});
test('Late Unity instance gets the current mute and pause',async()=>{const x=setup();x.events.pause();x.events.audio(true);const received=[];x.life.attachUnity({SendMessage:(o,m,v)=>received.push([m,v])});assert.deepEqual(received,[['ReceiveOnAudioEnabledChange','true'],['ReceiveOnPauseEvent','']]);});
test('Empty and BUSES entry saves normalize for Unity JSON',async()=>{const x=setup();assert.deepEqual(JSON.parse(x.life.normalizeSave('')),{version:2,entries:[]});assert.equal(JSON.parse(x.life.normalizeSave('{"entries":[{"key":"Coins","value":"12"}]}')).entries[0].value,'12');});
test('Previous sand map remains compatible',async()=>{const x=setup();assert.equal(JSON.parse(x.life.normalizeSave('{"version":1,"values":{"Coins":123}}')).entries[0].value,'123');});
test('Malformed, duplicate and future schemas fail closed',async()=>{const x=setup();for(const raw of ['{','[]','{"version":9}','{"entries":[{"key":"a","value":1},{"key":"a","value":2}]}'])assert.throws(()=>x.life.normalizeSave(raw));});
test('Actual jslib load rejects corrupt data without an empty-save callback',async()=>{const x=setup();x.sdk.game.loadData=()=>Promise.resolve('{');x.lib.LoadGameData();await tick();assert.equal(x.messages.at(-1)[1],'ReceiveOnLoadSaveFailed');});
test('Load timeout ignores a late response',async()=>{const x=setup();let finish;x.sdk.game.loadData=()=>new Promise(resolve=>finish=resolve);x.lib.LoadGameData();x.expire();finish('');await tick();assert.equal(x.messages.filter(m=>m[1].startsWith('ReceiveOnLoadSave')).length,1);assert.equal(x.messages.at(-1)[1],'ReceiveOnLoadSaveFailed');});
test('Save starts synchronously, acknowledges only Promise success',async()=>{const x=setup();let finish,calls=0;x.sdk.game.saveData=()=>{calls++;return new Promise(resolve=>finish=resolve);};x.lib.SaveGameData('{}');assert.equal(calls,1);assert.equal(x.messages.filter(m=>m[1]==='ReceiveOnSaveResult').length,0);finish();await tick();assert.equal(x.messages.at(-1)[2],'success');});
test('Save timeout is terminal despite a late success',async()=>{const x=setup();let finish;x.sdk.game.saveData=()=>new Promise(resolve=>finish=resolve);x.lib.SaveGameData('{}');x.expire();finish();await tick();assert.deepEqual(x.messages.filter(m=>m[1]==='ReceiveOnSaveResult').map(m=>m[2]),['timeout']);});
test('Reward requires literal true; false, nonboolean, reject and throw fail',async()=>{for(const result of [true,false,1,'true','reject','throw']){const x=setup();x.sdk.ads.requestRewardedAd=()=>{if(result==='throw')throw Error();return result==='reject'?Promise.reject(Error()):Promise.resolve(result);};x.lib.RequestAnRewardedAd('shop_coins_100');await tick();assert.equal(x.messages.at(-1)[2],result===true?'true':'false');}});
test('Ad timeout cannot grant a late reward',async()=>{const x=setup();let finish;x.sdk.ads.requestRewardedAd=()=>new Promise(resolve=>finish=resolve);x.lib.RequestAnRewardedAd('shop_coins_100');x.expire();finish(true);await tick();assert.deepEqual(x.messages.filter(m=>m[1]==='ReceiveOnRewardedAdEvent').map(m=>m[2]),['false']);});
test('Ad completion waits for platform resume',async()=>{const x=setup();let finish;x.sdk.ads.requestRewardedAd=()=>new Promise(resolve=>finish=resolve);x.lib.RequestAnRewardedAd('reward');x.events.pause();finish(true);await tick();assert.equal(x.messages.filter(m=>m[1]==='ReceiveOnRewardedAdEvent').length,0);x.events.resume();assert.equal(x.messages.at(-1)[2],'true');});
test('SDK locale is fetched once for Mirra and the game',async()=>{const x=setup();let calls=0;x.sdk.system.getLanguage=()=>{calls++;return Promise.resolve('ru-RU');};assert.deepEqual(await Promise.all([x.life.getLanguage(),x.life.getLanguage()]),['ru-RU','ru-RU']);assert.equal(calls,1);});
(async()=>{for(const [name,run]of tests){await run();console.log('PASS '+name);}console.log(tests.length+' wrapper/lifecycle checks passed');})().catch(error=>{console.error(error);process.exitCode=1;});
