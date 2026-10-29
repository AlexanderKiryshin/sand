'use strict';
const assert=require('node:assert/strict'),fs=require('node:fs'),vm=require('node:vm');
const create=require('../../Assets/WebGLTemplates/MirraYoutubePlayablesTemplate/ytgame-lifecycle.js');
const calls={load:0,save:0,first:0,ready:0,audio:0,pause:0,resume:0,language:0,visibility:0,localStorage:0};
const sdk={IN_PLAYABLES_ENV:true,game:{loadData:async()=>{calls.load++;return '';},saveData:async()=>calls.save++,firstFrameReady:()=>calls.first++,gameReady:()=>calls.ready++},system:{isAudioEnabled:()=>false,onAudioEnabledChange:()=>calls.audio++,onPause:()=>calls.pause++,onResume:()=>calls.resume++,getLanguage:async()=>{calls.language++;return 'en-US';}}};
const life=create(sdk);life.initialize();
const context={window:{ytgame:sdk,YTGameLifecycle:life,location:{href:'https://www.youtube.com/playables/'},matchMedia:()=>({matches:false})},document:{referrer:'',addEventListener:()=>calls.visibility++},navigator:{userAgent:'test',language:'en-US'},Module:{},console:{log(){},warn(){},error(...args){throw new Error(args.join(' '));}},setTimeout,clearTimeout,setInterval,clearInterval,URL,TextDecoder,TextEncoder,atob,btoa};
Object.defineProperty(context,'localStorage',{get(){calls.localStorage++;throw new Error('Forbidden localStorage fallback');}});
vm.runInNewContext(fs.readFileSync('Packages/com.romanlee17.mirrasdk5/Runtime/MirraWeb/Plugins/mirraSDK.jspre','utf8'),context);
(async()=>{
 await context.Module.createMirraSDK({framework:{forcePlatformType:'YouTubePlayables'},logger:{enableTextMessages:false,enableWarningMessages:false,enableExceptionMessages:true}});
 await life.getLanguage();
 assert.ok(context.Module.mirraSDK.wrapper,'Real Mirra YouTube wrapper was initialized');
 context.Module.mirraSDK.analytics.gameIsReady();
 assert.deepEqual(calls,{load:0,save:0,first:1,ready:0,audio:1,pause:1,resume:1,language:1,visibility:0,localStorage:0});
 console.log('PASS Mirra YouTube preset initializes with no duplicate cloud, ready, lifecycle, locale or localStorage access');
 console.log(JSON.stringify(calls));
})().catch(error=>{console.error(error);process.exitCode=1;});
