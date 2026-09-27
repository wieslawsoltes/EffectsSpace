import { test, expect } from '@playwright/test';
import fs from 'node:fs/promises';
const base = process.env.EFFECTSSPACE_URL || 'http://127.0.0.1:4173/EffectsSpace/';
async function state(page) { return page.evaluate(() => globalThis.effectsSpaceDiagnostics); }
async function start(page) {
  page.on('console', m => { if (m.type() === 'error') console.log('BROWSER:', m.text()); });
  page.on('pageerror', error => console.log('PAGE ERROR:', error.message));
  await page.goto(base + '?test=1', {waitUntil:'domcontentloaded'});
  await page.waitForFunction(() => globalThis.effectsSpaceDiagnostics?.ready, null, {timeout:150000});
  await expect.poll(async () => (await state(page)).viewer.width).toBeGreaterThan(100);
  await page.waitForTimeout(1200);
}
async function focusViewer(page) { const s = await state(page); await page.mouse.click(s.viewer.x + 15, s.viewer.y + 18); }
async function selectRow(page, name) {
  const s = await state(page), row = s.rows.find(r => r.name === name && !r.property);
  if (!row) throw new Error('Missing row: ' + name);
  await page.mouse.click(s.timeline.x + 180, s.timeline.y + row.y + 11);
}

test('real Uno workbench starts, renders editable artwork, scrubs and plays', async ({ page }) => {
  await start(page); const s = await state(page);
  expect(s.runtime).toContain('Uno'); expect(s.layers).toBeGreaterThan(8); expect(s.compositions).toBe(2); expect(s.renderError).toBeNull();
  await fs.mkdir('artifacts/screenshots', {recursive:true}); await page.screenshot({path:'artifacts/screenshots/workbench.png'});
  await page.mouse.click(s.timeline.x + s.headerWidth + 4 * s.pixelsPerSecond, s.timeline.y + 32);
  await expect.poll(async () => (await state(page)).time).toBeCloseTo(4, 1);
  await page.keyboard.press('Space'); await expect.poll(async () => (await state(page)).playing).toBeTruthy();
  await page.waitForTimeout(350); await page.keyboard.press('Space'); await expect.poll(async () => (await state(page)).playing).toBeFalsy();
  expect((await state(page)).time).not.toBe(4);
});

test('draw, duplicate, undo/redo, delete and download through real input', async ({ page }) => {
  await start(page); const before = (await state(page)).layers;
  await focusViewer(page); await page.keyboard.press('q');
  await expect.poll(async () => (await state(page)).tool).toBe('Rectangle');
  let s = await state(page); const x = s.viewer.x + s.viewer.width * .38, y = s.viewer.y + s.viewer.height * .62;
  await page.mouse.move(x,y); await page.mouse.down(); await page.mouse.move(x + 105,y + 55,{steps:8}); await page.mouse.up();
  await expect.poll(async () => (await state(page)).layers).toBe(before + 1);
  s = await state(page); expect(s.kind).toBe('Rectangle'); expect(s.width).toBeGreaterThan(50);
  await page.keyboard.press('Control+d'); await expect.poll(async () => (await state(page)).layers).toBe(before + 2);
  await page.keyboard.press('Control+z'); await expect.poll(async () => (await state(page)).layers).toBe(before + 1);
  await page.keyboard.press('Control+Shift+z'); await expect.poll(async () => (await state(page)).layers).toBe(before + 2);
  await expect.poll(async () => (await state(page)).selection).toBe(1);
  await page.keyboard.press('Delete'); await expect.poll(async () => (await state(page)).layers).toBe(before + 1);
  const waiting = page.waitForEvent('download'); await page.keyboard.press('Control+s'); const download = await waiting;
  const path = await download.path(); const document = JSON.parse(await fs.readFile(path, 'utf8'));
  expect(document.schemaVersion).toBe(1); expect(document.compositions[0].layers.length).toBe(before + 1);
});

test('timeline property keyframes and persistent recovery', async ({ page }) => {
  await start(page); await selectRow(page, 'ORBITAL');
  await expect.poll(async () => (await state(page)).name).toBe('ORBITAL');
  const beforeKeys = (await state(page)).keys;
  await page.keyboard.press('p');
  await expect.poll(async () => (await state(page)).rows.some(r => r.name === 'ORBITAL' && r.property === 'X')).toBeTruthy();
  const s = await state(page), row = s.rows.find(r => r.name === 'ORBITAL' && r.property === 'X');
  await page.mouse.click(s.timeline.x + 92, s.timeline.y + row.y + 11);
  await expect.poll(async () => (await state(page)).keys).toBe(beforeKeys + 1);
  await page.keyboard.press('Shift+F3'); await expect.poll(async () => (await state(page)).graph).toBeTruthy();
  await page.screenshot({path:'artifacts/screenshots/graph-editor.png'});
  await page.waitForTimeout(2200); const layers = (await state(page)).layers;
  await page.reload({waitUntil:'domcontentloaded'}); await page.waitForFunction(() => globalThis.effectsSpaceDiagnostics?.ready, null, {timeout:150000});
  expect((await state(page)).layers).toBe(layers); expect((await state(page)).project).toContain('ORBITAL');
  await selectRow(page, 'ORBITAL');
  await expect.poll(async () => (await state(page)).keys).toBe(beforeKeys + 1);
});
