const canvas = document.getElementById('game-canvas');
const ctx = canvas.getContext('2d');
const minimapCanvas = document.getElementById('minimap');
const minimapCtx = minimapCanvas.getContext('2d');

let socket;
let playerId;
let players = {};
let foodItems = [];
let leaderboard = [];
let records24h = [];
let mapRadius = 1950;
let isAuthorized = false;
let mousePos = { x: 0, y: 0 };
let camera = { x: 1950, y: 1950 };
let minimapVisible = true;
let lastUpdateTime = 0;
let particles = [];

// Config for visual overhaul
const COLORS = {
    background: '#0a0a0b',
    grid: 'rgba(0, 255, 136, 0.05)',
    border: '#00ff88',
    text: '#ffffff'
};

let authView;
let activeSocketGeneration = 0;
let serverConfigPromise;

function getMonitor() {
    return document.getElementById('monitor');
}

function showAuthError(message) {
    if (!authView) return;
    authView.errorMsg.textContent = message;
}

function clearAuthError() {
    if (!authView) return;
    authView.errorMsg.textContent = '';
}

function setMonitorState(message, color = '#00ff88') {
    const monitor = getMonitor();
    monitor.textContent = message;
    monitor.style.color = color;
}

async function getServerConfig() {
    if (!serverConfigPromise) {
        serverConfigPromise = fetch('/config.json', { cache: 'no-store' })
            .then(async response => {
                if (!response.ok) {
                    throw new Error(`Config HTTP ${response.status}`);
                }

                const config = await response.json();
                if (!config.wsPort) {
                    throw new Error('Brak portu WebSocket w konfiguracji');
                }

                return config;
            })
            .catch(error => {
                console.warn('Nie udało się pobrać konfiguracji serwera, używam wartości domyślnych.', error);
                return { wsPort: 5006, httpPort: 5007 };
            });
    }

    return serverConfigPromise;
}

function resize() {
    canvas.width = window.innerWidth;
    canvas.height = window.innerHeight;
}

window.addEventListener('resize', resize);
resize();

// --- Input Handling ---
window.addEventListener('mousemove', (e) => {
    mousePos.x = e.clientX;
    mousePos.y = e.clientY;
});

window.addEventListener('keydown', (e) => {
    if (e.key === 'Tab') {
        e.preventDefault();
        minimapVisible = !minimapVisible;
        document.getElementById('minimap-container').style.display = minimapVisible ? 'block' : 'none';
    }
});

