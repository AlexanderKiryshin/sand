// Read-only certification audit of the exported player. SDK/cloud are local fixtures.
const fs = require('node:fs');
const path = require('node:path');
const http = require('node:http');
const {chromium} = require('playwright');
const root = path.resolve(process.argv[2] || 'Builds/YouTube/2026-10-28-economy-r4');
const output = __dirname;
const report = {date:'2026-10-30',bundle:root,environment:'Local Edge, software WebGL2, simulated YouTube SDK',checks:[],errors:[],requests:[],saveSizes:[],heap:[],observations:[]};
const source = fs.readFileSync(path.resolve('tasks/youtube/browser-smoke.cjs'),'utf8');
const fixture = source.match(/const fixture = `([\s\S]*?)`;/)[1]
 .replace('window.__ytTest={','window.__ytTest={sequence:[],')
 .replace('firstFrameReady:()=>__ytTest.first++','firstFrameReady:()=>{__ytTest.first++;__ytTest.sequence.push({event:"firstFrameReady",time:performance.now(),loading:getComputedStyle(document.querySelector("#loading")).display});}')
 .replace('gameReady:()=>__ytTest.ready++','gameReady:()=>{__ytTest.ready++;__ytTest.sequence.push({event:"gameReady",time:performance.now(),loading:getComputedStyle(document.querySelector("#loading")).display});}');
