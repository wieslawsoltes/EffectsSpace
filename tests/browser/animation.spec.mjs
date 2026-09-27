import { test, expect } from '@playwright/test';
import fs from 'node:fs/promises';
const base = process.env.EFFECTSSPACE_URL || 'http://127.0.0.1:4173/EffectsSpace/';
const state = page => page.evaluate(() => globalThis.effectsSpaceDiagnostics);
const track = (s, property) => s.keyframes.filter(k => k.property === property);
async function start(page) {
  page.on('pageerror', error => console.error(error.message));
  await page.goto(base + '?test=1', {waitUntil:'domcontentloaded'});
  await page.waitForFunction(() => globalThis.effectsSpaceDiagnostics?.ready, null, {timeout:150000});
  await expect.poll(async () => (await state(page)).name).toBe('ORBITAL');
  await page.keyboard.press('Shift+F2');
  await expect.poll(async () => (await state(page)).timeline.height).toBeGreaterThan(300);
  await fs.mkdir('artifacts/screenshots', {recursive:true});
}
async function selectProperty(page, property, shortcut = 'p') {
  await page.keyboard.press(shortcut);
  await expect.poll(async () => (await state(page)).rows.some(r => r.name === 'ORBITAL' && r.property === property)).toBeTruthy();
  const s = await state(page), row = s.rows.find(r => r.name === 'ORBITAL' && r.property === property);
  await page.mouse.click(s.timeline.x + 170, s.timeline.y + row.y + 11);
  await expect.poll(async () => (await state(page)).property).toBe(property);
}
async function clickKey(page, id, shift = false) {
  const s = await state(page), point = s.keyPositions.find(p => p.id === id);
  expect(point, 'visible key geometry').toBeTruthy();
  if (shift) await page.keyboard.down('Shift');
  await page.mouse.click(s.timeline.x + point.x, s.timeline.y + point.y);
  if (shift) await page.keyboard.up('Shift');
}
async function scrub(page, time) {
  const s = await state(page);
  await page.mouse.click(s.timeline.x + s.headerWidth + (time - s.scrollSeconds) * s.pixelsPerSecond, s.timeline.y + 32);
  await expect.poll(async () => (await state(page)).time).toBeCloseTo(time, 2);
}

test('multi-key drag, undo selection, clipboard paste and cut are atomic', async ({page}) => {
  await start(page); await selectProperty(page, 'Y');
  let s = await state(page); const original = track(s, 'Y');
  const a = original.find(k => Math.abs(k.time - .8) < .001), b = original.find(k => Math.abs(k.time - 6.3) < .001);
  await clickKey(page, a.id); await clickKey(page, b.id, true);
  await expect.poll(async () => (await state(page)).selectedKeys).toBe(2);
  s = await state(page); const p = s.keyPositions.find(k => k.id === a.id);
  await page.mouse.move(s.timeline.x+p.x,s.timeline.y+p.y); await page.mouse.down();
  await page.mouse.move(s.timeline.x+p.x+s.pixelsPerSecond*.2,s.timeline.y+p.y,{steps:8}); await page.mouse.up();
  await expect.poll(async () => (await state(page)).keyframes.find(k => k.id===a.id).time).toBeCloseTo(1,3);
  expect((await state(page)).keyframes.find(k => k.id===b.id).time).toBeCloseTo(6.5,3);
  await page.keyboard.press('Control+z');
  await expect.poll(async () => (await state(page)).keyframes.find(k => k.id===a.id).time).toBeCloseTo(.8,3);
  expect((await state(page)).selectedKeys).toBe(2);
  await page.keyboard.press('Control+c'); await expect.poll(async () => (await state(page)).clipboardKeys).toBe(2);
  await scrub(page,2); await page.keyboard.press('Control+v');
  await expect.poll(async () => track(await state(page),'Y').length).toBe(original.length+2);
  s=await state(page); expect(s.keyframes.filter(k=>k.selected).map(k=>k.time)).toEqual([2,7.5]);
  await page.keyboard.press('Control+z'); await expect.poll(async () => track(await state(page),'Y').length).toBe(original.length);
  await page.keyboard.press('Control+x'); await expect.poll(async () => track(await state(page),'Y').length).toBe(original.length-2);
  await page.keyboard.press('Control+z'); await expect.poll(async () => (await state(page)).selectedKeys).toBe(2);
  expect((await state(page)).renderError).toBeNull();
});

