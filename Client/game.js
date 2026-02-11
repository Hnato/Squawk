const config = {
    type: Phaser.AUTO,
    parent: 'game-container',
    width: window.innerWidth,
    height: window.innerHeight,
    backgroundColor: '#000000',
    scene: {
        preload: preload,
        create: create,
        update: update
    }
};

const COLORS = [
    { head: 0x00ff00, body: 0x008000 }, // Zielony
    { head: 0xffff00, body: 0xffd700 }, // Żółty
    { head: 0xadff2f, body: 0x32cd32 }, // Jasnozielony
    { head: 0xdaa520, body: 0xb8860b }, // Złoty
];

let game = new Phaser.Game(config);
let socket;
let playerId;
let players = {};
let feathers = {};
let cursors;
let isBoosting = false;
let mapRadius = 7500;
let cam;

function preload() {
    this.load.image('parrot_head', 'logo.png');
}

function create() {
    cam = this.cameras.main;
    cursors = this.input.keyboard.createCursorKeys();
    
    // Circular map background
    const bg = this.add.graphics();
    bg.fillStyle(0x050505, 1);
    bg.fillCircle(mapRadius, mapRadius, mapRadius);
    
    // Grid inside circle
    const grid = this.add.grid(mapRadius, mapRadius, mapRadius * 2, mapRadius * 2, 150, 150, 0x000000, 0, 0x00ff00, 0.05);
    
    // Circular Border
    const border = this.add.graphics();
    border.lineStyle(20, 0x00ff00, 0.8);
    border.strokeCircle(mapRadius, mapRadius, mapRadius);

    this.mapGraphics = { bg, grid, border };
    
    document.getElementById('startBtn').addEventListener('click', () => {
        const name = document.getElementById('playerName').value || 'Papuga';
        connectToServer(name);
        document.getElementById('login').style.display = 'none';
    });
}

function connectToServer(name) {
    socket = new WebSocket('ws://localhost:5004');

    socket.onopen = () => {
        console.log('Połączono z serwerem');
    };

    socket.onmessage = (event) => {
        const data = JSON.parse(event.data);

        if (data.Type === 'welcome') {
            playerId = data.PlayerId;
            // MapWidth in circular map is now used as Radius if provided
            if (data.MapRadius) {
                mapRadius = data.MapRadius;
                // Update map graphics if map size changed
                const scene = game.scene.scenes[0];
                if (scene && scene.mapGraphics) {
                    const { bg, grid, border } = scene.mapGraphics;
                    bg.clear();
                    bg.fillStyle(0x050505, 1);
                    bg.fillCircle(mapRadius, mapRadius, mapRadius);
                    
                    grid.setPosition(mapRadius, mapRadius);
                    grid.setSize(mapRadius * 2, mapRadius * 2);
                    
                    border.clear();
                    border.lineStyle(20, 0x00ff00, 0.8);
                    border.strokeCircle(mapRadius, mapRadius, mapRadius);
                }
            }
            socket.send(JSON.stringify({ Type: 'join', Name: name }));
        } else if (data.Type === 'update') {
            handleGameUpdate(data);
        } else if (data.Type === 'leaderboard') {
            updateLeaderboard(data.Entries);
        }
    };

    socket.onclose = () => {
        console.log('Rozłączono z serwerem');
        document.getElementById('login').style.display = 'block';
    };
}

