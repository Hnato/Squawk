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
    { head: 0x00ffa3, body: 0x008f5d }, // Emerald
    { head: 0x00d1ff, body: 0x00768f }, // Ocean
    { head: 0xfff500, body: 0x8f8a00 }, // Canary
    { head: 0xff005c, body: 0x8f0034 }, // Ruby
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
    const grid = this.add.grid(mapRadius, mapRadius, mapRadius * 2, mapRadius * 2, 200, 200, 0x000000, 0, 0xffffff, 0.03);
    
    // Circular Border
    const border = this.add.graphics();
    border.lineStyle(10, 0xffffff, 0.1);
    border.strokeCircle(mapRadius, mapRadius, mapRadius);

    // Mini-map setup
    const miniMapSize = 400; // Increased 2x from 200
    const miniMapMargin = 30;
    this.miniMapContainer = this.add.container(miniMapMargin, window.innerHeight - miniMapSize - miniMapMargin); // Moved to left bottom
    this.miniMapContainer.setScrollFactor(0);
    this.miniMapContainer.setDepth(100);

    const miniMapBg = this.add.graphics();
    miniMapBg.fillStyle(0x000000, 0.6);
    miniMapBg.fillCircle(miniMapSize/2, miniMapSize/2, miniMapSize/2);
    miniMapBg.lineStyle(2, 0x00ffa3, 0.3);
    miniMapBg.strokeCircle(miniMapSize/2, miniMapSize/2, miniMapSize/2);
    this.miniMapContainer.add(miniMapBg);

    this.miniMapDots = this.add.graphics();
    this.miniMapContainer.add(this.miniMapDots);

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
                    border.lineStyle(10, 0xffffff, 0.1);
                    border.strokeCircle(mapRadius, mapRadius, mapRadius);
                }
            }
            const name = document.getElementById('playerName').value || 'Papuga';
            socket.send(JSON.stringify({ Type: 'join', Name: name }));
        } else if (data.Type === 'death') {
            // Handle player death
            playerId = null;
            if (socket) {
                socket.close();
            }
            document.getElementById('login').style.display = 'block';
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
            players[id].segments.forEach(seg => {
                if (seg.eyes) seg.eyes.forEach(e => e.destroy());
                seg.destroy();
            });
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
                        fontSize: '14px', 
                        fill: '#ffffff',
                        fontStyle: '600',
                        stroke: '#000000',
                        strokeThickness: 2,
                        padding: { x: 4, y: 2 }
                    }).setOrigin(0.5),
                    color: COLORS[colorIndex]
                };
                // Subtly fade in name
                players[pData.Id].nameText.setAlpha(0);
                scene.tweens.add({
                    targets: players[pData.Id].nameText,
                    alpha: 0.8,
                    duration: 500
                });
        }

        const p = players[pData.Id];
        const size = 1 + (pData.Energy / 100);
        
        // Detect growth (energy increase)
        if (p.lastEnergy !== undefined && pData.Energy > p.lastEnergy) {
            // Growth Effect: Flash color or particles
            const head = p.segments[0];
            if (head) {
                // Scaling effect (gulp animation)
                scene.tweens.add({
                    targets: head,
                    scale: size * 1.3,
                    duration: 100,
                    yoyo: true,
                    ease: 'Quad.easeOut'
                });
                
                // Particle effect for consumption
                const emitter = scene.add.particles(0, 0, 'parrot_head', {
                    x: head.x,
                    y: head.y,
                    speed: { min: 50, max: 150 },
                    scale: { start: 0.1, end: 0 },
                    alpha: { start: 0.6, end: 0 },
                    lifespan: 400,
                    blendMode: 'ADD',
                    tint: p.color.head,
                    maxParticles: 5
                });
            }
        }
        p.lastEnergy = pData.Energy;

        // Update segments
        while (p.segments.length < pData.Segments.length) {
            const isHead = p.segments.length === 0;
            const radius = isHead ? 22 : 16;
            
            let segColor = isHead ? p.color.head : p.color.body;
            if (!isHead && p.segments.length % 5 === 0) {
                segColor = p.color.head;
            }

            const seg = scene.add.circle(0, 0, radius, segColor);
            
            if (isHead) {
                // Add Parrot Beak (dziób)
                const beak = scene.add.triangle(0, 0, 0, -10, -8, 15, 8, 15, 0xff9100);
                beak.setDepth(11);
                
                // Add simple eyes to the head
                const leftEye = scene.add.circle(-8, -5, 5, 0xffffff);
                const rightEye = scene.add.circle(8, -5, 5, 0xffffff);
                const leftPupil = scene.add.circle(-8, -5, 2.5, 0x000000);
                const rightPupil = scene.add.circle(8, -5, 2.5, 0x000000);
                
                // Add animated wings to head/body
                const leftWing = scene.add.ellipse(-15, 0, 25, 12, p.color.head);
                const rightWing = scene.add.ellipse(15, 0, 25, 12, p.color.head);
                leftWing.setDepth(4);
                rightWing.setDepth(4);
                
                // Wing flap animation
                scene.tweens.add({
                    targets: [leftWing, rightWing],
                    scaleY: 0.2,
                    duration: 300,
                    yoyo: true,
                    repeat: -1,
                    ease: 'Sine.easeInOut'
                });
                
                seg.parrotFeatures = [beak, leftEye, rightEye, leftPupil, rightPupil, leftWing, rightWing];
            }
            
            p.segments.push(seg);
        }
        while (p.segments.length > pData.Segments.length) {
            const seg = p.segments.pop();
            if (seg.parrotFeatures) seg.parrotFeatures.forEach(e => e.destroy());
            seg.destroy();
        }

        pData.Segments.forEach((pos, i) => {
            const seg = p.segments[i];
            seg.x = pos.X;
            seg.y = pos.Y;
            seg.setScale(size);
            
            if (seg.parrotFeatures) {
                const angle = i < pData.Segments.length - 1 ? 
                    Math.atan2(pData.Segments[i].Y - pData.Segments[i+1].Y, pData.Segments[i].X - pData.Segments[i+1].X) : 0;
                
                // Update beak
                const beak = seg.parrotFeatures[0];
                const beakDist = 15 * size;
                beak.x = seg.x + Math.cos(angle) * beakDist;
                beak.y = seg.y + Math.sin(angle) * beakDist;
                beak.rotation = angle + Math.PI/2;
                beak.setScale(size);

                // Update eyes
                seg.parrotFeatures.slice(1, 5).forEach((eye, eyeIdx) => {
                    const offsetX = eyeIdx < 2 ? (eyeIdx === 0 ? -8 : 8) : (eyeIdx === 2 ? -8 : 8);
                    const offsetY = -5;
                    const rotatedX = offsetX * Math.cos(angle) - offsetY * Math.sin(angle);
                    const rotatedY = offsetX * Math.sin(angle) + offsetY * Math.cos(angle);
                    eye.x = seg.x + rotatedX * size;
                    eye.y = seg.y + rotatedY * size;
                    eye.setScale(size);
                    eye.setDepth(12);
                });
                
                // Update wings
                const lWing = seg.parrotFeatures[5];
                const rWing = seg.parrotFeatures[6];
                const wingAngle = angle + Math.PI/2;
                lWing.x = seg.x + Math.cos(angle - Math.PI/2) * (15 * size);
                lWing.y = seg.y + Math.sin(angle - Math.PI/2) * (15 * size);
                lWing.rotation = wingAngle;
                lWing.setScale(size, lWing.scaleY); // Maintain flap scaleY
                
                rWing.x = seg.x + Math.cos(angle + Math.PI/2) * (15 * size);
                rWing.y = seg.y + Math.sin(angle + Math.PI/2) * (15 * size);
                rWing.rotation = wingAngle;
                rWing.setScale(size, rWing.scaleY);
            }
            seg.setDepth(5 - i * 0.01);
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

    // Update mini-map
    if (scene.miniMapDots) {
        scene.miniMapDots.clear();
        const miniMapSize = 400;
        const scale = miniMapSize / (mapRadius * 2);

        data.Parrots.forEach(pData => {
            const isLocal = pData.Id === playerId;
            scene.miniMapDots.fillStyle(isLocal ? 0xffffff : 0xff0000, isLocal ? 1 : 0.6);
            const dotX = pData.X * scale;
            const dotY = pData.Y * scale;
            scene.miniMapDots.fillCircle(dotX, dotY, isLocal ? 4 : 3);
        });
    }

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
            if (fData.Type === 0) { // WORLD_FEATHER
                // Random color for world feathers
                const foodColors = [0x00ffa3, 0x00d1ff, 0xfff500, 0xff005c, 0xff9100, 0x00ff22];
                color = foodColors[Math.floor(Math.random() * foodColors.length)];
            } else if (fData.Type === 1) { // BOOST
                color = 0xffffff;
                radius = 8;
            } else if (fData.Type === 2) { // DEATH
                color = 0xff4d6d;
                radius = 5;
            }
            
            const feather = scene.add.circle(fData.X, fData.Y, radius * fData.Value, color);
            feather.setStrokeStyle(1, 0xffffff, 0.3);
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
    const scene = game.scene.scenes[0];
    if (scene && scene.miniMapContainer) {
        const miniMapSize = 400;
        const miniMapMargin = 30;
        scene.miniMapContainer.setPosition(miniMapMargin, window.innerHeight - miniMapSize - miniMapMargin);
    }
});
