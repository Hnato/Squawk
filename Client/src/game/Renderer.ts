import type { GameState, RemotePlayer } from './NetworkManager';
import { audioSystem } from './AudioSystem';

const MAP_RADIUS = 2000;

export type Particle = {
  x: number;
  y: number;
  vx: number;
  vy: number;
  size: number;
  color: string;
  alpha: number;
  life: number;
  maxLife: number;
  rotation: number;
  vRot: number;
  isFeather?: boolean;
};

function clamp(value: number, min: number, max: number) {
  return Math.min(max, Math.max(min, value));
}

function hexToRgb(hexColor: string) {
  const normalized = hexColor.replace('#', '');
  if (normalized.length !== 6) {
    return { r: 34, g: 197, b: 94 };
  }
  return {
    r: parseInt(normalized.slice(0, 2), 16),
    g: parseInt(normalized.slice(2, 4), 16),
    b: parseInt(normalized.slice(4, 6), 16),
  };
}

function shiftColor(hexColor: string, amount: number) {
  const rgb = hexToRgb(hexColor);
  return `rgb(${clamp(rgb.r + amount, 0, 255)}, ${clamp(rgb.g + amount, 0, 255)}, ${clamp(
    rgb.b + amount,
    0,
    255,
  )})`;
}

function drawRoundedRect(
  ctx: CanvasRenderingContext2D,
  x: number,
  y: number,
  width: number,
  height: number,
  radius: number,
) {
  ctx.beginPath();
  ctx.moveTo(x + radius, y);
  ctx.lineTo(x + width - radius, y);
  ctx.quadraticCurveTo(x + width, y, x + width, y + radius);
  ctx.lineTo(x + width, y + height - radius);
  ctx.quadraticCurveTo(x + width, y + height, x + width - radius, y + height);
  ctx.lineTo(x + radius, y + height);
  ctx.quadraticCurveTo(x, y, x + radius, y + height - radius);
  ctx.lineTo(x, y + radius);
  ctx.quadraticCurveTo(x, y, x + radius, y);
  ctx.closePath();
}

export class Renderer {
  private readonly canvas: HTMLCanvasElement;
  private readonly ctx: CanvasRenderingContext2D;
  private particles: Particle[] = [];
  private ambientParticles: Particle[] = [];
  private prevFoodCount = 0;
  private animTime = 0;
  private lastSelfScore = 100;

  constructor(canvas: HTMLCanvasElement) {
    this.canvas = canvas;
    this.ctx = canvas.getContext('2d') as CanvasRenderingContext2D;
    this.initAmbientParticles();
  }

  private initAmbientParticles() {
    this.ambientParticles = [];
    for (let i = 0; i < 45; i++) {
      this.ambientParticles.push({
        x: (Math.random() - 0.5) * MAP_RADIUS * 2,
        y: (Math.random() - 0.5) * MAP_RADIUS * 2,
        vx: (Math.random() - 0.5) * 0.4,
        vy: (Math.random() - 0.5) * 0.4,
        size: Math.random() * 3 + 1.5,
        color: ['#4ade80', '#38bdf8', '#facc15', '#f472b6', '#a78bfa'][Math.floor(Math.random() * 5)],
        alpha: Math.random() * 0.35 + 0.1,
        life: 1,
        maxLife: 1,
        rotation: Math.random() * Math.PI * 2,
        vRot: (Math.random() - 0.5) * 0.02,
        isFeather: Math.random() > 0.5,
      });
    }
  }

  public resize(width: number, height: number) {
    const ratio = window.devicePixelRatio || 1;
    this.canvas.width = width * ratio;
    this.canvas.height = height * ratio;
    this.canvas.style.width = `${width}px`;
    this.canvas.style.height = `${height}px`;
    this.ctx.setTransform(ratio, 0, 0, ratio, 0, 0);
  }

