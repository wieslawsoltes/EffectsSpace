import { test, expect } from '@playwright/test';
import fs from 'node:fs/promises';
const base = process.env.EFFECTSSPACE_URL || 'http://127.0.0.1:4173/EffectsSpace/';
const state = page => page.evaluate(() => globalThis.effectsSpaceDiagnostics);
async function begin(page) {
  await page.goto(base + '?test=1', { waitUntil: 'domcontentloaded' });
  await page.waitForFunction(() => globalThis.effectsSpaceDiagnostics?.ready, null, { timeout: 150000 });
  await page.keyboard.press('Control+Alt+l');
  await expect.poll(async () => (await state(page)).project, { timeout: 30000 }).toContain('CLOCKWORK');
  await expect.poll(async () => (await state(page)).waveformReady).toBeTruthy();
  await expect.poll(async () => (await state(page)).videoImageCreations).toBeGreaterThan(0);
  expect((await state(page)).renderError).toBeNull();
}
async function focusTimeline(page) {
  const s = await state(page); await page.mouse.click(s.timeline.x + 170, s.timeline.y + 28);
}
async function control(page, name, type = 'StudioButton') {
  await expect.poll(async () => (await state(page)).controls.some(c => c.name === name && c.type === type)).toBeTruthy();
  return (await state(page)).controls.find(c => c.name === name && c.type === type);
}
async function click(page, name) { const c = await control(page,name); await page.mouse.click(c.x+c.width/2,c.y+c.height/2); }
async function download(page, shortcut) {
  const pending = page.waitForEvent('download', { timeout: 90000 }); await page.keyboard.press(shortcut);
  const file = await pending; return { name: file.suggestedFilename(), bytes: await fs.readFile(await file.path()) };
}

test('Motion JPEG footage uses source-time frames and an audio-clock-driven browser preview', async ({ page }) => {
  await begin(page); expect((await state(page)).kind).toBe('Video');
  let s = await state(page);
  await page.mouse.click(s.timeline.x+s.headerWidth+s.pixelsPerSecond,s.timeline.y+32);
  await expect.poll(async () => (await state(page)).videoFrame).toBe(24);
  await page.keyboard.press('Home'); await page.keyboard.press('Space');
  await expect.poll(async () => (await state(page)).audioClock, { timeout: 60000 }).toBeTruthy();
  await expect.poll(async () => (await state(page)).time).toBeGreaterThan(0);
  const output = await page.evaluate(() => globalThis.effectsSpaceAudioDiagnostics);
  expect(output.state).toBe('running'); expect(output.playing).toBe(true); expect(output.duration).toBeCloseTo(2,3);
  await page.keyboard.press('Space'); await expect.poll(async () => (await state(page)).playing).toBeFalsy();
  expect((await page.evaluate(() => globalThis.effectsSpaceAudioDiagnostics)).playing).toBe(false);
  await page.keyboard.press('Space'); await expect.poll(async () => (await state(page)).audioClock).toBeTruthy();
  expect((await page.evaluate(() => globalThis.effectsSpaceAudioDiagnostics)).loads).toBe(output.loads);
  await page.keyboard.press('Space');
  await fs.mkdir('artifacts/screenshots',{recursive:true}); await page.screenshot({path:'artifacts/screenshots/media-footage-waveform.png'});
});

test('audio gain, mute and source freeze are real undoable editor operations', async ({ page }) => {
  await begin(page);
  const c = await control(page,'Audio gain dB','TextBox');
  await page.mouse.click(c.x+c.width/2,c.y+c.height/2); await page.keyboard.press('Control+a'); await page.keyboard.type('-6'); await page.keyboard.press('Enter');
  await expect.poll(async () => (await state(page)).audioGain).toBe(-6);
  await focusTimeline(page); await page.keyboard.press('Control+z'); await expect.poll(async () => (await state(page)).audioGain).toBe(0);
  await page.keyboard.press('Control+Shift+z'); await expect.poll(async () => (await state(page)).audioGain).toBe(-6);
  await click(page,'Audio Enabled'); await expect.poll(async () => (await state(page)).audioEnabled).toBeFalsy();
  await focusTimeline(page); await page.keyboard.press('Control+z'); await expect.poll(async () => (await state(page)).audioEnabled).toBeTruthy();
  let s = await state(page); await page.mouse.click(s.timeline.x+s.headerWidth+s.pixelsPerSecond,s.timeline.y+32);
  await page.keyboard.press('Control+Alt+f'); await expect.poll(async () => (await state(page)).timeRemapEnabled).toBeTruthy();
  await page.keyboard.press('Home'); await expect.poll(async () => (await state(page)).videoFrame).toBe(24);
  await page.keyboard.press('Control+z'); await expect.poll(async () => (await state(page)).timeRemapEnabled).toBeFalsy();
  await expect.poll(async () => (await state(page)).videoFrame).toBe(0);
});

test('AVI and WAVE export round-trip through actual downloads and media import', async ({ page }) => {
  await begin(page); await focusTimeline(page);
  const avi = await download(page,'Control+Alt+m');
  expect(avi.name).toMatch(/\.avi$/); expect(avi.bytes.subarray(0,4).toString()).toBe('RIFF');
  expect(avi.bytes.subarray(8,12).toString()).toBe('AVI '); expect(avi.bytes.includes(Buffer.from('MJPG'))).toBe(true); expect(avi.bytes.includes(Buffer.from('auds'))).toBe(true);
  const picker = page.waitForEvent('filechooser'); await page.keyboard.press('Control+i');
  await (await picker).setFiles({name:'Roundtrip.avi',mimeType:'video/x-msvideo',buffer:avi.bytes});
  await expect.poll(async () => (await state(page)).assets).toBe(2);
  await expect.poll(async () => (await state(page)).name).toBe('Roundtrip.avi');
  expect((await state(page)).kind).toBe('Video'); expect((await state(page)).renderError).toBeNull();
  const wave = await download(page,'Control+Alt+w');
  expect(wave.name).toMatch(/\.wav$/); expect(wave.bytes.subarray(8,12).toString()).toBe('WAVE');
  expect(wave.bytes.readUInt32LE(24)).toBe(48000); expect(wave.bytes.readUInt16LE(22)).toBe(2); expect(wave.bytes.readUInt32LE(40)).toBe(96000*4);
  expect(wave.bytes.subarray(44).some(b=>b!==0)).toBe(true);
  const audioPicker = page.waitForEvent('filechooser'); await page.keyboard.press('Control+i');
  await (await audioPicker).setFiles({name:'Mixed.wav',mimeType:'audio/wav',buffer:wave.bytes});
  await expect.poll(async () => (await state(page)).kind).toBe('Audio'); await expect.poll(async () => (await state(page)).assets).toBe(3);
  await expect.poll(async () => (await state(page)).waveformReady).toBeTruthy();
  const project = await download(page,'Control+s'); const data = JSON.parse(project.bytes.toString('utf8'));
  expect(data.assets.map(a=>a.mimeType)).toEqual(['video/x-msvideo','video/x-msvideo','audio/wav']);
  expect(data.compositions[0].layers[0].audioEnabled).toBe(true);
  await page.screenshot({path:'artifacts/screenshots/media-render-queue.png'});
});
