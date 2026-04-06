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
    
    // 1. Gray Square Grid on Black Background
    // We create a static grid covering the entire map
    const grid = this.add.grid(mapRadius, mapRadius, mapRadius * 2, mapRadius * 2, 64, 64, 0x000000, 1, 0x444444, 0.2);
    grid.setDepth(-10);

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

    // 2. Circular Game Board Boundary
    const border = this.add.graphics();
    border.setDepth(-4);
    border.lineStyle(20, 0x00ff00, 1); // Thick green line
    border.strokeCircle(mapRadius, mapRadius, mapRadius);

    this.mapGraphics = { grid, border };
    cam.centerOn(mapRadius, mapRadius);

    setupAuthUI();
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
    // WebSocket is usually on port - 1 if served via HttpListener in this app
    const currentPort = window.location.port ? parseInt(window.location.port) : 5006;
    const wsPort = currentPort - 1;
    const wsHost = window.location.hostname || '127.0.0.1';
    
    console.log(`Connecting to WebSocket on ${wsHost}:${wsPort}...`);
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
            playerId = msg.Data.Id; // Changed to uppercase Id to match server
            console.log('Spawning successful. Joined with ID:', playerId);
            break;

        case 'state':
            updateGameState(msg.Data);
            break;

        case 'death':
            console.warn('Player died:', msg.Data.reason);
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

    // Update Players (Parrots/Snakes)
    data.players.forEach(p => {
        if (!players[p.Id]) {
            console.log('NEW PLAYER/BOT:', p.Name, 'ID:', p.Id, 'LOCAL:', p.Id === playerId);
            players[p.Id] = createParrot(scene, p);
        }
        updateParrot(players[p.Id], p);
    });

    // Remove disconnected players
    const activeIds = data.players.map(p => p.Id);
    Object.keys(players).forEach(id => {
        if (!activeIds.includes(id)) {
            console.log('REMOVING:', id);
            if (players[id]) {
                players[id].destroy();
                delete players[id];
            }
        }
    });

    // Camera follow local player
    if (playerId) {
        const localPlayer = players[playerId];
        if (localPlayer) {
            const localPlayerData = data.players.find(p => p.Id === playerId);
            if (localPlayerData && localPlayerData.Body && localPlayerData.Body.length > 0) {
                const head = localPlayerData.Body[0];
                cam.centerOn(head.X, head.Y);
            }
        } else {
            // Player exists in system but not in current state yet
            cam.centerOn(mapRadius, mapRadius);
        }
    }

    // Update Leaderboard
    updateLeaderboard(data.leaderboard);

    // Draw Food
    foodGraphics.clear();
    data.food.forEach(f => {
        if (f.Value > 0) {
            const color = Phaser.Display.Color.HexStringToColor(f.Color).color;
            foodGraphics.fillStyle(color, 1);
            if (f.IsPowerUp) {
                drawStar(foodGraphics, f.Position.X, f.Position.Y, 5, 12, 6);
            } else {
                foodGraphics.fillCircle(f.Position.X, f.Position.Y, 6);
            }
        }
    });

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
    // Create container at (0,0) initially, will be moved in updateParrot
    const container = scene.add.container(0, 0);
    container.setDepth(5);
    
    // 1. Body segments graphics object
    const bodySegments = scene.add.graphics();
    bodySegments.setDepth(1);
    container.add(bodySegments);

    // 2. Head graphics object (will be at 0,0 relative to container)
    const headGraphics = scene.add.graphics();
    headGraphics.setDepth(2);
    container.add(headGraphics);

    // 3. Name Text (above head)
    const nameText = scene.add.text(0, -40, data.Name, {
        fontSize: '14px', 
        fontFamily: 'Arial, sans-serif',
        fill: '#ffffff',
        fontStyle: 'bold',
        stroke: '#000000',
        strokeThickness: 3,
        shadow: { offsetX: 2, offsetY: 2, color: '#000000', blur: 2, fill: true }
    }).setOrigin(0.5);
    nameText.setDepth(10);
    container.add(nameText);
    
    container.bodySegments = bodySegments;
    container.headGraphics = headGraphics;
    container.nameText = nameText;
    
    return container;
}

function updateParrot(container, data) {
    if (!data.Body || data.Body.length === 0) return;
    
    const head = data.Body[0];
    const color = Phaser.Display.Color.HexStringToColor(data.Color).color;
    
    // Move the WHOLE container to the head position
    container.setPosition(head.X, head.Y);
    
    // Clear and redraw body relative to head (container)
    container.bodySegments.clear();
    
    // Draw body segments relative to head (head is at 0,0 in container)
    for (let i = data.Body.length - 1; i >= 0; i--) {
        const seg = data.Body[i];
        const alpha = 1 - (i / data.Body.length) * 0.4;
        const radius = 12 - (i / data.Body.length) * 4;
        
        // Coordinates relative to head (container)
        const relX = seg.X - head.X;
        const relY = seg.Y - head.Y;
        
        container.bodySegments.fillStyle(color, alpha);
        container.bodySegments.fillCircle(relX, relY, radius);
        
        if (i === 0) {
            container.bodySegments.lineStyle(2, 0xffffff, 0.5);
            container.bodySegments.strokeCircle(0, 0, radius + 2);
        }
    }

    // Redraw head face (parrot) - head is at 0,0 relative to container
    container.headGraphics.clear();
    container.headGraphics.setRotation(data.Angle);

    // Face shadow
    container.headGraphics.fillStyle(0x000000, 0.2);
    container.headGraphics.fillEllipse(0, 10, 25, 10);

    // Face/Head circle
    container.headGraphics.fillStyle(color, 1);
    container.headGraphics.fillCircle(12, 0, 14);
    
    // Animated wings
    const wingTime = Date.now() / 150;
    const wingAngle = Math.sin(wingTime) * 0.3;
    container.headGraphics.fillStyle(color, 0.9);
    container.headGraphics.fillEllipse(-2, -10, 20, 10, wingAngle);
    container.headGraphics.fillEllipse(-2, 10, 20, 10, -wingAngle);
    
    // Eye
    container.headGraphics.fillStyle(0xffffff, 1);
    container.headGraphics.fillCircle(16, -5, 4);
    container.headGraphics.fillStyle(0x000000, 1);
    container.headGraphics.fillCircle(17, -5, 2);
    
    // Beak
    container.headGraphics.fillStyle(0xffa500, 1);
    container.headGraphics.fillTriangle(22, -6, 22, 6, 35, 0);

    // Name text stays at 0,-40 relative to container (head)
    
    // Particles - draw feathers in world coordinates
    if (Math.random() < 0.05) {
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
