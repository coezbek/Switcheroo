/*
 * Switcheroo - The incremental-search task switcher for Windows.
 * https://github.com/coezbek/switcheroo
 * Copyright 2009, 2010 James Sulak
 * Copyright 2014 Regin Larsen
 * 
 * Switcheroo is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * Switcheroo is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 * 
 * You should have received a copy of the GNU General Public License
 * along with Switcheroo.  If not, see <http://www.gnu.org/licenses/>.
 */

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.IO;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using System.Windows.Forms;
using System.Windows.Interop;
using ManagedWinapi;
using ManagedWinapi.Windows;
using Microsoft.Toolkit.Uwp.Notifications;
using Switcheroo.Core;
using Switcheroo.Core.Matchers;
using Switcheroo.Properties;
using System.Windows.Input;
using System.Windows.Interop;
using Application = System.Windows.Application;
using MenuItem = System.Windows.Forms.MenuItem;
using MessageBox = System.Windows.MessageBox;
using NotifyIcon = System.Windows.Forms.NotifyIcon;

namespace Switcheroo
{
    public partial class MainWindow : Window
    {
        private WindowCloser _windowCloser;
        private List<AppWindowViewModel> _unfilteredWindowList;
        private NotifyIcon _notifyIcon;
        private HotKey _hotkey;

        public static readonly RoutedUICommand CloseWindowCommand = new RoutedUICommand();
        public static readonly RoutedUICommand SwitchToWindowCommand = new RoutedUICommand();
        public static readonly RoutedUICommand ScrollListDownCommand = new RoutedUICommand();
        public static readonly RoutedUICommand ScrollListUpCommand = new RoutedUICommand();
        public static readonly RoutedUICommand DismissCommand = new RoutedUICommand();
        public static readonly RoutedUICommand CloseColumnCommand = new RoutedUICommand();
        public static readonly RoutedUICommand StartExplorerCommand = new RoutedUICommand();
        public static readonly RoutedUICommand StartNewInstanceCommand = new RoutedUICommand();
        private OptionsWindow _optionsWindow;
        private AboutWindow _aboutWindow;
        private AltTabHook _altTabHook;
        private SystemWindow _foregroundWindow;
        private bool _altTabAutoSwitch;
        private MonitorInfo _currentMonitor; // Cache the current monitor for reloads

        // New collections for each column
        private readonly ObservableCollection<AppWindowViewModel> _listLeft1;
        private readonly ObservableCollection<AppWindowViewModel> _listLeft2;
        private readonly ObservableCollection<AppWindowViewModel> _listLeft3;
        private readonly ObservableCollection<AppWindowViewModel> _listCenter;
        private readonly ObservableCollection<AppWindowViewModel> _listRight;

        // For navigation
        private readonly List<System.Windows.Controls.ListBox> _listBoxes;
        private List<System.Windows.Controls.ListBox> _visibleListBoxes;
        private int _activeColumnIndex = 0;

        public MainWindow()
        {
            InitializeComponent();
            
            _listLeft1 = new ObservableCollection<AppWindowViewModel>();
            _listLeft2 = new ObservableCollection<AppWindowViewModel>();
            _listLeft3 = new ObservableCollection<AppWindowViewModel>();
            _listCenter = new ObservableCollection<AppWindowViewModel>();
            _listRight = new ObservableCollection<AppWindowViewModel>();
            
            _listBoxes = new List<System.Windows.Controls.ListBox> { ListBoxLeft1, ListBoxLeft2, ListBoxLeft3, ListBoxCenter, ListBoxRight };
            _visibleListBoxes = new List<System.Windows.Controls.ListBox>();

            ListBoxLeft1.ItemsSource = _listLeft1;
            ListBoxLeft2.ItemsSource = _listLeft2;
            ListBoxLeft3.ItemsSource = _listLeft3;
            ListBoxCenter.ItemsSource = _listCenter;
            ListBoxRight.ItemsSource = _listRight;

            SetUpKeyBindings();

            SetUpNotifyIcon();

            SetUpHotKey();

            SetUpAltTabHook();

            SetUpToastNotifications();

            CheckForUpdates();

            ShowStartupNotification();

            Opacity = 0;
        }

        /// =================================

        #region Private Methods

        /// =================================