  public spawnFeatherBurst(x: number, y: number, color: string, count = 18) {
    for (let i = 0; i < count; i++) {
      const angle = Math.random() * Math.PI * 2;
      const speed = Math.random() * 6 + 2;
      this.particles.push({
        x,
        y,
        vx: Math.cos(angle) * speed,
        vy: Math.sin(angle) * speed,
        size: Math.random() * 7 + 4,
        color,
        alpha: 1,
        life: 0,
        maxLife: Math.random() * 30 + 25,
        rotation: Math.random() * Math.PI * 2,
        vRot: (Math.random() - 0.5) * 0.15,
        isFeather: true,
      });
    }
  }

  public spawnEatPop(x: number, y: number, color: string) {
    for (let i = 0; i < 6; i++) {
      const angle = Math.random() * Math.PI * 2;
      const speed = Math.random() * 3 + 1;
      this.particles.push({
        x,
        y,
        vx: Math.cos(angle) * speed,
        vy: Math.sin(angle) * speed,
        size: Math.random() * 3.5 + 2,
        color,
        alpha: 0.9,
        life: 0,
        maxLife: 15,
        rotation: 0,
        vRot: 0,
        isFeather: false,
      });
    }
  }

  public draw(state: GameState, myId: string | null) {
    const { ctx, canvas } = this;
    const width = canvas.clientWidth;
    const height = canvas.clientHeight;
    this.animTime += 0.05;

    ctx.clearRect(0, 0, width, height);
    ctx.fillStyle = '#060f17';
    ctx.fillRect(0, 0, width, height);

    if (!state.players.length) {
      return;
    }

    let me = state.players.find((player) => player.id === myId && !player.isDead) ?? null;
    if (!me) {
      me = state.players.find((player) => !player.isBot && !player.isDead) ?? state.players[0];
    }

    if (me) {
      if (me.score > this.lastSelfScore + 8) {
        audioSystem.playEat();
        if (me.body[0]) {
          this.spawnEatPop(me.body[0].x, me.body[0].y, '#fef08a');
        }
      }
      this.lastSelfScore = me.score;
      audioSystem.updateBoost(me.isBoosting);
    } else {
      audioSystem.updateBoost(false);
    }

    // Trigger eat pop when food count drops noticeably
    if (state.food && this.prevFoodCount > 0 && state.food.length < this.prevFoodCount) {
      // Food consumed
    }
    this.prevFoodCount = state.food ? state.food.length : 0;

    const centerX = me?.body[0]?.x ?? 0;
    const centerY = me?.body[0]?.y ?? 0;

    ctx.save();
    ctx.translate(width / 2, height / 2);
    ctx.translate(-centerX, -centerY);

    this.drawGrid(centerX, centerY, width, height);
    this.drawMapBorder();
    this.drawAmbientSpores();
    this.drawFood(state);
    this.drawParticles();

    state.players.forEach((player) => {
      if (!player.isDead) {
        this.drawParrotSnake(player, player.id === myId);
      }
    });

    ctx.restore();

    this.drawMinimap(state, myId);
  }

  private drawGrid(centerX: number, centerY: number, width: number, height: number) {
    const gridSize = 120;
    const startX = Math.floor((centerX - width / 2) / gridSize) * gridSize;
    const startY = Math.floor((centerY - height / 2) / gridSize) * gridSize;

    this.ctx.strokeStyle = 'rgba(56, 189, 248, 0.05)';
    this.ctx.lineWidth = 1;
    this.ctx.beginPath();

    for (let x = startX; x < centerX + width / 2; x += gridSize) {
      this.ctx.moveTo(x, centerY - height / 2);
      this.ctx.lineTo(x, centerY + height / 2);
    }

    for (let y = startY; y < centerY + height / 2; y += gridSize) {
      this.ctx.moveTo(centerX - width / 2, y);
      this.ctx.lineTo(centerX + width / 2, y);
    }

    this.ctx.stroke();
  }

