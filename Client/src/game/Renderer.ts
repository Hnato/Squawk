import type { GameState, RemotePlayer } from './NetworkManager';

const MAP_RADIUS = 2000;

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
  ctx.quadraticCurveTo(x, y + height, x, y + height - radius);
  ctx.lineTo(x, y + radius);
  ctx.quadraticCurveTo(x, y, x + radius, y);
  ctx.closePath();
}

export class Renderer {
  private readonly canvas: HTMLCanvasElement;
  private readonly ctx: CanvasRenderingContext2D;

  constructor(canvas: HTMLCanvasElement) {
    this.canvas = canvas;
    this.ctx = canvas.getContext('2d') as CanvasRenderingContext2D;
  }

  public resize(width: number, height: number) {
    const ratio = window.devicePixelRatio || 1;
    this.canvas.width = width * ratio;
    this.canvas.height = height * ratio;
    this.canvas.style.width = `${width}px`;
    this.canvas.style.height = `${height}px`;
    this.ctx.setTransform(ratio, 0, 0, ratio, 0, 0);
  }

  public draw(state: GameState, myId: string | null) {
    const { ctx, canvas } = this;
    const width = canvas.clientWidth;
    const height = canvas.clientHeight;

    ctx.clearRect(0, 0, width, height);
    ctx.fillStyle = '#07131c';
    ctx.fillRect(0, 0, width, height);

    if (!state.players.length) {
      return;
    }

    let me = state.players.find((player) => player.id === myId && !player.isDead) ?? null;
    if (!me) {
      me = state.players.find((player) => !player.isBot && !player.isDead) ?? state.players[0];
    }

    const centerX = me?.body[0]?.x ?? 0;
    const centerY = me?.body[0]?.y ?? 0;

    ctx.save();
    ctx.translate(width / 2, height / 2);
    ctx.translate(-centerX, -centerY);

    this.drawGrid(centerX, centerY, width, height);
    this.drawMapBorder();
    this.drawFood(state);
    state.players.forEach((player) => {
      if (!player.isDead) {
        this.drawSnake(player, player.id === myId);
      }
    });

    ctx.restore();

    this.drawMinimap(state, myId);
  }

  private drawGrid(centerX: number, centerY: number, width: number, height: number) {
    const gridSize = 100;
    const startX = Math.floor((centerX - width / 2) / gridSize) * gridSize;
    const startY = Math.floor((centerY - height / 2) / gridSize) * gridSize;

    this.ctx.strokeStyle = 'rgba(255, 255, 255, 0.06)';
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
    this.ctx.beginPath();
    this.ctx.arc(0, 0, MAP_RADIUS, 0, Math.PI * 2);
    this.ctx.strokeStyle = '#22c55e';
    this.ctx.lineWidth = 16;
    this.ctx.stroke();
  }

  private drawFood(state: GameState) {
    if (!state.food || state.food.length === 0) return;

    // Potato PC optimization: Group foods by color to batch canvas draw calls
    const foodByColor: Record<string, Array<{ x: number; y: number }>> = {};
    for (let i = 0; i < state.food.length; i++) {
      const f = state.food[i];
      if (!foodByColor[f.color]) {
        foodByColor[f.color] = [];
      }
      foodByColor[f.color].push(f.position);
    }

    for (const color in foodByColor) {
      const positions = foodByColor[color];
      this.ctx.fillStyle = color;
      this.ctx.beginPath();
      for (let i = 0; i < positions.length; i++) {
        const p = positions[i];
        this.ctx.moveTo(p.x + 6, p.y);
        this.ctx.arc(p.x, p.y, 6, 0, Math.PI * 2);
      }
      this.ctx.fill();
    }
  }

