<!DOCTYPE html>
<html lang="pl">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>Squawk - Multiplayer Parrot Game</title>
    <link rel="icon" type="image/x-icon" href="/ico/logo.ico">
    <link rel="apple-touch-icon" href="/img/logo.png">
    <link rel="manifest" href="/manifest.json">
    <meta name="theme-color" content="#121214">
    <style>
        body {
            margin: 0;
            padding: 0;
            overflow: hidden;
            background-color: #0a0a0b;
            font-family: 'Inter', -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
            color: #f8f9fa;
        }
        #ui {
            position: absolute;
            top: 24px;
            right: 24px;
            background: rgba(18, 18, 20, 0.7);
            backdrop-filter: blur(12px);
            -webkit-backdrop-filter: blur(12px);
            padding: 20px;
            border-radius: 16px;
            border: 1px solid rgba(255, 255, 255, 0.1);
            pointer-events: none;
            box-shadow: 0 8px 32px rgba(0, 0, 0, 0.4);
            min-width: 180px;
        }
        #ui h3 {
            margin: 0 0 12px 0;
            font-size: 14px;
            font-weight: 600;
            text-align: left;
            color: rgba(255, 255, 255, 0.5);
            text-transform: uppercase;
            letter-spacing: 1.5px;
        }
        #leaderboard div {
            margin-bottom: 8px;
            font-size: 14px;
            display: flex;
            justify-content: space-between;
            color: #e9ecef;
            padding-bottom: 4px;
        }
        #login-overlay {
            position: absolute;
            top: 0;
            left: 0;
            width: 100%;
            height: 100%;
            background: radial-gradient(circle at center, rgba(18, 18, 20, 0.4) 0%, rgba(10, 10, 11, 0.9) 100%);
            display: flex;
            align-items: center;
            justify-content: center;
            z-index: 100;
        }
        #login {
            background: rgba(18, 18, 20, 0.85);
            backdrop-filter: blur(24px);
            -webkit-backdrop-filter: blur(24px);
            padding: 48px;
            border-radius: 32px;
            text-align: center;
            box-shadow: 0 32px 80px rgba(0, 0, 0, 0.8);
            border: 1px solid rgba(255, 255, 255, 0.1);
            width: 360px;
            animation: fadeIn 0.6s cubic-bezier(0.16, 1, 0.3, 1);
        }
        @keyframes fadeIn {
            from { opacity: 0; transform: scale(0.9) translateY(20px); }
            to { opacity: 1; transform: scale(1) translateY(0); }
        }
        #login h1 {
            margin-top: 0;
            margin-bottom: 12px;
            font-size: 48px;
            font-weight: 900;
            background: linear-gradient(135deg, #00ff88 0%, #00bdff 100%);
            -webkit-background-clip: text;
            -webkit-text-fill-color: transparent;
            letter-spacing: -2px;
        }
        #login p {
            color: rgba(255, 255, 255, 0.5);
            margin-bottom: 32px;
            font-size: 14px;
        }
        .input-group {
            margin-bottom: 20px;
            text-align: left;
        }
        .input-group label {
            display: block;
            font-size: 12px;
            color: rgba(255, 255, 255, 0.4);
            margin-bottom: 8px;
            margin-left: 4px;
            text-transform: uppercase;
            letter-spacing: 1px;
        }
        #login input {
            padding: 16px 20px;
            width: 100%;
            background: rgba(255, 255, 255, 0.04);
            border: 1px solid rgba(255, 255, 255, 0.08);
            border-radius: 16px;
            color: #fff;
            font-size: 16px;
            outline: none;
            transition: all 0.3s ease;
            box-sizing: border-box;
        }
        #login input:focus {
            background: rgba(255, 255, 255, 0.08);
            border-color: #00ff88;
            box-shadow: 0 0 20px rgba(0, 255, 136, 0.15);
        }
        #login button {
            padding: 18px 32px;
            width: 100%;
            background: linear-gradient(135deg, #00ff88 0%, #00bdff 100%);
            color: #000;
            border: none;
            border-radius: 16px;
            cursor: pointer;
            font-size: 18px;
            font-weight: 800;
            margin-top: 12px;
            transition: all 0.3s cubic-bezier(0.16, 1, 0.3, 1);
            box-shadow: 0 10px 30px rgba(0, 255, 136, 0.2);
        }
        #login button:hover {
            transform: translateY(-3px) scale(1.02);
            box-shadow: 0 15px 40px rgba(0, 255, 136, 0.3);
            filter: brightness(1.1);
        }
        #login button:active {
            transform: translateY(-1px) scale(1);
        }
    </style>
    <script src="https://cdn.jsdelivr.net/npm/phaser@3.60.0/dist/phaser.min.js"></script>
</head>
<body>
    <div id="game-container"></div>
    <div id="ui" style="display: none;">
        <h3>Ranking</h3>
        <div id="leaderboard"></div>
    </div>
    <div id="login-overlay">
        <div id="login">
            <h1>Squawk</h1>
            <p>Wejdź do świata bitew papug</p>
            <div class="input-group">
                <label>Nazwa gracza</label>
                <input type="text" id="playerName" placeholder="Podaj swój nick..." maxlength="15">
            </div>
            <button id="startBtn">DOŁĄCZ DO GRY</button>
        </div>
    </div>
    <script src="game.js"></script>
</body>
</html>