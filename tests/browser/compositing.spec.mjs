import { test, expect } from '@playwright/test';
import fs from 'node:fs/promises';
const base = process.env.EFFECTSSPACE_URL || 'http://127.0.0.1:4173/EffectsSpace/';
const state = page => page.evaluate(() => globalThis.effectsSpaceDiagnostics);
async function start(page) {
  page.on('pageerror', error => console.error(error.message));
  await page.goto(base + '?test=1', {waitUntil:'domcontentloaded'});
  await page.waitForFunction(() => globalThis.effectsSpaceDiagnostics?.ready, null, {timeout:150000});
  await fs.mkdir('artifacts/screenshots', {recursive:true});
}
async function click(page, name, type = 'StudioButton') {
  await expect.poll(async () => (await state(page)).controls.some(c => c.name === name && c.type === type)).toBeTruthy();
  const c = (await state(page)).controls.find(c => c.name === name && c.type === type);
  await page.mouse.click(c.x+c.width/2,c.y+c.height/2);
}
async function setNumber(page, name, value) {
  await click(page,name,'TextBox'); await page.keyboard.press('Control+a'); await page.keyboard.type(String(value)); await page.keyboard.press('Enter');
}
async function scrub(page, time) {
  const s = await state(page);
  await page.mouse.click(s.timeline.x+s.headerWidth+(time-s.scrollSeconds)*s.pixelsPerSecond,s.timeline.y+32);
  await expect.poll(async () => (await state(page)).time).toBeCloseTo(time,2);
}
function fixture() {
  const channel = value => ({value});
  const baseLayer = (id,name,kind,fill,width=512,height=288) => ({id,name,kind,fill,width,height,outPoint:4});
  const card=baseLayer('card','Masked card','rectangle','#73DCC0',300,200);
  card.cornerRadius=12; card.transform={x:channel(106),y:channel(44)};
  const matte=baseLayer('matte','Luma source','solid','#FFFFFF',230,288); matte.enabled=false;
  card.matteId='matte'; card.matte='luma';
  const sub={id:'sub',name:'Nested source',width:512,height:288,duration:4,workEnd:4,layers:[baseLayer('dot','Moving card','rectangle','#BCA2F8',90,90)]};
  sub.layers[0].transform={x:{keys:[{id:'kx1',time:0,value:20,interpolation:'linear'},{id:'kx2',time:3.9,value:360,interpolation:'linear'}]},y:channel(90)};
  const main={id:'main',name:'Compositing study',width:512,height:288,duration:4,workEnd:4,layers:[card,matte,baseLayer('background','Background','solid','#202D45')]};
  return {schemaVersion:1,name:'Compositing acceptance',activeCompositionId:'main',compositions:[main,sub],assets:[]};
}
async function open(page, project) {
  const chooser=page.waitForEvent('filechooser'); await page.keyboard.press('Control+o');
  await (await chooser).setFiles({name:'compositing.effects',mimeType:'application/json',buffer:Buffer.from(JSON.stringify(project))});
  await expect.poll(async () => (await state(page)).project).toBe(project.name);
}
async function selectRow(page,name) {
  await expect.poll(async () => (await state(page)).rows.some(r=>r.name===name&&!r.property)).toBeTruthy();
  const s=await state(page),r=s.rows.find(r=>r.name===name&&!r.property);
  await page.mouse.click(s.timeline.x+180,s.timeline.y+r.y+11);
  await expect.poll(async () => (await state(page)).name).toBe(name);
}

test('masked adjustment layer, luma input and guide status survive save/undo',async({page})=>{
  await start(page); await open(page,fixture()); await selectRow(page,'Masked card');
  expect((await state(page)).matteMode).toBe('Luma');
  await page.keyboard.press('Control+Alt+y'); await expect.poll(async()=>(await state(page)).kind).toBe('Adjustment');
  await click(page,'Invert Composite'); await expect.poll(async()=>(await state(page)).adjustmentLayers).toBe(1);
  await click(page,'Add Mask'); await expect.poll(async()=>(await state(page)).masks).toBe(1);
  await setNumber(page,'Mask 1 feather',18); await expect.poll(async()=>(await state(page)).maskDetails[0].feather).toBe(18);
  await setNumber(page,'Mask 1 opacity',65); await expect.poll(async()=>(await state(page)).maskDetails[0].opacity).toBe(65);
  await click(page,'Guide Layer'); await expect.poll(async()=>(await state(page)).guide).toBeTruthy();
  await scrub(page,1); await page.keyboard.press('Control+z'); await expect.poll(async()=>(await state(page)).guide).toBeFalsy();
  expect((await state(page)).renderError).toBeNull();
  await page.screenshot({path:'artifacts/screenshots/compositing-adjustment.png'});
  const waiting=page.waitForEvent('download'); await page.keyboard.press('Control+s');
  const download=await waiting, saved=JSON.parse(await fs.readFile(await download.path(),'utf8'));
  const adjustment=saved.compositions[0].layers[0]; expect(adjustment.kind).toBe('adjustment'); expect(adjustment.masks[0].opacity).toBe(65);
});