// --- Auth & Session ---
function setupUI() {
    const authModal = document.getElementById('auth-modal');
    const deathModal = document.getElementById('death-modal');
    const authBtn = document.getElementById('auth-btn');
    const switchLink = document.getElementById('switch-link');
    const playAgainBtn = document.getElementById('play-again-btn');
    const logoutBtn = document.getElementById('logout-btn');
    const usernameInput = document.getElementById('username');
    const passwordInput = document.getElementById('password');
    const errorMsg = document.getElementById('error-msg');
    
    authView = {
        authModal,
        deathModal,
        authBtn,
        switchLink,
        usernameInput,
        passwordInput,
        errorMsg,
        isRegisterMode: false,
        isPending: false
    };

    const savedUsername = localStorage.getItem('squawk_username');
    if (savedUsername) usernameInput.value = savedUsername;

    function updateAuthUi() {
        const actionLabel = authView.isRegisterMode ? 'Zarejestruj się' : 'Zaloguj się';
        document.getElementById('modal-title').textContent = authView.isRegisterMode ? 'Rejestracja' : 'Logowanie';
        authBtn.textContent = authView.isPending
            ? (authView.isRegisterMode ? 'Rejestrowanie...' : 'Logowanie...')
            : actionLabel;
        authBtn.disabled = authView.isPending;
        usernameInput.disabled = authView.isPending;
        passwordInput.disabled = authView.isPending;
        switchLink.textContent = authView.isRegisterMode ? 'Zaloguj się' : 'Zarejestruj się';
        document.getElementById('switch-text').textContent = authView.isRegisterMode ? 'Masz już konto?' : 'Nie masz konta?';
    }

    function setAuthPending(isPending) {
        authView.isPending = isPending;
        updateAuthUi();
    }

    function validateInputs(username, password) {
        if (!username || username.trim().length < 3) {
            showAuthError('Nazwa użytkownika musi mieć co najmniej 3 znaki.');
            return false;
        }

        if (username.length > 24) {
            showAuthError('Nazwa użytkownika może mieć maksymalnie 24 znaki.');
            return false;
        }

        if (!password || password.length < 4) {
            showAuthError('Hasło musi mieć co najmniej 4 znaki.');
            return false;
        }

        if (password.length > 64) {
            showAuthError('Hasło może mieć maksymalnie 64 znaki.');
            return false;
        }

        return true;
    }

    async function submitAuth() {
        if (authView.isPending) return;

        clearAuthError();
        const user = usernameInput.value.trim();
        const pass = passwordInput.value;

        if (!validateInputs(user, pass)) {
            return;
        }

        localStorage.setItem('squawk_username', user);
        setAuthPending(true);

        try {
            await connectToServer(user, pass, authView.isRegisterMode);
        } catch (error) {
            console.error('Auth connect error', error);
            showAuthError('Nie udało się połączyć z serwerem uwierzytelniania.');
            setAuthPending(false);
        }
    }

    switchLink.addEventListener('click', (e) => {
        e.preventDefault();
        if (authView.isPending) return;
        clearAuthError();
        authView.isRegisterMode = !authView.isRegisterMode;
        updateAuthUi();
    });

    authBtn.addEventListener('click', (e) => {
        e.preventDefault();
        void submitAuth();
    });

    usernameInput.addEventListener('keydown', (e) => {
        if (e.key === 'Enter') {
            e.preventDefault();
            passwordInput.focus();
        }
    });
    
    passwordInput.addEventListener('keydown', (e) => {
        if (e.key === 'Enter') {
            e.preventDefault();
            void submitAuth();
        }
    });

    usernameInput.addEventListener('input', clearAuthError);
    passwordInput.addEventListener('input', clearAuthError);

    playAgainBtn.addEventListener('click', () => {
        deathModal.style.display = 'none';
        if (socket && socket.readyState === WebSocket.OPEN && isAuthorized) {
            socket.send(JSON.stringify({ Type: 'join', Data: {} }));
        } else {
            authModal.style.display = 'flex';
        }
    });

    logoutBtn.addEventListener('click', () => {
        activeSocketGeneration++;
        isAuthorized = false;
        playerId = null;
        deathModal.style.display = 'none';
        authModal.style.display = 'flex';
        clearAuthError();
        setAuthPending(false);
        if (socket) {
            socket.onclose = null;
            socket.close();
            socket = null;
        }
        setMonitorState('WYLOGOWANO', '#ffcc00');
    });

    updateAuthUi();
}

