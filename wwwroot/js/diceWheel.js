let canvas;
let ctx;
let slots = [];
let rotation = 0;
let centerState = {};

export function initialize(canvasId, state) {
    canvas = document.getElementById(canvasId);
    ctx = canvas.getContext("2d");
    slots = state.slots;
    centerState = state;
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
    draw(rotation);
}

export function updateCenter(state) {
    centerState = state;
    draw(rotation);
}

export function spinTo(targetIndex, duration) {
    return new Promise(resolve => {
        const arc = Math.PI * 2 / slots.length;
        const desired = -Math.PI / 2 - (targetIndex + 0.5) * arc;
        const start = rotation;
        let finish = desired;
        while (finish <= start) finish += Math.PI * 2;
        finish += Math.PI * 2 * 5;
        const started = performance.now();

        function frame(now) {
            const t = Math.min(1, (now - started) / duration);
            const eased = 1 - Math.pow(1 - t, 4);
            rotation = start + (finish - start) * eased;
            draw(rotation);
            if (t < 1) requestAnimationFrame(frame);
            else {
                rotation %= Math.PI * 2;
                draw(rotation);
                resolve();
            }
        }
        requestAnimationFrame(frame);
    });
}

function draw(angleOffset) {
    if (!ctx || slots.length === 0) return;
    const size = canvas.clientWidth;
    const c = size / 2;
    const radius = c - 18;
    const inner = radius * 0.46;
    const arc = Math.PI * 2 / slots.length;
    ctx.clearRect(0, 0, size, size);

    ctx.save();
    ctx.translate(c, c);
    ctx.shadowColor = "rgba(0,0,0,.55)";
    ctx.shadowBlur = 18;
    ctx.beginPath();
    ctx.arc(0, 0, radius + 5, 0, Math.PI * 2);
    ctx.fillStyle = "#080a0b";
    ctx.fill();
    ctx.shadowBlur = 0;

    slots.forEach((slot, index) => {
        const start = angleOffset + index * arc;
        const end = start + arc;
        ctx.beginPath();
        ctx.arc(0, 0, radius, start, end);
        ctx.arc(0, 0, inner, end, start, true);
        ctx.closePath();
        ctx.fillStyle = slot.isHard ? "#d4a72c" : (index % 2 ? "#202526" : "#713429");
        ctx.fill();
        ctx.strokeStyle = "rgba(238,224,190,.22)";
        ctx.lineWidth = 1;
        ctx.stroke();

        const middle = start + arc / 2;
        ctx.save();
        ctx.rotate(middle);
        ctx.translate(radius * .79, 0);
        ctx.rotate(Math.PI / 2);
        ctx.fillStyle = slot.isHard ? "#15191a" : "#f4ecd9";
        ctx.font = `700 ${Math.max(9, size * .023)}px system-ui`;
        ctx.textAlign = "center";
        ctx.textBaseline = "middle";
        ctx.fillText(slot.label, 0, 0);
        ctx.restore();
    });

    ctx.beginPath();
    ctx.arc(0, 0, inner - 5, 0, Math.PI * 2);
    ctx.fillStyle = "#102f27";
    ctx.fill();
    ctx.strokeStyle = "#d4a72c";
    ctx.lineWidth = 3;
    ctx.stroke();

    if (centerState.lastTotal) {
        drawDie(-inner * .32, 0, centerState.dieOne, inner * .38);
        drawDie(inner * .32, 0, centerState.dieTwo, inner * .38);
    } else {
        ctx.fillStyle = "#e4d7b8";
        ctx.textAlign = "center";
        ctx.font = `600 ${size * .035}px Georgia, serif`;
        ctx.fillText("36 COMBINATIONS", 0, -6);
        ctx.fillStyle = "#87a39a";
        ctx.font = `500 ${size * .021}px system-ui`;
        ctx.fillText("EVERY SLOT IS EQUALLY LIKELY", 0, 22);
    }
    ctx.restore();

    ctx.beginPath();
    ctx.moveTo(c, 13);
    ctx.lineTo(c - 15, 43);
    ctx.lineTo(c + 15, 43);
    ctx.closePath();
    ctx.fillStyle = "#f5d36b";
    ctx.fill();
    ctx.strokeStyle = "#5e4611";
    ctx.lineWidth = 2;
    ctx.stroke();
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
        ctx.arc(dx * size * .24, dy * size * .24, size * .065, 0, Math.PI * 2);
        ctx.fill();
    }
    ctx.restore();
}
