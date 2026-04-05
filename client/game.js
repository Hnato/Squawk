const config = {
    type: Phaser.AUTO,
    parent: 'game-container',
    width: window.innerWidth,
    height: window.innerHeight,
    backgroundColor: '#000000',
    physics: {
        default: 'arcade',
        arcade: {
            gravity: { y: 0 },
            debug: false
        }
    },
    scene: {
        preload: preload,
        create: create,
        update: update
    }
};

let game = new Phaser.Game(config);
let socket;
let playerId;
let players = {};
let foodItems = {};
let mapRadius = 1500;
let cam;
let isAuthorized = false;
let parallaxGrid;
let foodGraphics;
let minimapCanvas, minimapCtx;
let terrainGraphics;
let minimapVisible = true;

function preload() {
    // We'll use graphics for the parrot design as requested
}

function create() {
    cam = this.cameras.main;
    
    // 0. Terrain Textures (drawn once)
    terrainGraphics = this.add.graphics();
    terrainGraphics.setDepth(-8);
    drawTerrain(terrainGraphics);

    foodGraphics = this.add.graphics();
    foodGraphics.setDepth(-3);
    
    // Initialize Minimap
    minimapCanvas = document.getElementById('minimap');
    if (minimapCanvas) {
        minimapCtx = minimapCanvas.getContext('2d');
    }

    // Tab key for minimap toggle
    this.input.keyboard.on('keydown-TAB', (event) => {
        event.preventDefault();
        minimapVisible = !minimapVisible;
        const minimapContainer = document.getElementById('minimap-container');
        if (minimapContainer) {
            minimapContainer.style.display = minimapVisible ? 'block' : 'none';
        }
    });

    // 1. Dynamic Parallax Grid (Scrolling background)
    parallaxGrid = this.add.grid(0, 0, window.innerWidth * 2, window.innerHeight * 2, 64, 64, 0x000000, 0, 0x00ff88, 0.05);
    parallaxGrid.setScrollFactor(0);
    parallaxGrid.setDepth(-10);

    // 2. Circular Game Board
    const bg = this.add.graphics();
    bg.setDepth(-9);
    bg.fillStyle(0x050505, 1);
    bg.fillCircle(mapRadius, mapRadius, mapRadius);
    
    // Thick green boundary line
    const border = this.add.graphics();
    border.setDepth(-4);
    border.lineStyle(20, 0x00ff00, 1); // Thick green line
    border.strokeCircle(mapRadius, mapRadius, mapRadius);

    this.mapGraphics = { bg, border };
    cam.centerOn(mapRadius, mapRadius);

    setupAuthUI();
}

function drawTerrain(graphics) {
    // Random terrain patches (grass, sand, rocks)
    const patchCount = 100;
    const colors = [0x2d5a27, 0xc2b280, 0x4a4a4a]; // Green, Sand, Rock
    
    for (let i = 0; i < patchCount; i++) {
        const angle = Math.random() * Math.PI * 2;
        const dist = Math.random() * (mapRadius - 100);
        const x = mapRadius + Math.cos(angle) * dist;
        const y = mapRadius + Math.sin(angle) * dist;
        const size = 50 + Math.random() * 150;
        
        graphics.fillStyle(colors[Math.floor(Math.random() * colors.length)] || colors[0], 0.1);
        graphics.fillEllipse(x, y, size, size * 0.7);
    }
}

function setupAuthUI() {
    const authModal = document.getElementById('auth-modal');
    const authBtn = document.getElementById('auth-btn');
    const switchLink = document.getElementById('switch-link');
    const modalTitle = document.getElementById('modal-title');
    const switchText = document.getElementById('switch-text');
    const errorMsg = document.getElementById('error-msg');
    const usernameInput = document.getElementById('username');

    // Load saved username
    const savedUsername = localStorage.getItem('squawk_username');
    if (savedUsername) {
        usernameInput.value = savedUsername;
    }

    let isRegisterMode = false;

    switchLink.addEventListener('click', () => {
        isRegisterMode = !isRegisterMode;
        modalTitle.textContent = isRegisterMode ? 'Rejestracja' : 'Logowanie';
        authBtn.textContent = isRegisterMode ? 'Zarejestruj się' : 'Zaloguj się';
        switchText.textContent = isRegisterMode ? 'Masz już konto?' : 'Nie masz konta?';
        switchLink.textContent = isRegisterMode ? 'Zaloguj się' : 'Zarejestruj się';
        errorMsg.textContent = '';
    });

    authBtn.addEventListener('click', () => {
        const username = usernameInput.value;
        const password = document.getElementById('password').value;

        if (!username || !password) {
            errorMsg.textContent = 'Wypełnij wszystkie pola!';
            return;
        }

        // Save username for next time
        localStorage.setItem('squawk_username', username);

        connectToServer(username, password, isRegisterMode);
    });
}

