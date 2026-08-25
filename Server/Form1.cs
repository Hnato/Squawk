using System;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.SignalR;
using Server.Data;
using Server.Game;

namespace Server;

public partial class Form1 : Form
{
    private const string ClientCorsPolicy = "ClientCors";
    private WebApplication? _app;
    private bool _botsEnabled = true;

    private enum ServerStatus { Stopped, Starting, Running, Stopping }
    private ServerStatus _currentStatus = ServerStatus.Stopped;

    public static Form1? Instance { get; private set; }

    public Form1()
    {
        Instance = this;
        InitializeComponent();

        this.Text = "Squawk Game Engine by Hnato";

        try
        {
            var iconPath = System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "logo.ico");
            if (System.IO.File.Exists(iconPath))
            {
                this.Icon = new Icon(iconPath);
            }
            else
            {
                using var stream = typeof(Form1).Assembly.GetManifestResourceStream("Server.logo.ico");
                if (stream != null)
                {
                    this.Icon = new Icon(stream);
                }
                else
                {
                    var exePath = System.IO.Path.Combine(System.AppContext.BaseDirectory, "Squawk.exe");
                    if (System.IO.File.Exists(exePath))
                    {
                        this.Icon = Icon.ExtractAssociatedIcon(exePath);
                    }
                }
            }
        }
        catch { }

        ApplyRoundedCorners(btnServer, 15);
        ApplyRoundedCorners(btnBots, 15);
        ApplyRoundedCorners(btnRestart, 15);
        ApplyRoundedCorners(btnResetDB, 15);