  private drawSnake(player: RemotePlayer, isSelf: boolean) {
    if (!player.body || player.body.length === 0) {
      return;
    }

    const headRadius = clamp(16 + Math.sqrt(player.score) * 0.45, 16, 40);
    const bodyLength = Math.max(player.body.length, 1);
    const mainColor = shiftColor(player.skinColor, isSelf ? 10 : 0);
    const highlightColor = shiftColor(player.skinColor, 45);
    const shadowColor = shiftColor(player.skinColor, -45);
    const outlineColor = isSelf ? 'rgba(255, 255, 255, 0.9)' : 'rgba(0, 0, 0, 0.45)';

    // Full-Body Boost Nitro Energy Glow (applied across the ENTIRE length of the snake)
    if (player.isBoosting) {
      this.ctx.save();
      this.ctx.lineCap = 'round';
      this.ctx.lineJoin = 'round';

      // Layer 1: Outer glowing energy aura along the ENTIRE body path
      for (let index = player.body.length - 1; index > 0; index -= 1) {
        const p1 = player.body[index];
        const p2 = player.body[index - 1];
        const t = 1 - index / bodyLength;
        const radius = clamp(headRadius * (0.5 + t * 0.5), 9, headRadius);

        this.ctx.beginPath();
        this.ctx.moveTo(p1.x, p1.y);
        this.ctx.lineTo(p2.x, p2.y);
        this.ctx.strokeStyle = 'rgba(250, 204, 21, 0.45)';
        this.ctx.lineWidth = radius * 2.55;
        this.ctx.stroke();
      }

      // Layer 2: Bright energy core line along the ENTIRE body path
      for (let index = player.body.length - 1; index > 0; index -= 1) {
        const p1 = player.body[index];
        const p2 = player.body[index - 1];
        const t = 1 - index / bodyLength;
        const radius = clamp(headRadius * (0.5 + t * 0.5), 9, headRadius);

        this.ctx.beginPath();
        this.ctx.moveTo(p1.x, p1.y);
        this.ctx.lineTo(p2.x, p2.y);
        this.ctx.strokeStyle = 'rgba(255, 255, 255, 0.85)';
        this.ctx.lineWidth = radius * 1.15;
        this.ctx.stroke();
      }

      // Layer 3: Pulse energy sparkles along all body joints
      for (let index = player.body.length - 1; index >= 0; index -= 2) {
        const seg = player.body[index];
        const t = 1 - index / bodyLength;
        const radius = clamp(headRadius * (0.5 + t * 0.5), 9, headRadius);

        this.ctx.beginPath();
        this.ctx.arc(seg.x, seg.y, radius * 0.55, 0, Math.PI * 2);
        this.ctx.fillStyle = '#fef08a';
        this.ctx.fill();
      }

      this.ctx.restore();
    }

    // Step 1: Draw continuous underlying body shadow / outer boundary stroke
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
      this.ctx.lineWidth = radius * 2 + 4;
      this.ctx.stroke();
    }

    // Step 2: Draw continuous body fill
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

    // Step 3: Draw gradient segment discs and spine scale highlights for rich 3D look
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

      gradient.addColorStop(0, highlightColor);
      gradient.addColorStop(0.5, mainColor);
      gradient.addColorStop(1, shadowColor);

      this.ctx.beginPath();
      this.ctx.arc(segment.x, segment.y, radius, 0, Math.PI * 2);
      this.ctx.fillStyle = gradient;
      this.ctx.fill();

