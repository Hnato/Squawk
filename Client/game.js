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
    { 
        name: 'Green',
        base: 0x5CCB00, 
        accent: 0xA8E600, 
        beak: 0xFFD400, 
        wing: 0xFFEA00,
        outline: 0x2D6600
    },
    { 
        name: 'Blue',
        base: 0x2CB7FF, 
        accent: 0x7DD8FF, 
        beak: 0xFF9E00, 
        wing: 0x005EFF,
        outline: 0x165C80
    },
    { 
        name: 'Purple',
        base: 0x8A2BE2, 
        accent: 0xB266FF, 
        beak: 0xFFB347, 
        wing: 0x4B0082,
        outline: 0x451571
    }
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
    const miniMapSize = 350; 
    const miniMapMargin = 20;
    this.miniMapContainer = this.add.container(miniMapMargin, this.scale.height - miniMapSize - miniMapMargin);
    this.miniMapContainer.setScrollFactor(0);
    this.miniMapContainer.setDepth(1000); // Ensure it's on top of everything

    const miniMapBg = this.add.graphics();
    miniMapBg.fillStyle(0x000000, 0.7);
    miniMapBg.fillCircle(miniMapSize/2, miniMapSize/2, miniMapSize/2);
    miniMapBg.lineStyle(3, 0x00ffa3, 0.5);
    miniMapBg.strokeCircle(miniMapSize/2, miniMapSize/2, miniMapSize/2);
    this.miniMapContainer.add(miniMapBg);

    this.miniMapDots = this.add.graphics();
    this.miniMapContainer.add(this.miniMapDots);

    // V13: Debug Label
    this.miniMapDebug = this.add.text(miniMapSize/2, miniMapSize + 5, 'MiniMap: INIT', { fontSize: '10px', fill: '#00ffa3' }).setOrigin(0.5);
    this.miniMapContainer.add(this.miniMapDebug);
    console.log('MiniMap system initialized at:', miniMapMargin, this.scale.height - miniMapSize - miniMapMargin);

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
            const p = players[id];
            
            // V11: Death Feather Particles
            p.segments.forEach((seg, i) => {
                const emitter = scene.add.particles(0, 0, 'parrot_head', {
                    x: seg.x,
                    y: seg.y,
                    speed: { min: 20, max: 100 },
                    scale: { start: 0.15, end: 0 },
                    alpha: { start: 0.8, end: 0 },
                    lifespan: 600,
                    tint: p.color.base,
                    maxParticles: 3
                });
                
                // Cleanup emitter after burst
                scene.time.delayedCall(600, () => emitter.destroy());
                seg.destroy();
            });
            
            p.nameText.destroy();
            delete players[id];
        }
    });

    data.Parrots.forEach(pData => {
        // Create or update player
        if (!players[pData.Id]) {
            let colorIndex = pData.Name.length % COLORS.length;
            if (pData.Id === playerId) colorIndex = 0; 
            
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
                color: COLORS[colorIndex],
                lastEnergy: pData.Energy,
                lastSize: pData.Size || 1,
                currentVisualSize: pData.Size || 1,
                wingRotation: 0
            };
            players[pData.Id].nameText.setAlpha(0);
            scene.tweens.add({
                targets: players[pData.Id].nameText,
                alpha: 0.8,
                duration: 500
            });
        }

        const p = players[pData.Id];
        
        // V12: Smooth Size Transition (Size Jump at 40 energy handled by server pData.Size)
        if (p.currentVisualSize !== pData.Size) {
            scene.tweens.add({
                targets: p,
                currentVisualSize: pData.Size,
                duration: 800,
                ease: 'Cubic.out'
            });
        }
        
        const size = p.currentVisualSize;
        const headHeight = 24 * size;
        
        // V12: Optimization - LOD (Level of Detail)
        // If too many players or far away, simplify rendering
        const distToPlayer = Phaser.Math.Distance.Between(cam.worldView.centerX, cam.worldView.centerY, pData.X, pData.Y);
        const isFar = distToPlayer > 1500;
        const maxSegmentsLOD = isFar ? Math.min(pData.Segments.length, 5) : pData.Segments.length;

        while (p.segments.length < maxSegmentsLOD) {
            const isHead = p.segments.length === 0;
            const segmentIndex = p.segments.length;
            const isTail = segmentIndex >= pData.Segments.length - 3 && pData.Segments.length > 5;
            
            let seg;
            if (isHead) {
                seg = scene.add.container(0, 0);
                
                // Uproszczona grafika dla wydajności
                const headBase = scene.add.ellipse(0, 0, headHeight, headHeight * 1.1, p.color.base);
                headBase.setStrokeStyle(headHeight * 0.1, p.color.outline);
                
                const eyeWhite = scene.add.circle(headHeight * 0.2, -headHeight * 0.1, headHeight * 0.35 / 2, 0xffffff);
                const pupil = scene.add.circle(headHeight * 0.25, -headHeight * 0.1, headHeight * 0.2 / 2, 0x000000);
                
                const beakUpper = scene.add.ellipse(headHeight * 0.5, headHeight * 0.1, headHeight * 0.6, headHeight * 0.4, p.color.beak);
                beakUpper.setStrokeStyle(2, p.color.outline);

                seg.add([headBase, beakUpper, eyeWhite, pupil]);
                seg.headFeatures = { headBase, beakUpper, eyeWhite, pupil };
            } else {
                seg = scene.add.container(0, 0);
                const segWidth = headHeight * 0.8;
                const segHeight = headHeight * 0.6;
                
                const bodyPart = scene.add.rectangle(0, 0, segWidth, segHeight, p.color.base, 1);
                bodyPart.setStrokeStyle(segHeight * 0.1, p.color.outline);
                
                seg.add([bodyPart]);

                if (isTail && !isFar) {
                    const wing = scene.add.ellipse(0, 0, headHeight * 1.2, headHeight * 0.4, p.color.wing);
                    wing.setStrokeStyle(2, p.color.outline);
                    wing.setOrigin(0, 0.5);
                    seg.add(wing);
                    seg.wing = wing;
                    seg.sendToBack(wing);
                }
            }
            
            p.segments.push(seg);
        }

        while (p.segments.length > maxSegmentsLOD) {
            const seg = p.segments.pop();
            seg.destroy();
        }

        // V12: Optimization - Culling (Don't update if far off screen)
        const isVisible = cam.worldView.contains(pData.X, pData.Y) || distToPlayer < 1000;
        
        if (isVisible) {
            p.nameText.setVisible(true);
            p.segments.forEach(s => s.setVisible(true));
            
            p.wingRotation = Math.sin(Date.now() / 200) * 0.2;

            pData.Segments.forEach((pos, i) => {
                if (i >= p.segments.length) return;
                const seg = p.segments[i];
                seg.x = pos.X;
                seg.y = pos.Y;
                
                const angle = i < pData.Segments.length - 1 ? 
                    Math.atan2(pData.Segments[i].Y - pData.Segments[i+1].Y, pData.Segments[i].X - pData.Segments[i+1].X) : 
                    (i > 0 ? Math.atan2(pData.Segments[i-1].Y - pData.Segments[i].Y, pData.Segments[i-1].X - pData.Segments[i].X) : 0);
                
                seg.rotation = angle;
                
                // V11: Boost stretching (10-15%)
                const boostScale = pData.IsBoosting ? 1.15 : 1.0;
                seg.setScale((size / (1 + (pData.Energy/100))) * boostScale);

                if (seg.headFeatures) {
                    if (pData.IsBoosting) {
                        seg.headFeatures.headBase.setStrokeStyle(headHeight * 0.15, 0xffffff, 0.8);
                        
                        // V11: Trail particle in BaseColor during boost
                        if (i === 0 && Math.random() < 0.3) {
                            const trail = scene.add.particles(0, 0, 'parrot_head', {
                                x: seg.x,
                                y: seg.y,
                                speed: 20,
                                scale: { start: 0.1 * size, end: 0 },
                                alpha: { start: 0.4, end: 0 },
                                lifespan: 300,
                                tint: p.color.base,
                                maxParticles: 1
                            });
                            scene.time.delayedCall(300, () => trail.destroy());
                        }
                    } else {
                        seg.headFeatures.headBase.setStrokeStyle(headHeight * 0.1, p.color.outline);
                    }
                }

                if (seg.wing) {
                    seg.wing.rotation = p.wingRotation * (i % 2 === 0 ? 1 : -1);
                }

                seg.setDepth(100 - i);
            });
        } else {
            p.nameText.setVisible(false);
            p.segments.forEach(s => s.setVisible(false));
        }

        // Growth effect / Swallow
        if (pData.Energy > p.lastEnergy) {
            const head = p.segments[0];
            scene.tweens.add({
                targets: head,
                scale: 1.2,
                duration: 100,
                yoyo: true
            });
            // Particles
            const emitter = scene.add.particles(0, 0, 'parrot_head', {
                x: pData.X,
                y: pData.Y,
                speed: { min: 50, max: 150 },
                scale: { start: 0.1, end: 0 },
                lifespan: 400,
                tint: p.color.base,
                maxParticles: 5
            });
        }
        p.lastEnergy = pData.Energy;

        p.nameText.x = pData.X;
        p.nameText.y = pData.Y - 60 * size;
        p.nameText.setDepth(200);

        if (pData.Id === playerId) {
            cam.startFollow(p.segments[0], true, 0.1, 0.1);
            const zoom = Math.max(0.3, 0.8 / size);
            cam.setZoom(Phaser.Math.Linear(cam.zoom, zoom, 0.1));
        }
    });

    // Update mini-map
    if (scene.miniMapDots) {
        scene.miniMapDots.clear();
        const miniMapSize = 350;
        const scale = miniMapSize / (mapRadius * 2);

        // V13: Diagnostic log (once per 100 frames)
        if (Date.now() % 100 < 20) {
            if (scene.miniMapDebug) {
                scene.miniMapDebug.setText(`Map: ${data.Parrots.length}P, ${data.Feathers.length}F`);
                if (!scene.miniMapContainer.visible) console.warn('MiniMap container is hidden!');
                if (scene.miniMapContainer.alpha < 0.1) console.warn('MiniMap container is transparent!');
            }
        }

        // Draw Feathers (Food) - Optimized: only draw larger/special ones or subset
        data.Feathers.forEach((fData, index) => {
            // Optimization: Draw every 2nd feather if count is high (> 200)
            if (data.Feathers.length > 200 && index % 2 !== 0) return;
            
            let color = 0xaaaaaa;
            if (fData.Type === 0) color = 0x00ffa3;
            else if (fData.Type === 1) color = 0xffffff;
            else if (fData.Type === 2) color = 0xff4d6d;

            const dotX = fData.X * scale;
            const dotY = fData.Y * scale;
            
            // Only draw if within minimap circle bounds (approx)
            const distSq = Math.pow(dotX - miniMapSize/2, 2) + Math.pow(dotY - miniMapSize/2, 2);
            if (distSq <= Math.pow(miniMapSize/2, 2)) {
                scene.miniMapDots.fillStyle(color, 0.4);
                scene.miniMapDots.fillCircle(dotX, dotY, 1.5);
            }
        });

        // Draw Parrots (Players/Bots)
        data.Parrots.forEach(pData => {
            const isLocal = pData.Id === playerId;
            const dotX = pData.X * scale;
            const dotY = pData.Y * scale;
            
            const distSq = Math.pow(dotX - miniMapSize/2, 2) + Math.pow(dotY - miniMapSize/2, 2);
            if (distSq <= Math.pow(miniMapSize/2, 2)) {
                scene.miniMapDots.fillStyle(isLocal ? 0xffffff : 0xff0000, isLocal ? 1 : 0.8);
                scene.miniMapDots.fillCircle(dotX, dotY, isLocal ? 5 : 4);
                
                // V13: Direction indicator for players
                if (isLocal || pData.Energy > 100) {
                    scene.miniMapDots.lineStyle(1, 0xffffff, 0.5);
                    scene.miniMapDots.lineBetween(
                        dotX, dotY, 
                        dotX + Math.cos(pData.Direction) * 8, 
                        dotY + Math.sin(pData.Direction) * 8
                    );
                }
            }
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
        const miniMapSize = 350;
        const miniMapMargin = 20;
        scene.miniMapContainer.setPosition(miniMapMargin, scene.scale.height - miniMapSize - miniMapMargin);
    }
});
