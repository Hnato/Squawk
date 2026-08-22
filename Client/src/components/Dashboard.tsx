import { useEffect, useState } from 'react';
import type { UserProfile } from '../App';

type DashboardProps = {
  user: UserProfile;
  onLogout: () => void;
  onSavedSkin: (skinColor: string) => void;
  onStart: () => void;
};

type ScoreRow = {
  username?: string;
  score: number;
  date: string;
};

export default function Dashboard({
  user,
  onLogout,
  onSavedSkin,
  onStart,
}: DashboardProps) {
  const [color, setColor] = useState(user.skinColor);
  const [topScores, setTopScores] = useState<ScoreRow[]>([]);
  const [personalScores, setPersonalScores] = useState<ScoreRow[]>([]);
  const [isSavingSkin, setIsSavingSkin] = useState(false);
  const [saveMessage, setSaveMessage] = useState('');

  useEffect(() => {
    setColor(user.skinColor);
  }, [user.skinColor]);

  useEffect(() => {
    const loadScores = async () => {
      try {
        const [topResponse, personalResponse] = await Promise.all([
          fetch('/api/scores/top24h'),
          fetch(`/api/scores/user/${encodeURIComponent(user.username)}`),
        ]);

        if (!topResponse.ok || !personalResponse.ok) {
          throw new Error('Nie udało się pobrać wyników.');
        }

        setTopScores((await topResponse.json()) as ScoreRow[]);
        setPersonalScores((await personalResponse.json()) as ScoreRow[]);
      } catch {
        setTopScores([]);
        setPersonalScores([]);
      }
    };

    void loadScores();
  }, [user.username]);

  const handleSaveSkin = async () => {
    setIsSavingSkin(true);
    setSaveMessage('');

    try {
      const response = await fetch('/api/saveskin', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({
          username: user.username,
          color,
          pattern: user.skinPattern,
        }),
      });

      if (!response.ok) {
        throw new Error('Nie udało się zapisać koloru.');
      }

      onSavedSkin(color);
      setSaveMessage('Kolor został zapisany.');
    } catch (caughtError) {
      setSaveMessage(
        caughtError instanceof Error ? caughtError.message : 'Błąd zapisu koloru.',
      );
    } finally {
      setIsSavingSkin(false);
    }
  };

  const PRESET_COLORS = [
    '#22c55e', // Emerald
    '#ef4444', // Ruby
    '#3b82f6', // Neon Blue
    '#eab308', // Amber Gold
    '#a855f7', // Cyber Purple
    '#ec4899', // Pink Flame
    '#06b6d4', // Cyan
    '#f97316', // Orange Fire
  ];

  return (
    <main className="page-shell">
      <section className="dashboard-shell">
        <article className="panel-card dashboard-hero">
          <div className="dashboard-hero-copy">
            <h1 className="hero-title">
              <span className="text-gradient">{user.username}</span>
            </h1>

            <p className="lead">
              Wybierz kolor węża i dołącz do rozgrywki na arenie.
            </p>

            <div className="button-row cta-row">
              <button type="button" className="primary-glow-button big-cta" onClick={onStart}>
                Dołącz do Areny
              </button>
              <button type="button" className="secondary-button" onClick={onLogout}>
                Wyloguj się
              </button>
            </div>
          </div>

          <div className="panel-card setup-card">
            <div className="card-title-badge">KONTROWANIE WYGLĄDU</div>
            <h2>Wgląd węża</h2>

            <div className="snake-preview-container">
              <div className="snake-preview-spine">
                <div className="preview-head" style={{ backgroundColor: color }}>
                  <div className="preview-eye left" />
                  <div className="preview-eye right" />
                </div>
                <div className="preview-segment s1" style={{ backgroundColor: color }} />
                <div className="preview-segment s2" style={{ backgroundColor: color }} />
                <div className="preview-segment s3" style={{ backgroundColor: color }} />
                <div className="preview-segment s4" style={{ backgroundColor: color }} />
              </div>
            </div>

            <div className="color-palette">
              {PRESET_COLORS.map((preset) => (
                <button
                  key={preset}
                  type="button"
                  className={`color-swatch ${color === preset ? 'selected' : ''}`}
                  style={{ backgroundColor: preset }}
                  onClick={() => setColor(preset)}
                />
              ))}
            </div>

            <div className="color-row">
              <div className="color-preview" style={{ backgroundColor: color }} />
              <div className="grow">
                <label htmlFor="dashboard-color">Własny kolor</label>
                <input
                  id="dashboard-color"
                  type="color"
                  value={color}
                  onChange={(event) => setColor(event.target.value)}
                />
              </div>
            </div>

            {saveMessage ? <p className="status-msg">{saveMessage}</p> : null}

            <button type="button" className="secondary-button full-width" onClick={handleSaveSkin}>
              {isSavingSkin ? 'Zapisywanie...' : 'Zapisz kolor'}
            </button>
          </div>
        </article>

        <section className="dashboard-grid">
          <article className="panel-card">
            <div className="card-title-badge">STEROWANIE</div>
            <h2>Zasady gry</h2>
            <div className="hint-grid">
              <div className="hint-card">
                <div>
                  <strong>Myszka</strong>
                  <span>Wskaźnik myszy wyznacza kierunek ruchu głowy.</span>
                </div>
              </div>
              <div className="hint-card">
                <div>
                  <strong>Spacja / Prawy Przycisk</strong>
                  <span>Włącza chwilowe przyspieszenie kosztem punktów.</span>
                </div>
              </div>
              <div className="hint-card">
                <div>
                  <strong>Pokarm</strong>
                  <span>Zbieranie kulek zwiększa wynik i rozmiar węża.</span>
                </div>
              </div>
              <div className="hint-card">
                <div>
                  <strong>Kolizje</strong>
                  <span>Wjechanie w inny wąż eliminuje gracza z mapy.</span>
                </div>
              </div>
            </div>
          </article>

          <article className="panel-card">
            <div className="card-title-badge">RANKING 24H</div>
            <h2>Najlepsze wyniki</h2>
            <div className="score-list">
              {topScores.length === 0 ? (
                <p className="muted">Brak zarejestrowanych wyników.</p>
              ) : (
                topScores.map((entry, index) => (
                  <div key={`${entry.username}-${entry.date}-${entry.score}`} className="score-row">
                    <span className="rank-name">
                      <span className={`rank-badge rank-${index + 1}`}>{index + 1}</span>
                      {entry.username}
                    </span>
                    <strong className="score-val">{entry.score} pkt</strong>
                  </div>
                ))
              )}
            </div>
          </article>

          <article className="panel-card panel-span-2">
            <div className="card-title-badge">HISTORIA</div>
            <h2>Twoje Ostatnie Wyniki</h2>
            <div className="score-list horizontal-scroll">
              {personalScores.length === 0 ? (
                <p className="muted">Brak wcześniejszych gier.</p>
              ) : (
                personalScores.map((entry) => (
                  <div key={`${entry.date}-${entry.score}`} className="score-card">
                    <span className="score-date">{new Date(entry.date).toLocaleString()}</span>
                    <strong className="score-big">{entry.score} pkt</strong>
                  </div>
                ))
              )}
            </div>
          </article>
        </section>

        <footer className="footer-credits">
          <span>Squawk by <a href="https://github.com/Hnato" target="_blank" rel="noreferrer" className="author-link">Hnato</a></span>
        </footer>
      </section>
    </main>
  );
}
