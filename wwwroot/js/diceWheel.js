let canvas;
let ctx;
let state = { slots: [] };
let rotation = 0;
let ball = null;

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
    new ResizeObserver(resize).observe(canvas);
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
    return new Promise(resolve => {
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
            if (t < 1) requestAnimationFrame(frame);
            else {
                rotation %= Math.PI * 2;
                ball = { angle: -Math.PI / 2, distance: .9 };
                draw();
                resolve();
            }
        }
        requestAnimationFrame(frame);
    });
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
