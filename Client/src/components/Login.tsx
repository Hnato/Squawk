import { useState } from 'react';
import type { UserProfile } from '../App';

type LoginProps = {
  initialUser: UserProfile | null;
  onAuthenticated: (user: UserProfile) => void;
};

type AuthResponse = {
  username: string;
  skinColor: string;
  skinPattern?: string;
};

export default function Login({ initialUser, onAuthenticated }: LoginProps) {
  const [username, setUsername] = useState(initialUser?.username ?? '');
  const [password, setPassword] = useState('');
  const [isRegister, setIsRegister] = useState(false);
  const [error, setError] = useState('');
  const [isSubmitting, setIsSubmitting] = useState(false);

  const handleSubmit = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();

    const trimmedUsername = username.trim();

    if (trimmedUsername.length < 3) {
      setError('Nick musi mieć przynajmniej 3 znaki.');
      return;
    }

    if (password.length < 3) {
      setError('Hasło musi mieć przynajmniej 3 znaki.');
      return;
    }

    setError('');
    setIsSubmitting(true);

    try {
      const endpoint = isRegister ? '/api/register' : '/api/login';
      const response = await fetch(endpoint, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({
          username: trimmedUsername,
          password,
        }),
      });

      if (!response.ok) {
        let message = isRegister ? 'Rejestracja nie powiodła się.' : 'Logowanie nie powiodło się.';
        try {
          const errData = (await response.json()) as { message?: string };
          if (errData?.message) {
            message = errData.message;
          }
        } catch {
          // Fallback message
        }
        throw new Error(message);
      }

      const data = (await response.json()) as AuthResponse;
      onAuthenticated({
        username: data.username,
        skinColor: data.skinColor,
        skinPattern: data.skinPattern ?? 'solid',
      });
    } catch (caughtError) {
      setError(caughtError instanceof Error ? caughtError.message : 'Nie udało się połączyć.');
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <main className="page-shell">
      <section className="auth-layout">
        <article className="hero-card auth-info">
          <h1 className="hero-title">Squawk</h1>

          <p className="lead">
            Gra sieciowa Snake w czasie rzeczywistym. Zbieraj punkty na mapie, omijaj przeszkody i wyeliminuj innych graczy.
          </p>

          <div className="feature-list">
            <div className="feature-item">
              <div>
                <strong>Tryb sieciowy</strong>
                <span>Równoległa rozgrywka na jednej mapie z graczami i botami.</span>
              </div>
            </div>
            <div className="feature-item">
              <div>
                <strong>System wyników</strong>
                <span>Zapis wyników w lokalnej bazie danych SQLite.</span>
              </div>
            </div>
            <div className="feature-item">
              <div>
                <strong>Optymalizacja</strong>
                <span>Lekki silnik graficzny dostosowany do słabszego sprzętu.</span>
              </div>
            </div>
          </div>
        </article>

        <article className="hero-card auth-form-card">
          <div className="auth-tabs">
            <button
              type="button"
              className={`tab-btn ${!isRegister ? 'active' : ''}`}
              onClick={() => {
                setIsRegister(false);
                setError('');
              }}
            >
              Logowanie
            </button>
            <button
              type="button"
              className={`tab-btn ${isRegister ? 'active' : ''}`}
              onClick={() => {
                setIsRegister(true);
                setError('');
              }}
            >
              Rejestracja
            </button>
          </div>

          <div className="form-header">
            <h2>{isRegister ? 'Rejestracja konta' : 'Logowanie do gry'}</h2>
            <p className="muted">
              {isRegister
                ? 'Podaj nick i hasło, aby utworzyć konto.'
                : 'Zaloguj się, aby kontynuować rozgrywkę.'}
            </p>
          </div>

          <form className="stack-form" onSubmit={handleSubmit}>
            <div className="input-group">
              <label htmlFor="username">Nick gracza</label>
              <input
                id="username"
                type="text"
                maxLength={18}
                placeholder="Wpisz nick"
                value={username}
                onChange={(event) => setUsername(event.target.value)}
                required
              />
            </div>

            <div className="input-group">
              <label htmlFor="password">Hasło</label>
              <input
                id="password"
                type="password"
                placeholder="Wpisz hasło"
                value={password}
                onChange={(event) => setPassword(event.target.value)}
                required
              />
            </div>

            {error ? <div className="error-badge">{error}</div> : null}

            <button type="submit" className="primary-glow-button" disabled={isSubmitting}>
              {isSubmitting
                ? 'Łączenie...'
                : isRegister
                  ? 'Zarejestruj się'
                  : 'Zaloguj się'}
            </button>
          </form>

          <footer className="footer-credits">
            <span>Project Squawk by <a href="https://github.com/Hnato" target="_blank" rel="noreferrer">Hnato</a></span>
          </footer>
        </article>
      </section>
    </main>
  );
}
