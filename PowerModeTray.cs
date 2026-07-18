// PowerModeTray - system tray switcher for Windows power plans.
// Wraps `powercfg /setactive` behind a NotifyIcon menu. No external deps; build with build.bat.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace PowerModeTray
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.Run(new TrayAppContext()); // ApplicationContext = no main window, tray-only.
        }
    }

    // One power plan option in the tray menu.
    internal sealed class PowerMode
    {
        public string Label;         // menu text
        public string LookupName;    // name to match against `powercfg /list` output
        public string FallbackGuid;  // well-known GUID to use if not found by name
        public Color IconColor;      // battery fill color for this mode
        public double Level;         // battery fill level 0..1 (visual intensity, low -> high)
        public string Guid;          // resolved GUID actually used with /setactive
        public Icon TrayIcon;        // pre-rendered icon for this mode
        public ToolStripMenuItem MenuItem;
    }

    // Result of shelling out to powercfg.exe.
    internal struct PowercfgResult
    {
        public int ExitCode;
        public string StdOut;
        public string StdErr;
    }

    internal sealed class TrayAppContext : ApplicationContext
    {
        // Matches lines like: "Power Scheme GUID: <guid>  (<name>)"
        private static readonly Regex SchemeRegex =
            new Regex(@"Power Scheme GUID:\s*([0-9a-fA-F-]{36})\s*\(([^)]+)\)", RegexOptions.Compiled);

        // Standard "Ultimate Performance" GUID. Often present but hidden until first activated.
        private const string UltimatePerformanceSeedGuid = "e9a42b02-d5df-448d-aa00-03f14749eb61";

        private const string AppVersion = "1.0.0";
        private const string AppDate = "2026-07-18";

        private readonly NotifyIcon trayIcon;
        private readonly ContextMenuStrip menu;
        private readonly List<PowerMode> modes;

        public TrayAppContext()
        {
            // GUIDs below are Windows' standard built-in scheme IDs - same on every machine.
            modes = new List<PowerMode>
            {
                new PowerMode { Label = "Power Saver", LookupName = "Power saver",
                    FallbackGuid = "a1841308-3541-4fab-bc81-f71556f20b4a", IconColor = Color.MediumSeaGreen, Level = 0.25 },
                new PowerMode { Label = "Balanced", LookupName = "Balanced",
                    FallbackGuid = "381b4222-f694-41f0-9685-ff5bb260df2e", IconColor = Color.DodgerBlue, Level = 0.5 },
                new PowerMode { Label = "High Performance", LookupName = "High performance",
                    FallbackGuid = "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c", IconColor = Color.Orange, Level = 0.75 },
                new PowerMode { Label = "Ultimate Performance", LookupName = "Ultimate Performance",
                    FallbackGuid = UltimatePerformanceSeedGuid, IconColor = Color.Crimson, Level = 1.0 },
            };

            DiscoverSchemes();

            foreach (var mode in modes)
            {
                mode.TrayIcon = CreateBatteryIcon(mode.IconColor, mode.Level);
            }

            menu = new ContextMenuStrip();
            foreach (var mode in modes.Where(m => m.Guid != null))
            {
                var item = new ToolStripMenuItem(mode.Label) { Tag = mode };
                item.Click += OnModeClick;
                menu.Items.Add(item);
                mode.MenuItem = item;
            }
            menu.Items.Add(new ToolStripSeparator());
            var aboutItem = new ToolStripMenuItem("About");
            aboutItem.Click += OnAbout;
            menu.Items.Add(aboutItem);
            var exitItem = new ToolStripMenuItem("Exit");
            exitItem.Click += OnExit;
            menu.Items.Add(exitItem);
            menu.Opening += (s, e) => RefreshActiveMode(); // stay in sync if changed outside the app

            var startupIcon = modes.FirstOrDefault(m => m.Guid != null);
            trayIcon = new NotifyIcon
            {
                Text = "Power Mode",
                Icon = startupIcon != null ? startupIcon.TrayIcon : SystemIcons.Application,
                ContextMenuStrip = menu,
                Visible = true
            };

            RefreshActiveMode();
        }

        // Resolves each mode's real GUID: prefer a name match from `powercfg /list`, else fall back
        // to the well-known GUID (schemes that were never activated don't show up in /list even
        // when they exist, so an unmatched name here does NOT mean the scheme is missing).
        private void DiscoverSchemes()
        {
            var listResult = RunPowercfg("/list");
            var schemes = ParseSchemes(listResult.StdOut);

            foreach (var mode in modes)
            {
                string guid;
                mode.Guid = schemes.TryGetValue(mode.LookupName, out guid) ? guid : mode.FallbackGuid;
            }
        }

        private void OnModeClick(object sender, EventArgs e)
        {
            var item = (ToolStripMenuItem)sender;
            var mode = (PowerMode)item.Tag;
            SetActive(mode);
        }

        private void SetActive(PowerMode mode)
        {
            var result = RunPowercfg("/setactive " + mode.Guid);

            if (result.ExitCode != 0 && mode.FallbackGuid != null)
            {
                // GUID truly doesn't exist on this system - clone the standard scheme and retry once.
                // Duplicating assigns a brand new GUID, so it has to be parsed back out of the output.
                var dupResult = RunPowercfg("-duplicatescheme " + mode.FallbackGuid);
                var newSchemes = ParseSchemes(dupResult.StdOut);
                if (newSchemes.Count > 0)
                {
                    mode.Guid = newSchemes.Values.First();
                    result = RunPowercfg("/setactive " + mode.Guid);
                }
            }

            if (result.ExitCode == 0)
            {
                trayIcon.BalloonTipIcon = ToolTipIcon.Info;
                trayIcon.BalloonTipTitle = "Power Mode";
                trayIcon.BalloonTipText = "Power mode set to " + mode.Label + ".";
                trayIcon.ShowBalloonTip(3000);
            }
            else
            {
                trayIcon.BalloonTipIcon = ToolTipIcon.Error;
                trayIcon.BalloonTipTitle = "Power Mode";
                trayIcon.BalloonTipText = "Failed to set " + mode.Label + ". Try running this app as administrator.";
                trayIcon.ShowBalloonTip(5000);
            }
            RefreshActiveMode();
        }

        // Syncs checkmarks + tray icon to whatever scheme is actually active right now.
        private void RefreshActiveMode()
        {
            var result = RunPowercfg("/getactivescheme");
            var match = SchemeRegex.Match(result.StdOut ?? string.Empty);
            string activeGuid = match.Success ? match.Groups[1].Value.Trim() : null;

            PowerMode active = null;
            foreach (var mode in modes)
            {
                bool isActive = mode.Guid != null && activeGuid != null &&
                                string.Equals(mode.Guid, activeGuid, StringComparison.OrdinalIgnoreCase);
                if (mode.MenuItem != null)
                {
                    mode.MenuItem.Checked = isActive;
                }
                if (isActive)
                {
                    active = mode;
                }
            }

            if (active != null)
            {
                trayIcon.Icon = active.TrayIcon;
                trayIcon.Text = "Power Mode: " + active.Label;
            }
        }

        private void OnAbout(object sender, EventArgs e)
        {
            MessageBox.Show(
                "PowerModeTray v" + AppVersion + " (" + AppDate + ")\n" +
                "Tray-based switcher for Windows power plans.\n\n" +
                "Author: nathan-pocock\n" +
                "License: MIT",
                "About Power Mode",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        private void OnExit(object sender, EventArgs e)
        {
            trayIcon.Visible = false; // must hide before exit or the icon ghosts until moused over
            ExitThread();
        }

        // Runs powercfg.exe hidden and captures its output.
        private static PowercfgResult RunPowercfg(string arguments)
        {
            var psi = new ProcessStartInfo("powercfg.exe", arguments)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };

            using (var proc = Process.Start(psi))
            {
                string stdout = proc.StandardOutput.ReadToEnd();
                string stderr = proc.StandardError.ReadToEnd();
                proc.WaitForExit();
                return new PowercfgResult { ExitCode = proc.ExitCode, StdOut = stdout, StdErr = stderr };
            }
        }

        // Extracts {name -> GUID} pairs from `powercfg /list` or `-duplicatescheme` output.
        private static Dictionary<string, string> ParseSchemes(string output)
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (Match m in SchemeRegex.Matches(output ?? string.Empty))
            {
                string guid = m.Groups[1].Value.Trim();
                string name = m.Groups[2].Value.Trim();
                dict[name] = guid;
            }
            return dict;
        }

        // Renders a battery glyph: outline + nub, filled to `level` (0..1) in `color`.
        private static Icon CreateBatteryIcon(Color color, double level)
        {
            var body = new Rectangle(2, 9, 24, 14);
            var nub = new Rectangle(26, 13, 4, 6);
            int fillWidth = (int)Math.Round((body.Width - 4) * Math.Max(0, Math.Min(1, level)));
            var fill = new Rectangle(body.X + 2, body.Y + 3, fillWidth, body.Height - 6);

            using (var bmp = new Bitmap(32, 32))
            {
                using (var g = Graphics.FromImage(bmp))
                using (var bodyPath = RoundedRect(body, 3))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.Clear(Color.Transparent);

                    g.FillPath(Brushes.White, bodyPath);
                    using (var fillBrush = new SolidBrush(color))
                    {
                        g.FillRectangle(fillBrush, fill);
                    }
                    using (var pen = new Pen(Color.Black, 2f))
                    {
                        g.DrawPath(pen, bodyPath);
                    }
                    g.FillRectangle(Brushes.Black, nub);
                }
                return Icon.FromHandle(bmp.GetHicon());
            }
        }

        private static GraphicsPath RoundedRect(Rectangle bounds, int radius)
        {
            int d = radius * 2;
            var path = new GraphicsPath();
            path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
            path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
            path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
