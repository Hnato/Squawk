import { useEffect, useState } from 'react';
import type { UserProfile } from '../App';
import { audioSystem } from '../game/AudioSystem';

type DashboardProps = {
  user: UserProfile;
  onLogout: () => void;
  onSavedSkin: (skinColor: string, skinPattern: string) => void;
  onStart: () => void;
};

type ScoreRow = {
  username?: string;
  score: number;
  date: string;
};

export type ParrotSpecies = {
  id: string;
  name: string;
  latin: string;
  defaultColor: string;
  accentColor: string;
  description: string;
  icon: string;
  rarity: string;
};

export const PARROT_SPECIES: ParrotSpecies[] = [
  {
    id: 'ara',
    name: 'Ara Karmazynowa',
    latin: 'Ara macao',
    defaultColor: '#ef4444',
    accentColor: '#3b82f6',
    description: 'Królewska szkarłatna czerwień z lazurowymi skrzydłami i zakrzywionym dziobem.',
    icon: '🦜',
    rarity: 'Legendarne',
  },
  {
    id: 'ararauna',
    name: 'Ara Ararauna',
    latin: 'Ara ararauna',
    defaultColor: '#38bdf8',
    accentColor: '#eab308',
    description: 'Głęboki błękit oceanu połączony ze słonecznym, złotym brzuchem.',
    icon: '🪶',
    rarity: 'Egzotyczne',
  },
  {
    id: 'nimfa',
    name: 'Nimfa z Czubkiem',
    latin: 'Nymphicus hollandicus',
    defaultColor: '#94a3b8',
    accentColor: '#facc15',
    description: 'Srebrzysto-szare pióra, charakterystyczny wysoki żółty czubek i rumiane policzki.',
    icon: '👑',
    rarity: 'Klasyczne',
  },
  {
    id: 'kakadu',
    name: 'Kakadu Żółtoczuba',
    latin: 'Cacatua galerita',
    defaultColor: '#f8fafc',
    accentColor: '#fde047',
    description: 'Śnieżnobiałe upierzenie z okazałym, jaskrawożółtym irokezem.',
    icon: '⚡',
    rarity: 'Mistrzowskie',
  },
  {
    id: 'zako',
    name: 'Żako Afrykańskie',
    latin: 'Psittacus erithacus',
    defaultColor: '#475569',
    accentColor: '#dc2626',
    description: 'Inteligentne popielate ciało zakończone intensywnie rubinowym wachlarzem ogona.',
    icon: '💎',
    rarity: 'Rzadkie',
  },
  {
    id: 'falista',
    name: 'Papużka Falista',
    latin: 'Melopsittacus undulatus',
    defaultColor: '#22c55e',
    accentColor: '#06b6d4',
    description: 'Żywa seledynowa zieleń z prążkowanymi falami na skrzydłach.',
    icon: '🌿',
    rarity: 'Zwinne',
  },
  {
    id: 'lorysa',
    name: 'Tęczowa Lorysa',
    latin: 'Trichoglossus moluccanus',
    defaultColor: '#a855f7',
    accentColor: '#f43f5e',
    description: 'Prawdziwa feeria barw – fiolety, turkusy, czerwień i złoto w płynnym gradiencie.',
    icon: '🌈',
    rarity: 'Mityczne',
  },
  {
    id: 'sloneczna',
    name: 'Papuga Słoneczna',
    latin: 'Aratinga solstitialis',
    defaultColor: '#f97316',
    accentColor: '#eab308',
    description: 'Gorący gradient słońca tropików – od mandarynkowego pomarańczu po płomienną żółć.',
    icon: '☀️',
    rarity: 'Ogniste',
  },
  {
    id: 'amazonka',
    name: 'Amazonka Dżunglowa',
    latin: 'Amazona aestiva',
    defaultColor: '#10b981',
    accentColor: '#ef4444',
    description: 'Głęboka zieleń lasu deszczowego z rubinowymi i żółtymi akcentami skrzydeł.',
    icon: '🌴',
    rarity: 'Tropikalne',
  },
  {
    id: 'cyber',
    name: 'Cyber Papuga (Neon)',
    latin: 'Psittacus cyberneticus',
    defaultColor: '#06b6d4',
    accentColor: '#ec4899',
    description: 'Obsydianowy cyber-pancerz z neonowo-błękitnym pióropuszem i laserowym okiem.',
    icon: '🔮',
    rarity: 'Cyberpunk',
  },
];