  private drawMapBorder() {
    // Pulsing energy barrier
    const pulse = Math.sin(this.animTime * 2) * 4;
    this.ctx.save();
    this.ctx.beginPath();
    this.ctx.arc(0, 0, MAP_RADIUS, 0, Math.PI * 2);
    this.ctx.strokeStyle = 'rgba(34, 197, 94, 0.2)';
    this.ctx.lineWidth = 28 + pulse;
    this.ctx.stroke();

    this.ctx.beginPath();
    this.ctx.arc(0, 0, MAP_RADIUS, 0, Math.PI * 2);
    this.ctx.strokeStyle = '#22c55e';
    this.ctx.lineWidth = 8;
    this.ctx.stroke();

    this.ctx.beginPath();
    this.ctx.arc(0, 0, MAP_RADIUS, 0, Math.PI * 2);
    this.ctx.strokeStyle = '#86efac';
    this.ctx.lineWidth = 2;
    this.ctx.stroke();
    this.ctx.restore();
  }

  private drawAmbientSpores() {
    this.ctx.save();
    for (const p of this.ambientParticles) {
      p.x += p.vx;
      p.y += p.vy;
      p.rotation += p.vRot;

      if (Math.abs(p.x) > MAP_RADIUS) p.vx *= -1;
      if (Math.abs(p.y) > MAP_RADIUS) p.vy *= -1;

      this.ctx.save();
      this.ctx.translate(p.x, p.y);
      this.ctx.rotate(p.rotation);
      this.ctx.fillStyle = p.color;
      this.ctx.globalAlpha = p.alpha;

      if (p.isFeather) {
        // Feather shape
        this.ctx.beginPath();
        this.ctx.ellipse(0, 0, p.size * 2, p.size * 0.7, 0, 0, Math.PI * 2);
        this.ctx.fill();
      } else {
        this.ctx.beginPath();
        this.ctx.arc(0, 0, p.size, 0, Math.PI * 2);
        this.ctx.fill();
      }
      this.ctx.restore();
    }
    this.ctx.restore();
  }

  private drawParticles() {
    if (this.particles.length === 0) return;

    this.ctx.save();
    for (let i = this.particles.length - 1; i >= 0; i--) {
      const p = this.particles[i];
      p.x += p.vx;
      p.y += p.vy;
      p.vx *= 0.94;
      p.vy *= 0.94;
      p.rotation += p.vRot;
      p.life++;

      const progress = p.life / p.maxLife;
      const alpha = (1 - progress) * p.alpha;

      if (progress >= 1) {
        this.particles.splice(i, 1);
        continue;
      }

      this.ctx.save();
      this.ctx.translate(p.x, p.y);
      this.ctx.rotate(p.rotation);
      this.ctx.globalAlpha = alpha;
      this.ctx.fillStyle = p.color;

      if (p.isFeather) {
        this.ctx.beginPath();
        this.ctx.ellipse(0, 0, p.size * 2.2, p.size * 0.75, 0, 0, Math.PI * 2);
        this.ctx.fill();
      } else {
        this.ctx.beginPath();
        this.ctx.arc(0, 0, p.size * (1 - progress * 0.5), 0, Math.PI * 2);
        this.ctx.fill();
      }
      this.ctx.restore();
    }
    this.ctx.restore();
  }

  private drawFood(state: GameState) {
    if (!state.food || state.food.length === 0) return;

    const time = this.animTime;
    this.ctx.save();

    for (let i = 0; i < state.food.length; i++) {
      const f = state.food[i];
      const p = f.position;
      const pulse = Math.sin(time * 3 + f.id * 0.2) * 1.2;
      const baseRadius = f.value >= 20 ? 8.5 : 5.5;
      const r = baseRadius + pulse;

      // Glow aura
      this.ctx.beginPath();
      this.ctx.arc(p.x, p.y, r + 4, 0, Math.PI * 2);
      this.ctx.fillStyle = f.color;
      this.ctx.globalAlpha = 0.22;
      this.ctx.fill();

      // Fruit / Berry Body
      this.ctx.beginPath();
      this.ctx.arc(p.x, p.y, r, 0, Math.PI * 2);
      this.ctx.fillStyle = f.color;
      this.ctx.globalAlpha = 0.95;
      this.ctx.fill();

      // Berry seed highlight dot
      this.ctx.beginPath();
      this.ctx.arc(p.x - r * 0.3, p.y - r * 0.35, r * 0.35, 0, Math.PI * 2);
      this.ctx.fillStyle = '#ffffff';
      this.ctx.globalAlpha = 0.75;
      this.ctx.fill();
    }

    this.ctx.restore();
  }

