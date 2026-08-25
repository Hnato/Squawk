// Web Audio procedural sound synthesizer for Squawk (no external audio assets required)

class SoundFX {
  private ctx: AudioContext | null = null;
  private muted = false;
  private boostOsc: OscillatorNode | null = null;
  private boostGain: GainNode | null = null;

  private initContext() {
    if (!this.ctx) {
      const AudioCtx =
        window.AudioContext ||
        (window as unknown as { webkitAudioContext: typeof AudioContext }).webkitAudioContext;
      if (AudioCtx) {
        this.ctx = new AudioCtx();
      }
    }
    if (this.ctx && this.ctx.state === 'suspended') {
      void this.ctx.resume();
    }
  }

  public toggleMute(): boolean {
    this.muted = !this.muted;
    if (this.muted && this.boostGain && this.ctx) {
      this.boostGain.gain.setValueAtTime(0, this.ctx.currentTime);
    }
    return this.muted;
  }

  public isMuted(): boolean {
    return this.muted;
  }

  public playEat() {
    if (this.muted) return;
    this.initContext();
    if (!this.ctx) return;

    const osc = this.ctx.createOscillator();
    const gain = this.ctx.createGain();

    const pitches = [523.25, 587.33, 659.25, 783.99, 880.0, 1046.5];
    const pitch = pitches[Math.floor(Math.random() * pitches.length)];

    osc.type = 'sine';
    osc.frequency.setValueAtTime(pitch, this.ctx.currentTime);
    osc.frequency.exponentialRampToValueAtTime(pitch * 1.5, this.ctx.currentTime + 0.08);

    gain.gain.setValueAtTime(0.12, this.ctx.currentTime);
    gain.gain.exponentialRampToValueAtTime(0.001, this.ctx.currentTime + 0.08);

    osc.connect(gain);
    gain.connect(this.ctx.destination);

    osc.start();
    osc.stop(this.ctx.currentTime + 0.09);
  }

  public playSquawk(highPitch = false) {
    if (this.muted) return;
    this.initContext();
    if (!this.ctx) return;

    const now = this.ctx.currentTime;
    const osc = this.ctx.createOscillator();
    const gain = this.ctx.createGain();

    osc.type = 'sawtooth';
    const baseFreq = highPitch ? 1200 : 750;
    osc.frequency.setValueAtTime(baseFreq, now);
    osc.frequency.linearRampToValueAtTime(baseFreq * 1.6, now + 0.06);
    osc.frequency.exponentialRampToValueAtTime(baseFreq * 0.7, now + 0.18);

    gain.gain.setValueAtTime(0.08, now);
    gain.gain.linearRampToValueAtTime(0.14, now + 0.04);
    gain.gain.exponentialRampToValueAtTime(0.001, now + 0.19);

    osc.connect(gain);
    gain.connect(this.ctx.destination);

    osc.start();
    osc.stop(now + 0.2);
  }

  public playDeath() {
    if (this.muted) return;
    this.initContext();
    if (!this.ctx) return;

    const now = this.ctx.currentTime;

    const osc = this.ctx.createOscillator();
    const gain = this.ctx.createGain();
    osc.type = 'sawtooth';
    osc.frequency.setValueAtTime(900, now);
    osc.frequency.exponentialRampToValueAtTime(180, now + 0.4);

    gain.gain.setValueAtTime(0.2, now);
    gain.gain.exponentialRampToValueAtTime(0.001, now + 0.4);

    osc.connect(gain);
    gain.connect(this.ctx.destination);
    osc.start();
    osc.stop(now + 0.42);
  }

  public updateBoost(isBoosting: boolean) {
    if (this.muted || !isBoosting) {
      if (this.boostGain && this.ctx) {
        this.boostGain.gain.setTargetAtTime(0, this.ctx.currentTime, 0.05);
      }
      return;
    }

    this.initContext();
    if (!this.ctx) return;

    if (!this.boostOsc) {
      this.boostOsc = this.ctx.createOscillator();
      this.boostGain = this.ctx.createGain();

      this.boostOsc.type = 'triangle';
      this.boostOsc.frequency.setValueAtTime(160, this.ctx.currentTime);
      this.boostGain.gain.setValueAtTime(0, this.ctx.currentTime);

      this.boostOsc.connect(this.boostGain);
      this.boostGain.connect(this.ctx.destination);
      this.boostOsc.start();
    }

    if (this.boostGain) {
      this.boostGain.gain.setTargetAtTime(0.06, this.ctx.currentTime, 0.04);
    }
  }
}

export const audioSystem = new SoundFX();
