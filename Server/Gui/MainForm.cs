using System;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace Squawk.Server.Gui
{
    public class MainForm : Form
    {
        private readonly string _url;
        private WebView2 _webView = new WebView2();

        public MainForm(string url)
        {
            _url = url;
            Text = "Squawk";
            StartPosition = FormStartPosition.CenterScreen;
            Width = 1200;
            Height = 800;
            var iconPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Client", "logo.ico");
            if (System.IO.File.Exists(iconPath))
            {
                Icon = new System.Drawing.Icon(iconPath);
            }
            InitializeComponent();
        }

        private async void InitializeComponent()
        {
            try
            {
                _webView.Dock = DockStyle.Fill;
                Controls.Add(_webView);
                await _webView.EnsureCoreWebView2Async(null);
                _webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
                _webView.CoreWebView2.Settings.IsStatusBarEnabled = false;
                _webView.Source = new Uri(_url);
            }
            catch
            {
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = _url,
                        UseShellExecute = true
                    });
                }
                catch
                {
                }
                var link = new LinkLabel
                {
                    Text = _url,
                    Dock = DockStyle.Fill,
                    TextAlign = System.Drawing.ContentAlignment.MiddleCenter
                };
                link.LinkClicked += (_, __) =>
                {
                    try
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = _url,
                            UseShellExecute = true
                        });
                    }
                    catch
                    {
                    }
                };
                Controls.Add(link);
            }
        }
    }
}