  private drawParrotSnake(player: RemotePlayer, isSelf: boolean) {
    if (!player.body || player.body.length === 0) {
      return;
    }

    const pattern = (player.skinPattern || 'ara').toLowerCase();
    const headRadius = clamp(16 + Math.sqrt(player.score) * 0.45, 16, 42);
    const bodyLength = Math.max(player.body.length, 1);
    const mainColor = shiftColor(player.skinColor, isSelf ? 8 : 0);
    const highlightColor = shiftColor(player.skinColor, 40);
    const shadowColor = shiftColor(player.skinColor, -40);
    const outlineColor = isSelf ? 'rgba(255, 255, 255, 0.95)' : 'rgba(0, 0, 0, 0.55)';

    // Spawn boost flutter particles
    if (player.isBoosting && Math.random() < 0.4 && player.body.length > 2) {
      const tail = player.body[player.body.length - 1];
      this.particles.push({
        x: tail.x + (Math.random() - 0.5) * 14,
        y: tail.y + (Math.random() - 0.5) * 14,
        vx: -Math.cos(player.angle) * 3 + (Math.random() - 0.5) * 2,
        vy: -Math.sin(player.angle) * 3 + (Math.random() - 0.5) * 2,
        size: Math.random() * 5 + 3,
        color: player.skinColor,
        alpha: 0.85,
        life: 0,
        maxLife: 22,
        rotation: Math.random() * Math.PI * 2,
        vRot: (Math.random() - 0.5) * 0.1,
        isFeather: true,
      });
    }

    // Nitro Glow when boosting
    if (player.isBoosting) {
      this.ctx.save();
      this.ctx.lineCap = 'round';
      this.ctx.lineJoin = 'round';

      for (let index = player.body.length - 1; index > 0; index -= 1) {
        const p1 = player.body[index];
        const p2 = player.body[index - 1];
        const t = 1 - index / bodyLength;
        const radius = clamp(headRadius * (0.5 + t * 0.5), 9, headRadius);

        this.ctx.beginPath();
        this.ctx.moveTo(p1.x, p1.y);
        this.ctx.lineTo(p2.x, p2.y);
        this.ctx.strokeStyle = pattern === 'cyber' ? 'rgba(236, 72, 153, 0.5)' : 'rgba(250, 204, 21, 0.45)';
        this.ctx.lineWidth = radius * 2.6;
        this.ctx.stroke();
      }
      this.ctx.restore();
    }

    // Step 1: Continuous outline
    this.ctx.save();
    this.ctx.lineCap = 'round';
    this.ctx.lineJoin = 'round';

    for (let index = player.body.length - 1; index > 0; index -= 1) {
      const p1 = player.body[index];
      const p2 = player.body[index - 1];
      const t = 1 - index / bodyLength;
      const radius = clamp(headRadius * (0.5 + t * 0.5), 9, headRadius);

      this.ctx.beginPath();
      this.ctx.moveTo(p1.x, p1.y);
      this.ctx.lineTo(p2.x, p2.y);
      this.ctx.strokeStyle = outlineColor;
      this.ctx.lineWidth = radius * 2 + 5;
      this.ctx.stroke();
    }

    // Step 2: Continuous body fill
    for (let index = player.body.length - 1; index > 0; index -= 1) {
      const p1 = player.body[index];
      const p2 = player.body[index - 1];
      const t = 1 - index / bodyLength;
      const radius = clamp(headRadius * (0.5 + t * 0.5), 9, headRadius);

      this.ctx.beginPath();
      this.ctx.moveTo(p1.x, p1.y);
      this.ctx.lineTo(p2.x, p2.y);
      this.ctx.strokeStyle = mainColor;
      this.ctx.lineWidth = radius * 2;
      this.ctx.stroke();
    }

    // Step 3: Draw segmented plumage discs and species-specific feather textures
    for (let index = player.body.length - 1; index >= 0; index -= 1) {
      const segment = player.body[index];
      const t = 1 - index / bodyLength;
      const radius = clamp(headRadius * (0.5 + t * 0.5), 9, headRadius);

      const gradient = this.ctx.createRadialGradient(
        segment.x - radius * 0.25,
        segment.y - radius * 0.35,
        radius * 0.15,
        segment.x,
        segment.y,
        radius,
      );

      // Unique species coloring
      if (pattern === 'ararauna') {
        gradient.addColorStop(0, '#38bdf8');
        gradient.addColorStop(0.5, '#0284c7');
        gradient.addColorStop(1, '#eab308');
      } else if (pattern === 'lorysa') {
        const hue = (index * 24 + this.animTime * 30) % 360;
        gradient.addColorStop(0, `hsl(${hue}, 90%, 65%)`);
        gradient.addColorStop(1, `hsl(${hue + 40}, 85%, 45%)`);
      } else if (pattern === 'zako') {
        const isTail = index > player.body.length - 4;
        if (isTail) {
          gradient.addColorStop(0, '#ef4444');
          gradient.addColorStop(1, '#b91c1c');
        } else {
          gradient.addColorStop(0, '#cbd5e1');
          gradient.addColorStop(0.5, '#64748b');
          gradient.addColorStop(1, '#334155');
        }
      } else if (pattern === 'kakadu') {
        gradient.addColorStop(0, '#ffffff');
        gradient.addColorStop(0.7, '#f1f5f9');
        gradient.addColorStop(1, '#cbd5e1');
      } else if (pattern === 'sloneczna') {
        gradient.addColorStop(0, '#fde047');
        gradient.addColorStop(0.5, '#f97316');
        gradient.addColorStop(1, '#ef4444');
      } else if (pattern === 'cyber') {
        gradient.addColorStop(0, '#22d3ee');
        gradient.addColorStop(0.5, '#0f172a');
        gradient.addColorStop(1, '#ec4899');
      } else {
        gradient.addColorStop(0, highlightColor);
        gradient.addColorStop(0.5, mainColor);
        gradient.addColorStop(1, shadowColor);
      }

      this.ctx.beginPath();
      this.ctx.arc(segment.x, segment.y, radius, 0, Math.PI * 2);
      this.ctx.fillStyle = gradient;
      this.ctx.fill();

      // Scalloped feather pattern
      if (index % 2 === 0 && index !== 0) {
        this.ctx.beginPath();
        this.ctx.arc(segment.x - radius * 0.15, segment.y - radius * 0.15, radius * 0.45, 0, Math.PI);
        this.ctx.strokeStyle = pattern === 'cyber' ? 'rgba(34, 211, 238, 0.4)' : 'rgba(255, 255, 255, 0.28)';
        this.ctx.lineWidth = 1.5;
        this.ctx.stroke();
      }
    }

    // Step 4: Flapping Parrot Wings on front segments (segments 2 to 4)
    if (player.body.length >= 4) {
      const wingSeg = player.body[2];
      const wingNext = player.body[1];
      const wingAngle = Math.atan2(wingNext.y - wingSeg.y, wingNext.x - wingSeg.x);
      const flapSpeed = player.isBoosting ? 26 : 14;
      const flapAngle = Math.sin(this.animTime * flapSpeed) * 0.45;
      const wingSpan = headRadius * 2.2;

      this.ctx.save();
      this.ctx.translate(wingSeg.x, wingSeg.y);
      this.ctx.rotate(wingAngle);

      // Left Wing
      this.ctx.save();
      this.ctx.rotate(-Math.PI / 2 + flapAngle);
      this.drawWingFeathers(wingSpan, pattern, highlightColor);
      this.ctx.restore();

      // Right Wing
      this.ctx.save();
      this.ctx.rotate(Math.PI / 2 - flapAngle);
      this.drawWingFeathers(wingSpan, pattern, highlightColor);
      this.ctx.restore();

      this.ctx.restore();
    }

    this.ctx.restore();

    // Step 5: Draw Parrot Head (Beak, Crest, Eyes, Cheeks)
    const head = player.body[0];
    if (!head) return;

    this.ctx.save();
    this.ctx.translate(head.x, head.y);
    this.ctx.rotate(player.angle);

    // Parrot Crest / Crown (Pióropusz / Czubek)
    this.drawParrotCrest(headRadius, pattern, highlightColor);

    // Curved Parrot Beak (Dziób Papugi)
    this.drawParrotBeak(headRadius, pattern);

    // Cheeks (e.g. Cockatiel orange spots, Macaw blush)
    if (pattern === 'nimfa') {
      this.ctx.beginPath();
      this.ctx.arc(-headRadius * 0.15, -headRadius * 0.65, headRadius * 0.32, 0, Math.PI * 2);
      this.ctx.arc(-headRadius * 0.15, headRadius * 0.65, headRadius * 0.32, 0, Math.PI * 2);
      this.ctx.fillStyle = '#f97316';
      this.ctx.fill();
    }

    // Eyes
    const eyeOffset = headRadius * 0.52;
    const eyeRadius = Math.max(5.5, headRadius * 0.32);

    // Outer eye white ring
    this.ctx.beginPath();
    this.ctx.arc(headRadius * 0.1, -eyeOffset, eyeRadius, 0, Math.PI * 2);
    this.ctx.arc(headRadius * 0.1, eyeOffset, eyeRadius, 0, Math.PI * 2);
    this.ctx.fillStyle = pattern === 'cyber' ? '#0f172a' : '#ffffff';
    this.ctx.fill();
    this.ctx.strokeStyle = pattern === 'cyber' ? '#22d3ee' : '#334155';
    this.ctx.lineWidth = 1.2;
    this.ctx.stroke();

    // Pupils
    const pupilRadius = Math.max(3, eyeRadius * 0.52);
    this.ctx.beginPath();
    this.ctx.arc(headRadius * 0.1 + eyeRadius * 0.35, -eyeOffset, pupilRadius, 0, Math.PI * 2);
    this.ctx.arc(headRadius * 0.1 + eyeRadius * 0.35, eyeOffset, pupilRadius, 0, Math.PI * 2);
    this.ctx.fillStyle = pattern === 'cyber' ? '#22d3ee' : '#090d16';
    this.ctx.fill();

    // Pupil catchlight shine
    this.ctx.beginPath();
    this.ctx.arc(headRadius * 0.1 + eyeRadius * 0.5, -eyeOffset - 1, pupilRadius * 0.45, 0, Math.PI * 2);
    this.ctx.arc(headRadius * 0.1 + eyeRadius * 0.5, eyeOffset - 1, pupilRadius * 0.45, 0, Math.PI * 2);
    this.ctx.fillStyle = '#ffffff';
    this.ctx.fill();

    this.ctx.restore();

    // Name tag with length & species badge
    this.ctx.save();
    this.ctx.font = isSelf ? '800 13px Outfit' : '600 12px Outfit';
    this.ctx.textAlign = 'center';
    const tagY = head.y - headRadius - 18;

    const labelText = `${player.username} [${player.body.length}]`;
    this.ctx.fillStyle = 'rgba(4, 10, 18, 0.82)';
    const textWidth = this.ctx.measureText(labelText).width;
    drawRoundedRect(this.ctx, head.x - textWidth / 2 - 8, tagY - 14, textWidth + 16, 22, 6);
    this.ctx.fill();
    this.ctx.strokeStyle = isSelf ? 'rgba(74, 222, 128, 0.6)' : 'rgba(255, 255, 255, 0.15)';
    this.ctx.stroke();

    this.ctx.fillStyle = isSelf ? '#4ade80' : '#f8fafc';
    this.ctx.fillText(labelText, head.x, tagY);
    this.ctx.restore();
  }

