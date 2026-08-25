using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Server.Game;

namespace Server.Hubs;

public class GameHub : Hub
{
    public override Task OnConnectedAsync()
    {
        Form1.Instance?.Log($"Nowe połączenie: {Context.ConnectionId}");
        return base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        Form1.Instance?.Log($"Rozłączono: {Context.ConnectionId}");
        GameEngine.Instance.RemovePlayer(Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }

    public void JoinGame(string username, string skinColor, string skinPattern = "ara")
    {
        Form1.Instance?.Log($"Gracz dołączył: {username} ({Context.ConnectionId})");
        GameEngine.Instance.AddPlayer(Context.ConnectionId, username, skinColor, skinPattern);
    }

    public void UpdateInput(float angle, bool isBoosting)
    {
        GameEngine.Instance.UpdatePlayerInput(Context.ConnectionId, angle, isBoosting);
    }
}
