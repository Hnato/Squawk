using System;
using System.Windows.Forms;

namespace Server;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new Form1());
    }
}