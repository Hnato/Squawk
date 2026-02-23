using System;
using System.Windows.Forms;
using System.Drawing;

namespace Squawk.Server.Gui
{
    public class MainForm : Form
    {
        private TextBox _logBox = new TextBox();
        private Button _btnGame = new Button();
        private Button _btnNetwork = new Button();
        private Button _btnBots = new Button();
        private Image _iconOn = null!;
        private Image _iconOff = null!;

        public MainForm()
        {
            Text = "Squawk Server Panel";
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(900, 700);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            var iconPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Client", "logo.ico");
            if (System.IO.File.Exists(iconPath))
            {
                Icon = new Icon(iconPath);
            }
            InitializeComponent();
            Program.Log.Subscribe(AppendLogLine);
            Program.GameStateChanged += OnGameStateChanged;
            Program.NetworkStateChanged += OnNetworkStateChanged;
            Program.BotsStateChanged += OnBotsStateChanged;
            UpdateButtonsInitial();
        }

        private void InitializeComponent()
        {
            _logBox.Multiline = true;
            _logBox.ReadOnly = true;
            _logBox.ScrollBars = ScrollBars.Vertical;
            _logBox.BorderStyle = BorderStyle.None;
            _logBox.BackColor = Color.Black;
            _logBox.ForeColor = Color.White;

            _iconOn = CreateIcon(Color.Green);
            _iconOff = CreateIcon(Color.Yellow);

            BackColor = Color.Black;
            ForeColor = Color.White;
            Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point);

            ConfigureButton(_btnGame);
            ConfigureButton(_btnNetwork);
            ConfigureButton(_btnBots);

            _btnGame.Click += (_, __) =>
            {
                if (Program.IsGameRunning) Program.StopGame(); else Program.StartGame();
            };

            _btnNetwork.Click += (_, __) =>
            {
                if (Program.IsNetworkRunning) Program.StopNetwork(); else Program.StartNetwork();
            };

            _btnBots.Click += (_, __) =>
            {
                Program.SetBotsEnabled(!Program.AreBotsEnabled);
            };

            var headerPanel = new Panel();
            headerPanel.Dock = DockStyle.Top;
            headerPanel.Height = 80;
            headerPanel.BackColor = Color.Black;

            var titleLabel = new Label();
            titleLabel.Text = "SQUAWK SERVER PANEL";
            titleLabel.ForeColor = Color.White;
            titleLabel.Font = new Font("Segoe UI", 18F, FontStyle.Bold, GraphicsUnit.Point);
            titleLabel.AutoSize = true;
            titleLabel.Location = new Point(20, 15);

            var subtitleLabel = new Label();
            subtitleLabel.Text = "Kontrola serwera gry, serwera WWW i botów";
            subtitleLabel.ForeColor = Color.Yellow;
            subtitleLabel.Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point);
            subtitleLabel.AutoSize = true;
            subtitleLabel.Location = new Point(22, 48);

            headerPanel.Controls.Add(titleLabel);
            headerPanel.Controls.Add(subtitleLabel);

            var buttonsPanel = new Panel();
            buttonsPanel.Dock = DockStyle.Top;
            buttonsPanel.Height = 90;
            buttonsPanel.BackColor = Color.Black;

            _btnGame.Width = 220;
            _btnNetwork.Width = 220;
            _btnBots.Width = 220;
            _btnGame.Height = 50;
            _btnNetwork.Height = 50;
            _btnBots.Height = 50;

            var centerPanel = new Panel();
            centerPanel.Width = 3 * 220 + 2 * 20;
            centerPanel.Height = 50;
            centerPanel.Left = (ClientSize.Width - centerPanel.Width) / 2;
            centerPanel.Top = 20;
            centerPanel.Anchor = AnchorStyles.Top;

            _btnGame.Location = new Point(0, 0);
            _btnNetwork.Location = new Point(220 + 20, 0);
            _btnBots.Location = new Point(2 * (220 + 20), 0);