      // Spine pattern accent
      if (index % 3 === 0 && index !== 0) {
        this.ctx.beginPath();
        this.ctx.arc(segment.x - radius * 0.15, segment.y - radius * 0.2, radius * 0.22, 0, Math.PI * 2);
        this.ctx.fillStyle = 'rgba(255, 255, 255, 0.22)';
        this.ctx.fill();
      }
    }

    this.ctx.restore();

    // Step 4: Draw head features (eyes, snout, pupils, self indicator)
    const head = player.body[0];
    if (!head) {
      return;
    }

    this.ctx.save();
    this.ctx.translate(head.x, head.y);
    this.ctx.rotate(player.angle);

    // Snout accent / crown
    this.ctx.beginPath();
    this.ctx.moveTo(headRadius * 0.6, -headRadius * 0.6);
    this.ctx.lineTo(headRadius * 1.35, 0);
    this.ctx.lineTo(headRadius * 0.6, headRadius * 0.6);
    this.ctx.fillStyle = highlightColor;
    this.ctx.fill();

    // Eyes outer white
    const eyeOffset = headRadius * 0.55;
    const eyeRadius = Math.max(5, headRadius * 0.32);
    this.ctx.beginPath();
    this.ctx.arc(0, -eyeOffset, eyeRadius, 0, Math.PI * 2);
    this.ctx.arc(0, eyeOffset, eyeRadius, 0, Math.PI * 2);
    this.ctx.fillStyle = '#ffffff';
    this.ctx.fill();

    // Pupils black
    const pupilRadius = Math.max(2.5, eyeRadius * 0.52);
    this.ctx.beginPath();
    this.ctx.arc(eyeRadius * 0.4, -eyeOffset, pupilRadius, 0, Math.PI * 2);
    this.ctx.arc(eyeRadius * 0.4, eyeOffset, pupilRadius, 0, Math.PI * 2);
    this.ctx.fillStyle = '#0f172a';
    this.ctx.fill();

    // Pupil shine dot
    this.ctx.beginPath();
    this.ctx.arc(eyeRadius * 0.55, -eyeOffset - 1, pupilRadius * 0.4, 0, Math.PI * 2);
    this.ctx.arc(eyeRadius * 0.55, eyeOffset - 1, pupilRadius * 0.4, 0, Math.PI * 2);
    this.ctx.fillStyle = '#ffffff';
    this.ctx.fill();

    this.ctx.restore();

    // Name tag with length badge
    this.ctx.save();
    this.ctx.font = isSelf ? '800 14px Outfit' : '600 13px Outfit';
    this.ctx.textAlign = 'center';
    const tagY = head.y - headRadius - 16;

    const labelText = `${player.username} [dł: ${player.body.length}]`;
    this.ctx.fillStyle = 'rgba(4, 10, 18, 0.78)';
    const textWidth = this.ctx.measureText(labelText).width;
    drawRoundedRect(this.ctx, head.x - textWidth / 2 - 8, tagY - 14, textWidth + 16, 22, 6);
    this.ctx.fill();

    this.ctx.fillStyle = isSelf ? '#4ade80' : '#f8fafc';
    this.ctx.fillText(labelText, head.x, tagY);
    this.ctx.restore();
  }

  private drawMinimap(state: GameState, myId: string | null) {
    if (!state.players || state.players.length === 0) return;

    const size = 150;
    const padding = 24;
    const x = padding;
    const y = this.canvas.clientHeight - size - padding;
    const scale = (size / 2) / MAP_RADIUS;

    drawRoundedRect(this.ctx, x, y, size, size, 20);
    this.ctx.fillStyle = 'rgba(5, 10, 15, 0.82)';
    this.ctx.fill();
    this.ctx.strokeStyle = 'rgba(255, 255, 255, 0.14)';
    this.ctx.stroke();

    this.ctx.save();
    this.ctx.translate(x + size / 2, y + size / 2);
    
    // Outer border ring
    this.ctx.beginPath();
    this.ctx.arc(0, 0, size / 2 - 12, 0, Math.PI * 2);
    this.ctx.strokeStyle = '#22c55e';
    this.ctx.lineWidth = 2;
    this.ctx.stroke();

    // Apply circular clipping mask so no dots bleed outside the minimap ring
    this.ctx.save();
    this.ctx.beginPath();
    this.ctx.arc(0, 0, size / 2 - 13, 0, Math.PI * 2);
    this.ctx.clip();

    // Render ALL living players on minimap
    for (let i = 0; i < state.players.length; i++) {
      const p = state.players[i];
      if (p.isDead || !p.body || p.body.length === 0) continue;

      const pHead = p.body[0];
      const isMe = p.id === myId;
      const dotRadius = isMe ? 4.5 : Math.min(6, 2.5 + Math.sqrt(p.score) * 0.08);

      // Clamp position to minimap radius so edge dots stay neatly inside
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
