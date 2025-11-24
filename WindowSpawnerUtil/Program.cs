using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Switcheroo.WindowSpawnerUtil
{
    static class Program
    {
        // P/Invoke to extract icons from system DLLs
        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern int ExtractIconEx(string lpszFile, int nIconIndex, IntPtr[] phiconLarge, IntPtr[] phiconSmall, int nIcons);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private extern static bool DestroyIcon(IntPtr handle);

        // P/Invoke to show window without activating it
        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        private const int SW_SHOWNOACTIVATE = 4;
        private static readonly IntPtr HWND_BOTTOM = new IntPtr(1);
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOSIZE = 0x0001;

        // Word list for generating searchable titles
        private static readonly string[] _words = new[]
        {
            "Alpha", "Bravo", "Charlie", "Delta", "Echo", "Foxtrot", "Golf", "Hotel",
            "India", "Juliet", "Kilo", "Lima", "Mike", "November", "Oscar", "Papa",
            "Quebec", "Romeo", "Sierra", "Tango", "Uniform", "Victor", "Whiskey",
            "X-ray", "Yankee", "Zulu", "Project", "Report", "Analysis", "Budget",
            "Chrome", "Firefox", "Code", "Studio", "Note", "Pad"
        };

        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            var controlForm = new ControlForm();
            Application.Run(controlForm);
        }

        public class ControlForm : Form
        {
            private Button _btnLaunch, _btnLaunch500;
            private Label _lblStatus;
            private List<Form> _windows = new List<Form>();

            public ControlForm()
            {
                Text = "Switcheroo Performance Test";
                Size = new Size(350, 300);
                StartPosition = FormStartPosition.CenterScreen;

                // Set a distinctive icon for the spawner window
                SetSpawnerIcon();

                _btnLaunch = new Button
                {
                    Text = "Launch 50 Windows",
                    Location = new Point(50, 50),
                    Size = new Size(230, 50)
                };
                _btnLaunch.Click += BtnLaunch_Click;

                _btnLaunch500 = new Button
                {
                    Text = "Launch 500 Windows",
                    Location = new Point(50, 125),
                    Size = new Size(230, 50)
                };
                _btnLaunch500.Click += Btn500Launch_Click;

                _lblStatus = new Label
                {
                    Text = "Ready to launch...",
                    Location = new Point(50, 200),
                    AutoSize = true
                };

                Controls.Add(_btnLaunch);
                Controls.Add(_btnLaunch500);
                Controls.Add(_lblStatus);
            }

            private void SetSpawnerIcon()
            {
                try
                {
                    // Extract a distinctive icon from shell32.dll (index 137 is a monitor/screen icon)
                    IntPtr[] largeIcons = new IntPtr[1];
                    IntPtr[] smallIcons = new IntPtr[1];
                    int extracted = ExtractIconEx("shell32.dll", 137, largeIcons, smallIcons, 1);

                    if (extracted > 0 && largeIcons[0] != IntPtr.Zero)
                    {
                        Icon = Icon.FromHandle(largeIcons[0]);
                    }
                }
                catch
                {
                    // If icon extraction fails, just use default icon
                }
            }

            private void BtnLaunch_Click(object sender, EventArgs e)
            {
                _lblStatus.Text = "Extracting icons and launching windows...";
                Application.DoEvents();

                try
                {
                    LaunchWindows(50);
                    _lblStatus.Text = $"Running with {_windows.Count} windows open.";
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                }
            }

            private void Btn500Launch_Click(object sender, EventArgs e)
            {
                _lblStatus.Text = "Extracting icons and launching windows...";
                Application.DoEvents();

                try
                {
                    LaunchWindows(500);
                    _lblStatus.Text = $"Running with {_windows.Count} windows open.";
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                }
            }

            private void LaunchWindows(int count)
            {
                // 1. Extract icons from shell32.dll to get variety
                int iconLimit = 100;
                IntPtr[] largeIcons = new IntPtr[iconLimit];
                IntPtr[] smallIcons = new IntPtr[iconLimit];

                // ExtractIconEx returns the number of icons extracted
                int extractedCount = ExtractIconEx("shell32.dll", 0, largeIcons, smallIcons, iconLimit);

                var icons = new List<Icon>();
                for (int i = 0; i < extractedCount; i++)
                {
                    if (smallIcons[i] != IntPtr.Zero)
                    {
                        // Clone the icon so we can manage the handle properly later if needed
                        // (Though for a test utility, letting the OS clean up on exit is often acceptable)
                        icons.Add((Icon)Icon.FromHandle(smallIcons[i]).Clone());

                        // Clean up raw handles
                        DestroyIcon(smallIcons[i]);
                        DestroyIcon(largeIcons[i]);
                    }
                }

                // 2. Launch Windows
                var rnd = new Random();
                int startIndex = _windows.Count; // Start numbering from current count

                for (int i = 0; i < count; i++)
                {
                    var f = new Form();

                    // Generate a varied title for filtering tests
                    string word1 = _words[rnd.Next(_words.Length)];
                    string word2 = _words[rnd.Next(_words.Length)];
                    int windowNumber = startIndex + i;
                    f.Text = $"{windowNumber:D3} - {word1} {word2} - Test Window";

                    // Basic styling
                    f.Size = new Size(200, 100);
                    f.StartPosition = FormStartPosition.Manual;
                    f.ShowInTaskbar = true; // Important for Switcheroo detection
                    f.FormBorderStyle = FormBorderStyle.SizableToolWindow;

                    // Stack them so they don't cover the whole screen, but keep them visible
                    // Windows that are fully off-screen or invisible might be filtered out by Switcheroo
                    f.Location = new Point(50 + (windowNumber % 20) * 10, 50 + (windowNumber % 20) * 10);

                    // Assign random icon
                    if (icons.Count > 0)
                    {
                        f.Icon = icons[windowNumber % icons.Count];
                    }

                    // Create window but don't show it yet
                    f.CreateControl();

                    // Show window without activating it and place it at the bottom of Z-order
                    ShowWindow(f.Handle, SW_SHOWNOACTIVATE);
                    SetWindowPos(f.Handle, HWND_BOTTOM, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);

                    _windows.Add(f);

                    // Pump messages every 50 windows to keep UI responsive
                    if (i % 50 == 0) Application.DoEvents();
                }

                // Bring the control form back to front
                BringToFront();
                Activate();
            }

            protected override void OnFormClosing(FormClosingEventArgs e)
            {
                // Close all spawned windows when main controller closes
                foreach (var f in _windows)
                {
                    if (!f.IsDisposed) f.Close();
                }
                base.OnFormClosing(e);
            }
        }
    }
}