async function connectToServer(username, password, isRegister) {
    const config = await getServerConfig();
    const wsPort = config.wsPort || 5006;
    let wsHost = window.location.hostname;
    const wsProtocol = window.location.protocol === 'https:' ? 'wss' : 'ws';
    
    // Fallback for local files or invalid hostnames
    if (!wsHost || wsHost === "0.0.0.0") wsHost = "127.0.0.1";

    if (socket) {
        socket.onclose = null;
        socket.onerror = null;
        socket.close();
    }

    const connectionGeneration = ++activeSocketGeneration;
    socket = new WebSocket(`${wsProtocol}://${wsHost}:${wsPort}`);
    setMonitorState('ŁĄCZENIE...', '#ffcc00');

    socket.onopen = () => {
        if (connectionGeneration !== activeSocketGeneration) return;
        setMonitorState('UWIERZYTELNIANIE...', '#ffcc00');
        socket.send(JSON.stringify({
            Type: 'auth',
            Data: { username, password, register: isRegister }
        }));
    };

    socket.onmessage = (event) => {
        if (connectionGeneration !== activeSocketGeneration) return;
        const msg = JSON.parse(event.data);
        handleServerMessage(msg, username);
    };

    socket.onclose = (event) => {
        if (connectionGeneration !== activeSocketGeneration) return;
        isAuthorized = false;
        console.warn(`[MONITOR] Socket closed: Code ${event.code}, Reason: ${event.reason}`);
        setMonitorState(`OFFLINE: ${event.reason || 'Server Closed'}`, '#ff4444');
        if (authView) {
            authView.authModal.style.display = 'flex';
            authView.isPending = false;
            updateAuthUiState();
        }
    };
    
    socket.onerror = (err) => {
        if (connectionGeneration !== activeSocketGeneration) return;
        console.error("[MONITOR] WebSocket Error: ", err);
        setMonitorState('NETWORK ERROR', '#ff4444');
        showAuthError('Nie udało się połączyć z serwerem.');
    };

    function updateAuthUiState() {
        if (!authView) return;
        authView.authBtn.disabled = authView.isPending;
        authView.usernameInput.disabled = authView.isPending;
        authView.passwordInput.disabled = authView.isPending;
        authView.authBtn.textContent = authView.isPending
            ? (authView.isRegisterMode ? 'Rejestrowanie...' : 'Logowanie...')
            : (authView.isRegisterMode ? 'Zarejestruj się' : 'Zaloguj się');
    };
}

function handleServerMessage(msg, username) {
    switch (msg.Type) {
        case 'auth_response':
            if (msg.Data.success) {
                clearAuthError();
                isAuthorized = true;
                setMonitorState('AUTORYZACJA OK', '#00ff88');
                socket.send(JSON.stringify({ Type: 'join', Data: { name: username } }));
            } else {
                isAuthorized = false;
                showAuthError(msg.Data.message || 'Logowanie nie powiodło się.');
                if (authView) {
                    authView.isPending = false;
                    authView.authBtn.disabled = false;
                    authView.usernameInput.disabled = false;
                    authView.passwordInput.disabled = false;
                    authView.authBtn.textContent = authView.isRegisterMode ? 'Zarejestruj się' : 'Zaloguj się';
                }
                setMonitorState('BŁĄD AUTORYZACJI', '#ff4444');
            }
            break;
        case 'joined':
            playerId = msg.Data.Id;
            if (authView) {
                authView.isPending = false;
                authView.authModal.style.display = 'none';
                authView.authBtn.disabled = false;
                authView.usernameInput.disabled = false;
                authView.passwordInput.disabled = false;
                authView.authBtn.textContent = authView.isRegisterMode ? 'Zarejestruj się' : 'Zaloguj się';
            }
            setMonitorState('ONLINE', '#00ff88');
            break;
        case 'playerSpawned':
            if (msg.Data.Id === playerId) {
                camera.x = msg.Data.x;
                camera.y = msg.Data.y;
                console.log(`Player spawned at ${camera.x}, ${camera.y}`);
            }
            break;
        case 'auth_required':
            isAuthorized = false;
            showAuthError(msg.Data.message || 'Zaloguj się, aby kontynuować.');
            if (authView) {
                authView.isPending = false;
                authView.authModal.style.display = 'flex';
                authView.authBtn.disabled = false;
                authView.usernameInput.disabled = false;
                authView.passwordInput.disabled = false;
                authView.authBtn.textContent = authView.isRegisterMode ? 'Zarejestruj się' : 'Zaloguj się';
            }
            setMonitorState('WYMAGANE LOGOWANIE', '#ff4444');
            break;
        case 'state':
            updateState(msg.Data);
            break;
        case 'death':
            showDeathScreen(msg.Data.reason);
            break;
    }
}

