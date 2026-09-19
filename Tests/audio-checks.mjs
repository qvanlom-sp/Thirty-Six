import { readFile } from 'node:fs/promises';
import assert from 'node:assert/strict';

// Exercise sound routing without browser permissions, speakers, or animation timing.
const oscillators = [];
class AudioContextMock {
    state = 'running';
    currentTime = 0;
    destination = {};
    resume() { return Promise.resolve(); }
    createOscillator() {
        const oscillator = { frequency: { value: 0, exponentialRampToValueAtTime() {} },
            connect(gain) { return gain; }, start() {}, stop() {} };
        oscillators.push(oscillator);
        return oscillator;
    }
    createGain() {
        return { gain: { setValueAtTime() {}, linearRampToValueAtTime() {}, exponentialRampToValueAtTime() {} }, connect() {} };
    }
}
globalThis.AudioContext = AudioContextMock;
const source = await readFile(new URL('../wwwroot/js/diceWheel.js', import.meta.url), 'utf8');
const { celebrate } = await import(`data:text/javascript;base64,${Buffer.from(source).toString('base64')}`);
async function sound(prize, saved, enabled, seven) {
    oscillators.length = 0;
    celebrate(prize, false, saved, enabled, true, seven);
    await Promise.resolve();
    return oscillators.map(o => [o.type, o.frequency.value]);
}
assert.deepEqual(await sound(0, false, false, true), [], 'muted seven-out is silent');
const loss = await sound(0, false, true, true);
assert.equal(loss.length, 4, 'zero-payout seven-out plays sound');
assert.ok(loss.every(n => n[0] === 'triangle'));
assert.ok(loss.every((n, i) => i === 0 || n[1] < loss[i - 1][1]), 'loss motif descends');
assert.deepEqual(await sound(100, false, true, true), loss, 'red-7 prize does not replace seven-out sound with a win chime');
const saved = await sound(0, true, true, true);
assert.deepEqual(saved.map(n => n[1]), [440, 660, 880], 'extra life uses saved chime');
assert.ok((await sound(100, false, true, false)).every(n => n[0] === 'sine'), 'wins retain their chime');
assert.deepEqual(await sound(0, false, true, false), [], 'ordinary miss stays silent');
console.log('7 audio routing checks passed (including reduced-motion sound).');
