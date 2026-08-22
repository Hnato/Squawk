import { useEffect, useRef, useState } from 'react';
import type { UserProfile } from '../App';
import { NetworkManager, type GameState, type RemotePlayer } from '../game/NetworkManager';
import { Renderer } from '../game/Renderer';

type GameProps = {
  user: UserProfile;
  onBack: () => void;
};

export default function Game({ user, onBack }: GameProps) {
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const networkManagerRef = useRef<NetworkManager | null>(null);
  const rendererRef = useRef<Renderer | null>(null);
  const isBoostingRef = useRef(false);
  const mousePositionRef = useRef({ x: window.innerWidth / 2, y: window.innerHeight / 2 });
  const myIdRef = useRef<string | null>(null);
  const lastSavedScoreRef = useRef<number | null>(null);

  const [score, setScore] = useState(100);
  const [leaderboard, setLeaderboard] = useState<RemotePlayer[]>([]);
  const [gameOverScore, setGameOverScore] = useState<number | null>(null);
  const [humanCount, setHumanCount] = useState(0);
  const [botCount, setBotCount] = useState(0);
  const [snakeSize, setSnakeSize] = useState(0);

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
        // If saving fails, the user still sees the final score locally.
      }
    };

    const handleStateUpdate = (state: GameState) => {
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
        .slice(0, 10);

      setLeaderboard(nextLeaderboard);
      setHumanCount(state.players.filter((player) => !player.isBot && !player.isDead).length);
      setBotCount(state.players.filter((player) => player.isBot && !player.isDead).length);
      renderer.draw(state, myIdRef.current);
    };

    const handleGameOver = (finalScore: number) => {
      setGameOverScore(finalScore);
      void persistScore(finalScore);
    };

    const networkManager = new NetworkManager({
      username: user.username,
      skinColor: user.skinColor,
      onConnected: () => {},
      onConnectionError: () => {},
      onStateUpdate: handleStateUpdate,
      onGameOver: handleGameOver,
    });

    networkManagerRef.current = networkManager;

    resize();
    window.addEventListener('resize', resize);
    void networkManager.connect();

    const inputInterval = window.setInterval(() => {
      const centerX = window.innerWidth / 2;
      const centerY = window.innerHeight / 2;
      const angle = Math.atan2(
        mousePositionRef.current.y - centerY,
        mousePositionRef.current.x - centerX,
      );

      networkManager.sendInput(angle, isBoostingRef.current);
    }, 50);

    return () => {
      window.removeEventListener('resize', resize);
      window.clearInterval(inputInterval);
      networkManager.disconnect();
    };
  }, [user.skinColor, user.username]);

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

  return (
    <main
      className="arena-page"
      onMouseMove={handleMouseMove}
      onMouseDown={handleMouseDown}
      onMouseUp={handleMouseUp}
      onContextMenu={(event) => event.preventDefault()}
    >
      <canvas ref={canvasRef} className="arena-canvas" />

      {/* Floating Top Minimal HUD */}
      <header className="hud-top-bar">
        <div className="hud-pill">
          <span className="hud-brand">Squawk</span>
          <span className="hud-user">{user.username}</span>
          <span className="hud-stat-badge">{score} pkt</span>
          <span className="hud-substat">Długość: {snakeSize}</span>
          <span className="hud-substat">Gracze: {humanCount}</span>
          <span className="hud-substat">Boty: {botCount}</span>
          <button type="button" className="hud-leave-btn" onClick={onBack}>
            Opuść grę
          </button>
        </div>
      </header>

      {/* Floating Right Leaderboard */}
      <aside className="hud-leaderboard">
        <div className="hud-leaderboard-header">Ranking na żywo</div>
        <div className="hud-leaderboard-list">
          {leaderboard.slice(0, 7).map((player, index) => (
            <div key={player.id} className={`hud-leaderboard-row ${player.id === myIdRef.current ? 'self' : ''}`}>
              <span className="hud-rank">#{index + 1}</span>
              <span className="hud-name">{player.username}{player.isBot ? ' [BOT]' : ''}</span>
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
            <span className="card-title-badge">KONIEC GRY</span>
            <h2>Wynik końcowy: {gameOverScore} pkt</h2>
            <p className="muted">
              Twój wąż został wyeliminowany. Wynik został zapisany w rankingu.
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
