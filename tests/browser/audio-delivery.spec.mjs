import { test, expect } from '@playwright/test';
import fs from 'node:fs/promises';
const base = process.env.EFFECTSSPACE_URL || 'http://127.0.0.1:4173/EffectsSpace/';
const state = page => page.evaluate(() => globalThis.effectsSpaceDiagnostics);
async function begin(page) {
  await page.goto(base + '?test=1', {waitUntil:'domcontentloaded'});
  await page.waitForFunction(() => globalThis.effectsSpaceDiagnostics?.ready, null, {timeout:150000});
  await page.keyboard.press('Control+Alt+l');
  await expect.poll(async () => (await state(page)).project, {timeout:30000}).toContain('CLOCKWORK');
  await expect.poll(async () => (await state(page)).waveformReady).toBeTruthy();
}
async function control(page, name, type = 'StudioButton') {
  await expect.poll(async () => (await state(page)).controls.some(c => c.name === name && c.type === type)).toBeTruthy();
  return (await state(page)).controls.find(c => c.name === name && c.type === type);
}
async function click(page, name) {
  const c = await control(page, name); expect(c.enabled).toBe(true);
  await page.mouse.click(c.x + c.width / 2, c.y + c.height / 2);
}
async function wave(page) {
  const pending = page.waitForEvent('download', {timeout:120000});
  await page.keyboard.press('Control+Alt+w');
  const file = await pending; expect(file.suggestedFilename()).toMatch(/\.wav$/);
  const bytes = await fs.readFile(await file.path());
  await expect.poll(async () => (await state(page)).rendering).toBeFalsy();
  return bytes;
}
function chunks(bytes) {
  expect(bytes.subarray(0,4).toString()).toBe('RIFF');
  expect(bytes.readUInt32LE(4)).toBe(bytes.length-8);
  expect(bytes.subarray(8,12).toString()).toBe('WAVE');
  const result = new Map();
  for (let offset=12; offset<bytes.length;) {
    const name=bytes.subarray(offset,offset+4).toString(), size=bytes.readUInt32LE(offset+4);
    expect(offset+8+size).toBeLessThanOrEqual(bytes.length);
    result.set(name,bytes.subarray(offset+8,offset+8+size)); offset+=8+size+(size&1);
  }
  return result;
}

test('PCM24 rate and dither settings drive real WAVE delivery without editing the project', async ({page}) => {
  await begin(page); const revision=(await state(page)).revision;
  await click(page,'Audio Delivery Settings');
  await click(page,'WAVE PCM 24-bit'); await click(page,'44.1 kHz'); await click(page,'TPDF Dither');
  await expect.poll(async () => (await state(page)).status).toContain('Pcm24 / 44100 Hz / TPDF');
  expect((await state(page)).revision).toBe(revision);
  const first=await wave(page), parts=chunks(first), format=parts.get('fmt ');
  expect(format.readUInt16LE(0)).toBe(1); expect(format.readUInt16LE(2)).toBe(2);
  expect(format.readUInt32LE(4)).toBe(44100); expect(format.readUInt16LE(14)).toBe(24);
  expect(parts.get('data').length).toBe(88200*6); expect(parts.get('data').some(b=>b!==0)).toBe(true);
  const second=await wave(page); expect(first.equals(second)).toBe(true);
  expect((await state(page)).revision).toBe(revision);
  await fs.mkdir('artifacts/screenshots',{recursive:true});
  await page.screenshot({path:'artifacts/screenshots/audio-delivery-settings.png'});
});

test('Float32 delivery preserves gain headroom and resampling quality invalidates prepared audio', async ({page}) => {
  await begin(page);
  await page.keyboard.press('Space'); await expect.poll(async () => (await state(page)).audioClock,{timeout:60000}).toBeTruthy();
  await page.keyboard.press('Space'); await expect.poll(async () => (await state(page)).playing).toBeFalsy();
  const originalLoads=await page.evaluate(()=>globalThis.effectsSpaceAudioDiagnostics.loads);
  await click(page,'Audio Delivery Settings'); await click(page,'Linear resampling');
  await expect.poll(async () => (await state(page)).status).toContain('Linear');
  await page.keyboard.press('Space'); await expect.poll(async () => (await state(page)).audioClock,{timeout:60000}).toBeTruthy();
  expect(await page.evaluate(()=>globalThis.effectsSpaceAudioDiagnostics.loads)).toBeGreaterThan(originalLoads);
  await page.keyboard.press('Space'); await expect.poll(async () => (await state(page)).playing).toBeFalsy();
  await click(page,'Back to Media');
  const gain=await control(page,'Audio gain dB','TextBox');
  await page.mouse.click(gain.x+gain.width/2,gain.y+gain.height/2); await page.keyboard.press('Control+a'); await page.keyboard.type('24'); await page.keyboard.press('Enter');
  await expect.poll(async () => (await state(page)).audioGain).toBe(24);
  await click(page,'Audio Delivery Settings'); await click(page,'WAVE Float 32-bit'); await click(page,'96 kHz'); await click(page,'Band-limited resampling');
  expect((await control(page,'TPDF Dither')).enabled).toBe(false);
  const bytes=await wave(page), parts=chunks(bytes), format=parts.get('fmt ');
  expect(format.readUInt16LE(0)).toBe(3); expect(format.readUInt16LE(14)).toBe(32); expect(format.readUInt32LE(4)).toBe(96000);
  expect(parts.get('fact').readUInt32LE(0)).toBe(192000); expect(parts.get('data').length).toBe(192000*8);
  let peak=0; const data=parts.get('data');
  for(let offset=0;offset<data.length;offset+=4){const value=data.readFloatLE(offset);expect(Number.isFinite(value)).toBe(true);peak=Math.max(peak,Math.abs(value));}
  expect(peak).toBeGreaterThan(1); expect(peak).toBeLessThan(4);
  expect((await state(page)).renderError).toBeNull();
});
