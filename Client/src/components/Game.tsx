import { useEffect, useRef, useState } from 'react';
import type { UserProfile } from '../App';
import { NetworkManager, type GameState, type RemotePlayer } from '../game/NetworkManager';
import { Renderer } from '../game/Renderer';
import { audioSystem } from '../game/AudioSystem';

type GameProps = {
  user: UserProfile;
  onBack: () => void;
};

function clamp(value: number, min: number, max: number) {
  return Math.min(max, Math.max(min, value));
}

export default function Game({ user, onBack }: GameProps) {
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const networkManagerRef = useRef<NetworkManager | null>(null);
  const rendererRef = useRef<Renderer | null>(null);
  const isBoostingRef = useRef(false);
  const mousePositionRef = useRef({ x: window.innerWidth / 2, y: window.innerHeight / 2 });
  const myIdRef = useRef<string | null>(null);
  const lastSavedScoreRef = useRef<number | null>(null);
  const latestStateRef = useRef<GameState | null>(null);

  // Touch controls
  const [touchJoystick, setTouchJoystick] = useState<{
    active: boolean;
    startX: number;
    startY: number;
    currX: number;
    currY: number;
  } | null>(null);
  const touchJoystickRef = useRef<{
    active: boolean;
    startX: number;
    startY: number;
    currX: number;
    currY: number;
  } | null>(null);
  const joystickTouchIdRef = useRef<number | null>(null);

  // Orientation state
  const [isPortrait, setIsPortrait] = useState(
    typeof window !== 'undefined' && window.innerHeight > window.innerWidth && window.innerWidth < 850,
  );

  const [score, setScore] = useState(100);
  const [leaderboard, setLeaderboard] = useState<RemotePlayer[]>([]);
  const [gameOverScore, setGameOverScore] = useState<number | null>(null);
  const [humanCount, setHumanCount] = useState(0);
  const [botCount, setBotCount] = useState(0);
  const [snakeSize, setSnakeSize] = useState(0);
  const [isMuted, setIsMuted] = useState(audioSystem.isMuted());
  const [showMobileLeaderboard, setShowMobileLeaderboard] = useState(false);

  // Check orientation changes
  useEffect(() => {
    const checkOrientation = () => {
      const portrait = window.innerHeight > window.innerWidth && window.innerWidth < 850;
      setIsPortrait(portrait);
    };

    window.addEventListener('resize', checkOrientation);
    window.addEventListener('orientationchange', checkOrientation);

    return () => {
      window.removeEventListener('resize', checkOrientation);
      window.removeEventListener('orientationchange', checkOrientation);
    };
  }, []);

  // Keyboard controls
  useEffect(() => {
    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.code === 'Space') {
        event.preventDefault();
        isBoostingRef.current = true;
      }
    };

    const handleKeyUp = (event: KeyboardEvent) => {
      if (event.code === 'Space') {
        isBoostingRef.current = false;
      }
    };

    window.addEventListener('keydown', handleKeyDown);
    window.addEventListener('keyup', handleKeyUp);

    return () => {
      window.removeEventListener('keydown', handleKeyDown);
      window.removeEventListener('keyup', handleKeyUp);
    };
  }, []);

  // Main game rendering loop & Network setup
  useEffect(() => {
    const canvas = canvasRef.current;
    if (!canvas) {
      return;
    }

    const renderer = new Renderer(canvas);
    rendererRef.current = renderer;

    const resize = () => {
      renderer.resize(window.innerWidth, window.innerHeight);
    };

    const persistScore = async (nextScore: number) => {
      if (lastSavedScoreRef.current === nextScore) {
        return;
      }

      lastSavedScoreRef.current = nextScore;

      try {
        await fetch('/api/scores', {
          method: 'POST',
          headers: {
            'Content-Type': 'application/json',
          },
          body: JSON.stringify({
            username: user.username,
            score: nextScore,
          }),
        });
      } catch {
        // Fallback
      }
    };

    const handleStateUpdate = (state: GameState) => {
      latestStateRef.current = state;
    };

    const handleGameOver = (finalScore: number) => {
      setGameOverScore(finalScore);
      audioSystem.playDeath();
      if (latestStateRef.current && myIdRef.current) {
        const me = latestStateRef.current.players.find((p) => p.id === myIdRef.current);
        if (me && me.body[0]) {
          renderer.spawnFeatherBurst(me.body[0].x, me.body[0].y, me.skinColor, 30);
        }
      }
      void persistScore(finalScore);
    };

    const networkManager = new NetworkManager({
      username: user.username,
      skinColor: user.skinColor,
      skinPattern: user.skinPattern || 'ara',
      onConnected: () => {
        audioSystem.playSquawk(false);
      },
      onConnectionError: () => {},
      onStateUpdate: handleStateUpdate,
      onGameOver: handleGameOver,
    });

    networkManagerRef.current = networkManager;

    resize();
    window.addEventListener('resize', resize);
    void networkManager.connect();

    // 60+ FPS Rendering loop via requestAnimationFrame
    let animationFrameId: number;
    const renderLoop = () => {
      if (latestStateRef.current) {
        renderer.draw(latestStateRef.current, myIdRef.current);
      }
      animationFrameId = requestAnimationFrame(renderLoop);
    };
    animationFrameId = requestAnimationFrame(renderLoop);

    // Native Touch Event Listeners directly on canvas with passive: false to prevent browser gesture capture
    const handleNativeTouchStart = (e: TouchEvent) => {
      e.preventDefault();
      for (let i = 0; i < e.changedTouches.length; i++) {
        const touch = e.changedTouches[i];
        // Target is not the turbo button
        if (touch.clientX < window.innerWidth * 0.8 || touch.clientY < window.innerHeight * 0.7) {
          joystickTouchIdRef.current = touch.identifier;
          const jData = {
            active: true,
            startX: touch.clientX,
            startY: touch.clientY,
            currX: touch.clientX,
            currY: touch.clientY,
          };
          touchJoystickRef.current = jData;
          setTouchJoystick(jData);
          mousePositionRef.current = { x: touch.clientX, y: touch.clientY };
        }
      }
    };

    const handleNativeTouchMove = (e: TouchEvent) => {
      e.preventDefault();
      for (let i = 0; i < e.changedTouches.length; i++) {
        const touch = e.changedTouches[i];
        if (touch.identifier === joystickTouchIdRef.current && touchJoystickRef.current) {
          const jData = {
            ...touchJoystickRef.current,
            currX: touch.clientX,
            currY: touch.clientY,
          };
          touchJoystickRef.current = jData;
          setTouchJoystick(jData);
          mousePositionRef.current = { x: touch.clientX, y: touch.clientY };
        }
      }
    };

    const handleNativeTouchEnd = (e: TouchEvent) => {
      e.preventDefault();
      for (let i = 0; i < e.changedTouches.length; i++) {
        const touch = e.changedTouches[i];
        if (touch.identifier === joystickTouchIdRef.current) {
          joystickTouchIdRef.current = null;
          touchJoystickRef.current = null;
          setTouchJoystick(null);
        }
      }
    };

    canvas.addEventListener('touchstart', handleNativeTouchStart, { passive: false });
    canvas.addEventListener('touchmove', handleNativeTouchMove, { passive: false });
    canvas.addEventListener('touchend', handleNativeTouchEnd, { passive: false });
    canvas.addEventListener('touchcancel', handleNativeTouchEnd, { passive: false });

    // Network input stream (every 35ms)
    const inputInterval = window.setInterval(() => {
      let angle: number;

      if (touchJoystickRef.current && touchJoystickRef.current.active) {
        const dx = touchJoystickRef.current.currX - touchJoystickRef.current.startX;
        const dy = touchJoystickRef.current.currY - touchJoystickRef.current.startY;
        if (Math.hypot(dx, dy) > 6) {
          angle = Math.atan2(dy, dx);
        } else {
          const centerX = window.innerWidth / 2;
          const centerY = window.innerHeight / 2;
          angle = Math.atan2(mousePositionRef.current.y - centerY, mousePositionRef.current.x - centerX);
        }
      } else {
        const centerX = window.innerWidth / 2;
        const centerY = window.innerHeight / 2;
        angle = Math.atan2(mousePositionRef.current.y - centerY, mousePositionRef.current.x - centerX);
      }

      networkManager.sendInput(angle, isBoostingRef.current);
    }, 35);

    // Throttled HUD update (every 180ms)
    const hudInterval = window.setInterval(() => {
      const state = latestStateRef.current;
      if (!state) return;

      const me =
        state.players.find((player) => player.id === myIdRef.current) ??
        state.players.find((player) => player.username === user.username && !player.isBot);

      if (me) {
        myIdRef.current = me.id;
        setScore(me.score);
        setSnakeSize(me.body.length);
      }

      const nextLeaderboard = [...state.players]
        .filter((player) => !player.isDead)
        .sort((left, right) => right.score - left.score)
        .slice(0, 8);

      setLeaderboard(nextLeaderboard);
      setHumanCount(state.players.filter((player) => !player.isBot && !player.isDead).length);
      setBotCount(state.players.filter((player) => player.isBot && !player.isDead).length);
    }, 180);

    return () => {
      window.removeEventListener('resize', resize);
      window.cancelAnimationFrame(animationFrameId);
      window.clearInterval(inputInterval);
      window.clearInterval(hudInterval);
      canvas.removeEventListener('touchstart', handleNativeTouchStart);
      canvas.removeEventListener('touchmove', handleNativeTouchMove);
      canvas.removeEventListener('touchend', handleNativeTouchEnd);
      canvas.removeEventListener('touchcancel', handleNativeTouchEnd);
      networkManager.disconnect();
    };
  }, [user.skinColor, user.skinPattern, user.username]);

  // Mouse handlers for PC
  const handleMouseMove = (event: React.MouseEvent<HTMLDivElement>) => {
    mousePositionRef.current = { x: event.clientX, y: event.clientY };
  };

  const handleMouseDown = (event: React.MouseEvent<HTMLDivElement>) => {
    if (event.button === 2) {
      isBoostingRef.current = true;
    }
  };

  const handleMouseUp = (event: React.MouseEvent<HTMLDivElement>) => {
    if (event.button === 2) {
      isBoostingRef.current = false;
    }
  };

  const toggleSound = () => {
    const nextMuted = audioSystem.toggleMute();
    setIsMuted(nextMuted);
  };

  const requestLandscapeAndFullscreen = async () => {
    try {
      if (!document.fullscreenElement) {
        await document.documentElement.requestFullscreen();
      }
      const orientation = screen.orientation as ScreenOrientation & {
        lock?: (orientation: string) => Promise<void>;
      };
      if (orientation && orientation.lock) {
        await orientation.lock('landscape');
      }
    } catch {
      // Browser might restrict automatic lock
    }
  };

  return (
    <main
      className="arena-page"
      onMouseMove={handleMouseMove}
      onMouseDown={handleMouseDown}
      onMouseUp={handleMouseUp}
      onContextMenu={(event) => event.preventDefault()}
    >
      <canvas ref={canvasRef} className="arena-canvas" />

      {/* Force Landscape Orientation Overlay on Mobile */}
      {isPortrait ? (
        <div className="portrait-rotate-overlay">
          <div className="rotate-device-card">
            <div className="phone-rotate-animation">
              <span className="phone-icon">📱</span>
              <span className="rotate-arrow">🔄</span>
            </div>
            <h2>Obróć telefon poziomo</h2>
            <p className="muted">
              Squawk wymaga orientacji panoramicznej (poziomej) dla pełnego pola widzenia i wygody sterowania.
            </p>
            <div className="button-row center-row">
              <button
                type="button"
                className="primary-glow-button"
                onClick={requestLandscapeAndFullscreen}
              >
                ⛶ Pełny Ekran & Obróć
              </button>
            </div>
          </div>
        </div>
      ) : null}

      {/* Virtual Touch Joystick indicator */}
      {touchJoystick && touchJoystick.active ? (
        <div
          className="virtual-joystick-base"
          style={{
            left: touchJoystick.startX - 45,
            top: touchJoystick.startY - 45,
          }}
        >
          <div
            className="virtual-joystick-thumb"
            style={{
              transform: `translate(${clamp(touchJoystick.currX - touchJoystick.startX, -35, 35)}px, ${clamp(
                touchJoystick.currY - touchJoystick.startY,
                -35,
                35,
              )}px)`,
            }}
          />
        </div>
      ) : null}

      {/* Mobile Turbo Boost Button */}
      <div className="mobile-touch-controls">
        <button
          type="button"
          className={`mobile-boost-btn ${isBoostingRef.current ? 'active' : ''}`}
          onTouchStart={(e) => {
            e.preventDefault();
            e.stopPropagation();
            isBoostingRef.current = true;
          }}
          onTouchEnd={(e) => {
            e.preventDefault();
            e.stopPropagation();
            isBoostingRef.current = false;
          }}
          onTouchCancel={(e) => {
            e.preventDefault();
            isBoostingRef.current = false;
          }}
          onMouseDown={() => {
            isBoostingRef.current = true;
          }}
          onMouseUp={() => {
            isBoostingRef.current = false;
          }}
        >
          <span className="boost-icon">⚡</span>
          <span className="boost-label">TURBO</span>
        </button>
      </div>

      {/* Floating Top Minimal HUD */}
      <header className="hud-top-bar">
        <div className="hud-pill">
          <span className="hud-brand">🦜 Squawk</span>
          <span className="hud-user">{user.username}</span>
          <span className="hud-stat-badge">{score} pkt</span>
          <span className="hud-substat hide-on-tiny">Dł: {snakeSize}</span>
          <span className="hud-substat hide-on-tiny">Gracze: {humanCount}</span>
          <span className="hud-substat hide-on-tiny">Boty: {botCount}</span>

          <button
            type="button"
            className="hud-icon-btn"
            title={isMuted ? 'Włącz dźwięk' : 'Wycisz dźwięk'}
            onClick={toggleSound}
          >
            {isMuted ? '🔇' : '🔊'}
          </button>

          <button
            type="button"
            className="hud-icon-btn hide-on-desktop"
            title="Pokaż ranking"
            onClick={() => setShowMobileLeaderboard((prev) => !prev)}
          >
            🏆
          </button>

          <button
            type="button"
            className="hud-icon-btn hide-on-desktop"
            title="Pełny ekran"
            onClick={requestLandscapeAndFullscreen}
          >
            ⛶
          </button>

          <button type="button" className="hud-leave-btn" onClick={onBack}>
            Wyjdź
          </button>
        </div>
      </header>

      {/* Floating Right Leaderboard */}
      <aside className={`hud-leaderboard ${showMobileLeaderboard ? 'mobile-visible' : ''}`}>
        <div className="hud-leaderboard-header">
          <span>Ranking na żywo</span>
          {showMobileLeaderboard ? (
            <button
              type="button"
              className="hud-close-btn"
              onClick={() => setShowMobileLeaderboard(false)}
            >
              ✕
            </button>
          ) : null}
        </div>
        <div className="hud-leaderboard-list">
          {leaderboard.slice(0, 7).map((player, index) => (
            <div
              key={player.id}
              className={`hud-leaderboard-row ${player.id === myIdRef.current ? 'self' : ''}`}
            >
              <span className="hud-rank">#{index + 1}</span>
              <span className="hud-name">
                {player.username}
                {player.isBot ? ' 🤖' : ''}
              </span>
              <span className="hud-score">
                {player.score} <small className="hud-length">({player.body ? player.body.length : 0} seg)</small>
              </span>
            </div>
          ))}
        </div>
      </aside>

      {gameOverScore !== null ? (
        <div className="game-overlay">
          <div className="panel-card overlay-card">
            <span className="card-title-badge">KONIEC LOTU</span>
            <h2>Wynik końcowy: {gameOverScore} pkt</h2>
            <p className="muted">
              Twój wąż papugowy został wyeliminowany. Wynik został zarejestrowany.
            </p>
            <div className="button-row center-row">
              <button type="button" className="primary-glow-button" onClick={onBack}>
                Wróć do menu
              </button>
            </div>
          </div>
        </div>
      ) : null}
    </main>
  );
}
