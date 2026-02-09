(() => {
  const state = {
    input: { x: 0, y: 0, down: false, space: false, w: 0, h: 0 },
    ctx: null,
    canvas: null
  };
  function init(canvas) {
    state.canvas = canvas;
    state.ctx = canvas.getContext('2d');
    resize();
    canvas.addEventListener('mousemove', e => {
      const rect = canvas.getBoundingClientRect();
      state.input.x = e.clientX - rect.left;
      state.input.y = e.clientY - rect.top;
    });
    canvas.addEventListener('mousedown', () => state.input.down = true);
    canvas.addEventListener('mouseup', () => state.input.down = false);
    window.addEventListener('keydown', e => { if (e.code === 'Space') state.input.space = true; });
    window.addEventListener('keyup', e => { if (e.code === 'Space') state.input.space = false; });
    window.addEventListener('resize', resize);
  }
  function resize() {
    if (!state.canvas) return;
    state.canvas.width = window.innerWidth;
    state.canvas.height = window.innerHeight;
    state.input.w = state.canvas.width;
    state.input.h = state.canvas.height;
  }
  function getInput() {
    return { x: state.input.x, y: state.input.y, down: state.input.down, space: state.input.space, w: state.input.w, h: state.input.h };
  }
  function draw(payload) {
    const ctx = state.ctx;
    if (!ctx) return;
    ctx.save();
    ctx.clearRect(0, 0, state.canvas.width, state.canvas.height);
    ctx.translate(state.canvas.width / 2, state.canvas.height / 2);
    ctx.scale(payload.camera.zoom, payload.camera.zoom);
    ctx.translate(-payload.camera.x, -payload.camera.y);
    ctx.fillStyle = '#0b1e2d';
    ctx.fillRect(payload.bounds.x, payload.bounds.y, payload.bounds.w, payload.bounds.h);
    for (const f of payload.feathers) {
      ctx.beginPath();
      ctx.fillStyle = f.t === 0 ? '#6bd3ff' : f.t === 1 ? '#ffd166' : '#ff6e6e';
      ctx.arc(f.x, f.y, Math.max(3, f.v * 0.7), 0, Math.PI * 2);
      ctx.fill();
    }
    for (const p of payload.parrots) {
      if (!p.alive) continue;
      const hue = p.hue ?? 200;
      // Body feathers (elliptical)
      for (let i = 0; i < p.segments.length; i++) {
        const s = p.segments[i];
        const light = Math.max(35, 65 - i * 1.0);
        ctx.fillStyle = `hsl(${hue},80%,${light}%)`;
        ctx.beginPath();
        let angle = Math.atan2(p.diry, p.dirx);
        if (i > 0) {
            const prev = p.segments[i - 1];
            angle = Math.atan2(prev.y - s.y, prev.x - s.x);
        }
        ctx.ellipse(s.x, s.y, s.r * 1.15, s.r * 0.85, angle, 0, Math.PI * 2);
        ctx.fill();
      }
      // Head with beak and eye
      if (p.segments.length) {
        const head = p.segments[0];
        const ang = Math.atan2(p.diry, p.dirx);
        // Head circle
        ctx.fillStyle = `hsl(${hue},80%,60%)`;
        ctx.beginPath();
        ctx.arc(head.x, head.y, head.r * 0.9, 0, Math.PI * 2);
        ctx.fill();
        // Beak triangle
        const beakLen = head.r * 1.2;
        const bx = Math.cos(ang), by = Math.sin(ang);
        const p1 = { x: head.x + bx * beakLen, y: head.y + by * beakLen };
        const p2 = { x: head.x + Math.cos(ang + 0.6) * head.r * 0.7, y: head.y + Math.sin(ang + 0.6) * head.r * 0.7 };
        const p3 = { x: head.x + Math.cos(ang - 0.6) * head.r * 0.7, y: head.y + Math.sin(ang - 0.6) * head.r * 0.7 };
        ctx.fillStyle = '#f4b43b';
        ctx.beginPath();
        ctx.moveTo(p1.x, p1.y); ctx.lineTo(p2.x, p2.y); ctx.lineTo(p3.x, p3.y); ctx.closePath(); ctx.fill();
        // Eye
        ctx.fillStyle = '#111';
        const ex = head.x + Math.cos(ang + 0.3) * head.r * 0.4;
        const ey = head.y + Math.sin(ang + 0.3) * head.r * 0.4;
        ctx.beginPath(); ctx.arc(ex, ey, head.r * 0.12, 0, Math.PI * 2); ctx.fill();
        // Name label
        if (p.name) {
          ctx.fillStyle = 'rgba(255,255,255,0.9)';
          ctx.font = `${Math.max(10, head.r * 0.6)}px sans-serif`;
          ctx.textAlign = 'center';
          ctx.fillText(p.name, head.x, head.y - head.r * 1.4);
        }
      }
    }
    ctx.restore();
  }
  window.SquawkGame = { init, resize, getInput, draw };
})();