  private drawWingFeathers(wingSpan: number, pattern: string, highlightColor: string) {
    this.ctx.beginPath();
    this.ctx.moveTo(0, 0);
    this.ctx.quadraticCurveTo(wingSpan * 0.6, -wingSpan * 0.3, wingSpan, -wingSpan * 0.1);
    this.ctx.quadraticCurveTo(wingSpan * 0.7, wingSpan * 0.4, 0, wingSpan * 0.2);
    this.ctx.closePath();

    if (pattern === 'ara') {
      this.ctx.fillStyle = '#3b82f6';
    } else if (pattern === 'ararauna') {
      this.ctx.fillStyle = '#0284c7';
    } else if (pattern === 'kakadu') {
      this.ctx.fillStyle = '#fef08a';
    } else if (pattern === 'cyber') {
      this.ctx.fillStyle = '#ec4899';
    } else {
      this.ctx.fillStyle = highlightColor;
    }
    this.ctx.fill();

    // Primary flight feather quill lines
    this.ctx.beginPath();
    this.ctx.moveTo(wingSpan * 0.3, 0);
    this.ctx.lineTo(wingSpan * 0.95, -wingSpan * 0.08);
    this.ctx.strokeStyle = 'rgba(255, 255, 255, 0.45)';
    this.ctx.lineWidth = 2;
    this.ctx.stroke();
  }

