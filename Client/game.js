const config = {
    type: Phaser.AUTO,
    parent: 'game-container',
    width: window.innerWidth,
    height: window.innerHeight,
    backgroundColor: '#3498db',
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
let feathers = {};
let cursors;
let isBoosting = false;
let mapWidth = 3000;
let mapHeight = 3000;
let cam;

function preload() {
    this.load.image('parrot_head', 'logo.png'); // Using logo as head for now
}

function create() {
    cam = this.cameras.main;
    cursors = this.input.keyboard.createCursorKeys();
    
    // Background pattern or tiles could be added here
    const grid = this.add.grid(mapWidth/2, mapHeight/2, mapWidth, mapHeight, 100, 100, 0x3498db, 1, 0x2980b9);
    
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
            mapWidth = data.MapWidth;
            mapHeight = data.MapHeight;
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
            players[pData.Id] = {
                segments: [],
                nameText: scene.add.text(0, 0, pData.Name, { fontSize: '14px', fill: '#fff' }).setOrigin(0.5)
            };
        }

        const p = players[pData.Id];
        const size = 1 + (pData.Energy / 100);

        // Update segments
        while (p.segments.length < pData.Segments.length) {
            const seg = scene.add.circle(0, 0, 15, 0xffffff);
            p.segments.push(seg);
        }
        while (p.segments.length > pData.Segments.length) {
            p.segments.pop().destroy();
        }

        pData.Segments.forEach((pos, i) => {
            const seg = p.segments[i];
            seg.x = pos.X;
            seg.y = pos.Y;
            seg.setScale(size);
            if (i === 0) {
                seg.setFillStyle(pData.Id === playerId ? 0x2ecc71 : 0xe74c3c);
            } else {
                seg.setFillStyle(pData.Id === playerId ? 0x27ae60 : 0xc0392b);
            }
        });

        p.nameText.x = pData.X;
        p.nameText.y = pData.Y - 40 * size;

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
            if (fData.Type === 1) color = 0xffffff; // BOOST
            if (fData.Type === 2) color = 0xe67e22; // DEATH
            
            feathers[fData.Id] = scene.add.circle(fData.X, fData.Y, 5 * fData.Value, color);
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