function updateState(data) {
    players = {};
    data.players.forEach(p => { players[p.Id] = p; });
    foodItems = data.food;
    leaderboard = data.leaderboard;
    records24h = data.records24h || [];
    const top10 = data.top10 || [];

    // Update UI
    document.getElementById('player-count').textContent = data.players.length;
    // document.getElementById('food-count').textContent = foodItems.length;
    const local = players[playerId];
    if (local && local.Body && local.Body.length > 0) {
        document.getElementById('player-score').textContent = local.Score;
        camera.x = local.Body[0].X;
        camera.y = local.Body[0].Y;
    } else if (data.players.length > 0 && (camera.x === 0 && camera.y === 0)) {
        // Fallback to center if not spawned yet and camera at (0,0)
        camera.x = mapRadius;
        camera.y = mapRadius;
    }
    
    // Throttled leaderboard update (every 500ms)
    const now = Date.now();
    if (now - lastUpdateTime > 500) {
        updateLeaderboardUI(top10, records24h);
        lastUpdateTime = now;
    }
}

function showDeathScreen(reason) {
    const local = players[playerId];
    document.getElementById('death-modal').style.display = 'flex';
    document.getElementById('death-reason').textContent = reason || 'Zginąłeś!';
    document.getElementById('final-score').textContent = local ? local.Score : 0;
    playerId = null;
}

function updateLeaderboardUI(top10, recs24) {
    const lb = document.getElementById('leaderboard');
    const recs = document.getElementById('records-24h');
    
    lb.innerHTML = top10.map((e, i) => `
        <div style="font-size: 13px; margin-bottom: 8px;">
            <span style="color: ${i === 0 ? '#ffd700' : '#fff'}">${i + 1}. ${e.Name} - ${e.Score} punktów (długość: ${e.Length || 0})</span>
        </div>
    `).join('');

    recs.innerHTML = recs24.map((e, i) => `
        <div style="font-size: 12px; color: #aaa;">
            <span>${e.Name} - ${e.Score} punktów (długość: ${e.Length || 0})</span>
        </div>
    `).join('');
}

// --- Particles ---
function emitParticles(x, y, color, count = 3) {
    for (let i = 0; i < count; i++) {
        particles.push({
            x, y,
            vx: (Math.random() - 0.5) * 4,
            vy: (Math.random() - 0.5) * 4,
            life: 1.0,
            color
        });
    }
}

function updateParticles() {
    for (let i = particles.length - 1; i >= 0; i--) {
        const p = particles[i];
        p.x += p.vx;
        p.y += p.vy;
        p.life -= 0.02;
        if (p.life <= 0) particles.splice(i, 1);
    }
}

function drawParticles(ox, oy) {
    particles.forEach(p => {
        ctx.globalAlpha = p.life;
        ctx.fillStyle = p.color;
        ctx.beginPath();
        ctx.arc(p.x + ox, p.y + oy, 2, 0, Math.PI * 2);
        ctx.fill();
    });
    ctx.globalAlpha = 1.0;
}

// --- Rendering Logic ---
function draw() {
    ctx.clearRect(0, 0, canvas.width, canvas.height);
    
    const offsetX = canvas.width / 2 - camera.x;
    const offsetY = canvas.height / 2 - camera.y;

    drawBackground(offsetX, offsetY);
    drawFood(offsetX, offsetY);
    drawParticles(offsetX, offsetY);
    drawPlayers(offsetX, offsetY);
    drawMapBorder(offsetX, offsetY);
    if (minimapVisible) drawMinimap();

    updateParticles();

    const local = players[playerId];
    if (local && local.Body && local.Body.length > 0) {
        const angle = Math.atan2(mousePos.y - canvas.height / 2, mousePos.x - canvas.width / 2);
        socket.send(JSON.stringify({ Type: 'move', Data: { angle } }));
    }

    requestAnimationFrame(draw);
}

