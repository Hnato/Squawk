import * as signalR from '@microsoft/signalr';

export type Vector2D = {
  x: number;
  y: number;
};

export type RemotePlayer = {
  id: string;
  username: string;
  skinColor: string;
  isBot: boolean;
  body: Vector2D[];
  angle: number;
  isBoosting: boolean;
  score: number;
  isDead: boolean;
};

export type Food = {
  id: number;
  position: Vector2D;
  color: string;
  value: number;
};

export type GameState = {
  players: RemotePlayer[];
  food: Food[];
};

type NetworkManagerOptions = {
  username: string;
  skinColor: string;
  onConnected: () => void;
  onConnectionError: (message: string) => void;
  onStateUpdate: (state: GameState) => void;
  onGameOver: (score: number) => void;
};

export class NetworkManager {
  private readonly connection: signalR.HubConnection;
  private readonly options: NetworkManagerOptions;

  constructor(options: NetworkManagerOptions) {
    this.options = options;
    this.connection = new signalR.HubConnectionBuilder()
      .withUrl('/gamehub')
      .withAutomaticReconnect()
      .build();

    this.connection.on('UpdateState', (state: GameState) => {
      this.options.onStateUpdate(state);
    });

    this.connection.on('GameOver', (score: number) => {
      this.options.onGameOver(score);
    });

    this.connection.onclose((error) => {
      if (error) {
        this.options.onConnectionError('Polaczenie z serwerem zostalo przerwane.');
      }
    });
  }

  public async connect() {
    try {
      await this.connection.start();
      await this.connection.invoke(
        'JoinGame',
        this.options.username,
        this.options.skinColor,
      );
      this.options.onConnected();
    } catch (caughtError) {
      const message =
        caughtError instanceof Error
          ? caughtError.message
          : 'Nie udalo sie polaczyc z serwerem gry.';
      this.options.onConnectionError(message);
    }
  }

  public sendInput(angle: number, isBoosting: boolean) {
    if (this.connection.state !== signalR.HubConnectionState.Connected) {
      return;
    }

    void this.connection.invoke('UpdateInput', angle, isBoosting);
  }

  public disconnect() {
    void this.connection.stop();
  }
}