        private void SetUpKeyBindings()
        {
            // Enter and Esc bindings are not executed before the keys have been released.
            // This is done to prevent that the window being focused after the key presses
            // to get 'KeyUp' messages.

            KeyDown += (sender, args) =>
            {
                var key = (args.Key == Key.System) ? args.SystemKey : args.Key;

                // Opacity is set to 0 right away so it appears that action has been taken right away...
                if (key == Key.Enter && !Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
                {
                    Opacity = 0;
                }
                else if ((args.Key == Key.Escape) || (args.Key == Key.Q && Keyboard.Modifiers.HasFlag(ModifierKeys.Alt)))
                {
                    Opacity = 0;
                }
                else if (args.SystemKey == Key.S && Keyboard.Modifiers.HasFlag(ModifierKeys.Alt))
                {
                    _altTabAutoSwitch = false;
                    tb.Text = "";
                    tb.IsEnabled = true;
                    tb.Focus();
                }
            };

            KeyUp += (sender, args) =>
            {
                var key = (args.Key == Key.System) ? args.SystemKey : args.Key;

                // Debugging output
                // Console.WriteLine("KeyUp: " + key + " Modifiers: " + Keyboard.Modifiers + " AutoSwitch: " + _altTabAutoSwitch + " SystemKey: " + args.SystemKey);

                // ... But only when the keys are release, the action is actually executed
                if (key == Key.Enter && !Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
                {
                    Switch();
                }
                // Handle both Esc key and Alt+Q to dismiss Switcheroo
                else if ((args.Key == Key.Escape) || (args.Key == Key.Q && Keyboard.Modifiers.HasFlag(ModifierKeys.Alt)))
                {
                    HideWindow();
                }
                // This case handles when the user Presses and Releases the Left Alt key alone (note that args.SystemKey is used for Alt key) -> this would only happen while the window is open (for instance in search mode)
                else if (args.SystemKey == Key.LeftAlt && !Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
                {
                    Switch();
                }
                // This case handles when the user releases the Left Alt key which was held down when Tab was pressed
                // We only do this if CTRL is not held down, as that would indicate the user wants to keep Switcheroo open
                else if (args.Key == Key.LeftAlt && _altTabAutoSwitch && !Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
                {
                    Switch();
                }
            };
        }

        private void SetUpHotKey()
        {
            _hotkey = new HotKey();
            _hotkey.LoadSettings();

            Application.Current.Properties["hotkey"] = _hotkey;

            _hotkey.HotkeyPressed += hotkey_HotkeyPressed;
            try
            {
                _hotkey.Enabled = Settings.Default.EnableHotKey;
            }
            catch (HotkeyAlreadyInUseException)
            {
                var boxText = "The current hotkey for activating Switcheroo is in use by another program." +
                              Environment.NewLine +
                              Environment.NewLine +
                              "You can change the hotkey by right-clicking the Switcheroo icon in the system tray and choosing 'Options'.";
                MessageBox.Show(boxText, "Hotkey already in use", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void SetUpAltTabHook()
        {
            _altTabHook = new AltTabHook();
            _altTabHook.Pressed += AltTabPressed;
        }

        private int _wheelRemainder;

        private void MainWindow_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            _wheelRemainder += e.Delta;

            // Alt+Shift => move between lists (columns)
            var mods = Keyboard.Modifiers;
            bool altShift = (mods & (ModifierKeys.Alt | ModifierKeys.Shift)) == (ModifierKeys.Alt | ModifierKeys.Shift);

            int WHEEL_DELTA = altShift ? 360 : 180;

            while (Math.Abs(_wheelRemainder) >= WHEEL_DELTA)
            {
                var scrollUpLeft = _wheelRemainder > 0;
                if (altShift)
                {
                    int count = _visibleListBoxes.Count;
                    if (count > 0)
                    {
                        int next = (_activeColumnIndex + (scrollUpLeft ? -1 : +1) + count) % count;
                        SetActiveColumn(next);
                    }
                }
                else
                {
                    if (scrollUpLeft) 
                        PreviousItem();
                    else
                        NextItem();
                }

                _wheelRemainder += (scrollUpLeft ? -WHEEL_DELTA : WHEEL_DELTA);
            }

            e.Handled = true; // prevent underlying ListBox from also scrolling
        }

        private void SetUpNotifyIcon()
        {
            var icon = Properties.Resources.icon;

            var runOnStartupMenuItem = new MenuItem("Run on Startup", (s, e) => RunOnStartup(s as MenuItem))
            {
                Checked = new AutoStart().IsEnabled
            };

            _notifyIcon = new NotifyIcon
            {
                Text = "Switcheroo",
                Icon = icon,
                Visible = true,
                ContextMenu = new System.Windows.Forms.ContextMenu(new[]
                {
                    new MenuItem("Options", (s, e) => Options()),
                    runOnStartupMenuItem,
                    new MenuItem("About", (s, e) => About()),
                    new MenuItem("Exit", (s, e) => Quit())
                })
            };
        }

        private void SetUpToastNotifications()
        {
            // Register the app for toast notifications with a unique AUMID
            ToastNotificationManagerCompat.OnActivated += toastArgs =>
            {
                // Handle toast activation if needed (e.g., when user clicks the notification)
            };
        }

        private static void RunOnStartup(MenuItem menuItem)
        {
            try
            {
                var autoStart = new AutoStart
                {
                    IsEnabled = !menuItem.Checked
                };
                menuItem.Checked = autoStart.IsEnabled;
            }
            catch (AutoStartException e)
            {
                MessageBox.Show(e.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private static void CheckForUpdates()
        {
            var currentVersion = Assembly.GetEntryAssembly().GetName().Version;
            if (currentVersion == new Version(0, 0, 0, 0))
            {
                return;
            }

            var timer = new DispatcherTimer();

            timer.Tick += async (sender, args) =>
            {
                timer.Stop();
                var latestVersion = await GetLatestVersion();
                if (latestVersion != null && latestVersion > currentVersion)
                {
                    var result = MessageBox.Show(
                        string.Format(
                            "Switcheroo v{0} is available (you have v{1}).\r\n\r\nDo you want to download it?",
                            latestVersion, currentVersion),
                        "Update Available", MessageBoxButton.YesNo, MessageBoxImage.Information);
                    if (result == MessageBoxResult.Yes)
                    {
                        Process.Start("https://github.com/kvakulo/Switcheroo/releases/latest");
                    }
                }
                else
                {
                    timer.Interval = new TimeSpan(24, 0, 0);
                    timer.Start();
                }
            };

            timer.Interval = new TimeSpan(0, 0, 0);
            timer.Start();
        }

        private static async Task<Version> GetLatestVersion()
        {
            try
            {
                var versionAsString =
                    await
                        new WebClient().DownloadStringTaskAsync(
                            "https://raw.github.com/kvakulo/Switcheroo/update/version.txt");
                Version newVersion;
                if (Version.TryParse(versionAsString, out newVersion))
                {
                    return newVersion;
                }
            }
            catch (WebException)
            {
            }
            return null;
        }

        private void LoadData(InitialFocus focus)
        {
            // Use cached monitor if available, otherwise will fall back to primary screen
            LoadData(focus, _currentMonitor);
        }

        private void LoadData(InitialFocus focus, MonitorInfo monitor)
        {
            // Cache the monitor for future reloads (when window is already visible)
            if (monitor != null)
            {
                _currentMonitor = monitor;
                // Console.WriteLine($"[DEBUG] LoadData: Caching monitor with DPI={monitor.DpiScale:F2}");
            }
            else
            {
                monitor = _currentMonitor;
                // Console.WriteLine($"[DEBUG] LoadData: Using cached monitor with DPI={(monitor?.DpiScale.ToString("F2") ?? "null")}");
            }
            
            _unfilteredWindowList = new WindowFinder().GetWindows().Select(window => new AppWindowViewModel(window)).ToList();
            var firstWindow = _unfilteredWindowList.FirstOrDefault();
            bool foregroundWindowMovedToBottom = false;

            if (firstWindow != null && AreWindowsRelated(firstWindow.AppWindow, _foregroundWindow))
            {
                _unfilteredWindowList.RemoveAt(0);
                _unfilteredWindowList.Add(firstWindow);
                foregroundWindowMovedToBottom = true;
            }
            
            _listLeft1.Clear();
            _listLeft2.Clear();
            _listLeft3.Clear();
            _listCenter.Clear();
            _listRight.Clear();

            var handledHwnds = new HashSet<IntPtr>();

            var pinnedProcesses = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (Settings.Default.PinnedProcesses != null)
            {
                foreach (string process in Settings.Default.PinnedProcesses)
                {
                    if (!string.IsNullOrWhiteSpace(process))
                    {
                        pinnedProcesses.Add(process.Trim().ToLowerInvariant());
                    }
                }
            }

            var rightWindows = _unfilteredWindowList
                .Where(w => pinnedProcesses.Contains(w.ProcessTitle.ToLowerInvariant()))
                .ToList();
            foreach (var window in rightWindows)
            {
                _listRight.Add(window);
                handledHwnds.Add(window.HWnd);
            }
            
            var remainingForTopApps = _unfilteredWindowList.Where(w => !handledHwnds.Contains(w.HWnd)).ToList();
            var topApps = remainingForTopApps
                .GroupBy(w => w.ProcessTitle)
                .OrderByDescending(g => g.Count())
                .ThenBy(g => g.Key)
                .Take(Settings.Default.NumberOfAppColumns)
                .ToList();

            var appColumnDataSources = new[] { _listLeft1, _listLeft2, _listLeft3 };
            var min_number_of_windows_for_own_column = 0;
        
            for (int i = 0; i < topApps.Count; i++)
            {
                var appGroup = topApps[i];

                // Only give an app its own column if it meets the minimum window count.
                if (appGroup.Count() >= min_number_of_windows_for_own_column)
                {
                    // The most frequent app (topApps[0]) goes into the column closest to the center (ListBoxLeft3 -> _listLeft3).
                    // The data source array is ordered left-to-right: [_listLeft1, _listLeft2, _listLeft3].
                    // So, we map the most frequent group to the last index of the data source array.
                    int targetListIndex = (appColumnDataSources.Length - 1) - i;
                    if (targetListIndex >= 0)
                    {
                        var targetList = appColumnDataSources[targetListIndex];
                        foreach (var window in appGroup)
                        {
                            targetList.Add(window);
                            handledHwnds.Add(window.HWnd);
                        }
                    }
                }
            }

            var first10Windows = _unfilteredWindowList.Take(10);
            var first10Set= new HashSet<IntPtr>();    
            foreach (var window in first10Windows)
            {
                first10Set.Add(window.HWnd);
            }

            // We always keep a.) the first 10 windows in the center column (no matter if they are already in left/right) and b.) the window we tab away from
            var remainingForCenter = _unfilteredWindowList.Where(
                w => first10Set.Contains(w.HWnd) 
                || !handledHwnds.Contains(w.HWnd)
                || w == firstWindow
            );
            var centerWindows = remainingForCenter.ToList();
            foreach (var window in centerWindows)
            {
                _listCenter.Add(window);
            }

            _windowCloser = new WindowCloser();

            // Set initial formatted titles for all windows
            TitleFormatter.FormatTitlesForDisplay(_unfilteredWindowList);

            int centerIndex = _listBoxes.IndexOf(ListBoxCenter);
            SetActiveColumn(centerIndex, focus);

            // Correct the selection for the initial Shift+Alt+Tab after the active window has been moved to the end
            if (foregroundWindowMovedToBottom && focus == InitialFocus.PreviousItem)
            {
                PreviousItem();
            }

            tb.Clear();
            tb.Focus();
            CenterWindow(monitor);
            ScrollSelectedItemIntoView();
        }

        private static bool AreWindowsRelated(SystemWindow window1, SystemWindow window2)
        {
            return window1.HWnd == window2.HWnd || window1.Process.Id == window2.Process.Id;
        }
        
        private void SetActiveColumn(int index, InitialFocus focus = InitialFocus.NextItem)
        {
            var targetListBox = _listBoxes[index];
            _visibleListBoxes.Clear();
            _visibleListBoxes.AddRange(_listBoxes.Where(lb => lb.Visibility == Visibility.Visible));
            int visibleIndex = _visibleListBoxes.IndexOf(targetListBox);

            if (visibleIndex < 0)
            {
                // If the target column is not visible, default to the center column
                targetListBox = ListBoxCenter;
                visibleIndex = _visibleListBoxes.IndexOf(targetListBox);
                if (visibleIndex < 0) // Fallback if center is somehow not visible
                {
                    visibleIndex = 0;
                }
            }

            if (_activeColumnIndex >= 0 && _activeColumnIndex < _visibleListBoxes.Count)
            {
                _visibleListBoxes[_activeColumnIndex].Background = Brushes.Transparent;
            }

            _activeColumnIndex = visibleIndex;
            var currentListBox = _visibleListBoxes[_activeColumnIndex];

            currentListBox.Background = Brushes.AliceBlue;

            if (currentListBox.Items.Count > 0)
            {
                // If no item is selected, select one based on the focus direction.
                // Otherwise, keep the current selection.
                if (currentListBox.SelectedIndex == -1)
                {
                    if (focus == InitialFocus.PreviousItem)
                    {
                        currentListBox.SelectedIndex = currentListBox.Items.Count - 1;
                    }
                    else
                    {
                        currentListBox.SelectedIndex = 0;
                    }
                }
                ScrollSelectedItemIntoView();
            }
        }

        /// <summary>
        /// Configures the column layout and places the Switcheroo window in the center of the screen.
        /// The logic ensures the center column remains anchored to the middle of the screen.
        /// Note: This should only be called during/after loading data on initial show.
        /// </summary>
        private void CenterWindow()
        {
            CenterWindow(null);
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(
            IntPtr hWnd,
            IntPtr hWndInsertAfter,
            int X,
            int Y,
            int cx,
            int cy,
            uint uFlags);

        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_NOACTIVATE = 0x0010;

        /// <summary>
        /// Configures the column layout, size, and position of the Switcheroo window on the specified monitor.
        /// </summary>
        private void CenterWindow(MonitorInfo monitor)
        {
            if (monitor == null)
            {
                throw new ArgumentNullException(nameof(monitor));
            }

            // Step 1: Ensure the window is physically on the correct monitor before any sizing.
            // This forces the OS and WPF to use the target monitor's DPI for all subsequent layout calculations.
            EnsureWindowIsOnCorrectMonitor(monitor);
            
            // Calculate the base column width, adjusted for the monitor's DPI scaling.
            double baseColumnWidth = Settings.Default.UserWidth > 0 ? Settings.Default.UserWidth : 250;
            double columnWidthInDips = Math.Max(100, baseColumnWidth / Math.Sqrt(monitor.DpiScale));

            // Step 2: Configure the internal grid layout based on content.
            int numVisibleColumns = ConfigureColumnLayout(columnWidthInDips);

            // Step 3: Calculate the main window's overall size based on the layout.
            CalculateAndSetWindowSize(numVisibleColumns, columnWidthInDips, monitor);

            // Step 4: Calculate the final centered position for the correctly-sized window.
            CalculateAndSetPosition(monitor);
        }

        /// <summary>
        /// Moves the window to the target monitor based on raw pixel coordinates. After this call,
        /// DPI-scaled coordinates will correctly target the right monitor.
        /// </summary>
        private void EnsureWindowIsOnCorrectMonitor(MonitorInfo monitor)
        {
            if (!IsVisible)
            {
                var hwnd = new WindowInteropHelper(this).EnsureHandle();
                SetWindowPos(hwnd, IntPtr.Zero, monitor.WorkArea.Left, monitor.WorkArea.Top, 100, 100, SWP_NOZORDER | SWP_NOACTIVATE);
            }
        }

        /// <summary>
        /// Sets the visibility and width for each of the five columns in the grid based on whether their
        /// corresponding ListBox contains items.
        /// </summary>
        /// <returns>The total number of visible columns.</returns>
        private int ConfigureColumnLayout(double columnWidthInDips)
        {
            int numAppColumns = Settings.Default.NumberOfAppColumns;

            var columnDefinitions = new[] { ColLeft1, ColLeft2, ColLeft3, ColCenter, ColRight };
            var listBoxes = new[] { ListBoxLeft1, ListBoxLeft2, ListBoxLeft3, ListBoxCenter, ListBoxRight };

            // Logic to determine if an app column should be visible
            bool showLeft1 = numAppColumns >= 3 && _listLeft1.Any();
            bool showLeft2 = numAppColumns >= 2 && _listLeft2.Any();
            bool showLeft3 = numAppColumns >= 1 && _listLeft3.Any();
            
            var visibilityFlags = new[] { showLeft1, showLeft2, showLeft3, true, _listRight.Any() };

            int visibleCount = 0;
            for (int i = 0; i < columnDefinitions.Length; i++)
            {
                if (visibilityFlags[i])
                {
                    columnDefinitions[i].Width = new GridLength(1, GridUnitType.Star);
                    listBoxes[i].Visibility = Visibility.Visible;
                    visibleCount++;
                }
                else
                {
                    columnDefinitions[i].Width = new GridLength(0);
                    listBoxes[i].Visibility = Visibility.Collapsed;
                }
            }

            return visibleCount;
        }

        /// <summary>
        /// Calculates and sets the main window's Width and MaxHeight properties based on the number of
        /// visible columns and the target monitor's work area.
        /// </summary>
        private void CalculateAndSetWindowSize(int numVisibleColumns, double columnWidthInDips, MonitorInfo monitor)
        {
            double calculatedWidthInDips = numVisibleColumns * columnWidthInDips;
            double maxWidthInDips = monitor.WpfWorkAreaWidth * 0.95;

            Width = Math.Min(calculatedWidthInDips, maxWidthInDips);
            Border.MaxHeight = monitor.WpfWorkAreaHeight * 0.9;
            MinWidth = 400;

            // If the calculated width exceeds the final window width, switch to proportional ("star") sizing
            // to ensure all visible columns fit within the constrained window size.
            if (calculatedWidthInDips > Width)
            {
                if (ColLeft1.Width.Value > 0) ColLeft1.Width = new GridLength(1, GridUnitType.Star);
                if (ColLeft2.Width.Value > 0) ColLeft2.Width = new GridLength(1, GridUnitType.Star);
                if (ColLeft3.Width.Value > 0) ColLeft3.Width = new GridLength(1, GridUnitType.Star);
                ColCenter.Width = new GridLength(1, GridUnitType.Star);
                if (ColRight.Width.Value > 0) ColRight.Width = new GridLength(1, GridUnitType.Star);
            }

            // Force the layout to update so that ActualWidth and ActualHeight are correct for positioning.
            UpdateLayout();
        }

        /// <summary>
        /// Calculates the final Top and Left position of the window to center it on the target monitor.
        /// This method should be called after the window's size has been set.
        /// </summary>
        private void CalculateAndSetPosition(MonitorInfo monitor)
        {
            // Convert the window's actual size from DIPs to physical pixels for accurate positioning.
            double actualWidthInPixels = ActualWidth * monitor.DpiScale;
            double actualHeightInPixels = ActualHeight * monitor.DpiScale;

            // Calculate the horizontal center point in physical pixels.
            double physicalCenterOfMonitorX = monitor.WorkArea.Left + (monitor.WorkAreaWidth / 2.0);
            double desiredLeftInPixels = physicalCenterOfMonitorX - (actualWidthInPixels / 2.0);

            // Ensure the window stays within the monitor's boundaries.
            desiredLeftInPixels = Math.Max(monitor.WorkArea.Left, Math.Min(desiredLeftInPixels, monitor.WorkArea.Left + monitor.WorkAreaWidth - actualWidthInPixels));
            
            // Convert the final physical position back to DIPs for WPF.
            Left = desiredLeftInPixels / monitor.DpiScale;

            // Calculate the vertical position, preferring a 256px top margin but centering if necessary.
            double desiredTopInPixels = Math.Min(
                monitor.WorkArea.Top + 256, // Preferred top position
                monitor.WorkArea.Top + (monitor.WorkAreaHeight / 2.0) - (actualHeightInPixels / 2.0) // Fallback to center
            );

            Top = desiredTopInPixels / monitor.DpiScale;
        }

        /// <summary>
        /// Switches the window associated with the selected item.
        /// </summary>
        private void Switch()
        {
            if (_visibleListBoxes.Count == 0 || _activeColumnIndex < 0 || _activeColumnIndex >= _visibleListBoxes.Count)
            {
                HideWindow();
                return;
            }

            var currentListBox = _visibleListBoxes[_activeColumnIndex];
            if (currentListBox.SelectedItems.Count > 0)
            {
                foreach (var item in currentListBox.SelectedItems)
                {
                    var win = (AppWindowViewModel)item;
                    win.AppWindow.SwitchToLastVisibleActivePopup();
                }
            }

            HideWindow();
        }

        private void HideWindow()
        {
            if (_windowCloser != null)
            {
                _windowCloser.Dispose();
                _windowCloser = null;
            }

            _altTabAutoSwitch = false;
            _currentMonitor = null; // Clear cached monitor so it's detected fresh next time
            Opacity = 0;
            Dispatcher.BeginInvoke(new Action(Hide), DispatcherPriority.Input);
        }

        #endregion

        /// =================================

        #region Right-click menu functions

        /// =================================
        /// <summary>
        /// Show Options dialog.
        /// </summary>
        private void Options()
        {
            if (_optionsWindow == null)
            {
                _optionsWindow = new OptionsWindow
                {
                    WindowStartupLocation = WindowStartupLocation.CenterScreen
                };
                _optionsWindow.Closed += (sender, args) => _optionsWindow = null;
                _optionsWindow.ShowDialog();
            }
            else
            {
                _optionsWindow.Activate();
            }
        }

        /// <summary>
        /// Show About dialog.
        /// </summary>
        private void About()
        {
            if (_aboutWindow == null)
            {
                _aboutWindow = new AboutWindow
                {
                    WindowStartupLocation = WindowStartupLocation.CenterScreen
                };
                _aboutWindow.Closed += (sender, args) => _aboutWindow = null;
                _aboutWindow.ShowDialog();
            }
            else
            {
                _aboutWindow.Activate();
            }
        }

        /// <summary>
        /// Quit Switcheroo
        /// </summary>
        private void Quit()
        {
            _notifyIcon.Dispose();
            _notifyIcon = null;
            _hotkey.Dispose();
            Application.Current.Shutdown();
        }

        #endregion

        /// =================================

        #region Event Handlers

        /// =================================
        private void OnClose(object sender, System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = true;
            HideWindow();
        }

        private void MainWindow_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            // The actual key pressed, ignoring modifiers. This is important for system keys like Alt+Left.
            var key = (e.Key == Key.System) ? e.SystemKey : e.Key;

            // Alt+~ to rotate backwards through center and left columns
            if (Keyboard.Modifiers == ModifierKeys.Alt && key == Key.OemTilde)
            {
                e.Handled = true;

                // 1. Create a list of columns eligible for this specific cycle
                //    (all visible columns except the pinned column on the right).
                var cycleableColumns = _visibleListBoxes.Where(lb => lb != ListBoxRight).ToList();

                if (cycleableColumns.Count == 0)
                {
                    return; // Nothing to cycle through
                }

                // 2. Get the currently active ListBox and find its position in our special cycle list.
                var currentActiveListBox = _visibleListBoxes[_activeColumnIndex];
                int currentCycleIndex = cycleableColumns.IndexOf(currentActiveListBox);

                int nextCycleIndex;
                if (currentCycleIndex == -1)
                {
                    // 3a. If the active column was the pinned column (not in our list),
                    //     jump to the last column in the cycle (which is always the center column).
                    nextCycleIndex = cycleableColumns.Count - 1;
                }
                else
                {
                    // 3b. Otherwise, just cycle backward within the eligible columns.
                    nextCycleIndex = (currentCycleIndex - 1 + cycleableColumns.Count) % cycleableColumns.Count;
                }

                // 4. Get the target ListBox from our cycle list and activate it.
                var nextListBox = cycleableColumns[nextCycleIndex];
                SetActiveColumn(_listBoxes.IndexOf(nextListBox));

                return;
            }

            // Handle Left/Right arrow navigation. This allows navigation when the textbox isn't focused,
            // or at any time when the Alt key is held down.
            if (!tb.IsFocused || Keyboard.Modifiers.HasFlag(ModifierKeys.Alt))
            {
                if (key == Key.Left)
                {
                    e.Handled = true;
                    int nextIndex = _activeColumnIndex - 1;
                    if (nextIndex >= 0)
                    {
                        SetActiveColumn(_listBoxes.IndexOf(_visibleListBoxes[nextIndex]));
                    }
                    return;
                }

                if (key == Key.Right)
                {
                    e.Handled = true;
                    int nextIndex = _activeColumnIndex + 1;
                    if (nextIndex < _visibleListBoxes.Count)
                    {
                        SetActiveColumn(_listBoxes.IndexOf(_visibleListBoxes[nextIndex]));
                    }
                    return;
                }
            }
        }

        // When a listbox gets focus, make it the active column
        private void ListBox_GotFocus(object sender, RoutedEventArgs e)
        {
            var focusedListBox = sender as System.Windows.Controls.ListBox;
            if (focusedListBox != null)
            {
                int newIndex = _listBoxes.IndexOf(focusedListBox);
                if (newIndex != -1)
                {
                     SetActiveColumn(newIndex, InitialFocus.NextItem);
                }
            }
        }

        private void hotkey_HotkeyPressed(object sender, EventArgs e)
        {
            if (!Settings.Default.EnableHotKey)
            {
                return;
            }

            if (Visibility != Visibility.Visible)
            {
                _foregroundWindow = SystemWindow.ForegroundWindow;

                // Get the monitor where the mouse cursor is located
                var cursorMonitor = MonitorHelper.GetMonitorFromCursor();
                Console.WriteLine($"[DEBUG] hotkey_HotkeyPressed: Got monitor with DPI={(cursorMonitor?.DpiScale.ToString("F2") ?? "null")}");

                LoadData(InitialFocus.NextItem, cursorMonitor);

                tb.IsEnabled = true;
                Show();
                Activate();
                Keyboard.Focus(tb);
                Opacity = 1;
            }
            else
            {
                HideWindow();
            }
        }

        private void AltTabPressed(object sender, AltTabHookEventArgs e)
        {
            if (!Settings.Default.AltTabHook)
            {
                // Ignore Alt+Tab presses if the hook is not activated by the user
                return;
            }

            _foregroundWindow = SystemWindow.ForegroundWindow;

            if (_foregroundWindow.ClassName == "MultitaskingViewFrame")
            {
                // If Windows' task switcher is on the screen then don't do anything
                return;
            }

            e.Handled = true;

            if (Visibility != Visibility.Visible)
            {
                // Get the monitor where the mouse cursor is located
                var cursorMonitor = MonitorHelper.GetMonitorFromCursor();
                // Console.WriteLine($"\n\n\n[DEBUG] AltTabPressed: Got monitor with DPI={(cursorMonitor?.DpiScale.ToString("F2") ?? "null")} and working area {cursorMonitor?.WorkArea}");
                
                LoadData(e.ShiftDown ? InitialFocus.PreviousItem : InitialFocus.NextItem, cursorMonitor);

                tb.IsEnabled = true;
                ActivateAndFocusMainWindow();
                Keyboard.Focus(tb);

                if (Settings.Default.AutoSwitch && !e.CtrlDown)
                {
                    _altTabAutoSwitch = true;
                    tb.IsEnabled = false;
                    tb.Text = "Press Alt + S to search";
                }
                Opacity = 1;
            }
            else
            {
                if (e.ShiftDown)
                {
                    PreviousItem();
                }
                else
                {
                    NextItem();
                }
            }
        }

        private void ActivateAndFocusMainWindow()
        {
            // What happens below looks a bit weird, but for Switcheroo to get focus when using the Alt+Tab hook,
            // it is needed to simulate an Alt keypress will bring Switcheroo to the foreground. Otherwise Switcheroo
            // will become the foreground window, but the previous window will retain focus, and receive keep getting
            // the keyboard input.
            // http://www.codeproject.com/Tips/76427/How-to-bring-window-to-top-with-SetForegroundWindo

            var thisWindowHandle = new WindowInteropHelper(this).Handle;
            var thisWindow = new AppWindow(thisWindowHandle);
            var altKey = new KeyboardKey(System.Windows.Forms.Keys.Alt);
            var altKeyPressed = false;

            // Press the Alt key if it is not already being pressed
            if ((altKey.AsyncState & 0x8000) == 0)
            {
                altKey.Press();
                altKeyPressed = true;
            }

            // Bring the Switcheroo window to the foreground
            Show();
            SystemWindow.ForegroundWindow = thisWindow;
            Activate();

            // Release the Alt key if it was pressed above
            if (altKeyPressed)
            {
                altKey.Release();
            }
        }

        private void TextChanged(object sender, TextChangedEventArgs args)
        {
            if (!tb.IsEnabled) return;

            if (string.IsNullOrEmpty(tb.Text))
            {
                LoadData(InitialFocus.NextItem);
                return;
            }

            // During search, we only modify the center list.
            _listCenter.Clear();

            // If a side column was active, switch to the center for search results.
            int centerIndex = _listBoxes.IndexOf(ListBoxCenter);
            int visibleCenterIndex = _visibleListBoxes.IndexOf(ListBoxCenter);

            if (_activeColumnIndex != visibleCenterIndex)
            {
                SetActiveColumn(centerIndex);
            }

            var query = tb.Text;

            var context = new WindowFilterContext<AppWindowViewModel>
            {
                Windows = _unfilteredWindowList,
                ForegroundWindowProcessTitle = new AppWindow(_foregroundWindow.HWnd).ProcessTitle
            };

            var filterResults = new WindowFilterer().Filter(context, query).ToList();

            foreach (var filterResult in filterResults)
            {
                filterResult.AppWindow.FormattedTitle = GetFormattedTitleFromBestResult(filterResult.WindowTitleMatchResults);
                filterResult.AppWindow.FormattedProcessTitle = GetFormattedTitleFromBestResult(filterResult.ProcessTitleMatchResults);
                _listCenter.Add(filterResult.AppWindow);
            }

            if (ListBoxCenter.Items.Count > 0)
            {
                ListBoxCenter.SelectedItem = ListBoxCenter.Items[0];
            }
        }

        private static string GetFormattedTitleFromBestResult(IList<MatchResult> matchResults)
        {
            var bestResult = matchResults.FirstOrDefault(r => r.Matched) ?? matchResults.First();
            return new XamlHighlighter().Highlight(bestResult.StringParts);
        }

        private void OnEnterPressed(object sender, ExecutedRoutedEventArgs e)
        {
            Switch();
            e.Handled = true;
        }

        private void ListBoxItem_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            // Double-click always switches
            Switch();
            e.Handled = true;
        }

        private void ListBoxItem_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (Settings.Default.SwitchOnSingleClick)
            {
                // Only switch on a single click if the user is NOT holding down a modifier key
                // used for multi-selection. This allows Ctrl+Click and Shift+Click to work as expected.
                if ((Keyboard.Modifiers & ModifierKeys.Control) == 0 && (Keyboard.Modifiers & ModifierKeys.Shift) == 0)
                {
                    Switch();
                    e.Handled = true;
                }
            }
        }

        private void ListBoxItem_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            var listBoxItem = sender as System.Windows.Controls.ListBoxItem;
            if (listBoxItem?.Content is AppWindowViewModel window)
            {
                var contextMenu = listBoxItem.ContextMenu;
                if (contextMenu != null)
                {
                    // Find the Pin/Unpin menu item
                    foreach (var item in contextMenu.Items)
                    {
                        if (item is System.Windows.Controls.MenuItem menuItem &&
                            menuItem.Tag != null && menuItem.Tag.ToString() == "PinUnpin")
                        {
                            bool isPinned = IsProcessPinned(window.ProcessTitle);
                            menuItem.Header = isPinned ? "Unpin" : "Pin";
                            break;
                        }
                    }
                }
            }
        }

        private bool IsProcessPinned(string processTitle)
        {
            if (Settings.Default.PinnedProcesses == null)
                return false;

            var processLower = processTitle.ToLowerInvariant();
            foreach (string pinnedProcess in Settings.Default.PinnedProcesses)
            {
                if (!string.IsNullOrWhiteSpace(pinnedProcess) &&
                    pinnedProcess.Trim().ToLowerInvariant() == processLower)
                {
                    return true;
                }
            }
            return false;
        }

        private async void ListBoxItem_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.MiddleButton == MouseButtonState.Pressed)
            {
                var middleClickAction = Settings.Default.MiddleClickAction;

                // 0 = Do nothing, 1 = Close item under cursor, 2 = Close highlighted item
                if (middleClickAction == 0)
                {
                    return; // Do nothing
                }

                AppWindowViewModel win = null;

                if (middleClickAction == 1)
                {
                    // Close the item under the mouse cursor
                    var listBoxItem = sender as System.Windows.Controls.ListBoxItem;
                    if (listBoxItem != null)
                    {
                        win = listBoxItem.Content as AppWindowViewModel;
                    }
                }
                else if (middleClickAction == 2)
                {
                    // Close the currently SELECTED/highlighted window
                    win = _visibleListBoxes[_activeColumnIndex].SelectedItem as AppWindowViewModel;
                }

                if (win != null)
                {
                    bool isClosed = await _windowCloser.TryCloseAsync(win);
                    if (isClosed)
                        RemoveWindowFromAllLists(win);

                    if (_unfilteredWindowList.Count == 0)
                        HideWindow();
                }

                e.Handled = true; // Prevent selecting the item under cursor
            }
        }

        /// <summary>
        /// Event handler for the CloseWindow command (e.g., Ctrl+W).
        /// Closes all currently selected windows in the active column.
        /// This operation will attempt to close all selected windows, even if some fail.
        /// </summary>
        private async void CloseWindow(object sender, ExecutedRoutedEventArgs e)
        {
            var currentListBox = _visibleListBoxes[_activeColumnIndex];
            if (currentListBox.SelectedItem == null)
            {
                e.Handled = true;
                return;
            }

            var windowsToClose = currentListBox.SelectedItems.Cast<AppWindowViewModel>().ToList();
            await CloseWindowsAsync(windowsToClose, abortOnFailure: false);
            e.Handled = true;
        }

        /// <summary>
        /// Event handler for the CloseColumn command (e.g., Alt+Shift+W).
        /// Closes all windows in one of the three leftmost application columns.
        /// This is a sequential operation that will stop if any window fails to close.
        /// </summary>
        private async void CloseColumn(object sender, ExecutedRoutedEventArgs e)
        {
            // This command only applies to the three leftmost "app" columns.
            var currentListBox = _visibleListBoxes[_activeColumnIndex];
            var appListBoxes = new[] { ListBoxLeft1, ListBoxLeft2, ListBoxLeft3 };
            if (appListBoxes.Contains(currentListBox))
            {
                if (currentListBox.Items.Count > 0)
                {
                    var windowsToClose = currentListBox.Items.Cast<AppWindowViewModel>().ToList();
                    await CloseWindowsAsync(windowsToClose, abortOnFailure: true);
                }
            }
            e.Handled = true;
        }

        /// <summary>
        /// The main worker method for closing one or more windows and intelligently updating the UI.
        /// </summary>
        /// <param name="windowsToClose">The collection of windows to attempt to close.</param>
        /// <param name="abortOnFailure">If true, the process will stop if any single window fails to close.</param>
        private async Task CloseWindowsAsync(IEnumerable<AppWindowViewModel> windowsToClose, bool abortOnFailure)
        {
            // 1. Capture the state BEFORE closing windows.
            var originalActiveListBox = _visibleListBoxes.Count > _activeColumnIndex ? _visibleListBoxes[_activeColumnIndex] : null;
            int originalSelectedIndex = originalActiveListBox?.SelectedIndex ?? -1;
            var originalVisibleIndex = _activeColumnIndex;

            // 2. Calculate how many items BEFORE the current selection are being closed to adjust the index later.
            int selectionAdjustment = 0;
            if (originalActiveListBox != null && originalSelectedIndex > 0)
            {
                var windowsToCloseSet = new HashSet<AppWindowViewModel>(windowsToClose);
                for (int i = 0; i < originalSelectedIndex; i++)
                {
                    if (windowsToCloseSet.Contains(originalActiveListBox.Items[i] as AppWindowViewModel))
                    {
                        selectionAdjustment++;
                    }
                }
            }

            // 3. Sequentially attempt to close the windows.
            foreach (var win in windowsToClose.ToList()) // Use ToList() to create a copy, avoiding collection modification issues.
            {
                bool isClosed = await _windowCloser.TryCloseAsync(win);
                if (isClosed)
                {
                    RemoveWindowFromAllLists(win);
                }
                else if (abortOnFailure)
                {
                    break; // Stop the entire operation if one window fails to close.
                }
            }

            // 4. After closing, check if we need to exit entirely.
            if (_unfilteredWindowList.Count == 0)
            {
                HideWindow();
                return;
            }

            // 5. Re-evaluate which columns are visible now that data has changed.
            ConfigureColumnLayout(0); // Width doesn't matter here, this just updates visibility.
            _visibleListBoxes.Clear();
            _visibleListBoxes.AddRange(_listBoxes.Where(lb => lb.Visibility == Visibility.Visible));

            if (_visibleListBoxes.Count == 0) { HideWindow(); return; } // Safeguard

            // 6. Intelligently determine the new active column and selection.
            int newActiveVisibleIndex;

            // Case A: The original column is still visible and has items.
            if (originalActiveListBox != null && originalActiveListBox.Visibility == Visibility.Visible && originalActiveListBox.HasItems)
            {
                newActiveVisibleIndex = _visibleListBoxes.IndexOf(originalActiveListBox);
                int newSelectedIndex = originalSelectedIndex - selectionAdjustment;
                originalActiveListBox.SelectedIndex = Math.Max(0, Math.Min(newSelectedIndex, originalActiveListBox.Items.Count - 1));
            }
            else // Case B: The original column disappeared. Find a logical fallback.
            {
                // Try to focus on the column that now occupies the original's position, clamping to the new bounds.
                newActiveVisibleIndex = Math.Min(originalVisibleIndex, _visibleListBoxes.Count - 1);
            }

            // 7. Activate the determined column and scroll the selection into view.
            SetActiveColumn(_listBoxes.IndexOf(_visibleListBoxes[newActiveVisibleIndex]));
            ScrollSelectedItemIntoView();
        }


        private void DismissWindow(object sender, ExecutedRoutedEventArgs e)
        {
            HideWindow();
            e.Handled = true;
        }

        /// <summary>
        /// Event handler for the StartExplorer command (Alt+E).
        /// Starts a new Windows Explorer instance.
        /// </summary>
        private void StartExplorer(object sender, ExecutedRoutedEventArgs e)
        {
            try
            {
                Process.Start("explorer.exe");
                HideWindow();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to start Windows Explorer: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            e.Handled = true;
        }

        /// <summary>
        /// Event handler for the StartNewInstance command (Alt+N).
        /// Starts a new instance of the currently selected process.
        /// </summary>
        private void StartNewInstance(object sender, ExecutedRoutedEventArgs e)
        {
            var currentListBox = _visibleListBoxes[_activeColumnIndex];
            if (currentListBox.SelectedItem == null)
            {
                e.Handled = true;
                return;
            }

            var selectedWindow = (AppWindowViewModel)currentListBox.SelectedItem;
            try
            {
                var executablePath = selectedWindow.AppWindow.ExecutablePath;
                if (!string.IsNullOrEmpty(executablePath) && File.Exists(executablePath))
                {
                    Process.Start(executablePath);
                    HideWindow();
                }
                else
                {
                    MessageBox.Show($"Unable to find executable path for {selectedWindow.ProcessTitle}", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to start new instance of {selectedWindow.ProcessTitle}: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            e.Handled = true;
        }

        private void RemoveWindowFromAllLists(AppWindowViewModel window)
        {
            _listLeft1.Remove(window);
            _listLeft2.Remove(window);
            _listLeft3.Remove(window);
            _listCenter.Remove(window);
            _listRight.Remove(window);
            _unfilteredWindowList.Remove(window);
        }

        private void ScrollListUp(object sender, ExecutedRoutedEventArgs e)
        {
            PreviousItem();
            e.Handled = true;
        }

        private void PreviousItem()
        {
            // Add this guard clause to prevent out-of-range exceptions.
            if (_visibleListBoxes.Count == 0 || _activeColumnIndex < 0 || _activeColumnIndex >= _visibleListBoxes.Count)
            {
                return;
            }

            var currentListBox = _visibleListBoxes[_activeColumnIndex];
            if (currentListBox.Items.Count > 0)
            {
                if (currentListBox.SelectedIndex > 0)
                {
                    currentListBox.SelectedIndex--;
                }
                else
                {
                    currentListBox.SelectedIndex = currentListBox.Items.Count - 1;
                }
                ScrollSelectedItemIntoView();
            }
        }

        private void ScrollListDown(object sender, ExecutedRoutedEventArgs e)
        {
            NextItem();
            e.Handled = true;
        }

        private void NextItem()
        {
            // Add this guard clause to prevent out-of-range exceptions.
            if (_visibleListBoxes.Count == 0 || _activeColumnIndex < 0 || _activeColumnIndex >= _visibleListBoxes.Count)
            {
                return;
            }

            var currentListBox = _visibleListBoxes[_activeColumnIndex];
            if (currentListBox.Items.Count > 0)
            {
                if (currentListBox.SelectedIndex < currentListBox.Items.Count - 1)
                {
                    currentListBox.SelectedIndex++;
                }
                else
                {
                    currentListBox.SelectedIndex = 0;
                }

                ScrollSelectedItemIntoView();
            }
        }

        private void ScrollSelectedItemIntoView()
        {
            var currentListBox = _visibleListBoxes[_activeColumnIndex];
            if (currentListBox.SelectedItem != null)
            {
                currentListBox.ScrollIntoView(currentListBox.SelectedItem);
            }
        }

        private void MainWindow_OnLostFocus(object sender, EventArgs e)
        {
            HideWindow();
        }

        private void MainWindow_OnLoaded(object sender, RoutedEventArgs e)
        {
            DisableSystemMenu();
        }

        private void DisableSystemMenu()
        {
            var windowHandle = new WindowInteropHelper(this).Handle;
            var window = new SystemWindow(windowHandle);
            window.Style = window.Style & ~WindowStyleFlags.SYSMENU;
        }

        private void ShowHelpTextBlock_OnPreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            var duration = new Duration(TimeSpan.FromSeconds(0.150));
            var newHeight = HelpPanel.Height > 0 ? 0 : +17;
            HelpPanel.BeginAnimation(HeightProperty, new DoubleAnimation(HelpPanel.Height, newHeight, duration));
        }

        private void ShowStartupNotification()
        {
            // Build a message based on configured hotkeys
            var message = BuildActivationMessage();

            if (!string.IsNullOrEmpty(message))
            {
                try
                {
                    // Get logo path from exe directory
                    var exeDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                    var logoPath = Path.Combine(exeDirectory, "logo_toast.png");

                    // Toast notifications don't work with network/UNC paths
                    // Check if we're running from a network path and copy to temp if needed
                    var logoUri = new Uri(logoPath);
                    if (logoUri.IsUnc || !logoUri.IsFile)
                    {
                        // Running from network path, copy to local temp folder
                        var tempLogoPath = Path.Combine(Path.GetTempPath(), "switcheroo_toast_logo.png");

                        if (!File.Exists(tempLogoPath) && File.Exists(logoPath))
                        {
                            File.Copy(logoPath, tempLogoPath, overwrite: true);
                        }

                        logoPath = tempLogoPath;
                    }

                    // Build the toast notification
                    var builder = new ToastContentBuilder()
                        .AddText("Switcheroo++ Started")
                        .AddText(message)
                        .SetToastDuration(ToastDuration.Short);

                    // Add the logo if it exists
                    if (File.Exists(logoPath))
                    {
                        builder.AddAppLogoOverride(new Uri(logoPath), ToastGenericAppLogoCrop.Default);
                    }

                    // Show silently
                    builder.AddAudio(new ToastAudio() { Silent = true });
                    builder.Show();
                }
                catch (Exception ex)
                {
                    // Fallback to balloon tip if toast notifications fail
                    Console.WriteLine($"Toast notification failed: {ex.Message}");
                    _notifyIcon.BalloonTipTitle = "Switcheroo Started";
                    _notifyIcon.BalloonTipText = message;
                    _notifyIcon.BalloonTipIcon = System.Windows.Forms.ToolTipIcon.None;
                    _notifyIcon.ShowBalloonTip(1000);
                }
            }
        }

        private string BuildActivationMessage()
        {
            var hasCustomHotkey = Settings.Default.EnableHotKey;
            var hasAltTab = Settings.Default.AltTabHook;

            if (!hasCustomHotkey && !hasAltTab)
            {
                return "No activation shortcuts configured. Use Options to configure.";
            }

            var parts = new List<string>();

            if (hasAltTab)
            {
                parts.Add("Alt+Tab");
            }

            if (hasCustomHotkey)
            {
                var hotkeyString = FormatHotkey();
                if (!string.IsNullOrEmpty(hotkeyString))
                {
                    parts.Add(hotkeyString);
                }
            }

            if (parts.Count == 0)
            {
                return string.Empty;
            }
            else if (parts.Count == 1)
            {
                return $"Press {parts[0]} to activate";
            }
            else
            {
                return $"Press {string.Join(" or ", parts)} to activate";
            }
        }

        private string FormatHotkey()
        {
            var shortcutText = new StringBuilder();

            if (Settings.Default.Ctrl)
            {
                shortcutText.Append("Ctrl+");
            }

            if (Settings.Default.Shift)
            {
                shortcutText.Append("Shift+");
            }

            if (Settings.Default.Alt)
            {
                shortcutText.Append("Alt+");
            }

            if (Settings.Default.WindowsKey)
            {
                shortcutText.Append("Win+");
            }

            var keyCode = (System.Windows.Forms.Keys)Settings.Default.HotKey;
            var keyString = KeyboardHelper.CodeToString((uint)keyCode).ToUpper().Trim();

            if (string.IsNullOrEmpty(keyString))
            {
                keyString = new System.Windows.Forms.KeysConverter().ConvertToString(keyCode);
            }

            // Handle special case for Escape key
            if (keyString == "\u001B")
            {
                keyString = "Escape";
            }

            shortcutText.Append(keyString);
            return shortcutText.ToString();
        }

        private void ContextMenu_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            var contextMenu = sender as System.Windows.Controls.ContextMenu;
            if (contextMenu != null)
            {
                // Close the context menu and let the event bubble to the main window
                // where the existing keyboard handlers will process it
                contextMenu.IsOpen = false;
                e.Handled = false; // Let it bubble
            }
        }

        private void ContextMenu_Switch(object sender, RoutedEventArgs e)
        {
            var menuItem = sender as System.Windows.Controls.MenuItem;
            if (menuItem?.DataContext is AppWindowViewModel window)
            {
                window.AppWindow.SwitchToLastVisibleActivePopup();
                HideWindow();
            }
        }

        private async void ContextMenu_Close(object sender, RoutedEventArgs e)
        {
            var menuItem = sender as System.Windows.Controls.MenuItem;
            if (menuItem?.DataContext is AppWindowViewModel window)
            {
                bool isClosed = await _windowCloser.TryCloseAsync(window);
                if (isClosed)
                {
                    RemoveWindowFromAllLists(window);
                }

                if (_unfilteredWindowList.Count == 0)
                {
                    HideWindow();
                }
            }
        }

        private void ContextMenu_PinUnpin(object sender, RoutedEventArgs e)
        {
            var menuItem = sender as System.Windows.Controls.MenuItem;
            if (menuItem?.DataContext is AppWindowViewModel window)
            {
                string processTitle = window.ProcessTitle.Trim().ToLowerInvariant();
                bool isPinned = IsProcessPinned(window.ProcessTitle);

                if (Settings.Default.PinnedProcesses == null)
                {
                    Settings.Default.PinnedProcesses = new System.Collections.Specialized.StringCollection();
                }

                if (isPinned)
                {
                    // Unpin: Remove from the collection
                    var itemsToRemove = new List<string>();
                    foreach (string pinnedProcess in Settings.Default.PinnedProcesses)
                    {
                        if (!string.IsNullOrWhiteSpace(pinnedProcess) &&
                            pinnedProcess.Trim().ToLowerInvariant() == processTitle)
                        {
                            itemsToRemove.Add(pinnedProcess);
                        }
                    }
                    foreach (var item in itemsToRemove)
                    {
                        Settings.Default.PinnedProcesses.Remove(item);
                    }
                }
                else
                {
                    // Pin: Add to the collection
                    Settings.Default.PinnedProcesses.Add(processTitle);
                }

                Settings.Default.Save();

                // Reload the data to reflect the changes
                LoadData(InitialFocus.NextItem);
            }
        }

        private void ContextMenu_CopyWindowTitle(object sender, RoutedEventArgs e)
        {
            var menuItem = sender as System.Windows.Controls.MenuItem;
            if (menuItem?.DataContext is AppWindowViewModel window)
            {
                try
                {
                    System.Windows.Clipboard.SetText(window.WindowTitle);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to copy to clipboard: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ContextMenu_OpenFileLocation(object sender, RoutedEventArgs e)
        {
            var menuItem = sender as System.Windows.Controls.MenuItem;
            if (menuItem?.DataContext is AppWindowViewModel window)
            {
                try
                {
                    string executablePath = window.AppWindow.ExecutablePath;
                    if (!string.IsNullOrEmpty(executablePath) && File.Exists(executablePath))
                    {
                        Process.Start("explorer.exe", $"/select,\"{executablePath}\"");
                    }
                    else
                    {
                        MessageBox.Show("Unable to determine the executable path for this window.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to open file location: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        #endregion

        private enum InitialFocus
        {
            NextItem,
            PreviousItem
        }
    }
}