test('mask nodes and Bezier tangents support live dragging, cancellation and undo',async({page})=>{
  await start(page); const p=fixture(); p.compositions[0].layers[0].matteId=null; await open(page,p); await selectRow(page,'Masked card');
  await page.keyboard.press('Shift+F5'); await click(page,'Add Mask'); await expect.poll(async()=>(await state(page)).masks).toBe(1);
  await click(page,'Mask 1 · Edit Path'); await expect.poll(async()=>(await state(page)).maskPoints.length).toBe(4);
  let s=await state(page), point=s.maskPoints.find(p=>p.node===0&&p.part==='Point'), original=s.maskDetails[0].nodes[0];
  await page.mouse.move(s.viewer.x+point.x,s.viewer.y+point.y); await page.mouse.down();
  await page.mouse.move(s.viewer.x+point.x+28,s.viewer.y+point.y+18,{steps:8}); await page.mouse.up();
  await expect.poll(async()=>(await state(page)).maskDetails[0].nodes[0].x).toBeGreaterThan(original.x);
  s=await state(page); const moved=s.maskDetails[0].nodes[0]; point=s.maskPoints.find(p=>p.node===0&&p.part==='Point');
  await page.keyboard.down('Alt'); await page.mouse.move(s.viewer.x+point.x,s.viewer.y+point.y); await page.mouse.down();
  await page.mouse.move(s.viewer.x+point.x+35,s.viewer.y+point.y+25,{steps:8}); await page.mouse.up(); await page.keyboard.up('Alt');
  await expect.poll(async()=>(await state(page)).maskPoints.length).toBe(6);
  s=await state(page); const handle=s.maskPoints.find(p=>p.node===0&&p.part==='Out'), old=s.maskDetails[0].nodes[0].outX;
  await page.mouse.move(s.viewer.x+handle.x,s.viewer.y+handle.y); await page.mouse.down();
  await page.mouse.move(s.viewer.x+handle.x+30,s.viewer.y+handle.y,{steps:6}); await page.keyboard.press('Escape'); await page.mouse.up();
  await expect.poll(async()=>(await state(page)).editing).toBeFalsy();
  expect((await state(page)).maskDetails[0].nodes[0].outX).toBeCloseTo(old,6);
  await page.screenshot({path:'artifacts/screenshots/mask-path-editor.png'});
  await page.keyboard.press('Enter'); await page.keyboard.press('Control+z');
  await expect.poll(async()=>(await state(page)).maskDetails[0].nodes[0].outX).toBe(0);
  expect((await state(page)).maskDetails[0].nodes[0].x).toBeCloseTo(moved.x,6);
  expect((await state(page)).renderError).toBeNull();
});

test('nested source remapping, freeze and reverse run through the UI',async({page})=>{
  await start(page); const p=fixture(); p.compositions[0].layers.unshift({id:'wrap',name:'Nested instance',kind:'composition',sourceId:'sub',width:512,height:288,outPoint:4});
  await open(page,p); await selectRow(page,'Nested instance'); await page.keyboard.press('Control+Alt+t');
  await expect.poll(async()=>(await state(page)).timeRemapEnabled).toBeTruthy();
  await expect.poll(async()=>(await state(page)).rows.some(r=>r.property==='TimeRemap')).toBeTruthy();
  await scrub(page,1); await setNumber(page,'Source time (s)',2.5);
  await expect.poll(async()=>(await state(page)).sourceTime).toBeCloseTo(2.5,6);
  await scrub(page,1); await page.keyboard.press('Control+Alt+f');
  await expect.poll(async()=>(await state(page)).keyframes.filter(k=>k.property==='TimeRemap').length).toBe(0);
  await scrub(page,3); expect((await state(page)).sourceTime).toBeCloseTo(2.5,6);
  await page.keyboard.press('Control+z'); await expect.poll(async()=>(await state(page)).keyframes.filter(k=>k.property==='TimeRemap').length).toBe(3);
  await click(page,'Reverse'); await scrub(page,0);
  expect((await state(page)).sourceTime).toBeCloseTo(4-1/30,5);
  await click(page,'Remap Graph'); await page.screenshot({path:'artifacts/screenshots/time-remapping.png'});
  expect((await state(page)).renderError).toBeNull();
});
