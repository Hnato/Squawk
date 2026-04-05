namespace SquawkServer;

partial class Form1
{
    /// <summary>
    ///  Required designer variable.
    /// </summary>
    private System.ComponentModel.IContainer components = null;

    /// <summary>
    ///  Clean up any resources being used.
    /// </summary>
    /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null))
        {
            components.Dispose();
        }
        base.Dispose(disposing);
    }

    #region Windows Form Designer generated code

    /// <summary>
    ///  Required method for Designer support - do not modify
    ///  the contents of this method with the code editor.
    /// </summary>
    private void InitializeComponent()
    {
        this.btnServer = new System.Windows.Forms.Button();
        this.btnBots = new System.Windows.Forms.Button();
        this.btnRestart = new System.Windows.Forms.Button();
        this.btnResetDB = new System.Windows.Forms.Button();
        this.lblStatusServer = new System.Windows.Forms.Label();
        this.lblStatusBots = new System.Windows.Forms.Label();
        this.txtLogs = new System.Windows.Forms.TextBox();
        this.SuspendLayout();

        // Form1
        this.BackColor = System.Drawing.Color.FromArgb(30, 30, 30);
        this.ClientSize = new System.Drawing.Size(1000, 600);
        this.ForeColor = System.Drawing.Color.White;
        this.Text = "Squawk Server Pro";
        this.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular);
        this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
        this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
        this.MaximizeBox = false;
        this.Name = "Form1";

        // btnServer (Red)
        this.btnServer.BackColor = System.Drawing.Color.FromArgb(220, 53, 69);
        this.btnServer.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
        this.btnServer.Font = new System.Drawing.Font("Segoe UI Semibold", 10F, System.Drawing.FontStyle.Bold);
        this.btnServer.ForeColor = System.Drawing.Color.White;
        this.btnServer.Location = new System.Drawing.Point(30, 20);
        this.btnServer.Name = "btnServer";
        this.btnServer.Size = new System.Drawing.Size(220, 55);
        this.btnServer.TabIndex = 0;
        this.btnServer.Text = "Włącz Serwer";
        this.btnServer.UseVisualStyleBackColor = false;
        this.btnServer.Click += new System.EventHandler(this.btnServer_Click);

        // btnBots (Yellow)
        this.btnBots.BackColor = System.Drawing.Color.FromArgb(255, 193, 7);
        this.btnBots.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
        this.btnBots.Font = new System.Drawing.Font("Segoe UI Semibold", 10F, System.Drawing.FontStyle.Bold);
        this.btnBots.ForeColor = System.Drawing.Color.Black;
        this.btnBots.Location = new System.Drawing.Point(270, 20);
        this.btnBots.Name = "btnBots";
        this.btnBots.Size = new System.Drawing.Size(220, 55);
        this.btnBots.TabIndex = 1;
        this.btnBots.Text = "Włącz Boty";
        this.btnBots.UseVisualStyleBackColor = false;
        this.btnBots.Click += new System.EventHandler(this.btnBots_Click);

        // btnRestart (Green)
        this.btnRestart.BackColor = System.Drawing.Color.FromArgb(40, 167, 69);
        this.btnRestart.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
        this.btnRestart.Font = new System.Drawing.Font("Segoe UI Semibold", 10F, System.Drawing.FontStyle.Bold);
        this.btnRestart.ForeColor = System.Drawing.Color.White;
        this.btnRestart.Location = new System.Drawing.Point(510, 20);
        this.btnRestart.Name = "btnRestart";
        this.btnRestart.Size = new System.Drawing.Size(220, 55);
        this.btnRestart.TabIndex = 2;
        this.btnRestart.Text = "Restart Serwera";
        this.btnRestart.UseVisualStyleBackColor = false;
        this.btnRestart.Click += new System.EventHandler(this.btnRestart_Click);

        // btnResetDB (Blue)
        this.btnResetDB.BackColor = System.Drawing.Color.FromArgb(0, 123, 255);
        this.btnResetDB.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
        this.btnResetDB.Font = new System.Drawing.Font("Segoe UI Semibold", 10F, System.Drawing.FontStyle.Bold);
        this.btnResetDB.ForeColor = System.Drawing.Color.White;
        this.btnResetDB.Location = new System.Drawing.Point(750, 20);
        this.btnResetDB.Name = "btnResetDB";
        this.btnResetDB.Size = new System.Drawing.Size(220, 55);
        this.btnResetDB.TabIndex = 3;
        this.btnResetDB.Text = "Reset Bazy Danych";
        this.btnResetDB.UseVisualStyleBackColor = false;
        this.btnResetDB.Click += new System.EventHandler(this.btnResetDB_Click);

        // lblStatusServer
        this.lblStatusServer.AutoSize = true;
        this.lblStatusServer.Location = new System.Drawing.Point(30, 85);
        this.lblStatusServer.Name = "lblStatusServer";
        this.lblStatusServer.Size = new System.Drawing.Size(120, 18);
        this.lblStatusServer.Text = "Serwer: Wyłączony";
        this.lblStatusServer.ForeColor = System.Drawing.Color.FromArgb(220, 53, 69);
        this.lblStatusServer.Font = new System.Drawing.Font("Segoe UI Semibold", 10F, System.Drawing.FontStyle.Bold);

        // lblStatusBots
        this.lblStatusBots.AutoSize = true;
        this.lblStatusBots.Location = new System.Drawing.Point(270, 85);
        this.lblStatusBots.Name = "lblStatusBots";
        this.lblStatusBots.Size = new System.Drawing.Size(100, 18);
        this.lblStatusBots.Text = "Boty: Wyłączone";
        this.lblStatusBots.ForeColor = System.Drawing.Color.FromArgb(220, 53, 69);
        this.lblStatusBots.Font = new System.Drawing.Font("Segoe UI Semibold", 10F, System.Drawing.FontStyle.Bold);

        // txtLogs
        this.txtLogs.BackColor = System.Drawing.Color.Black;
        this.txtLogs.BorderStyle = System.Windows.Forms.BorderStyle.None;
        this.txtLogs.ForeColor = System.Drawing.Color.Lime;
        this.txtLogs.Location = new System.Drawing.Point(30, 120);
        this.txtLogs.Multiline = true;
        this.txtLogs.Name = "txtLogs";
        this.txtLogs.ReadOnly = true;
        this.txtLogs.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
        this.txtLogs.Size = new System.Drawing.Size(940, 450);
        this.txtLogs.TabIndex = 4;
        this.txtLogs.Font = new System.Drawing.Font("Consolas", 11F, System.Drawing.FontStyle.Regular);

        this.Controls.Add(this.btnServer);
        this.Controls.Add(this.btnBots);
        this.Controls.Add(this.btnRestart);
        this.Controls.Add(this.btnResetDB);
        this.Controls.Add(this.lblStatusServer);
        this.Controls.Add(this.lblStatusBots);
        this.Controls.Add(this.txtLogs);
        
        this.ResumeLayout(false);
        this.PerformLayout();
    }

    private System.Windows.Forms.Button btnServer;
    private System.Windows.Forms.Button btnBots;
    private System.Windows.Forms.Button btnRestart;
    private System.Windows.Forms.Button btnResetDB;
    private System.Windows.Forms.Label lblStatusServer;
    private System.Windows.Forms.Label lblStatusBots;
    private System.Windows.Forms.TextBox txtLogs;
}
#endregion