function drawBackground(ox, oy) {
    // Colorful animated background
    const time = Date.now() / 2000;
    const gradient = ctx.createRadialGradient(
        canvas.width / 2, canvas.height / 2, 0,
        canvas.width / 2, canvas.height / 2, Math.max(canvas.width, canvas.height)
    );
    gradient.addColorStop(0, '#121214');
    gradient.addColorStop(1, '#050505');
    ctx.fillStyle = gradient;
    ctx.fillRect(0, 0, canvas.width, canvas.height);

    // Grid
    ctx.strokeStyle = COLORS.grid;
    ctx.lineWidth = 1;
    const gridSize = 64;
    const startX = (ox % gridSize);
    const startY = (oy % gridSize);

    ctx.beginPath();
    for (let x = startX; x < canvas.width; x += gridSize) {
        ctx.moveTo(x, 0);
        ctx.lineTo(x, canvas.height);
    }
    for (let y = startY; y < canvas.height; y += gridSize) {
        ctx.moveTo(0, y);
        ctx.lineTo(canvas.width, y);
    }
    ctx.stroke();
}

function drawMapBorder(ox, oy) {
    ctx.strokeStyle = COLORS.border;
    ctx.lineWidth = 10;
    ctx.beginPath();
    ctx.arc(ox + mapRadius, oy + mapRadius, mapRadius, 0, Math.PI * 2);
    ctx.stroke();
}

function drawFood(ox, oy) {
    const time = Date.now() / 200;
    const pulse = 1 + Math.sin(time) * 0.15;

    foodItems.forEach(f => {
        if (f.Value <= 0) return;
        const x = f.Position.X + ox;
        const y = f.Position.Y + oy;
        
        // Only draw if on screen
        if (x < -50 || x > canvas.width + 50 || y < -50 || y > canvas.height + 50) return;

        ctx.fillStyle = f.Color;
        ctx.shadowBlur = pulse * 10;
        ctx.shadowColor = f.Color;

        if (f.IsPowerUp) {
            drawStar(ctx, x, y, 5, 12 * pulse, 6 * pulse);
        } else {
            ctx.beginPath();
            ctx.arc(x, y, 6 * pulse, 0, Math.PI * 2);
            ctx.fill();
        }
    });
    ctx.shadowBlur = 0;
}

function drawStar(ctx, x, y, points, outerRadius, innerRadius) {
    let step = Math.PI / points;
    let rotation = -Math.PI / 2;
    ctx.beginPath();
    for (let i = 0; i < points * 2; i++) {
        let r = (i % 2 === 0) ? outerRadius : innerRadius;
        ctx.lineTo(x + Math.cos(rotation) * r, y + Math.sin(rotation) * r);
        rotation += step;
    }
    ctx.closePath();
    ctx.fill();
}