test('Bezier handles edit through pointer input, cancel restores state, velocity graph renders', async ({page}) => {
  await start(page); await selectProperty(page,'Y');
  let s=await state(page); const selected=track(s,'Y').find(k=>Math.abs(k.time-.8)<.001);
  await clickKey(page,selected.id); await page.keyboard.press('F9'); await page.keyboard.press('Shift+F3');
  await expect.poll(async () => (await state(page)).handlePositions.length).toBeGreaterThan(0);
  s=await state(page); let h=s.handlePositions.find(h=>h.rightId===selected.id && !h.outgoing);
  expect(h).toBeTruthy(); const old=s.keyframes.find(k=>k.id===h.leftId).x2;
  await page.mouse.move(s.timeline.x+h.x,s.timeline.y+h.y); await page.mouse.down();
  await page.mouse.move(s.timeline.x+h.x-15,s.timeline.y+h.y-15,{steps:8}); await page.mouse.up();
  await expect.poll(async () => (await state(page)).keyframes.find(k=>k.id===h.leftId).x2).not.toBe(old);
  s=await state(page); const saved=s.keyframes.find(k=>k.id===h.leftId);
  h=s.handlePositions.find(h=>h.rightId===selected.id && !h.outgoing);
  await page.mouse.move(s.timeline.x+h.x,s.timeline.y+h.y); await page.mouse.down();
  await page.mouse.move(s.timeline.x+h.x+10,s.timeline.y+h.y+12,{steps:5}); await page.keyboard.press('Escape'); await page.mouse.up();
  await expect.poll(async () => (await state(page)).editing).toBeFalsy();
  expect((await state(page)).keyframes.find(k=>k.id===saved.id).x2).toBeCloseTo(saved.x2,8);
  await page.screenshot({path:'artifacts/screenshots/bezier-handles.png'});
  await page.keyboard.press('Shift+F4'); await expect.poll(async () => (await state(page)).graphKind).toBe('Velocity');
  await page.waitForTimeout(500); await page.screenshot({path:'artifacts/screenshots/velocity-graph.png'});
  expect((await state(page)).renderError).toBeNull();
});

test('effect stopwatches, numeric edits, marquee and clipboard share timeline channels', async ({page}) => {
  await start(page); await page.keyboard.press('Control+Alt+b');
  await expect.poll(async () => (await state(page)).effects).toBe(1);
  await expect.poll(async () => (await state(page)).rows.some(r=>r.displayName==='Gaussian Blur · Blurriness')).toBeTruthy();
  let s=await state(page), row=s.rows.find(r=>r.displayName==='Gaussian Blur · Blurriness'); const path=row.property;
  await page.mouse.click(s.timeline.x+92,s.timeline.y+row.y+11);
  await expect.poll(async () => track(await state(page),path).length).toBe(1);
  await scrub(page,1);
  await expect.poll(async () => (await state(page)).controls.some(c=>c.type==='TextBox' && c.name==='Gaussian Blur Blurriness')).toBeTruthy();
  s=await state(page); const input=s.controls.find(c=>c.type==='TextBox' && c.name==='Gaussian Blur Blurriness');
  await page.mouse.click(input.x+input.width/2,input.y+input.height/2); await page.keyboard.press('Control+a'); await page.keyboard.type('42'); await page.keyboard.press('Enter');
  await expect.poll(async () => track(await state(page),path).length).toBe(2);
  expect(track(await state(page),path).find(k=>Math.abs(k.time-1)<.001).value).toBe(42);
  s=await state(page); row=s.rows.find(r=>r.property===path);
  // Real pointer marquee encloses the two effect keys without touching a key on press.
  const x1=s.timeline.x+s.headerWidth+.8*s.pixelsPerSecond, x2=s.timeline.x+s.headerWidth+2.6*s.pixelsPerSecond;
  const y=s.timeline.y+row.y+11;
  await page.mouse.move(x1,y-8); await page.mouse.down(); await page.mouse.move(x2,y+8,{steps:10}); await page.mouse.up();
  await expect.poll(async () => (await state(page)).selectedKeys).toBe(2);
  await page.keyboard.press('Control+c'); await scrub(page,3); await page.keyboard.press('Control+v');
  await expect.poll(async () => track(await state(page),path).length).toBe(4);
  expect(track(await state(page),path).filter(k=>k.selected).map(k=>k.time)).toEqual([3,4.4]);
  await page.keyboard.press('Shift+F3'); await page.waitForTimeout(500);
  await page.screenshot({path:'artifacts/screenshots/effect-animation.png'});
  expect((await state(page)).renderError).toBeNull();
});
