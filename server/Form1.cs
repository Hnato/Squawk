namespace SquawkServer;

using System.Windows.Forms;
using SquawkServer.Models;

public partial class Form1 : Form
{
    private enum ServerStatus { Stopped, Starting, Running, Stopping }
    private ServerStatus _currentStatus = ServerStatus.Stopped;

    private readonly IGameEngine _engine;
    private readonly DatabaseManager _db;
    private readonly WebSocketServer _wsServer;
    private readonly System.Windows.Forms.Timer _tickTimer;

    public Form1()
    {
        InitializeComponent();

        try
        {
            // Set window icon from resources
            using var stream = GetType().Assembly.GetManifestResourceStream("SquawkServer.client.ico.logo.ico");
            if (stream != null)
            {
                this.Icon = new Icon(stream);
            }
        }
        catch { }
        
        _engine = new GameEngine();
        _engine.OnLog += Log;
        
        _db = new DatabaseManager();
        
        _wsServer = new WebSocketServer(_engine, _db);
        _wsServer.OnLog += Log;
        
        _tickTimer = new System.Windows.Forms.Timer();
        _tickTimer.Interval = 16; // ~60 FPS
        _tickTimer.Tick += (s, e) => _engine.Tick();

        // Apply rounded corners to buttons
        ApplyRoundedCorners(btnServer, 15);
        ApplyRoundedCorners(btnBots, 15);
        ApplyRoundedCorners(btnRestart, 15);
        ApplyRoundedCorners(btnResetDB, 15);
        
        UpdateUI();

        Log("Squawk Server Initialized.");
    }

    private void UpdateUI()
    {
        switch (_currentStatus)
        {
            case ServerStatus.Stopped:
                btnServer.Text = "Włącz Serwer";
                btnServer.Enabled = true;
                lblStatusServer.Text = "Serwer: Wyłączony";
                lblStatusServer.ForeColor = System.Drawing.Color.FromArgb(220, 53, 69);
                btnServer.BackColor = System.Drawing.Color.FromArgb(220, 53, 69);
                break;
            case ServerStatus.Starting:
                btnServer.Text = "Uruchamianie...";
                btnServer.Enabled = false;
                break;
            case ServerStatus.Running:
                btnServer.Text = "Wyłącz Serwer";
                btnServer.Enabled = true;
                lblStatusServer.Text = "Serwer: Włączony";
                lblStatusServer.ForeColor = System.Drawing.Color.Lime;
                btnServer.BackColor = System.Drawing.Color.FromArgb(180, 40, 55);
                break;
            case ServerStatus.Stopping:
                btnServer.Text = "Zamykanie...";
                btnServer.Enabled = false;
                break;
        }
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

    private void Log(string message)
    {
        if (txtLogs.InvokeRequired)
        {
            txtLogs.Invoke(new Action(() => Log(message)));
            return;
        }
        txtLogs.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
    }

    private async void btnServer_Click(object sender, EventArgs e)
    {
        if (_currentStatus == ServerStatus.Stopped)
        {
            _currentStatus = ServerStatus.Starting;
            UpdateUI();

            try 
            {
                await System.Threading.Tasks.Task.Run(() => {
                    _engine.Start();
                    _wsServer.Start();
                });
                _tickTimer.Start();
                _currentStatus = ServerStatus.Running;
            }
            catch (Exception ex)
            {
                _engine.Stop();
                _wsServer.Stop();
                _tickTimer.Stop();
                _currentStatus = ServerStatus.Stopped;
                MessageBox.Show($"Błąd podczas uruchamiania serwera: {ex.Message}", "Błąd Startu", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Log($"Startup failed: {ex.Message}");
            }
            UpdateUI();
        }
        else if (_currentStatus == ServerStatus.Running)
        {
            _currentStatus = ServerStatus.Stopping;
            UpdateUI();

            await System.Threading.Tasks.Task.Run(() => {
                _engine.Stop();
                _wsServer.Stop();
            });
            _tickTimer.Stop();
            
            _currentStatus = ServerStatus.Stopped;
            UpdateUI();
        }
    }

    private void btnBots_Click(object sender, EventArgs e)
    {
        _engine.BotsEnabled = !_engine.BotsEnabled;
        btnBots.Text = _engine.BotsEnabled ? "Wyłącz Boty" : "Włącz Boty";
        lblStatusBots.Text = $"Boty: {(_engine.BotsEnabled ? "Włączone" : "Wyłączone")}";
        lblStatusBots.ForeColor = _engine.BotsEnabled ? System.Drawing.Color.Lime : System.Drawing.Color.FromArgb(220, 53, 69);
        btnBots.BackColor = _engine.BotsEnabled ? System.Drawing.Color.FromArgb(200, 150, 0) : System.Drawing.Color.FromArgb(255, 193, 7);
        Log($"Bots {(_engine.BotsEnabled ? "enabled" : "disabled")}.");
    }

    private void btnRestart_Click(object sender, EventArgs e)
    {
        Log("Restarting server...");
        if (_currentStatus == ServerStatus.Running)
        {
            btnServer_Click(sender, e); // Stop
        }
        btnServer_Click(sender, e); // Start
    }

    private void btnResetDB_Click(object sender, EventArgs e)
    {
        var result = MessageBox.Show("Czy na pewno chcesz zresetować bazę danych? Wszystkie konta użytkowników zostaną usunięte.", "Potwierdzenie", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
        if (result == DialogResult.Yes)
        {
            _db.ResetDatabase();
            Log("Database reset.");
        }
    }
}