function connectToServer(username, password, isRegister) {
    const wsPort = window.location.port ? parseInt(window.location.port) : 5005; 
    const wsHost = window.location.hostname || '127.0.0.1';
    
    if (socket) socket.close();
    
    socket = new WebSocket(`ws://${wsHost}:${wsPort}`);

    socket.onopen = () => {
        console.log('Connected to server, authenticating...');
        socket.send(JSON.stringify({
            Type: 'auth',
            Data: {
                username: username,
                password: password,
                register: isRegister
            }
        }));
    };

    socket.onmessage = (event) => {
        try {
            const msg = JSON.parse(event.data);
            handleServerMessage(msg, username);
        } catch (err) {
            console.error('Error parsing message:', err);
        }
    };

    socket.onclose = () => {
        console.log('Disconnected from server');
        isAuthorized = false;
        document.getElementById('auth-modal').style.display = 'flex';
        // Clear existing players
        Object.values(players).forEach(p => p.destroy());
        players = {};
    };
}

function handleServerMessage(msg, username) {
    switch (msg.Type) {
        case 'auth_response':
            if (msg.Data.success) {
                document.getElementById('auth-modal').style.display = 'none';
                isAuthorized = true;
                socket.send(JSON.stringify({
                    Type: 'join',
                    Data: { name: username }
                }));
            } else {
                document.getElementById('error-msg').textContent = msg.Data.message || 'Błąd autoryzacji!';
            }
            break;

        case 'joined':
            playerId = msg.Data.id;
            console.log('Joined with ID:', playerId);
            break;

        case 'state':
            updateGameState(msg.Data);
            break;

        case 'death':
            console.log('Player died:', msg.Data.reason);
            isAuthorized = false;
            playerId = null;
            document.getElementById('auth-modal').style.display = 'flex';
            document.getElementById('error-msg').textContent = 'Zginąłeś! ' + (msg.Data.reason || '');
            break;
    }
}

function updateGameState(data) {
    const scene = game.scene.scenes[0];
    if (!scene) return;

    // Update Player Counter
    const playerCountEl = document.getElementById('player-count');
    if (playerCountEl) {
        playerCountEl.textContent = data.players.length;
    }

    // Update Players (Parrots)
    data.players.forEach(p => {
        if (!players[p.Id]) {
            players[p.Id] = createParrot(scene, p);
        }
        updateParrot(players[p.Id], p);
    });

    // Remove disconnected players
    const activeIds = data.players.map(p => p.Id);
    Object.keys(players).forEach(id => {
        if (!activeIds.includes(id)) {
            players[id].destroy();
            delete players[id];
        }
    });

    // Camera follow local player
    if (playerId && players[playerId]) {
        const player = players[playerId];
        // Smoothly center the camera
        cam.scrollX = player.x - cam.width / 2;
        cam.scrollY = player.y - cam.height / 2;
    } else if (playerId) {
        // Fallback for initial state before player is created
        cam.centerOn(mapRadius, mapRadius);
    }

    // Update Leaderboard
    updateLeaderboard(data.leaderboard);

    // Draw Food and Power-ups
    foodGraphics.clear();
    data.food.forEach(f => {
        if (f.Value > 0) {
            const color = Phaser.Display.Color.HexStringToColor(f.Color).color;
            foodGraphics.fillStyle(color, 1);
            if (f.IsPowerUp) {
                // Draw star-like shape for power-ups
                drawStar(foodGraphics, f.Position.X, f.Position.Y, 5, 12, 6);
            } else {
                foodGraphics.fillCircle(f.Position.X, f.Position.Y, 6);
            }
        }
    });

    // Draw Minimap
    drawMinimap(data.players);
}

function drawMinimap(allPlayers) {
    if (!minimapCtx || !minimapCanvas || !minimapVisible) return;

    // Clear background
    minimapCtx.clearRect(0, 0, minimapCanvas.width, minimapCanvas.height);
    minimapCtx.fillStyle = 'rgba(0, 0, 0, 0.8)';
    minimapCtx.fillRect(0, 0, 200, 200);
    
    // Draw boundary
    minimapCtx.strokeStyle = '#00ff88';
    minimapCtx.lineWidth = 1;
    minimapCtx.strokeRect(5, 5, 190, 190);

    const scale = 190 / (mapRadius * 2);

    allPlayers.forEach(p => {
        const head = p.Body[0];
        const mx = 5 + head.X * scale;
        const my = 5 + head.Y * scale;

        if (p.Id === playerId) {
            // Local player indicator
            minimapCtx.fillStyle = '#ffffff';
            minimapCtx.beginPath();
            minimapCtx.arc(mx, my, 4, 0, Math.PI * 2);
            minimapCtx.fill();
            
            // Vision cone
            minimapCtx.strokeStyle = 'rgba(255, 255, 255, 0.2)';
            minimapCtx.beginPath();
            minimapCtx.arc(mx, my, 25, p.Angle - 0.5, p.Angle + 0.5);
            minimapCtx.stroke();
        } else {
            // Other players in their own color, bots in blue as requested
            minimapCtx.fillStyle = p.IsBot ? '#00b4ff' : p.Color;
            minimapCtx.beginPath();
            minimapCtx.arc(mx, my, 2.5, 0, Math.PI * 2);
            minimapCtx.fill();
        }
    });
}