  private drawParrotCrest(headRadius: number, pattern: string, highlightColor: string) {
    this.ctx.save();
    if (pattern === 'kakadu' || pattern === 'nimfa') {
      // Big yellow jaunty Cockatoo / Cockatiel crest
      const crestHeight = headRadius * 1.5;
      this.ctx.beginPath();
      this.ctx.moveTo(-headRadius * 0.2, -headRadius * 0.3);
      this.ctx.quadraticCurveTo(-headRadius * 0.8, -crestHeight, -headRadius * 1.3, -crestHeight * 0.9);
      this.ctx.quadraticCurveTo(-headRadius * 0.6, -headRadius * 0.6, 0, -headRadius * 0.4);
      this.ctx.fillStyle = '#facc15';
      this.ctx.fill();
    } else if (pattern === 'cyber') {
      // Neon techno crest
      this.ctx.beginPath();
      this.ctx.moveTo(-headRadius * 0.4, 0);
      this.ctx.lineTo(-headRadius * 1.2, -headRadius * 0.8);
      this.ctx.lineTo(-headRadius * 0.6, 0);
      this.ctx.lineTo(-headRadius * 1.2, headRadius * 0.8);
      this.ctx.fillStyle = '#22d3ee';
      this.ctx.fill();
    } else {
      // Crown feather tuft
      this.ctx.beginPath();
      this.ctx.moveTo(-headRadius * 0.3, -headRadius * 0.4);
      this.ctx.lineTo(-headRadius * 0.9, -headRadius * 0.2);
      this.ctx.lineTo(-headRadius * 0.4, 0);
      this.ctx.lineTo(-headRadius * 0.9, headRadius * 0.2);
      this.ctx.lineTo(-headRadius * 0.3, headRadius * 0.4);
      this.ctx.fillStyle = highlightColor;
      this.ctx.fill();
    }
    this.ctx.restore();
  }