let cloud = JSON.stringify({version:2,entries:Object.entries({Coins:5000,CoinsInPreviosLevel:5000,sand_progress_version:3,sand_progress_highest_unlocked:0,sand_progress_highest_completed:-1,sand_progress_current_level:0}).map(([key,value])=>({key,value:String(value)}))});
let browser,server,timer,page;
function save(){fs.writeFileSync(path.join(output,'runtime-results.json'),JSON.stringify(report,null,2));}
function check(ok,name){report.checks.push({name,pass:!!ok});save();console.log((ok?'PASS ':'FAIL ')+name);if(!ok)throw new Error(name);}
function value(key){return JSON.parse(cloud).entries.find(e=>e.key===key)?.value;}
const delay=ms=>new Promise(resolve=>setTimeout(resolve,ms));
async function shot(name){await page.screenshot({path:path.join(output,name+'.png')});}
async function click(x,y){await page.mouse.click(x,y);await delay(120);}
async function send(object,method,arg){await delay(120);await page.evaluate(([o,m,a])=>unityGameInstance.SendMessage(o,m,a),[object,method,arg]);await delay(120);}
async function esc(name){await page.keyboard.press('Escape');await delay(200);await shot(name);report.observations.push({scenario:name,keys:await page.evaluate(()=>__audit)});save();}
(async()=>{
 server=http.createServer((req,res)=>{const target=path.resolve(root,'.'+decodeURIComponent(new URL(req.url,'http://localhost').pathname));const file=target===root?path.join(root,'index.html'):target;if(!file.startsWith(root+path.sep)||!fs.existsSync(file)||!fs.statSync(file).isFile()){res.writeHead(404);res.end();return;}res.setHeader('Content-Type',({'.html':'text/html','.js':'text/javascript','.wasm':'application/wasm'})[path.extname(file)]||'application/octet-stream');fs.createReadStream(file).pipe(res);});
 await new Promise(resolve=>server.listen(0,'127.0.0.1',resolve));
 browser=await chromium.launch({executablePath:'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe',headless:true,args:['--use-angle=swiftshader','--enable-unsafe-swiftshader','--enable-precise-memory-info']});
 const context=await browser.newContext({viewport:{width:1280,height:900}});page=await context.newPage();
 await page.exposeFunction('testLoadCloud',()=>cloud);
 await page.exposeFunction('testSaveCloud',data=>{cloud=data;report.saveSizes.push({utf8:Buffer.byteLength(data),utf16:data.length*2});});
 await page.route('https://www.youtube.com/game_api/v1',route=>route.fulfill({contentType:'application/javascript',body:fixture}));
 page.on('pageerror',e=>report.errors.push(e.message));page.on('console',m=>{if(m.type()==='error')report.errors.push(m.text());});page.on('request',r=>report.requests.push(r.url()));
 await page.addInitScript(()=>{window.__audit={orientationLocks:0,escPrevented:0};if(screen.orientation?.lock){const lock=screen.orientation.lock.bind(screen.orientation);screen.orientation.lock=(...args)=>{__audit.orientationLocks++;return lock(...args);};}const prevent=Event.prototype.preventDefault;Event.prototype.preventDefault=function(){if((this.type==='keydown'||this.type==='keyup')&&this.key==='Escape')__audit.escPrevented++;return prevent.call(this);};});
 const cdp=await context.newCDPSession(page);let sampling=false;
 timer=setInterval(async()=>{if(sampling)return;sampling=true;try{report.heap.push({time:Date.now(),...await cdp.send('Runtime.getHeapUsage')});}catch{}finally{sampling=false;}},250);
 await page.goto('http://127.0.0.1:'+server.address().port+'/');await page.waitForFunction(()=>window.__ytTest?.ready===1,{},{timeout:120000});
 report.readiness=await page.evaluate(()=>__ytTest.sequence);
 check(report.readiness.map(e=>e.event).join(',')==='firstFrameReady,gameReady','First frame precedes gameReady exactly once');
 check(report.readiness[0].loading==='flex'&&report.readiness[1].loading==='none','Loading is visible for first frame and hidden for gameReady');
 await click(835,26);await click(805,154);await send('Canvas','Choose',12);await delay(150);await shot('background-2000');
 await send('Canvas','ApplyPreview');await page.waitForFunction(()=>__ytTest.balance()===3000);
 check(value('sand_theme_background_owned_12')==='1','Background costs 2000 gold and grants ownership');
 await send('Canvas','ApplyPreview');await delay(250);check(value('Coins')==='3000','Owned background is not charged twice');
 for(const [x,part,expected] of [[585,'conveyor',2000],[695,'casings',1000]]){await click(x,154);await send('Canvas','Choose',8);await delay(150);await shot(part+'-1000');await send('Canvas','ApplyPreview');await page.waitForFunction(n=>__ytTest.balance()===n,expected);check(value('sand_theme_'+part+'_owned_8')==='1',part+' costs 1000 gold and grants ownership');}
 await click(805,154);await send('Canvas','Choose',13);await send('Canvas','ApplyPreview');await delay(300);check(value('Coins')==='1000'&&!value('sand_theme_background_owned_13'),'Insufficient gold neither spends nor unlocks');
 await page.waitForFunction(()=>!__ytTest.pendingSaves);await page.reload();await page.waitForFunction(()=>window.__ytTest?.ready===1,{},{timeout:120000});
 check(value('Coins')==='1000'&&['background_owned_12','conveyor_owned_8','casings_owned_8'].every(k=>value('sand_theme_'+k)==='1'),'All three purchases survive reload');
 await click(835,26);await shot('resources-no-bundles');await esc('shop-after-esc');
 await click(326,66);await shot('audio-open');await esc('audio-after-esc');
 await click(640,26);await shot('gallery-open');await esc('gallery-after-esc');
 await click(835,26);await click(805,154);await send('Canvas','Choose',12);await send('Canvas','OpenBackgroundViewer');await shot('background-viewer-open');await esc('background-viewer-after-esc');
 await page.keyboard.press('Escape');
 for(const [name,width,height] of [['portrait',390,844],['landscape',844,390],['square',900,900],['narrow',360,1280],['wide',1280,360]]){await page.setViewportSize({width,height});await delay(150);await shot(name);check(await page.evaluate(()=>__ytTest.loads===1&&__ytTest.balance()===1000),'Resize '+name+' preserves session and balance');}
 check(await page.evaluate(()=>__audit.orientationLocks===0),'No orientation lock requested');
 await page.setViewportSize({width:1280,height:900});
 await send('GameSystems','HandleLevelComplete');await delay(2000);await shot('win-open');await esc('win-after-esc');
 // Seed the actual final winter level; invoke the real completion handler after loading it.
 const data=JSON.parse(cloud);const values=Object.fromEntries(data.entries.map(e=>[e.key,e.value]));Object.assign(values,{sand_progress_highest_unlocked:999,sand_progress_highest_completed:999,sand_progress_current_level:999,sand_selected_campaign:1,winter_highest_unlocked:199,winter_current:199});for(let i=1;i<200;i++)values['winter_completed_'+i]=1;
 cloud=JSON.stringify({version:2,entries:Object.entries(values).map(([key,value])=>({key,value:String(value)}))});
 await page.reload();await page.waitForFunction(()=>window.__ytTest?.ready===1,{},{timeout:120000});await delay(600);await shot('final-level');
 await send('GameSystems','HandleLevelComplete');await delay(2000);await shot('final-win');await esc('final-win-after-esc');
 await send('Polish','OnCompleteContinue');await delay(1500);await shot('final-collection');
 check(value('winter_completed_200')==='1','Final level completion is saved');
 report.finalKeys=await page.evaluate(()=>__audit);
 const forbidden=report.requests.filter(u=>!u.startsWith('http://127.0.0.1:')&&!u.startsWith('blob:http://127.0.0.1:')&&u!=='https://www.youtube.com/game_api/v1');
 check(!forbidden.length,'No external requests beyond YouTube SDK');check(!report.errors.length,'No browser or Unity errors');
 report.peakHeapBytes=Math.max(...report.heap.map(x=>x.usedSize));report.peakBackingStorageBytes=Math.max(...report.heap.map(x=>x.backingStorageSize||0));
 check(report.peakHeapBytes<512*1024*1024,'Sampled JS heap stays below 512 MiB');check(report.saveSizes.every(s=>Math.max(s.utf8,s.utf16)<3*1024*1024),'Observed saves stay below 3 MiB');
})().catch(e=>{report.failure=e.stack;console.error(e);process.exitCode=1;}).finally(async()=>{clearInterval(timer);save();if(browser)await browser.close();if(server)await new Promise(resolve=>server.close(resolve));});