function drawStar(graphics, x, y, points, outerRadius, innerRadius) {
    let step = Math.PI / points;
    let rotation = -Math.PI / 2;
    graphics.beginPath();
    for (let i = 0; i < points * 2; i++) {
        let r = (i % 2 === 0) ? outerRadius : innerRadius;
        let px = x + Math.cos(rotation) * r;
        let py = y + Math.sin(rotation) * r;
        if (i === 0) graphics.moveTo(px, py);
        else graphics.lineTo(px, py);
        rotation += step;
    }
    graphics.closePath();
    graphics.fillPath();
}

function createParrot(scene, data) {
    const container = scene.add.container(data.Body[0].X, data.Body[0].Y);
    
    // Shadow under parrot
    const shadow = scene.add.ellipse(0, 15, 30, 15, 0x000000, 0.3);
    container.add(shadow);

    // Parrot Body Design
    const body = scene.add.graphics();
    const color = Phaser.Display.Color.HexStringToColor(data.Color).color;
    
    // Feathers (body)
    body.fillStyle(color, 1);
    body.fillEllipse(0, 0, 40, 25); 
    
    // Wings with feather detail
    body.fillStyle(color, 0.8);
    body.fillEllipse(-5, -12, 22, 12);
    body.fillEllipse(-5, 12, 22, 12);
    
    // Head
    body.fillStyle(color, 1);
    body.fillCircle(15, 0, 14);
    
    // Eye
    body.fillStyle(0xffffff, 1);
    body.fillCircle(20, -4, 4);
    body.fillStyle(0x000000, 1);
    body.fillCircle(21, -4, 2);
    
    // Beak
    body.fillStyle(0xffd700, 1);
    body.fillTriangle(25, -6, 25, 6, 38, 0);

    container.add(body);
    
    // Updated Font Design
    const nameText = scene.add.text(0, -40, data.Name, { 
        fontSize: '14px', 
        fontFamily: 'Arial, sans-serif',
        fill: '#fff',
        fontStyle: 'bold',
        stroke: '#000',
        strokeThickness: 2,
        shadow: { offsetX: 2, offsetY: 2, color: '#000', blur: 0, stroke: true, fill: true }
    }).setOrigin(0.5);
    container.add(nameText);
    
    container.bodyGraphics = body;
    container.nameText = nameText;
    container.shadow = shadow;
    
    return container;
}

function updateParrot(container, data) {
    const head = data.Body[0];
    container.setPosition(head.X, head.Y);
    container.setRotation(data.Angle);
    container.nameText.setRotation(-data.Angle); // Keep name upright
    
    // Animated wings and shadow effect
    const wingTime = Date.now() / 100;
    const wingScale = 1 + Math.sin(wingTime) * 0.15;
    container.bodyGraphics.scaleY = wingScale;
    
    // Shadow pulsing
    container.shadow.setScale(1 + Math.sin(wingTime) * 0.05);
    
    // Emit particles if moving fast or just for effect
    if (Math.random() < 0.1) {
        emitFeather(container.scene, head.X, head.Y, data.Color);
    }
}

function emitFeather(scene, x, y, colorStr) {
    const color = Phaser.Display.Color.HexStringToColor(colorStr).color;
    const feather = scene.add.graphics();
    feather.fillStyle(color, 0.6);
    feather.fillEllipse(0, 0, 4, 8);
    feather.setPosition(x, y);
    feather.setDepth(-1);
    
    scene.tweens.add({
        targets: feather,
        x: x + (Math.random() - 0.5) * 40,
        y: y + (Math.random() - 0.5) * 40,
        alpha: 0,
        angle: 360,
        duration: 1000,
        onComplete: () => feather.destroy()
    });
}

function updateLeaderboard(entries) {
    const lb = document.getElementById('leaderboard');
    if (!lb) return;
    lb.innerHTML = entries.map((e, i) => `
        <div style="display: flex; justify-content: space-between; margin-bottom: 5px;">
            <span style="color: ${i === 0 ? '#ffd700' : '#fff'}">${i + 1}. ${e.Name}</span>
            <span style="font-weight: bold">${e.Score}</span>
        </div>
    `).join('');
}

function update() {
    if (!isAuthorized || !socket) return;

    // Parallax Grid Effect
    if (parallaxGrid) {
        parallaxGrid.y -= 1.5; // Constant scroll up
        if (parallaxGrid.y < -64) parallaxGrid.y = 0;
    }

    // Send input based on mouse position relative to camera center
    const pointer = this.input.activePointer;
    const angle = Phaser.Math.Angle.Between(
        window.innerWidth / 2, window.innerHeight / 2,
        pointer.x, pointer.y
    );
    
    if (socket.readyState === WebSocket.OPEN) {
        socket.send(JSON.stringify({
            Type: 'move',
            Data: { angle: angle }
        }));
    }
}

window.addEventListener('resize', () => {
    game.scale.resize(window.innerWidth, window.innerHeight);
    if (parallaxGrid) {
        parallaxGrid.setSize(window.innerWidth * 2, window.innerHeight * 2);
    }
});
