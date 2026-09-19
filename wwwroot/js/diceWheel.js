let canvas;
let ctx;
let state = { slots: [] };
let rotation = 0;
let ball = null;
let resizeObserver;
let effectFrame;
let spinFrame;
let finishSpin;
let audio;

const colors = {
    2: "#69458b", 3: "#514d82", 4: "#2e6393", 5: "#24746f", 6: "#286b4d",
    7: "#a53035", 8: "#286b4d", 9: "#24746f", 10: "#2e6393", 11: "#514d82", 12: "#69458b"
};

const hardColors = { 4: "#a8c9e2", 6: "#9ed3b7", 8: "#9ed3b7", 10: "#a8c9e2" };

export function initialize(canvasId, nextState) {
    canvas = document.getElementById(canvasId);
    ctx = canvas.getContext("2d");
    state = nextState;
    resize();
    resizeObserver = new ResizeObserver(resize);
    resizeObserver.observe(canvas.parentElement);
}

function resize() {
    const size = Math.min(canvas.parentElement.clientWidth, 620);
    const dpr = window.devicePixelRatio || 1;
    canvas.style.width = `${size}px`;
    canvas.style.height = `${size}px`;
    canvas.width = Math.round(size * dpr);
    canvas.height = Math.round(size * dpr);
    ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
    draw();
}

export function updateState(nextState) {
    state = nextState;
    draw();
}

export function spinTo(targetIndex, duration) {
    cancelAnimationFrame(effectFrame);
    const effects = document.getElementById("win-effects");
    if (effects) effects.getContext("2d").clearRect(0, 0, effects.width, effects.height);
    return new Promise(resolve => {
        finishSpin = resolve;
        const arc = Math.PI * 2 / state.slots.length;
        const desired = -Math.PI / 2 - (targetIndex + 0.5) * arc;
        const start = rotation;
        let finish = desired;
        while (finish <= start) finish += Math.PI * 2;
        finish += Math.PI * 2 * 5;
        const started = performance.now();
        ball = { angle: -Math.PI / 2, distance: 1 };

        function frame(now) {
            const t = Math.min(1, (now - started) / duration);
            const eased = 1 - Math.pow(1 - t, 4);
            rotation = start + (finish - start) * eased;
            ball.angle = -Math.PI / 2 - Math.PI * 2 * 7 * eased;
            ball.distance = 1 - .1 * eased;
            draw();
            if (t < 1) spinFrame = requestAnimationFrame(frame);
            else {
                rotation %= Math.PI * 2;
                ball = { angle: -Math.PI / 2, distance: .9 };
                draw();
                resolve();
                finishSpin = null;
            }
        }
        spinFrame = requestAnimationFrame(frame);
    });
}

export function prefersReducedMotion() {
    return window.matchMedia("(prefers-reduced-motion: reduce)").matches;
}

export function celebrate(prize, challenge, saved, sound, reducedMotion, sevenOut = false) {
    // Seven-out takes precedence over a red-7 payout; a saved seven keeps its uplifting chime.
    if (sevenOut && !saved) {
        if (sound) playChime(1, false, true);
        return;
    }
    const tier = challenge || prize >= 500 ? 3 : prize >= 100 ? 2 : 1;
    if (prize <= 0 && !saved) return;
    if (sound) playChime(tier, saved);
    if (reducedMotion) return;
    const layer = document.getElementById("win-effects");
    if (!layer || !canvas) return;
    cancelAnimationFrame(effectFrame);
    const size = canvas.clientWidth;
    const dpr = window.devicePixelRatio || 1;
    layer.width = size * dpr;
    layer.height = size * dpr;
    const fx = layer.getContext("2d");
    fx.setTransform(dpr, 0, 0, dpr, 0, 0);
    const palette = saved ? ["#8fffd2", "#fff", "#5db7ff"] : ["#ffe596", "#e6b844", "#fff5d8", "#79eac5"];
    const particles = Array.from({ length: tier * 34 }, () => {
        const angle = Math.random() * Math.PI * 2;
        const speed = size * (.18 + Math.random() * .48);
        return { vx: Math.cos(angle) * speed, vy: Math.sin(angle) * speed - size * .22,
            color: palette[Math.floor(Math.random() * palette.length)], size: 3 + Math.random() * 5, angle };
    });
    const start = performance.now();
    function frame(now) {
        const t = (now - start) / 1000;
        fx.clearRect(0, 0, size, size);
        fx.globalAlpha = Math.max(0, 1 - t / 1.8);
        fx.strokeStyle = saved ? "#8fffd2" : "#ffe596";
        fx.lineWidth = 3;
        fx.beginPath();
        fx.arc(size / 2, size / 2, Math.min(size * .47, t * size * .5), 0, Math.PI * 2);
        fx.stroke();
        for (const p of particles) {
            fx.save();
            fx.translate(size / 2 + p.vx * t, size / 2 + p.vy * t + size * .3 * t * t);
            fx.rotate(p.angle + t * 5);
            fx.fillStyle = p.color;
            fx.fillRect(-p.size / 2, -p.size / 2, p.size, p.size * .6);
            fx.restore();
        }
        if (t < 1.8) effectFrame = requestAnimationFrame(frame);
        else fx.clearRect(0, 0, size, size);
    }
    effectFrame = requestAnimationFrame(frame);
}