        UpdateUI();
    }

    private void Form1_Load(object sender, EventArgs e)
    {
        Log("Serwer Squawk uruchamiany...");
        try
        {
            using var db = new SquawkDbContext();
            db.Database.EnsureCreated();
            Log("Baza danych SQLite załadowana (squawk.db).");
        }
        catch (Exception ex)
        {
            Log($"Błąd bazy danych: {ex.Message}");
        }
    }

    private void UpdateUI()
    {
        switch (_currentStatus)
        {
            case ServerStatus.Stopped:
                btnServer.Text = "Włącz Serwer";
                btnServer.Enabled = true;
                lblStatusServer.Text = "Serwer: Wyłączony";
                lblStatusServer.ForeColor = Color.FromArgb(220, 53, 69);
                btnServer.BackColor = Color.FromArgb(220, 53, 69);
                break;
            case ServerStatus.Starting:
                btnServer.Text = "Uruchamianie...";
                btnServer.Enabled = false;
                break;
            case ServerStatus.Running:
                btnServer.Text = "Wyłącz Serwer";
                btnServer.Enabled = true;
                lblStatusServer.Text = "Serwer: Włączony";
                lblStatusServer.ForeColor = Color.Lime;
                btnServer.BackColor = Color.FromArgb(180, 40, 55);
                break;
            case ServerStatus.Stopping:
                btnServer.Text = "Zamykanie...";
                btnServer.Enabled = false;
                break;
        }

        lblStatusBots.Text = $"Boty: {(_botsEnabled ? "Włączone" : "Wyłączone")}";
        lblStatusBots.ForeColor = _botsEnabled ? Color.Lime : Color.FromArgb(220, 53, 69);
        btnBots.BackColor = _botsEnabled ? Color.FromArgb(200, 150, 0) : Color.FromArgb(255, 193, 7);
        btnBots.Text = _botsEnabled ? "Wyłącz Boty" : "Włącz Boty";
    }

    private void ApplyRoundedCorners(Button btn, int radius)
    {
        btn.FlatStyle = FlatStyle.Flat;
        btn.FlatAppearance.BorderSize = 0;
        
        var path = new System.Drawing.Drawing2D.GraphicsPath();
        path.AddArc(0, 0, radius, radius, 180, 90);
        path.AddArc(btn.Width - radius, 0, radius, radius, 270, 90);
        path.AddArc(btn.Width - radius, btn.Height - radius, radius, radius, 0, 90);
        path.AddArc(0, btn.Height - radius, radius, radius, 90, 90);
        path.CloseAllFigures();
        
        btn.Region = new Region(path);
    }

    public void Log(string message)
    {
        if (InvokeRequired)
        {
            Invoke(new Action<string>(Log), message);
            return;
        }
        txtLogs.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
        txtLogs.SelectionStart = txtLogs.Text.Length;
        txtLogs.ScrollToCaret();
    }

    private async void btnToggleGame_Click(object sender, EventArgs e)
    {
        if (_currentStatus == ServerStatus.Stopped)
        {
            _currentStatus = ServerStatus.Starting;
            UpdateUI();
            
            Log("Uruchamianie serwera gry...");
            await StartServer();
            _currentStatus = ServerStatus.Running;
            UpdateUI();
        }
        else if (_currentStatus == ServerStatus.Running)
        {
            _currentStatus = ServerStatus.Stopping;
            UpdateUI();

            Log("Zatrzymywanie serwera gry...");
            if (_app != null)
            {
                GameEngine.Instance.Stop();
                await _app.StopAsync();
                await _app.DisposeAsync();
                _app = null;
            }
            Log("Serwer gry zatrzymany.");
            
            _currentStatus = ServerStatus.Stopped;
            UpdateUI();
        }
    }

    private async Task StartServer()
    {
        var builder = WebApplication.CreateBuilder(new string[0]);

        builder.Services.AddSignalR()
            .AddJsonProtocol(options =>
            {
                options.PayloadSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
            });
        builder.Services.AddCors(options =>
        {
            options.AddPolicy(ClientCorsPolicy, policy =>
            {
                policy.AllowAnyHeader()
                      .AllowAnyMethod()
                      .SetIsOriginAllowed(_ => true)
                      .AllowCredentials();
            });
        });

        builder.WebHost.ConfigureKestrel(kestrelOptions =>
        {
            kestrelOptions.Listen(System.Net.IPAddress.Any, 5007); // Nasłuchiwanie na 0.0.0.0:5007 (LAN / wszystkie interfejsy)
            kestrelOptions.Listen(System.Net.IPAddress.Loopback, 5006); // Nasłuchiwanie na 127.0.0.1:5006 (lokalnie)
        });

        _app = builder.Build();
        _app.UseCors(ClientCorsPolicy);

        Microsoft.Extensions.FileProviders.IFileProvider? fileProvider = null;

        try
        {
            var embeddedProvider = new Microsoft.Extensions.FileProviders.ManifestEmbeddedFileProvider(typeof(Form1).Assembly, "wwwroot");
            if (embeddedProvider.GetFileInfo("index.html").Exists)
            {
                fileProvider = embeddedProvider;
                Log("Serwowanie plików klienta z wbudowanych zasobów pliku EXE.");
            }
        }
        catch { }

        if (fileProvider == null)
        {
            var clientPath = ResolveClientPath();
            if (System.IO.Directory.Exists(clientPath))
            {
                fileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(clientPath);
                Log($"Serwowanie plików statycznych klienta z: {clientPath}");
            }
        }

        if (fileProvider != null)
        {
            _app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = fileProvider });
            _app.UseStaticFiles(new StaticFileOptions { FileProvider = fileProvider });
            _app.MapFallbackToFile("index.html", new StaticFileOptions { FileProvider = fileProvider });
        }
        else
        {
            Log("UWAGA: Nie znaleziono plików klienta.");
        }

        _app.MapEndpoints();
        _app.MapHub<Hubs.GameHub>("/gamehub");

        await _app.StartAsync();
        
        var hubContext = _app.Services.GetRequiredService<IHubContext<Hubs.GameHub>>();
        GameEngine.Instance.Initialize(hubContext);
        GameEngine.Instance.SetBotsEnabled(_botsEnabled);
        GameEngine.Instance.Start();
        
        Log("Serwer rozgłasza grę na 0.0.0.0:5007 (wszystkie interfejsy) oraz 127.0.0.1:5006.");
        try
        {
            var hostName = System.Net.Dns.GetHostName();
            var ips = System.Net.Dns.GetHostAddresses(hostName)
                .Where(ip => ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                .Select(ip => ip.ToString());
            foreach (var ip in ips)
            {
                Log($"Adres w sieci LAN dla innych urządzeń: http://{ip}:5007");
            }
        }
        catch { }
    }

    private static string ResolveClientPath()
    {
        var searchRoots = new[]
        {
            AppDomain.CurrentDomain.BaseDirectory,
            System.IO.Directory.GetCurrentDirectory(),
            System.IO.Path.GetFullPath(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..")),
        };

        foreach (var root in searchRoots.Distinct())
        {
            var candidates = new[]
            {
                System.IO.Path.Combine(root, "wwwroot"),
                System.IO.Path.Combine(root, "dist"),
                System.IO.Path.Combine(root, "Client", "dist"),
                System.IO.Path.Combine(root, "ClientDist"),
            };

            foreach (var path in candidates)
            {
                if (System.IO.Directory.Exists(path) && System.IO.File.Exists(System.IO.Path.Combine(path, "index.html")))
                {
                    return path;
                }
            }
        }

        return System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "Client", "dist");
    }

    private void btnToggleBots_Click(object sender, EventArgs e)
    {
        _botsEnabled = !_botsEnabled;
        GameEngine.Instance.SetBotsEnabled(_botsEnabled);
        Log($"Boty zostały {(_botsEnabled ? "włączone" : "wyłączone")}.");
        UpdateUI();
    }

    private void btnClearDb_Click(object sender, EventArgs e)
    {
        var result = MessageBox.Show("Czy na pewno chcesz wyczyścić bazę danych?", "Potwierdzenie", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
        if (result == DialogResult.Yes)
        {
            try
            {
                using var db = new SquawkDbContext();
                db.Database.EnsureDeleted();
                db.Database.EnsureCreated();
                Log("Baza danych została wyczyszczona pomyślnie.");
            }
            catch (Exception ex)
            {
                Log($"Błąd podczas czyszczenia bazy: {ex.Message}");
            }
        }
    }

    private void btnRestart_Click(object sender, EventArgs e)
    {
        Application.Restart();
        Environment.Exit(0);
    }
}
