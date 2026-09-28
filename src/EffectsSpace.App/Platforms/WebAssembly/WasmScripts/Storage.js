(() => {
  'use strict';
  const testMode = new URLSearchParams(location.search).has('test');
  const DB = 'EffectsSpace', STORE = 'projects', KEY = 'recovery-v1';
  let connection;
  function database() {
    if (connection) return connection;
    connection = new Promise((resolve, reject) => {
      const request = indexedDB.open(DB, 1);
      request.onupgradeneeded = () => { if (!request.result.objectStoreNames.contains(STORE)) request.result.createObjectStore(STORE); };
      request.onerror = () => { connection = null; reject(request.error || new Error('IndexedDB could not be opened.')); };
      request.onsuccess = () => { const db = request.result; db.onversionchange = () => { db.close(); connection = null; }; resolve(db); };
      request.onblocked = () => { connection = null; reject(new Error('Close other EffectsSpace tabs to unlock local storage.')); };
    });
    return connection;
  }
  function mime(file) {
    const ext = file.name.split('.').pop().toLowerCase();
    return ({png:'image/png',jpg:'image/jpeg',jpeg:'image/jpeg',webp:'image/webp',avi:'video/x-msvideo',wav:'audio/wav',effects:'application/json',json:'application/json'})[ext] || file.type || 'application/octet-stream';
  }
  async function encode(file) {
    const project = /\.(effects|json)$/i.test(file.name);
    if (file.size > (project ? 96 : 32) * 1024 * 1024) throw new Error(`${file.name} exceeds the import size limit.`);
    const data = await new Promise((resolve, reject) => { const reader = new FileReader(); reader.onload = () => resolve(reader.result.split(',')[1]); reader.onerror = () => reject(reader.error); reader.readAsDataURL(file); });
    return {name:file.name, type:mime(file), data};
  }
  globalThis.effectsSpaceStorage = {
    async load() {
      const db = await database();
      return new Promise((resolve, reject) => { const tx = db.transaction(STORE, 'readonly'); const r = tx.objectStore(STORE).get(KEY); r.onsuccess = () => resolve(typeof r.result === 'string' ? r.result : ''); r.onerror = () => reject(r.error); });
    },
    async save(document) {
      if (document.length > 96 * 1024 * 1024) throw new Error('Recovery document is too large.');
      const db = await database();
      return new Promise((resolve, reject) => { const tx = db.transaction(STORE, 'readwrite'); tx.objectStore(STORE).put(document, KEY); tx.oncomplete = () => resolve('saved'); tx.onerror = () => reject(tx.error); tx.onabort = () => reject(tx.error || new Error('Recovery write was aborted.')); });
    },
    open(projectOnly) {
      return new Promise((resolve, reject) => {
        const input = document.createElement('input'); input.type = 'file'; input.multiple = !projectOnly; input.accept = projectOnly ? '.effects,.json' : '.png,.jpg,.jpeg,.webp,.avi,.wav,.effects,.json'; input.style.display = 'none'; document.body.append(input);
        let settled = false;
        const finish = (value, error) => { if (settled) return; settled = true; input.remove(); error ? reject(error) : resolve(value); };
        input.addEventListener('cancel', () => finish('[]'), {once:true});
        input.addEventListener('change', async () => { try { const files = [...input.files]; if (files.length > 64) throw new Error('Import at most 64 files at once.'); let bytes = 0; for (const f of files) bytes += f.size; if (bytes > 96 * 1024 * 1024) throw new Error('Import exceeds 96 MiB.'); finish(JSON.stringify(await Promise.all(files.map(encode)))); } catch (e) { finish(null, e); } }, {once:true});
        input.click();
      });
    },
    async download(name, base64, contentType) {
      const binary = atob(base64), chunks = [];
      for (let start = 0; start < binary.length; start += 65536) { const end = Math.min(start + 65536, binary.length); const bytes = new Uint8Array(end - start); for (let i = start; i < end; i++) bytes[i - start] = binary.charCodeAt(i); chunks.push(bytes); }
      const blob = new Blob(chunks, {type:contentType}), url = URL.createObjectURL(blob); const link = document.createElement('a'); link.href = url; link.download = name; link.style.display = 'none'; document.body.append(link); link.click(); link.remove(); setTimeout(() => URL.revokeObjectURL(url), 30000); return 'downloaded';
    },
    isTestMode() { return testMode; },
    publishDiagnostics(json) { if (!testMode) return; globalThis.effectsSpaceDiagnostics = Object.freeze(JSON.parse(json)); document.documentElement.dataset.effectsSpaceReady = String(globalThis.effectsSpaceDiagnostics.ready); }
  };
  // Let the C# editor receive application shortcuts, not browser Save/Open dialogs.
  document.addEventListener('keydown', event => {
    if (event.shiftKey && ['F2','F3','F4','F5','F6','F7'].includes(event.key)) event.preventDefault();
    if (event.ctrlKey && event.altKey && ['y','t','f','b','l','w','m'].includes(event.key.toLowerCase())) event.preventDefault();
    if ((event.ctrlKey || event.metaKey) && ['s','o','i','n','k','d','m'].includes(event.key.toLowerCase())) event.preventDefault();
    if (event.key === 'Backspace' && !['INPUT','TEXTAREA'].includes(event.target.tagName)) event.preventDefault();
  }, true);
})();