            centerPanel.Controls.Add(_btnGame);
            centerPanel.Controls.Add(_btnNetwork);
            centerPanel.Controls.Add(_btnBots);

            buttonsPanel.Controls.Add(centerPanel);

            var logContainer = new Panel();
            logContainer.Dock = DockStyle.Fill;
            logContainer.BackColor = Color.Black;
            logContainer.Padding = new Padding(16, 10, 16, 16);

            var logLabel = new Label();
            logLabel.Text = "Log zdarzeń";
            logLabel.ForeColor = Color.White;
            logLabel.Font = new Font("Segoe UI", 11F, FontStyle.Bold, GraphicsUnit.Point);
            logLabel.AutoSize = true;
            logLabel.Dock = DockStyle.Top;

            var logBorder = new Panel();
            logBorder.Dock = DockStyle.Fill;
            logBorder.BackColor = Color.White;
            logBorder.Padding = new Padding(1);

            var logInner = new Panel();
            logInner.Dock = DockStyle.Fill;
            logInner.BackColor = Color.Black;

            _logBox.Dock = DockStyle.Fill;

            logInner.Controls.Add(_logBox);
            logBorder.Controls.Add(logInner);

            logContainer.Controls.Add(logBorder);
            logContainer.Controls.Add(logLabel);

            Controls.Add(logContainer);
            Controls.Add(buttonsPanel);
            Controls.Add(headerPanel);
            UpdateButtonVisuals(_btnGame, Program.IsGameRunning, "Gra");
            UpdateButtonVisuals(_btnNetwork, Program.IsNetworkRunning, "Sieć");
            UpdateButtonVisuals(_btnBots, Program.AreBotsEnabled, "Boty");
        }

        private void ConfigureButton(Button btn)
        {
            btn.FlatStyle = FlatStyle.Flat;
            btn.FlatAppearance.BorderSize = 0;
            btn.BackColor = Color.Yellow;
            btn.ForeColor = Color.Black;
            btn.Font = new Font("Segoe UI", 11F, FontStyle.Bold, GraphicsUnit.Point);
        }

        private void AppendLogLine(string line)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action<string>(AppendLogLine), line);
                return;
            }
            _logBox.AppendText(line);
        }

        private void OnGameStateChanged(bool on)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action<bool>(OnGameStateChanged), on);
                return;
            }
            UpdateButtonVisuals(_btnGame, on, "Gra");
        }

        private void OnNetworkStateChanged(bool on)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action<bool>(OnNetworkStateChanged), on);
                return;
            }
            UpdateButtonVisuals(_btnNetwork, on, "Sieć");
        }

        private void OnBotsStateChanged(bool on)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action<bool>(OnBotsStateChanged), on);
                return;
            }
            UpdateButtonVisuals(_btnBots, on, "Boty");
        }

        private void UpdateButtonsInitial()
        {
            UpdateButtonVisuals(_btnGame, Program.IsGameRunning, "Gra");
            UpdateButtonVisuals(_btnNetwork, Program.IsNetworkRunning, "Sieć");
            UpdateButtonVisuals(_btnBots, Program.AreBotsEnabled, "Boty");
        }

        private void UpdateButtonVisuals(Button btn, bool on, string name)
        {
            btn.Text = on ? $"{name}: Włączone" : $"{name}: Wyłączone";
            btn.BackColor = on ? Color.Green : Color.Yellow;
            btn.ForeColor = Color.Black;
            btn.Image = on ? _iconOn : _iconOff;
            btn.ImageAlign = ContentAlignment.MiddleLeft;
        }

        private Image CreateIcon(Color color)
        {
            var bmp = new Bitmap(16, 16);
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Black);
                using (var b = new SolidBrush(color))
                {
                    g.FillEllipse(b, 2, 2, 12, 12);
                }
                using (var p = new Pen(Color.White))
                {
                    g.DrawEllipse(p, 2, 2, 12, 12);
                }
            }
            return bmp;
        }
    }
}