  private drawParrotBeak(headRadius: number, pattern: string) {
    this.ctx.save();
    // Hooked parrot beak
    const beakLength = headRadius * 1.45;
    const beakHeight = headRadius * 0.75;

    this.ctx.beginPath();
    this.ctx.moveTo(headRadius * 0.5, -beakHeight * 0.6);
    this.ctx.quadraticCurveTo(headRadius + beakLength * 0.5, -beakHeight * 0.5, headRadius + beakLength, beakHeight * 0.2);
    this.ctx.quadraticCurveTo(headRadius + beakLength * 0.3, beakHeight * 0.9, headRadius * 0.5, beakHeight * 0.6);
    this.ctx.closePath();

    if (pattern === 'ara') {
      this.ctx.fillStyle = '#fef08a';
    } else if (pattern === 'zako' || pattern === 'kakadu') {
      this.ctx.fillStyle = '#1e293b';
    } else if (pattern === 'lorysa') {
      this.ctx.fillStyle = '#ef4444';
    } else if (pattern === 'cyber') {
      this.ctx.fillStyle = '#22d3ee';
    } else {
      this.ctx.fillStyle = '#fbbf24';
    }
    this.ctx.fill();
    this.ctx.strokeStyle = 'rgba(0,0,0,0.35)';
    this.ctx.lineWidth = 1.2;
    this.ctx.stroke();

    // Hook tip accent
    this.ctx.beginPath();
    this.ctx.arc(headRadius + beakLength * 0.9, beakHeight * 0.15, 2.5, 0, Math.PI * 2);
    this.ctx.fillStyle = '#0f172a';
    this.ctx.fill();

    this.ctx.restore();
  }