function playChime(tier, saved, sevenOut = false) {
    try {
        audio ??= new AudioContext();
        audio.resume().then(() => {
            if (!audio || audio.state !== "running") return;
            const notes = sevenOut ? [220, 164.81, 110, 55] : saved ? [440, 660, 880] : [523.25, 659.25, 783.99, 1046.5].slice(0, tier + 1);
            notes.forEach((frequency, index) => {
                const oscillator = audio.createOscillator();
                const gain = audio.createGain();
                const time = audio.currentTime + index * (sevenOut ? .13 : .09);
                const duration = sevenOut ? .55 : .4;
                oscillator.type = sevenOut ? "triangle" : "sine";
                oscillator.frequency.value = frequency;
                if (sevenOut) oscillator.frequency.exponentialRampToValueAtTime(frequency * .72, time + duration);
                gain.gain.setValueAtTime(0, time);
                gain.gain.linearRampToValueAtTime(sevenOut ? .055 : .065, time + .015);
                gain.gain.exponentialRampToValueAtTime(.001, time + duration);
                oscillator.connect(gain).connect(audio.destination);
                oscillator.start(time);
                oscillator.stop(time + duration + .02);
            });
        }).catch(() => {});
    } catch { /* Audio is optional when the browser disallows it. */ }
}

export function dispose() {
    resizeObserver?.disconnect();
    cancelAnimationFrame(effectFrame);
    cancelAnimationFrame(spinFrame);
    finishSpin?.();
    finishSpin = null;
    audio?.close().catch(() => {});
    audio = null;
    ctx = null;
    canvas = null;
}

function draw() {
    if (!ctx || state.slots.length === 0) return;
    const size = canvas.clientWidth;
    const c = size / 2;
    const radius = c - 22;
    const inner = radius * .46;
    const arc = Math.PI * 2 / state.slots.length;
    ctx.clearRect(0, 0, size, size);

    ctx.save();
    ctx.translate(c, c);
    ctx.shadowColor = "rgba(0,0,0,.65)";
    ctx.shadowBlur = 24;
    ctx.beginPath();
    ctx.arc(0, 0, radius + 8, 0, Math.PI * 2);
    ctx.fillStyle = "#070908";
    ctx.fill();
    ctx.shadowBlur = 0;

    state.slots.forEach((slot, index) => {
        const start = rotation + index * arc;
        const end = start + arc;
        ctx.beginPath();
        ctx.arc(0, 0, radius, start, end);
        ctx.arc(0, 0, inner, end, start, true);
        ctx.closePath();
        ctx.fillStyle = slot.blocked ? "#1a1d1c" : (slot.isHard ? hardColors[slot.total] : colors[slot.total]);
        ctx.fill();
        ctx.strokeStyle = slot.blocked ? "#303633" : "rgba(249,238,210,.26)";
        ctx.lineWidth = 1;
        ctx.stroke();
        if (slot.powered) {
            ctx.save();
            ctx.shadowColor = "#fff2a3";
            ctx.shadowBlur = 16;
            ctx.strokeStyle = "#fff8c9";
            ctx.lineWidth = 3;
            ctx.stroke();
            ctx.restore();
        }
        if (slot.carded && !slot.powered) {
            ctx.save();
            ctx.shadowColor = "#86ead9";
            ctx.shadowBlur = 12;
            ctx.strokeStyle = "#cafff6";
            ctx.lineWidth = 2;
            ctx.stroke();
            ctx.restore();
        }

        const middle = start + arc / 2;
        ctx.save();
        ctx.rotate(middle);
        ctx.translate(radius * .78, 0);
        ctx.rotate(Math.PI / 2);
        ctx.fillStyle = slot.blocked ? "#616965" : (slot.isHard ? "#153024" : "#fff7e5");
        ctx.font = `700 ${Math.max(9, size * .023)}px system-ui`;
        ctx.textAlign = "center";
        ctx.textBaseline = "middle";
        ctx.fillText(slot.blocked ? "×" : slot.label, 0, 0);
        ctx.restore();

        if (slot.wagered && !slot.blocked) {
            ctx.save();
            ctx.rotate(middle);
            ctx.translate(radius * .58, 0);
            ctx.beginPath();
            ctx.arc(0, 0, Math.max(4, size * .009), 0, Math.PI * 2);
            ctx.fillStyle = "#f6d568";
            ctx.fill();
            ctx.strokeStyle = "#3f3008";
            ctx.lineWidth = 1.5;
            ctx.stroke();
            ctx.restore();
        }
    });

    ctx.beginPath();
    ctx.arc(0, 0, inner - 5, 0, Math.PI * 2);
    ctx.fillStyle = "#0e2921";
    ctx.fill();
    ctx.strokeStyle = "#d4a72c";
    ctx.lineWidth = 3;
    ctx.stroke();
    drawCenter(size, inner);

    if (ball) {
        const ballRadius = radius * ball.distance;
        const x = Math.cos(ball.angle) * ballRadius;
        const y = Math.sin(ball.angle) * ballRadius;
        ctx.shadowColor = "rgba(0,0,0,.9)";
        ctx.shadowBlur = 8;
        ctx.beginPath();
        ctx.arc(x, y, Math.max(6, size * .014), 0, Math.PI * 2);
        const gradient = ctx.createRadialGradient(x - 3, y - 4, 1, x, y, size * .016);
        gradient.addColorStop(0, "#fffdf1");
        gradient.addColorStop(.45, "#d7d0bd");
        gradient.addColorStop(1, "#6f706b");
        ctx.fillStyle = gradient;
        ctx.fill();
        ctx.shadowBlur = 0;
    }
    ctx.restore();

    ctx.beginPath();
    ctx.moveTo(c, 12);
    ctx.lineTo(c - 15, 44);
    ctx.lineTo(c + 15, 44);
    ctx.closePath();
    ctx.fillStyle = "#f5d36b";
    ctx.fill();
    ctx.strokeStyle = "#5e4611";
    ctx.lineWidth = 2;
    ctx.stroke();
}