(() => {
  'use strict';
  let context, buffer, source, serial = 0, anchor = 0, offset = 0, looping = false, loads = 0;
  function audioContext() {
    const Audio = globalThis.AudioContext || globalThis.webkitAudioContext;
    if (!Audio) throw new Error('Web Audio output is not available in this browser.');
    return context || (context = new Audio({ sampleRate: 48000 }));
  }
  function stopSource() {
    if (source) { source.onended = null; try { source.stop(); } catch { } source.disconnect(); source = undefined; }
  }
  globalThis.effectsSpaceAudio = {
    async unlock() {
      const audio = audioContext(); await audio.resume();
      if (audio.state !== 'running') throw new Error('Audio output is suspended. Enable site audio and start playback again.');
      return 'running';
    },
    async load(base64) {
      if (base64.length > 16 * 1024 * 1024) throw new Error('Prepared audio exceeds the 60-second preview budget.');
      const version = ++serial, audio = audioContext();
      const raw = atob(base64), bytes = new Uint8Array(raw.length);
      for (let i = 0; i < raw.length; i++) bytes[i] = raw.charCodeAt(i);
      const decoded = await audio.decodeAudioData(bytes.buffer);
      if (version !== serial) throw new Error('Audio preparation was cancelled.');
      if (decoded.duration > 60.001 || decoded.numberOfChannels !== 2) throw new Error('Invalid prepared stereo audio.');
      stopSource(); buffer = decoded; loads++; return 'loaded';
    },
    play(position, loop) {
      const audio = audioContext();
      if (!buffer || !Number.isFinite(position)) throw new Error('No prepared audio buffer.');
      stopSource();
      offset = Math.max(0, Math.min(position, Math.max(0, buffer.duration - 1 / buffer.sampleRate)));
      anchor = audio.currentTime; looping = loop;
      source = audio.createBufferSource(); source.buffer = buffer; source.loop = loop; source.connect(audio.destination);
      source.onended = () => { if (!looping) { source?.disconnect(); source = undefined; } };
      source.start(0, offset);
    },
    stop() { serial++; stopSource(); offset = 0; },
    position() {
      if (!context || !buffer || !source) return 0;
      const time = Math.max(0, offset + context.currentTime - anchor);
      return looping ? time % buffer.duration : Math.min(time, buffer.duration);
    }
  };
  if (new URLSearchParams(location.search).has('test')) {
    Object.defineProperty(globalThis, 'effectsSpaceAudioDiagnostics', {
      get() { return Object.freeze({ state: context?.state || 'not-created', playing: !!source, duration: buffer?.duration || 0, loads }); }
    });
  }
  addEventListener('pagehide', () => { serial++; stopSource(); buffer = undefined; context?.close(); context = undefined; });
})();