function handleGameUpdate(data) {
    const scene = game.scene.scenes[0];
    if (!scene) return;

    // Update players
    const currentIds = data.Parrots.map(p => p.Id);
    
    // Remove disconnected
    Object.keys(players).forEach(id => {
        if (!currentIds.includes(id)) {
            players[id].segments.forEach(s => s.destroy());
            players[id].nameText.destroy();
            delete players[id];
        }
    });

    data.Parrots.forEach(pData => {
        if (!players[pData.Id]) {
            // Assign color based on name or ID hash
            let colorIndex = pData.Name.length % COLORS.length;
            if (pData.Id === playerId) colorIndex = 0; // Local player always specific color if you want
            
            players[pData.Id] = {
                segments: [],
                nameText: scene.add.text(0, 0, pData.Name, { 
                    fontSize: '16px', 
                    fill: '#fff',
                    fontStyle: 'bold',
                    stroke: '#000',
                    strokeThickness: 3
                }).setOrigin(0.5),
                color: COLORS[colorIndex]
            };
        }

        const p = players[pData.Id];
        const size = 1 + (pData.Energy / 100);

        // Update segments
        while (p.segments.length < pData.Segments.length) {
            const isHead = p.segments.length === 0;
            const radius = isHead ? 20 : 15;
            const seg = scene.add.circle(0, 0, radius, isHead ? p.color.head : p.color.body);
            
            if (isHead) {
                // Add simple eyes to the head
                const leftEye = scene.add.circle(-7, -7, 4, 0xffffff);
                const rightEye = scene.add.circle(7, -7, 4, 0xffffff);
                const leftPupil = scene.add.circle(-7, -7, 2, 0x000000);
                const rightPupil = scene.add.circle(7, -7, 2, 0x000000);
                
                seg.eyes = [leftEye, rightEye, leftPupil, rightPupil];
            }
            
            p.segments.push(seg);
        }
        while (p.segments.length > pData.Segments.length) {
            const seg = p.segments.pop();
            if (seg.eyes) seg.eyes.forEach(e => e.destroy());
            seg.destroy();
        }

        pData.Segments.forEach((pos, i) => {
            const seg = p.segments[i];
            seg.x = pos.X;
            seg.y = pos.Y;
            seg.setScale(size);
            
            if (seg.eyes) {
                // Update eyes position and rotation based on movement
                const angle = i < pData.Segments.length - 1 ? 
                    Math.atan2(pData.Segments[i].Y - pData.Segments[i+1].Y, pData.Segments[i].X - pData.Segments[i+1].X) : 0;
                
                seg.eyes.forEach((eye, eyeIdx) => {
                    const offsetX = eyeIdx < 2 ? (eyeIdx === 0 ? -7 : 7) : (eyeIdx === 2 ? -7 : 7);
                    const offsetY = -7;
                    
                    const rotatedX = offsetX * Math.cos(angle) - offsetY * Math.sin(angle);
                    const rotatedY = offsetX * Math.sin(angle) + offsetY * Math.cos(angle);
                    
                    eye.x = seg.x + rotatedX * size;
                    eye.y = seg.y + rotatedY * size;
                    eye.setScale(size);
                    eye.setDepth(10);
                });
            }
            seg.setDepth(5 - i * 0.1);
        });

        p.nameText.x = pData.X;
        p.nameText.y = pData.Y - 50 * size;
        p.nameText.setDepth(20);

        if (pData.Id === playerId) {
            cam.startFollow(p.segments[0], true, 0.1, 0.1);
            const zoom = Math.max(0.3, 1 / size);
            cam.setZoom(Phaser.Math.Linear(cam.zoom, zoom, 0.1));
        }
    });

    // Update feathers
    const currentFeatherIds = data.Feathers.map(f => f.Id);
    Object.keys(feathers).forEach(id => {
        if (!currentFeatherIds.includes(id)) {
            feathers[id].destroy();
            delete feathers[id];
        }
    });

    data.Feathers.forEach(fData => {
        if (!feathers[fData.Id]) {
            let color = 0xf1c40f;
            let radius = 6;
            if (fData.Type === 1) { // BOOST
                color = 0x00f2ff;
                radius = 8;
            } else if (fData.Type === 2) { // DEATH
                color = 0xff4d6d;
                radius = 5;
            }
            
            const feather = scene.add.circle(fData.X, fData.Y, radius * fData.Value, color);
            feather.setStrokeStyle(2, 0xffffff, 0.5);
            feathers[fData.Id] = feather;

            // Simple pulse animation for feathers
            scene.tweens.add({
                targets: feather,
                scale: 1.2,
                duration: 800 + Math.random() * 400,
                yoyo: true,
                repeat: -1,
                ease: 'Sine.easeInOut'
            });
        }
    });
}

function updateLeaderboard(entries) {
    const list = document.getElementById('leaderboard');
    list.innerHTML = entries.map((e, i) => `<div>${i+1}. ${e.Name}: ${Math.floor(e.Score)}</div>`).join('');
}

function update() {
    if (!socket || socket.readyState !== WebSocket.OPEN || !playerId) return;

    const pointer = this.input.activePointer;
    const worldPoint = cam.getWorldPoint(pointer.x, pointer.y);

    const isBoosting = cursors.space.isDown || pointer.isDown;

    socket.send(JSON.stringify({
        Type: 'input',
        TargetX: worldPoint.x,
        TargetY: worldPoint.y,
        IsBoosting: isBoosting
    }));
}

window.addEventListener('resize', () => {
    game.scale.resize(window.innerWidth, window.innerHeight);
});
