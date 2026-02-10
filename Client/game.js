(() => {
  const state = {
    input: { x: 0, y: 0, down: false, space: false, w: 0, h: 0 },
    ctx: null,
    canvas: null,
    
    // Networking
    lastPayload: null,
    currentPayload: null,
    lastPayloadTime: 0,
    currentPayloadTime: 0,
    
    // Entities
    parrots: new Map(), // id -> Parrot object
    myId: null,
    
    // Camera
    camera: { x: 0, y: 0, zoom: 1 },
    mapRadius: 2500,
    
    renderOffset: 100 // ms delay for interpolation
  };

  class Parrot {
    constructor(data) {
      this.id = data.i;
      this.updateData(data);
      this.visualX = data.x;
      this.visualY = data.y;
      this.visualDirX = data.dx;
      this.visualDirY = data.dy;
      this.segments = [];
      this.initSegments();
    }
    
    updateData(data) {
      this.targetX = data.x;
      this.targetY = data.y;
      this.targetDirX = data.dx;
      this.targetDirY = data.dy;
      this.energy = data.e;
      this.hue = data.h;
      this.name = data.nm;
      this.size = 8 + Math.sqrt(Math.max(0, this.energy)) * 0.9;
    }
    
    initSegments() {
      const count = 10;
      const spacing = 4;
      for(let i=0; i<count; i++) {
        this.segments.push({
          x: this.visualX - this.visualDirX * i * spacing,
          y: this.visualY - this.visualDirY * i * spacing,
          r: this.size * 0.8
        });
      }
    }
    
    updateVisuals(alpha) {
      // Interpolate Head
      // Note: This is simple lerp between last known pos and current target.
      // For better results, we should lerp between snapshot T-1 and T.
      // But we just use current "visual" and move it towards "target".
      // Smooth damp factor
      const t = 0.2; // Adjust for smoothness vs lag
      this.visualX += (this.targetX - this.visualX) * t;
      this.visualY += (this.targetY - this.visualY) * t;
      this.visualDirX += (this.targetDirX - this.visualDirX) * t;
      this.visualDirY += (this.targetDirY - this.visualDirY) * t;
      
      // Update Segments
      this.updateSegments();
    }
    
    updateSegments() {
      const segmentSpacing = 4;
      
      // Head
      if (this.segments.length === 0) this.initSegments();
      
      let head = this.segments[0];
      head.x = this.visualX;
      head.y = this.visualY;
      head.r = this.size * 1.05;
      
      // Body
      for (let i = 1; i < this.segments.length; i++) {
        let prev = this.segments[i - 1];
        let cur = this.segments[i];
        
        let dx = prev.x - cur.x;
        let dy = prev.y - cur.y;
        let dist = Math.sqrt(dx*dx + dy*dy);
        
        if (dist > 0) {
            dx /= dist;
            dy /= dist;
        } else {
            dx = 1; dy = 0;
        }
        
        let tx = prev.x - dx * segmentSpacing;
        let ty = prev.y - dy * segmentSpacing;
        
        // Lerp segment
        cur.x += (tx - cur.x) * 0.75;
        cur.y += (ty - cur.y) * 0.75;
        
        cur.r = Math.max(3, this.size * 0.85 - i * 0.05 * this.size);
      }
      
      // Add/Remove
      const desiredCount = Math.min(40, Math.max(10, Math.floor(this.energy * 0.5) + 10));
      if (desiredCount > this.segments.length) {
          let tail = this.segments[this.segments.length - 1];
          this.segments.push({
              x: tail.x - this.visualDirX * segmentSpacing,
              y: tail.y - this.visualDirY * segmentSpacing,
              r: Math.max(3, this.size * 0.7)
          });
      } else if (desiredCount < this.segments.length) {
          this.segments.pop();
      }
    }
  }

  function init(canvas) {
    state.canvas = canvas;
    state.ctx = canvas.getContext('2d', { alpha: false }); // Optimize
    resize();
    
    // Events
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
    
    requestAnimationFrame(renderLoop);
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
    // Received snapshot
    state.currentPayload = payload;
    state.mapRadius = payload.mapRadius;
    
    // Update Camera Target (We will lerp in render)
    // Actually, payload camera is from server based on player pos.
    // We can use it directly or smooth it.
    // Let's smooth it in render.
    
    // Process Parrots
    const activeIds = new Set();
    if (payload.parrots) {
        for (const pData of payload.parrots) {
            activeIds.add(pData.i);
            if (state.parrots.has(pData.i)) {
                state.parrots.get(pData.i).updateData(pData);
            } else {
                state.parrots.set(pData.i, new Parrot(pData));
            }
        }
    }
    
    // Remove dead
    for (const [id, p] of state.parrots) {
        if (!activeIds.has(id)) {
            state.parrots.delete(id);
        }
    }
  }

  function renderLoop() {
    render();
    requestAnimationFrame(renderLoop);
  }
  
  function lerp(a, b, t) {
      return a + (b - a) * t;
  }

  function render() {
    const ctx = state.ctx;
    if (!ctx || !state.currentPayload) return;
    
    // Smooth Camera
    const targetCam = state.currentPayload.camera;
    state.camera.x = lerp(state.camera.x, targetCam.x, 0.1);
    state.camera.y = lerp(state.camera.y, targetCam.y, 0.1);
    state.camera.zoom = lerp(state.camera.zoom, targetCam.zoom, 0.1);
    
    // Update Entities Visuals
    for (const p of state.parrots.values()) {
        p.updateVisuals();
    }

    // Draw
    ctx.fillStyle = '#0f2b42';
    ctx.fillRect(0, 0, state.canvas.width, state.canvas.height);
    
    ctx.save();
    ctx.translate(state.canvas.width / 2, state.canvas.height / 2);
    ctx.scale(state.camera.zoom, state.camera.zoom);
    ctx.translate(-state.camera.x, -state.camera.y);

    // Map
    ctx.beginPath();
    ctx.arc(0, 0, state.mapRadius, 0, Math.PI * 2);
    ctx.fillStyle = '#0b1e2d';
    ctx.fill();
    
    // Thick green border
    ctx.lineWidth = 40;
    ctx.strokeStyle = '#00aa00'; // Green
    ctx.stroke();
    
    // Inner glow/edge
    ctx.lineWidth = 5;
    ctx.strokeStyle = '#00ff00';
    ctx.stroke();

    // Feathers (Batch Rendering)
    if (state.currentPayload.feathers) {
        // Group feathers by approximate hue to reduce state changes
        // Use a simple map: color -> Path2D
        const batches = new Map();
        
        for (const f of state.currentPayload.feathers) {
            let colorKey;
            if (f.h != null) {
                // Quantize hue to reduce batches (every 10 degrees)
                const qH = Math.floor(f.h / 10) * 10;
                colorKey = `hsl(${qH}, 70%, 60%)`;
            } else {
                colorKey = f.t === 0 ? '#6bd3ff' : f.t === 1 ? '#ffd166' : '#ff6e6e';
            }
            
            let path = batches.get(colorKey);
            if (!path) {
                path = new Path2D();
                batches.set(colorKey, path);
            }
            // Use rect for speed, or arc if needed. Arc is fine with Path2D batching.
            // MoveTo + Arc to avoid connecting lines
            path.moveTo(f.x + Math.max(3, f.v * 0.7), f.y);
            path.arc(f.x, f.y, Math.max(3, f.v * 0.7), 0, Math.PI * 2);
        }
        
        // Draw batches
        for (const [color, path] of batches) {
            ctx.fillStyle = color;
            ctx.fill(path);
        }
    }

    // Parrots (Optimized Snake Rendering)
    for (const p of state.parrots.values()) {
        if (p.segments.length > 0) {
            // Revert to circles because user said snake is invisible with lines.
            // Circles are robust. To optimize, we can use a single Path2D for all circles.
            
            const bodyPath = new Path2D();
            for (const s of p.segments) {
                 bodyPath.moveTo(s.x + s.r, s.y);
                 bodyPath.arc(s.x, s.y, s.r, 0, Math.PI * 2);
            }
            ctx.fillStyle = `hsl(${p.hue}, 70%, 50%)`;
            ctx.fill(bodyPath);
        }
        
        // Name
        ctx.fillStyle = 'white';
        ctx.font = '12px Arial';
        ctx.textAlign = 'center';
        ctx.fillText(p.name, p.visualX, p.visualY - p.size - 15);
    }

    ctx.restore();
  }

  window.SquawkGame = {
    init,
    getInput,
    draw
  };
})();
