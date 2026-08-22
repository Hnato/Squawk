import { useEffect, useState } from 'react';
import Dashboard from './components/Dashboard';
import Game from './components/Game';
import Login from './components/Login';
import './index.css';

type View = 'login' | 'dashboard' | 'game';

export type UserProfile = {
  username: string;
  skinColor: string;
  skinPattern: string;
};

const USER_STORAGE_KEY = 'squawk-online-user';

function loadUser(): UserProfile | null {
  const savedUser = window.localStorage.getItem(USER_STORAGE_KEY);

  if (!savedUser) {
    return null;
  }

  try {
    return JSON.parse(savedUser) as UserProfile;
  } catch {
    return null;
  }
}

export default function App() {
  const [user, setUser] = useState<UserProfile | null>(() => loadUser());
  const [view, setView] = useState<View>(() => (loadUser() ? 'dashboard' : 'login'));

  useEffect(() => {
    if (user) {
      window.localStorage.setItem(USER_STORAGE_KEY, JSON.stringify(user));
      return;
    }

    window.localStorage.removeItem(USER_STORAGE_KEY);
  }, [user]);

  const handleAuthenticated = (nextUser: UserProfile) => {
    setUser(nextUser);
    setView('dashboard');
  };

  const handleLogout = () => {
    setUser(null);
    setView('login');
  };

  const handleSkinSaved = (skinColor: string) => {
    setUser((currentUser) =>
      currentUser
        ? {
            ...currentUser,
            skinColor,
          }
        : currentUser,
    );
  };

  if (!user || view === 'login') {
    return <Login initialUser={user} onAuthenticated={handleAuthenticated} />;
  }

  if (view === 'game') {
    return (
      <Game
        user={user}
        onBack={() => setView('dashboard')}
      />
    );
  }

  return (
    <Dashboard
      user={user}
      onLogout={handleLogout}
      onSavedSkin={handleSkinSaved}
      onStart={() => setView('game')}
    />
  );
}