function drawCenter(size, inner) {
    ctx.textAlign = "center";
    ctx.textBaseline = "middle";
    if (state.lastTotal) {
        ctx.fillStyle = colors[state.lastTotal];
        ctx.beginPath();
        ctx.arc(0, -inner * .12, inner * .3, 0, Math.PI * 2);
        ctx.fill();
        ctx.fillStyle = "#fff9e9";
        ctx.font = `700 ${size * .095}px Georgia, serif`;
        ctx.fillText(state.lastTotal, 0, -inner * .13);
        drawDie(-inner * .14, inner * .53, state.dieOne, inner * .21);
        drawDie(inner * .14, inner * .53, state.dieTwo, inner * .21);
        if (state.point) {
            ctx.fillStyle = "#99ada5";
            ctx.font = `600 ${size * .018}px system-ui`;
            ctx.fillText(`POINT ${state.point}`, 0, -inner * .55);
        }
    } else {
        ctx.fillStyle = "#e8deca";
        ctx.font = `600 ${size * .034}px Georgia, serif`;
        ctx.fillText(state.phase === "ComeOut" ? "SET THE POINT" : "READY", 0, -5);
        ctx.fillStyle = "#8da39a";
        ctx.font = `600 ${size * .018}px system-ui`;
        ctx.fillText(state.phase === "ComeOut" ? "SAFE SPIN · NO BETS" : "36 DICE COMBINATIONS", 0, 22);
    }
}

function drawDie(x, y, value, size) {
    const half = size / 2;
    ctx.save();
    ctx.translate(x, y);
    ctx.fillStyle = "#eee5d2";
    ctx.beginPath();
    ctx.roundRect(-half, -half, size, size, size * .18);
    ctx.fill();
    const dots = {
        1:[[0,0]], 2:[[-1,-1],[1,1]], 3:[[-1,-1],[0,0],[1,1]],
        4:[[-1,-1],[1,-1],[-1,1],[1,1]], 5:[[-1,-1],[1,-1],[0,0],[-1,1],[1,1]],
        6:[[-1,-1],[-1,0],[-1,1],[1,-1],[1,0],[1,1]]
    };
    ctx.fillStyle = "#17211e";
    for (const [dx, dy] of dots[value]) {
        ctx.beginPath();
        ctx.arc(dx * size * .24, dy * size * .24, size * .07, 0, Math.PI * 2);
        ctx.fill();
    }
    ctx.restore();
}
