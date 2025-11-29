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
using Switcheroo.Core.Highlighting;
using Switcheroo.Highlighting;
using Switcheroo.Properties;
using System.Windows.Input;
using System.Windows.Automation.Peers;
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
        private HighlightService _highlightService;
        private HighlightConfigWindow _highlightConfigWindow;

        // New collections for each column
        private ObservableCollection<AppWindowViewModel> _listLeft1;
        private ObservableCollection<AppWindowViewModel> _listLeft2;
        private ObservableCollection<AppWindowViewModel> _listLeft3;
        private ObservableCollection<AppWindowViewModel> _listCenter;
        private ObservableCollection<AppWindowViewModel> _listRight;

        // For navigation
        private readonly List<PerformanceListBox> _listBoxes;
        private List<PerformanceListBox> _visibleListBoxes;
        private int _activeColumnIndex = 0;

        public MainWindow()
        {
            InitializeComponent();

            // Initialize Highlight Service
            _highlightService = new HighlightService();
            _highlightService.RulesChanged += (s, e) =>
            {
                if (this.IsVisible)
                {
                    Dispatcher.Invoke(RefreshHighlights);
                }
            };

            _listLeft1 = new ObservableCollection<AppWindowViewModel>();
            _listLeft2 = new ObservableCollection<AppWindowViewModel>();
            _listLeft3 = new ObservableCollection<AppWindowViewModel>();
            _listCenter = new ObservableCollection<AppWindowViewModel>();
            _listRight = new ObservableCollection<AppWindowViewModel>();
            
            _listBoxes = new List<PerformanceListBox> { ListBoxLeft1, ListBoxLeft2, ListBoxLeft3, ListBoxCenter, ListBoxRight };
            _visibleListBoxes = new List<PerformanceListBox>();

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

            // Preload data to eliminate lag on first toggle
            PreloadData();
        }

        // Prevent UI Automation apps from crawling this window.
        // If many windows are open (and thus a lot ListBoxItems), we can't guarantee opening < 300ms otherwise.
        protected override AutomationPeer OnCreateAutomationPeer()
        {
            return new SilentWindowAutomationPeer(this);
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
            var icon = new System.Drawing.Icon(Properties.Resources.icon, System.Windows.Forms.SystemInformation.SmallIconSize);

            var runOnStartupMenuItem = new MenuItem("Run on &Startup", (s, e) => RunOnStartup(s as MenuItem))
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
                    new MenuItem("&Options", (s, e) => Options()),
                    runOnStartupMenuItem,
                    new MenuItem("&About", (s, e) => About()),
                    new MenuItem("E&xit", (s, e) => Quit())
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

        private void PreloadData()
        {
            // This creates the window list and fetches icons into the MemoryCache
            // while the window is still invisible. This eliminates the lag on first toggle.
            LoadData(InitialFocus.NextItem);

            // Hide immediately so it doesn't flicker
            HideWindow();
        }

        private void LoadData(InitialFocus focus)
        {
            // Use cached monitor if available, otherwise will fall back to primary screen
            LoadData(focus, _currentMonitor);
        }

        private void LoadData(InitialFocus focus, MonitorInfo monitor)
        {
            bool isReload = this.IsVisible;

            if (monitor == null)
            {
                monitor = _currentMonitor ?? MonitorHelper.GetMonitorFromCursor();
            }

            // Cache for future reloads
            if (monitor != null)
            {
                _currentMonitor = monitor;
            }

            // 1. Capture State for ALL ListBoxes (Selection & Index)
            var selectionState = new Dictionary<int, IntPtr>();
            var indexState = new Dictionary<int, int>();

            if (isReload)
            {
                for (int i = 0; i < _listBoxes.Count; i++)
                {
                    if (_listBoxes[i].SelectedItem is AppWindowViewModel vm)
                    {
                        selectionState[i] = vm.HWnd;
                        indexState[i] = _listBoxes[i].SelectedIndex;
                    }
                }
            }

            // Capture which ListBox instance was active
            PerformanceListBox activeListBoxInstance = null;
            if (_visibleListBoxes.Count > 0 && _activeColumnIndex >= 0 && _activeColumnIndex < _visibleListBoxes.Count)
            {
                activeListBoxInstance = _visibleListBoxes[_activeColumnIndex];
            }

            var sw = System.Diagnostics.Stopwatch.StartNew();

            // 2. Fetch Windows
            _unfilteredWindowList = new WindowFinder().GetWindows().Select(window =>
            {
                var vm = new AppWindowViewModel(window);
                var rule = _highlightService.FindMatch(vm.ProcessTitle, vm.WindowTitle, window.ClassName);
                vm.ApplyHighlight(rule, false);
                return vm;
            }).ToList();

            long tFetch = sw.ElapsedMilliseconds;

            // The foreground window stays at the top (Index 0)
            var firstWindow = _unfilteredWindowList.FirstOrDefault();

            TitleFormatter.FormatTitlesForDisplay(_unfilteredWindowList);

            long tFormat = sw.ElapsedMilliseconds;

            var tmpLeft1 = new List<AppWindowViewModel>();
            var tmpLeft2 = new List<AppWindowViewModel>();
            var tmpLeft3 = new List<AppWindowViewModel>();
            var tmpCenter = new List<AppWindowViewModel>();
            var tmpRight = new List<AppWindowViewModel>();

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
                tmpRight.Add(window);
                handledHwnds.Add(window.HWnd);
            }

            var remainingForTopApps = _unfilteredWindowList.Where(w => !handledHwnds.Contains(w.HWnd)).ToList();
            var topApps = remainingForTopApps
                .GroupBy(w => w.ProcessTitle)
                .OrderByDescending(g => g.Count())
                .ThenBy(g => g.Key)
                .Take(Settings.Default.NumberOfAppColumns)
                .ToList();

            long tGrouping = sw.ElapsedMilliseconds;

            var appColumnTargetLists = new[] { tmpLeft1, tmpLeft2, tmpLeft3 };
            var min_number_of_windows_for_own_column = 0;

            for (int i = 0; i < topApps.Count; i++)
            {
                var appGroup = topApps[i];

                // Only give an app its own column if it meets the minimum window count.
                if (appGroup.Count() >= min_number_of_windows_for_own_column)
                {
                    int targetListIndex = (appColumnTargetLists.Length - 1) - i;
                    if (targetListIndex >= 0)
                    {
                        var targetList = appColumnTargetLists[targetListIndex];
                        foreach (var window in appGroup)
                        {
                            targetList.Add(window);
                            handledHwnds.Add(window.HWnd);
                        }
                    }
                }
            }

            var first10Windows = _unfilteredWindowList.Take(10);
            var first10Set = new HashSet<IntPtr>(first10Windows.Select(w => w.HWnd));

            long tAppColumns = sw.ElapsedMilliseconds;

            var remainingForCenter = _unfilteredWindowList.Where(
                w => first10Set.Contains(w.HWnd)
                || !handledHwnds.Contains(w.HWnd)
                || w == firstWindow
            );
            
            // Add remaining windows to center temp list
            tmpCenter.AddRange(remainingForCenter);

            long tWindowToColumnAssignment = sw.ElapsedMilliseconds;

            _windowCloser = new WindowCloser();

            long tWindowCloser = sw.ElapsedMilliseconds;

            // This was slightly faster than adding them individually and relying on ObservableCollection's notifications.
            _listLeft1 = new ObservableCollection<AppWindowViewModel>(tmpLeft1);
            ListBoxLeft1.ItemsSource = _listLeft1;

            _listLeft2 = new ObservableCollection<AppWindowViewModel>(tmpLeft2);
            ListBoxLeft2.ItemsSource = _listLeft2;

            _listLeft3 = new ObservableCollection<AppWindowViewModel>(tmpLeft3);
            ListBoxLeft3.ItemsSource = _listLeft3;

            _listCenter = new ObservableCollection<AppWindowViewModel>(tmpCenter);
            ListBoxCenter.ItemsSource = _listCenter;

            _listRight = new ObservableCollection<AppWindowViewModel>(tmpRight);
            ListBoxRight.ItemsSource = _listRight;

            long tBinding = sw.ElapsedMilliseconds;

            if (monitor != null)
            {
                CenterWindow(monitor);
            }

            long tLayout = sw.ElapsedMilliseconds;

            // Restore searching state
            bool isSearching = isReload && !string.IsNullOrEmpty(tb.Text) && tb.IsEnabled;

            if (isSearching)
            {
                Console.WriteLine("Restoring search filter: " + tb.Text);
                FilterList(tb.Text);
            }
            else
            {
                // Reset placeholder / Clear text logic
                tb.TextChanged -= TextChanged;
                if (_altTabAutoSwitch)
                {
                    tb.Text = "Press Alt + S to search";
                }
                else
                {
                    tb.Clear();
                }
                tb.TextChanged += TextChanged;

                if (tb.IsEnabled) tb.Focus();
            }

            // 3. Restore Selection for ALL columns
            long tBeforeSelection = sw.ElapsedMilliseconds;
            for (int i = 0; i < _listBoxes.Count; i++)
            {
                var lb = _listBoxes[i];
                var collection = lb.ItemsSource as ObservableCollection<AppWindowViewModel>;
                
                // Skip if empty (Layout update above will have collapsed it, but just in case)
                if (collection == null || collection.Count == 0) continue;

                bool restored = false;

                // Try to find exact HWnd match
                if (selectionState.TryGetValue(i, out IntPtr hwnd))
                {
                    var match = collection.FirstOrDefault(w => w.HWnd == hwnd);
                    if (match != null)
                    {
                        lb.SelectedItem = match;
                        restored = true;
                    }
                }

                // If exact match failed (item moved columns) AND we are just reloading (Pin/Unpin):
                // Keep the selection at the same index so the list doesn't look empty/reset.
                if (!restored && isReload)
                {
                    if (indexState.TryGetValue(i, out int oldIndex))
                    {
                        int newIndex = Math.Min(oldIndex, collection.Count - 1);
                        lb.SelectedIndex = Math.Max(0, newIndex);
                    }
                }
            }
            long tAfterSelection = sw.ElapsedMilliseconds;

            // 4. Determine Active Column & Focus
            int newActiveIndex = -1;

            if (isReload)
            {
                // Try to keep the same listbox active if it is still visible
                if (activeListBoxInstance != null && activeListBoxInstance.Visibility == Visibility.Visible)
                {
                    newActiveIndex = _listBoxes.IndexOf(activeListBoxInstance);
                }
                else
                {
                    // Fallback to Center if the active column disappeared (e.g. Unpinned last item in Right)
                    newActiveIndex = _listBoxes.IndexOf(ListBoxCenter);
                }
            }
            else
            {
                // Fresh Open (Alt-Tab): Always activate Center
                newActiveIndex = _listBoxes.IndexOf(ListBoxCenter);

                // FIX: Start at 2nd position (Index 1) for Alt-Tab (NextItem)
                // This keeps current window at Index 0, and selects the "previous" window at Index 1.
                if (ListBoxCenter.Items.Count > 1 && focus == InitialFocus.NextItem)
                {
                    ListBoxCenter.SelectedIndex = 1;
                }
                else if (ListBoxCenter.Items.Count > 0)
                {
                    // Shift+Tab -> Wrap to bottom; otherwise Top.
                    ListBoxCenter.SelectedIndex = (focus == InitialFocus.PreviousItem) 
                        ? ListBoxCenter.Items.Count - 1 
                        : 0;
                }
            }
            long tBeforeSetActiveColumn = sw.ElapsedMilliseconds;

            // Activate the calculated column
            SetActiveColumn(newActiveIndex, focus, false);

            long tAfterSetActiveColumn = sw.ElapsedMilliseconds;

            // Ensure the active item is visible
            var currentActive = _visibleListBoxes[_activeColumnIndex];
            if (currentActive.SelectedItem != null)
            {
                currentActive.ScrollIntoView(currentActive.SelectedItem);
            }

#if DEBUG
            long tScrollIntoView = sw.ElapsedMilliseconds;
            if (tScrollIntoView > 150)
            {
                // [PROFILE] Output results
                Console.WriteLine($"[PROFILE] LoadData timings (ms):" +
                                $" FetchWindows={tGrouping}ms" +
                                $" AppColumns={tAppColumns - tGrouping}ms" +
                                $" WindowToColumn={tWindowToColumnAssignment - tAppColumns}ms" +
                                $" WindowCloser={tWindowCloser - tWindowToColumnAssignment}ms" +
                                $" Binding={tBinding - tWindowCloser}ms" +
                                $" Layout={tLayout - tBinding}ms" +
                                $" SelectionRestore={tAfterSelection - tBeforeSelection}ms" +
                                $" SetActiveColumn={tAfterSetActiveColumn - tBeforeSetActiveColumn}ms" +
                                $" ScrollIntoView={tScrollIntoView - tAfterSetActiveColumn}ms" +
                                $" Total={tScrollIntoView}ms");
            }
#endif
        }

        private static System.Windows.Controls.ScrollViewer GetScrollViewer(System.Windows.DependencyObject o)
        {
            if (o is System.Windows.Controls.ScrollViewer sv) return sv;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(o); i++)
            {
                var child = VisualTreeHelper.GetChild(o, i);
                var result = GetScrollViewer(child);
                if (result != null) return result;
            }
            return null;
        }

        private static bool AreWindowsRelated(SystemWindow window1, SystemWindow window2)
        {
            return window1.HWnd == window2.HWnd || window1.Process.Id == window2.Process.Id;
        }
        
        private void SetActiveColumn(int index, InitialFocus focus = InitialFocus.NextItem, bool scrollIntoView = true)
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

            // Clear highlight from ALL listboxes to ensure no stale highlights remain.
            // This fixes the issue where the center column stays highlighted when switching via Alt+~
            foreach (var listBox in _listBoxes)
            {
                listBox.ClearValue(System.Windows.Controls.Control.BackgroundProperty);
            }

            _activeColumnIndex = visibleIndex;
            var currentListBox = _visibleListBoxes[_activeColumnIndex];

            currentListBox.SetResourceReference(System.Windows.Controls.Control.BackgroundProperty, "ActiveColumnBrush");

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
                if (scrollIntoView)
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
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOCOPYBITS = 0x0100;
        private const uint SWP_DEFERERASE = 0x2000;        

        /// <summary>
        /// Moves the window to the target monitor based on raw pixel coordinates. After this call,
        /// DPI-scaled coordinates will correctly target the right monitor.
        /// </summary>
        private void EnsureWindowIsOnCorrectMonitor(MonitorInfo monitor)
        {
            if (!IsVisible)
            {
                var hwnd = new WindowInteropHelper(this).EnsureHandle();
                SetWindowPos(hwnd, IntPtr.Zero, monitor.WorkArea.Left, monitor.WorkArea.Top, 0, 0, SWP_NOZORDER | SWP_NOACTIVATE | SWP_NOSIZE | SWP_NOCOPYBITS | SWP_DEFERERASE);
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
                    if (listBoxes[i].Visibility != Visibility.Visible)
                        listBoxes[i].Visibility = Visibility.Visible;
                    visibleCount++;
                }
                else
                {
                    columnDefinitions[i].Width = new GridLength(0);
                    if (listBoxes[i].Visibility != Visibility.Collapsed)
                        listBoxes[i].Visibility = Visibility.Collapsed;
                }
            }

            return visibleCount;
        }

        private void CenterWindow(MonitorInfo monitor)
        {
            if (monitor == null) throw new ArgumentNullException(nameof(monitor));

            var sw = System.Diagnostics.Stopwatch.StartNew();

            EnsureWindowIsOnCorrectMonitor(monitor);

            long tEnsureOnMonitor = sw.ElapsedMilliseconds;
            
            double baseColumnWidth = Settings.Default.UserWidth > 0 ? Settings.Default.UserWidth : 250;
            double columnWidthInDips = Math.Max(100, baseColumnWidth / Math.Sqrt(monitor.DpiScale));

            // 1. Configure Grid Columns
            int numVisibleColumns = ConfigureColumnLayout(columnWidthInDips);

            // 2. Calculate Width
            double calculatedWidth = numVisibleColumns * columnWidthInDips;
            double maxWidth = monitor.WpfWorkAreaWidth * 0.95;
            double finalWidth = Math.Min(calculatedWidth, maxWidth);
            
            // Apply Width
            if (Width != finalWidth)
                Width = finalWidth;
            if (calculatedWidth > finalWidth)
            {
                // Switch to star sizing if constrained
                if (ColLeft1.Width.Value > 0) ColLeft1.Width = new GridLength(1, GridUnitType.Star);
                if (ColLeft2.Width.Value > 0) ColLeft2.Width = new GridLength(1, GridUnitType.Star);
                if (ColLeft3.Width.Value > 0) ColLeft3.Width = new GridLength(1, GridUnitType.Star);
                ColCenter.Width = new GridLength(1, GridUnitType.Star);
                if (ColRight.Width.Value > 0) ColRight.Width = new GridLength(1, GridUnitType.Star);
            }
            
            long tCalculateAndSetWidth = sw.ElapsedMilliseconds;

            // 3. Calculate Height (Manually, to avoid UpdateLayout/Measure)
            double maxHeight = monitor.WpfWorkAreaHeight * 0.9;

            if (Border.MaxHeight != maxHeight)
                Border.MaxHeight = maxHeight;

            long tSetMaxHeight = sw.ElapsedMilliseconds;

            // Find max items in any visible column to estimate height
            int maxItems = 0;
            if (ListBoxLeft1.Visibility == Visibility.Visible) maxItems = Math.Max(maxItems, _listLeft1.Count);
            if (ListBoxLeft2.Visibility == Visibility.Visible) maxItems = Math.Max(maxItems, _listLeft2.Count);
            if (ListBoxLeft3.Visibility == Visibility.Visible) maxItems = Math.Max(maxItems, _listLeft3.Count);
            if (ListBoxCenter.Visibility == Visibility.Visible) maxItems = Math.Max(maxItems, _listCenter.Count);
            if (ListBoxRight.Visibility == Visibility.Visible) maxItems = Math.Max(maxItems, _listRight.Count);

            long countItems = sw.ElapsedMilliseconds;

            // Estimate: Header (~45px) + Items (~42px each) + Padding
            double estimatedHeight = 45 + (maxItems * 42) + 20;
            double finalHeight = Math.Min(estimatedHeight, maxHeight);

            // Enforce minimum height to look good if empty and round to integer
            finalHeight = Math.Max(200, Math.Round(finalHeight));

            if (Height != finalHeight) {
                // Console.WriteLine($"[DEBUG] Setting Height: {finalHeight} (Estimated: {estimatedHeight}, Max: {maxHeight}, Current: {Height})");
                Height = finalHeight;
            }

            long tCalculateAndSetHeight = sw.ElapsedMilliseconds;

            // 4. Position
            // Use the calculated dimensions (finalWidth/finalHeight) instead of ActualWidth/ActualHeight
            CalculateAndSetPosition(monitor, finalWidth, finalHeight);

            long tCalculateAndSetPosition = sw.ElapsedMilliseconds;

            // Console.WriteLine($"[DEBUG] CenterWindow timings (ms):\n" + 
            //                   $"  EnsureOnMonitor: {tEnsureOnMonitor}\n" +
            //                   $"  CalculateAndSetWidth: {tCalculateAndSetWidth - tEnsureOnMonitor}\n" +
            //                   $"  SetMaxHeight: {tSetMaxHeight - tCalculateAndSetWidth}\n" +
            //                   $"  CountItems: {countItems - tSetMaxHeight}\n" +
            //                   $"  CalculateAndSetHeight: {tCalculateAndSetHeight - countItems}\n" +
            //                   $"  CalculateAndSetPosition: {tCalculateAndSetPosition - tCalculateAndSetHeight}");
        }

        private void CalculateAndSetPosition(MonitorInfo monitor, double widthInDips, double heightInDips)
        {
            // Convert DIPs to Pixels for monitor math
            double actualWidthInPixels = widthInDips * monitor.DpiScale;
            double actualHeightInPixels = heightInDips * monitor.DpiScale;

            // Horizontal Center
            double physicalCenterOfMonitorX = monitor.WorkArea.Left + (monitor.WorkAreaWidth / 2.0);
            double desiredLeftInPixels = physicalCenterOfMonitorX - (actualWidthInPixels / 2.0);
            
            // Clamp X
            desiredLeftInPixels = Math.Max(monitor.WorkArea.Left, Math.Min(desiredLeftInPixels, monitor.WorkArea.Left + monitor.WorkAreaWidth - actualWidthInPixels));

            // Vertical Center / Top Preferred
            // Prefer 256px from top, unless that pushes the window off bottom, or unless the window is so tall it looks better centered.
            // Simple logic: Min(Top+256, Centered)
            double centeredTop = monitor.WorkArea.Top + (monitor.WorkAreaHeight / 2.0) - (actualHeightInPixels / 2.0);
            double preferredTop = monitor.WorkArea.Top + 256;
            
            double desiredTopInPixels = Math.Min(preferredTop, centeredTop);
            
            // Clamp Y (ensure it doesn't go above top)
            desiredTopInPixels = Math.Max(monitor.WorkArea.Top, desiredTopInPixels);

            // Location = new Point(desiredLeftInPixels / monitor.DpiScale, desiredTopInPixels / monitor.DpiScale);
            Left = desiredLeftInPixels / monitor.DpiScale;
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

            // Toggle Anonymization with Alt + P
            if (key == Key.P && Keyboard.Modifiers.HasFlag(ModifierKeys.Alt))
            {
                 TitleFormatter.Anonymize = !TitleFormatter.Anonymize;
                 TitleFormatter.FormatTitlesForDisplay(_unfilteredWindowList);
                 
                 // Refresh search results if active
                 if (!string.IsNullOrEmpty(tb.Text))
                 {
                     TextChanged(tb, null);
                 }
                 e.Handled = true;
                 return;
            }

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
            var focusedListBox = sender as PerformanceListBox;
            if (focusedListBox != null)
            {
                int newIndex = _listBoxes.IndexOf(focusedListBox);
                if (newIndex != -1)
                {
                     SetActiveColumn(newIndex, InitialFocus.NextItem);
                }
            }
        }

        private void SearchBoxContainer_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            // If the box is disabled (Alt-Tab mode), activate it
            if (!tb.IsEnabled)
            {
                _altTabAutoSwitch = false;
                tb.IsEnabled = true;
                tb.Text = ""; // Clear the placeholder
                tb.Focus();
                e.Handled = true;
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
                // Console.WriteLine($"[DEBUG] hotkey_HotkeyPressed: Got monitor with DPI={(cursorMonitor?.DpiScale.ToString("F2") ?? "null")}");

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

        private bool isMultiTaskingViewInForeground()
        {
            _foregroundWindow = SystemWindow.ForegroundWindow;
            
            // If the foreground window closes exactly when we query it, ManagedWinapi throws a Win32Exception.
            try
            {
                if (_foregroundWindow != null && _foregroundWindow.ClassName == "MultitaskingViewFrame")
                {
                    return true;
                }
            }
            catch (System.ComponentModel.Win32Exception)
            {
                // Window handle is invalid (window likely closed). 
                // We assume it's not the Windows Task Switcher and proceed.
            }

            return false;
        }

        private void AltTabPressed(object sender, AltTabHookEventArgs e)
        {
            if (!Settings.Default.AltTabHook)
            {
                // Ignore Alt+Tab presses if the hook is not activated by the user
                return;
            }

            if (isMultiTaskingViewInForeground())
            {
                // If the Windows Task View is open, do not interfere with Alt+Tab
                return;
            }

            e.Handled = true;

            if (Visibility != Visibility.Visible)
            {
                // [PROFILE] Start
                var sw = System.Diagnostics.Stopwatch.StartNew();

                // 1. Measure Monitor Detection
                var cursorMonitor = MonitorHelper.GetMonitorFromCursor();
                long tMonitor = sw.ElapsedMilliseconds;

                // 2. Measure Data Loading (Logic & WinAPI calls)
                LoadData(e.ShiftDown ? InitialFocus.PreviousItem : InitialFocus.NextItem, cursorMonitor);
                long tLoad = sw.ElapsedMilliseconds;

                // 3. Measure UI Rendering & Activation
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
                
                sw.Stop();

                // [PROFILE] Output results
                // If 'Total' > 300ms, Windows will timeout and show the default switcher.
                // Console.WriteLine($"[PROFILE] AltTab Activation:" +
                //                 $" Monitor={tMonitor}ms" +
                //                 $" | LoadData={tLoad - tMonitor}ms" +
                //                 $" | UI/Activate={sw.ElapsedMilliseconds - tLoad}ms" +
                //                 $" | Total={sw.ElapsedMilliseconds}ms");
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

            FilterList(tb.Text);
        }

        private void FilterList(string query)
        {
            // During search, we only modify the center list.
            _listCenter.Clear();

            // If a side column was active, switch to the center for search results.
            int centerIndex = _listBoxes.IndexOf(ListBoxCenter);
            int visibleCenterIndex = _visibleListBoxes.IndexOf(ListBoxCenter);

            if (_activeColumnIndex != visibleCenterIndex)
            {
                SetActiveColumn(centerIndex);
            }

            var context = new WindowFilterContext<AppWindowViewModel>
            {
                Windows = _unfilteredWindowList,
                ForegroundWindowProcessTitle = _foregroundWindow != null ? new AppWindow(_foregroundWindow.HWnd).ProcessTitle : string.Empty
            };

            var filterResultsEnumerable = new WindowFilterer().Filter(context, query);

            // Apply Maximum Result Count limit if enabled
            if (Settings.Default.MaximumResultCountEnabled)
            {
                filterResultsEnumerable = filterResultsEnumerable.Take(Settings.Default.MaximumResultCount);
            }

            var filterResults = filterResultsEnumerable.ToList();

            foreach (var filterResult in filterResults)
            {
                if (TitleFormatter.Anonymize)
                {
                    filterResult.AppWindow.FormattedTitle = TitleFormatter.GetFakeTitle(filterResult.AppWindow.HWnd.ToInt32());
                }
                else
                {
                    filterResult.AppWindow.FormattedTitle = GetFormattedTitleFromBestResult(filterResult.WindowTitleMatchResults);
                }
                filterResult.AppWindow.FormattedProcessTitle = GetFormattedTitleFromBestResult(filterResult.ProcessTitleMatchResults);
                _listCenter.Add(filterResult.AppWindow);
            }

            if (ListBoxCenter.Items.Count > 0 && ListBoxCenter.SelectedIndex == -1)
            {
                ListBoxCenter.SelectedIndex = 0;
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
                        if (item is System.Windows.Controls.MenuItem menuItem)
                        {
                            // Update Pin/Unpin text
                            if (menuItem.Tag != null && menuItem.Tag.ToString() == "PinUnpin")
                            {
                                bool isPinned = IsProcessPinned(window.ProcessTitle);
                                menuItem.Header = isPinned ? "Unpin" : "Pin";
                            }

                            // Update Class Menu Header to show cleaned class name
                            if (menuItem.Name == "MenuHighlightClassCustom")
                            {
                                string cleanName = CleanClassName(window.AppWindow.ClassName);
                                menuItem.Header = $"Custom/Edit '{cleanName}'...";
                            }
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
            var appListBoxes = new System.Windows.Controls.ListBox[] { ListBoxLeft1, ListBoxLeft2, ListBoxLeft3 };
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
            bool anyFailed = false;
            foreach (var win in windowsToClose.ToList()) // Use ToList() to create a copy, avoiding collection modification issues.
            {
                bool isClosed = await _windowCloser.TryCloseAsync(win);
                if (isClosed)
                {
                    RemoveWindowFromAllLists(win);
                }
                else
                {
                    // If this is the first failure, we should activate the window to bring it to the user's attention.
                    if (!anyFailed)
                    {
                        anyFailed = true;
                        win.AppWindow.SwitchToLastVisibleActivePopup();
                    }
                    if (abortOnFailure)
                    {
                        break; // Stop the entire operation if one window fails to close.
                    }
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
            // If Config is open, MainWindow is already hidden via ContextMenu_HighlightConfigure.
            // If Config is NOT open, we want to hide when user clicks away.
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

        private void RefreshHighlights()
        {
            if (_unfilteredWindowList == null) return;

            // Iterate existing view models and re-evaluate rules only
            foreach (var vm in _unfilteredWindowList)
            {
                // Re-match against the updated rules list
                var rule = _highlightService.FindMatch(vm.ProcessTitle, vm.WindowTitle, vm.AppWindow.ClassName);
                
                // Update the properties (Brush/Marker) on the ViewModel
                // Since these properties raise INotifyPropertyChanged, the UI updates instantly
                vm.ApplyHighlight(rule);
            }
        }

        private void ContextMenu_HighlightProcess_Custom(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.MenuItem menuItem && menuItem.DataContext is AppWindowViewModel window)
            {
                OpenHighlightDialog(MatchType.ProcessName, window.ProcessTitle, window.ProcessTitle);
            }
        }

        private void ContextMenu_HighlightClass_Custom(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.MenuItem menuItem && menuItem.DataContext is AppWindowViewModel window)
            {
                string rawClass = window.AppWindow.ClassName;
                string niceClass = CleanClassName(rawClass);

                // Use niceClass for Name, but rawClass for Argument to ensure matching works
                OpenHighlightDialog(MatchType.WindowClass, rawClass, "Class: " + niceClass);
            }
        }

        private void OpenHighlightDialog(MatchType type, string argument, string defaultName)
        {
            // Check for existing to edit
            var existing = _highlightService.Rules.FirstOrDefault(r =>
                r.MatchType == type &&
                string.Equals(r.Argument, argument, StringComparison.OrdinalIgnoreCase));

            EnsureHighlightConfigWindow();

            if (existing != null)
            {
                _highlightConfigWindow.SelectRule(existing);
            }
            else
            {
                // Pre-fill new rule
                var newRule = new HighlightRule
                {
                    Name = defaultName,
                    MatchType = type,
                    Argument = argument,
                    ColorHex = "#FFFF0000",
                    Marker = "🚩"
                };
                _highlightConfigWindow.SetupNewRule(newRule);
            }

            // Use Show() instead of ShowDialog() to remain modeless
            _highlightConfigWindow.Show();
            _highlightConfigWindow.Activate();
        }

        private void ContextMenu_HighlightConfigure(object sender, RoutedEventArgs e)
        {
            EnsureHighlightConfigWindow();

            // Try to select the active rule for this window if one exists
            if (sender is FrameworkElement fe && fe.DataContext is AppWindowViewModel window)
            {
                var rule = _highlightService.FindMatch(window.ProcessTitle, window.WindowTitle, window.AppWindow.ClassName);
                if (rule != null)
                {
                    _highlightConfigWindow.SelectRule(rule);
                }
                else
                {
                    // Create new class matcher rule with no color and symbol
                    var newRule = new HighlightRule
                    {
                        Name = "Class: " + CleanClassName(window.AppWindow.ClassName),
                        MatchType = MatchType.WindowClass,
                        Argument = window.AppWindow.ClassName,
                        ColorHex = "#00000000", // Transparent
                        Marker = ""
                    };
                    _highlightConfigWindow.SetupNewRule(newRule);
                }
            }
            
            // Hide the main window so the Config window acts independently
            HideWindow();
            
            _highlightConfigWindow.Show();
            _highlightConfigWindow.Activate();
            
            // Close context menu manually
            if (sender is System.Windows.Controls.Control ctrl)
            {
                var parent = FindParent<System.Windows.Controls.ContextMenu>(ctrl);
                if (parent != null) parent.IsOpen = false;
            }
        }

        // Unified Handler for Color/Marker changes
        private void ContextMenu_Highlight_Change(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn && btn.DataContext is AppWindowViewModel window)
            {
                string tag = btn.Tag?.ToString();
                
                // 1. Find existing rule that matches this window
                var rule = _highlightService.FindMatch(window.ProcessTitle, window.WindowTitle, window.AppWindow.ClassName);

                if (rule != null)
                {
                    // Modify existing rule
                    UpdateRule(rule, tag);
                }
                else
                {
                    // Create new Class rule
                    string rawClass = window.AppWindow.ClassName;
                    string cleanClass = CleanClassName(rawClass);
                    AddNewRule("Class: " + cleanClass, MatchType.WindowClass, rawClass, tag);
                }

                // Close the Context Menu manually
                var parent = FindParent<System.Windows.Controls.ContextMenu>(btn);
                if (parent != null) parent.IsOpen = false;
            }
        }
        
        public static T FindParent<T>(DependencyObject child) where T : DependencyObject
        {
            DependencyObject parentObject = VisualTreeHelper.GetParent(child);
            if (parentObject == null) return null;
            T parent = parentObject as T;
            if (parent != null) return parent;
            return FindParent<T>(parentObject);
        }

        private void AddNewRule(string name, MatchType matchType, string argument, string tag)
        {
            var rule = new HighlightRule
            {
                Name = name,
                MatchType = matchType,
                Argument = argument,
                ColorHex = Colors.Transparent.ToString(), 
                Marker = ""
            };
            _highlightService.AddRule(rule);
            UpdateRule(rule, tag);
        }

        private void UpdateRule(HighlightRule rule, string tag)
        {
            string newColorHex = null;
            string newMarker = null;
            bool isColorUpdate = false;
            bool isMarkerUpdate = false;

            switch (tag)
            {
                case "Red": newColorHex = Colors.Red.ToString(); isColorUpdate = true; break;
                case "Orange": newColorHex = Colors.Orange.ToString(); isColorUpdate = true; break;
                case "Green": newColorHex = Colors.Green.ToString(); isColorUpdate = true; break;
                case "Blue": newColorHex = Colors.Blue.ToString(); isColorUpdate = true; break;
                case "Purple": newColorHex = Colors.Purple.ToString(); isColorUpdate = true; break;
                
                case "Flag": newMarker = "🚩"; isMarkerUpdate = true; break;
                case "Star": newMarker = "⭐"; isMarkerUpdate = true; break;
                case "Warn": newMarker = "⚠️"; isMarkerUpdate = true; break;
                case "Fire": newMarker = "🔥"; isMarkerUpdate = true; break;
            }

            if (isColorUpdate)
            {
                // Toggle logic: If same color, remove it
                if (string.Equals(rule.ColorHex, newColorHex, StringComparison.OrdinalIgnoreCase))
                    rule.ColorHex = Colors.Transparent.ToString();
                else
                    rule.ColorHex = newColorHex;
            }
            else if (isMarkerUpdate)
            {
                // Toggle logic: If same marker, remove it
                if (string.Equals(rule.Marker, newMarker, StringComparison.Ordinal))
                    rule.Marker = "";
                else
                    rule.Marker = newMarker;
            }

            // Cleanup if empty
            bool hasColor = !string.IsNullOrEmpty(rule.ColorHex) && !rule.ColorHex.StartsWith("#00");
            bool hasMarker = !string.IsNullOrEmpty(rule.Marker);

            if (!hasColor && !hasMarker)
            {
                _highlightService.RemoveRule(rule);
            }
            else
            {
                _highlightService.SaveRules();
            }
        }

        private void EnsureHighlightConfigWindow()
        {
            if (_highlightConfigWindow == null || !_highlightConfigWindow.IsLoaded)
            {
                _highlightConfigWindow = new HighlightConfigWindow(_highlightService);
                _highlightConfigWindow.Closed += (s, e) => _highlightConfigWindow = null;
            }
        }

        private static string CleanClassName(string className)
        {
            if (string.IsNullOrEmpty(className)) return "";

            // Handle HwndWrapper[App.exe;;guid] -> App.exe
            if (className.StartsWith("HwndWrapper[") && className.Contains(";;"))
            {
                try
                {
                    int start = "HwndWrapper[".Length;
                    int end = className.IndexOf(";;", start);
                    if (end > start)
                    {
                        return className.Substring(start, end - start);
                    }
                }
                catch { }
            }

            return className;
        }

        #endregion

        private enum InitialFocus
        {
            NextItem,
            PreviousItem
        }
    }

    public class HighlightStateConverter : System.Windows.Data.IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            // Value[0]: The Button's Tag (string) e.g., "Red", "Flag"
            // Value[1]: The HighlightBackgroundBrush (SolidColorBrush)
            // Value[2]: The Marker (string)
            
            if (values.Length < 3 || values[0] == null)
                return false;

            string tag = values[0].ToString();
            
            // Extract current color from brush safely
            Color currentColor = Colors.Transparent;
            if (values[1] is SolidColorBrush brush)
            {
                currentColor = brush.Color;
            }

            string currentMarker = values[2] as string;

            // Define expectations based on Tag
            Color? targetColor = null;
            string targetMarker = null;

            try 
            {
                switch (tag)
                {
                    // Colors (Alpha 0x40 = 64)
                    case "Red": targetColor = (Color)ColorConverter.ConvertFromString("#40FF0000"); break;
                    case "Orange": targetColor = (Color)ColorConverter.ConvertFromString("#40FFA500"); break;
                    case "Green": targetColor = (Color)ColorConverter.ConvertFromString("#40008000"); break;
                    case "Blue": targetColor = (Color)ColorConverter.ConvertFromString("#400000FF"); break;
                    case "Purple": targetColor = (Color)ColorConverter.ConvertFromString("#40800080"); break;
                    
                    // Symbols
                    case "Flag": targetMarker = "🚩"; break;
                    case "Star": targetMarker = "⭐"; break;
                    case "Check": targetMarker = "✅"; break;
                    case "Warn": targetMarker = "⚠️"; break;
                    case "Fire": targetMarker = "🔥"; break;
                }
            }
            catch { return false; }

            // Comparison
            if (targetColor.HasValue)
            {
                return currentColor == targetColor.Value;
            }
            if (targetMarker != null)
            {
                return string.Equals(currentMarker, targetMarker, StringComparison.Ordinal);
            }

            return false;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}