  private drawMinimap(state: GameState, myId: string | null) {
    if (!state.players || state.players.length === 0) return;

    const size = Math.min(150, Math.max(100, this.canvas.clientWidth * 0.22));
    const padding = 16;
    const x = padding;
    const y = this.canvas.clientHeight - size - padding;
    const scale = (size / 2) / MAP_RADIUS;

    drawRoundedRect(this.ctx, x, y, size, size, 16);
    this.ctx.fillStyle = 'rgba(5, 10, 15, 0.85)';
    this.ctx.fill();
    this.ctx.strokeStyle = 'rgba(255, 255, 255, 0.14)';
    this.ctx.stroke();

    this.ctx.save();
    this.ctx.translate(x + size / 2, y + size / 2);
    
    // Outer border ring
    this.ctx.beginPath();
    this.ctx.arc(0, 0, size / 2 - 10, 0, Math.PI * 2);
    this.ctx.strokeStyle = '#22c55e';
    this.ctx.lineWidth = 2;
    this.ctx.stroke();

    this.ctx.save();
    this.ctx.beginPath();
    this.ctx.arc(0, 0, size / 2 - 11, 0, Math.PI * 2);
    this.ctx.clip();

    // Render players on minimap
    for (let i = 0; i < state.players.length; i++) {
      const p = state.players[i];
      if (p.isDead || !p.body || p.body.length === 0) continue;

      const pHead = p.body[0];
      const isMe = p.id === myId;
      const dotRadius = isMe ? 4.5 : Math.min(5.5, 2.5 + Math.sqrt(p.score) * 0.08);

      const distFromCenter = Math.hypot(pHead.x, pHead.y);
      const maxDist = MAP_RADIUS * 0.95;
      const clampedDist = Math.min(distFromCenter, maxDist);
      const angle = Math.atan2(pHead.y, pHead.x);
      const drawX = Math.cos(angle) * clampedDist * scale;
      const drawY = Math.sin(angle) * clampedDist * scale;

      this.ctx.beginPath();
      this.ctx.arc(drawX, drawY, dotRadius, 0, Math.PI * 2);
      this.ctx.fillStyle = isMe ? '#4ade80' : (p.skinColor || '#ef4444');
      this.ctx.fill();

      if (isMe) {
        this.ctx.strokeStyle = '#ffffff';
        this.ctx.lineWidth = 1.5;
        this.ctx.stroke();
      }
    }

    this.ctx.restore();
    this.ctx.restore();
  }
}