function drawPlayers(ox, oy) {
    Object.values(players).forEach(p => {
        if (!p.Body || p.Body.length === 0) return;
        
        const head = p.Body[0];
        const hx = head.X + ox;
        const hy = head.Y + oy;

        // Draw body segments (Rectangular)
        for (let i = p.Body.length - 1; i > 0; i--) {
            const seg = p.Body[i];
            const nextSeg = p.Body[i - 1];
            // Base size increased by 30% (18 * 1.3 = 23.4)
            const size = 23.4 + (p.Score / 100); 
            const alpha = 1 - (i / p.Body.length) * 0.5;
            
            ctx.save();
            ctx.translate(seg.X + ox, seg.Y + oy);
            ctx.rotate(Math.atan2(nextSeg.Y - seg.Y, nextSeg.X - seg.X));
            ctx.fillStyle = p.Color;
            ctx.globalAlpha = alpha;
            
            if (i === p.Body.length - 1) {
                // Round tail
                ctx.beginPath();
                ctx.arc(0, 0, size / 2, 0, Math.PI * 2);
                ctx.fill();
            } else {
                // Rectangular segment
                ctx.fillRect(-size / 2, -size / 2, size, size);
            }
            ctx.restore();
        }

        // Emit particles for local player
        if (p.Id === playerId && Math.random() < 0.2) {
            emitParticles(head.X, head.Y, p.Color);
        }

        // Draw Head
        ctx.save();
        ctx.translate(hx, hy);
        ctx.rotate(p.Angle);

        // Head Glow
        ctx.shadowBlur = 15;
        ctx.shadowColor = p.Color;

        // Round head
        ctx.fillStyle = p.Color;
        ctx.beginPath();
        ctx.arc(0, 0, 22, 0, Math.PI * 2);
        ctx.fill();
        ctx.shadowBlur = 0; // Reset for rest of head

        // Beak (two triangles)
        // Dark part
        ctx.fillStyle = '#cc8400';
        ctx.beginPath();
        ctx.moveTo(15, -8);
        ctx.lineTo(35, 0);
        ctx.lineTo(15, 0);
        ctx.fill();
        // Light part
        ctx.fillStyle = '#ffa500';
        ctx.beginPath();
        ctx.moveTo(15, 8);
        ctx.lineTo(35, 0);
        ctx.lineTo(15, 0);
        ctx.fill();

        // Eye tracking cursor
        const eyeX = 10, eyeY = -10;
        ctx.fillStyle = '#fff';
        ctx.beginPath();
        ctx.arc(eyeX, eyeY, 6, 0, Math.PI * 2);
        ctx.fill();

        // Pupil tracking
        const dx = mousePos.x - (hx + Math.cos(p.Angle) * eyeX - Math.sin(p.Angle) * eyeY);
        const dy = mousePos.y - (hy + Math.sin(p.Angle) * eyeX + Math.cos(p.Angle) * eyeY);
        const dist = Math.min(3, Math.sqrt(dx*dx + dy*dy) / 20);
        const eyeAngle = Math.atan2(dy, dx);
        
        ctx.fillStyle = '#000';
        ctx.beginPath();
        ctx.arc(eyeX + Math.cos(eyeAngle) * dist, eyeY + Math.sin(eyeAngle) * dist, 3, 0, Math.PI * 2);
        ctx.fill();

        ctx.restore();

        // Name
        ctx.fillStyle = '#fff';
        ctx.font = 'bold 14px Inter';
        ctx.textAlign = 'center';
        ctx.shadowColor = 'black';
        ctx.shadowBlur = 4;
        ctx.fillText(p.Name, hx, hy - 40);
        ctx.shadowBlur = 0;
    });
}

function drawMinimap() {
    minimapCtx.clearRect(0, 0, 200, 200);
    
    // Background (Circle)
    minimapCtx.fillStyle = 'rgba(0, 0, 0, 0.8)';
    minimapCtx.beginPath();
    minimapCtx.arc(100, 100, 100, 0, Math.PI * 2);
    minimapCtx.fill();
    
    // Border
    minimapCtx.strokeStyle = '#00ff88';
    minimapCtx.lineWidth = 2;
    minimapCtx.stroke();

    const scale = 100 / mapRadius; // 100 pixels represents mapRadius units

    Object.values(players).forEach(p => {
        if (!p.Body || p.Body.length === 0) return;
        
        const head = p.Body[0];
        // Calculate position relative to map center
        const relX = head.X - mapRadius;
        const relY = head.Y - mapRadius;
        
        const mx = 100 + relX * scale;
        const my = 100 + relY * scale;

        if (p.Id === playerId) {
            // Local player
            minimapCtx.fillStyle = '#fff';
            minimapCtx.shadowBlur = 10;
            minimapCtx.shadowColor = '#fff';
            minimapCtx.beginPath();
            minimapCtx.arc(mx, my, 4, 0, Math.PI * 2);
            minimapCtx.fill();
            minimapCtx.shadowBlur = 0;
            
            // Player direction indicator
            minimapCtx.strokeStyle = '#fff';
            minimapCtx.lineWidth = 2;
            minimapCtx.beginPath();
            minimapCtx.moveTo(mx, my);
            minimapCtx.lineTo(mx + Math.cos(p.Angle) * 8, my + Math.sin(p.Angle) * 8);
            minimapCtx.stroke();
        } else {
            // Others
            minimapCtx.fillStyle = p.IsBot ? '#00b4ff' : p.Color;
            minimapCtx.beginPath();
            // Size based on length (Score)
            const size = 2 + Math.min(3, p.Score / 500);
            minimapCtx.arc(mx, my, size, 0, Math.PI * 2);
            minimapCtx.fill();
        }
    });
}

setupUI();
requestAnimationFrame(draw);