export default function Dashboard({
  user,
  onLogout,
  onSavedSkin,
  onStart,
}: DashboardProps) {
  const [pattern, setPattern] = useState(user.skinPattern || 'ara');
  const [color, setColor] = useState(user.skinColor || '#ef4444');
  const [topScores, setTopScores] = useState<ScoreRow[]>([]);
  const [personalScores, setPersonalScores] = useState<ScoreRow[]>([]);
  const [isSavingSkin, setIsSavingSkin] = useState(false);
  const [saveMessage, setSaveMessage] = useState('');

  const currentSpecies = PARROT_SPECIES.find((s) => s.id === pattern) || PARROT_SPECIES[0];

  useEffect(() => {
    setColor(user.skinColor || '#ef4444');
    setPattern(user.skinPattern || 'ara');
  }, [user.skinColor, user.skinPattern]);

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

  const handleSelectSpecies = (sp: ParrotSpecies) => {
    setPattern(sp.id);
    setColor(sp.defaultColor);
    audioSystem.playSquawk(sp.id === 'nimfa' || sp.id === 'falista');
  };

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
          pattern,
        }),
      });

      if (!response.ok) {
        throw new Error('Nie udało się zapisać wyglądu.');
      }

      onSavedSkin(color, pattern);
      setSaveMessage('Wygląd wężo-papugi został zapisany!');
      audioSystem.playSquawk(false);
    } catch (caughtError) {
      setSaveMessage(
        caughtError instanceof Error ? caughtError.message : 'Błąd zapisu wyglądu.',
      );
    } finally {
      setIsSavingSkin(false);
    }
  };

  return (
    <main className="page-shell">
      <section className="dashboard-shell">
        <article className="panel-card dashboard-hero">
          <div className="dashboard-hero-copy">
            <span className="card-title-badge">ARENA SQUAWK</span>
            <h1 className="hero-title">
              Witaj, <span className="text-gradient">{user.username}</span>!
            </h1>

            <p className="lead">
              Wybierz swój unikalny gatunek <strong>Wężo-Papugi</strong>, dostosuj barwy piór i wzbij się do walki o przetrwanie na arenie.
            </p>

            <div className="button-row cta-row">
              <button
                type="button"
                className="primary-glow-button big-cta"
                onClick={() => {
                  audioSystem.playSquawk(false);
                  onStart();
                }}
              >
                🦜 Dołącz do Areny
              </button>
              <button type="button" className="secondary-button" onClick={onLogout}>
                Wyloguj się
              </button>
            </div>
          </div>

          <div className="panel-card setup-card">
            <div className="card-title-badge">GATUNEK: {currentSpecies.rarity.toUpperCase()}</div>
            <h2>{currentSpecies.name}</h2>
            <p className="species-latin"><em>{currentSpecies.latin}</em></p>
            <p className="species-desc">{currentSpecies.description}</p>

            {/* Live Parrot Snake Preview */}
            <div className="parrot-preview-box">
              <div className="parrot-preview-spine">
                {/* Tail fan */}
                <div
                  className="parrot-tail-fan"
                  style={{
                    backgroundColor: pattern === 'zako' ? '#ef4444' : currentSpecies.accentColor,
                  }}
                />

                {/* Body Segments */}
                <div className="parrot-seg s4" style={{ backgroundColor: color }} />
                <div className="parrot-seg s3" style={{ backgroundColor: color }} />
                
                {/* Wing Segment */}
                <div className="parrot-seg s2 wing-host" style={{ backgroundColor: color }}>
                  <div
                    className="parrot-wing left"
                    style={{ backgroundColor: currentSpecies.accentColor }}
                  />
                  <div
                    className="parrot-wing right"
                    style={{ backgroundColor: currentSpecies.accentColor }}
                  />
                </div>

                <div className="parrot-seg s1" style={{ backgroundColor: color }} />

                {/* Head */}
                <div className="parrot-head" style={{ backgroundColor: color }}>
                  {/* Crest / Czubek */}
                  <div
                    className={`parrot-crest ${pattern === 'kakadu' || pattern === 'nimfa' ? 'cockatoo' : ''}`}
                    style={{
                      backgroundColor:
                        pattern === 'kakadu' || pattern === 'nimfa' ? '#facc15' : currentSpecies.accentColor,
                    }}
                  />
                  {/* Beak / Dziób */}
                  <div
                    className="parrot-beak"
                    style={{
                      backgroundColor:
                        pattern === 'ara' ? '#fef08a' : pattern === 'zako' || pattern === 'kakadu' ? '#1e293b' : '#fbbf24',
                    }}
                  />
                  {/* Eyes */}
                  <div className="parrot-eye top"><div className="parrot-pupil" /></div>
                  <div className="parrot-eye bottom"><div className="parrot-pupil" /></div>
                  {/* Cheeks */}
                  {pattern === 'nimfa' ? <div className="parrot-cheek top" /> : null}
                  {pattern === 'nimfa' ? <div className="parrot-cheek bottom" /> : null}
                </div>
              </div>
            </div>

            <div className="color-row">
              <div className="color-preview" style={{ backgroundColor: color }} />
              <div className="grow">
                <label htmlFor="dashboard-color">Dostosuj odcień piór</label>
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
              {isSavingSkin ? 'Zapisywanie...' : 'Zapisz ten model'}
            </button>
          </div>
        </article>

        {/* Species Selection Grid */}
        <section className="species-selection-section">
          <div className="section-header">
            <span className="card-title-badge">KOLEKCJA MODELI</span>
            <h2>Gatunki Wężo-Papug ({PARROT_SPECIES.length})</h2>
            <p className="muted">Wybierz model i styl dla swojej postaci w grze:</p>
          </div>

          <div className="species-grid">
            {PARROT_SPECIES.map((sp) => {
              const isSelected = pattern === sp.id;
              return (
                <div
                  key={sp.id}
                  className={`species-card ${isSelected ? 'selected' : ''}`}
                  onClick={() => handleSelectSpecies(sp)}
                >
                  <div className="species-card-header">
                    <span className="species-icon">{sp.icon}</span>
                    <span className="species-rarity-pill">{sp.rarity}</span>
                  </div>
                  <h3 className="species-card-title">{sp.name}</h3>
                  <p className="species-card-latin">{sp.latin}</p>
                  <p className="species-card-desc">{sp.description}</p>
                  <div className="species-swatches">
                    <span className="swatch-dot" style={{ backgroundColor: sp.defaultColor }} />
                    <span className="swatch-dot" style={{ backgroundColor: sp.accentColor }} />
                    {isSelected ? <span className="selected-tag">WYBRANY</span> : null}
                  </div>
                </div>
              );
            })}
          </div>
        </section>

        {/* Rules and Leaderboard */}
        <section className="dashboard-grid">
          <article className="panel-card">
            <div className="card-title-badge">NOWOŚCI I STEROWANIE</div>
            <h2>Zasady & Tryb Mobilny</h2>
            <div className="hint-grid">
              <div className="hint-card">
                <div>
                  <strong>🎮 PC: Myszka / Spacja</strong>
                  <span>Kierunek myszką, spacja lub prawy przycisk myszy to Turbo Boost.</span>
                </div>
              </div>
              <div className="hint-card">
                <div>
                  <strong>📱 Smartfon / Dotyk</strong>
                  <span>Dotknij ekranu, by sterować wirtualnym joystickiem + przycisk TURBO.</span>
                </div>
              </div>
              <div className="hint-card">
                <div>
                  <strong>🪶 Żywe Cząsteczki & Dźwięki</strong>
                  <span>Machanie skrzydłami, zrzucanie piórek przy locie i syntetyzowane skrzeki.</span>
                </div>
              </div>
              <div className="hint-card">
                <div>
                  <strong>⚡ Wydajność i Płynność</strong>
                  <span>Zoptymalizowany transfer danych, brak zacinania się botów na krawędzi.</span>
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
            <h2>Twoje Ostatnie Loty</h2>
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
