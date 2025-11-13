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
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
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
        private OptionsWindow _optionsWindow;
        private AboutWindow _aboutWindow;
        private AltTabHook _altTabHook;
        private SystemWindow _foregroundWindow;
        private bool _altTabAutoSwitch;

        // New collections for each column
        private ObservableCollection<AppWindowViewModel> _listLeft1;
        private ObservableCollection<AppWindowViewModel> _listLeft2;
        private ObservableCollection<AppWindowViewModel> _listLeft3;
        private ObservableCollection<AppWindowViewModel> _listCenter;
        private ObservableCollection<AppWindowViewModel> _listRight;

        // For navigation
        private readonly List<System.Windows.Controls.ListBox> _listBoxes;
        private int _activeColumnIndex = 3; // Center is the default

        public MainWindow()
        {
            InitializeComponent();
            
            _listLeft1 = new ObservableCollection<AppWindowViewModel>();
            _listLeft2 = new ObservableCollection<AppWindowViewModel>();
            _listLeft3 = new ObservableCollection<AppWindowViewModel>();
            _listCenter = new ObservableCollection<AppWindowViewModel>();
            _listRight = new ObservableCollection<AppWindowViewModel>();
            
            _listBoxes = new List<System.Windows.Controls.ListBox> { ListBoxLeft1, ListBoxLeft2, ListBoxLeft3, ListBoxCenter, ListBoxRight };

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
                    int count = _listBoxes.Count;
                    int next = (_activeColumnIndex + (scrollUpLeft ? -1 : +1) + count) % count;
                    SetActiveColumn(next);
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
                .Take(3)
                .ToList();

            var min_number_of_windows_for_own_column = 0;

            if (topApps.Count > 2 && topApps[2].ToList().Count >= min_number_of_windows_for_own_column)
            {
                foreach (var window in topApps[2]) { _listLeft1.Add(window); handledHwnds.Add(window.HWnd); }
            }
            if (topApps.Count > 1 && topApps[1].ToList().Count >= min_number_of_windows_for_own_column)
            {
                foreach (var window in topApps[1]) { _listLeft2.Add(window); handledHwnds.Add(window.HWnd); }
            }
            if (topApps.Count > 0 && topApps[0].ToList().Count >= min_number_of_windows_for_own_column)
            {
                foreach (var window in topApps[0]) { _listLeft3.Add(window); handledHwnds.Add(window.HWnd); }
            }

            var first10Windows = _unfilteredWindowList.Take(10);
            var first10Set= new HashSet<IntPtr>();    
            foreach (var window in first10Windows)
            {
                first10Set.Add(window.HWnd);
            }

            // We always keep the first 10 windows in the center column (no matter if they are already in left/right)
            var remainingForCenter = _unfilteredWindowList.Where(w => first10Set.Contains(w.HWnd) || !handledHwnds.Contains(w.HWnd));
            var centerWindows = remainingForCenter.ToList();
            foreach (var window in centerWindows)
            {
                _listCenter.Add(window);
            }

            _windowCloser = new WindowCloser();

            // Set initial formatted titles for all windows
            TitleFormatter.FormatTitlesForDisplay(_unfilteredWindowList);

            SetActiveColumn(3, focus);

            // Correct the selection for the initial Shift+Alt+Tab after the active window has been moved to the end
            if (foregroundWindowMovedToBottom && focus == InitialFocus.PreviousItem)
            {
                PreviousItem();
            }

            tb.Clear();
            tb.Focus();
            CenterWindow();
            ScrollSelectedItemIntoView();
        }

        private static bool AreWindowsRelated(SystemWindow window1, SystemWindow window2)
        {
            return window1.HWnd == window2.HWnd || window1.Process.Id == window2.Process.Id;
        }
        
        private void SetActiveColumn(int index, InitialFocus focus = InitialFocus.NextItem)
        {
            if (index < 0 || index >= _listBoxes.Count) return;

            if (_activeColumnIndex >= 0 && _activeColumnIndex < _listBoxes.Count)
            {
                _listBoxes[_activeColumnIndex].Background = Brushes.Transparent;
            }

            _activeColumnIndex = index;
            var currentListBox = _listBoxes[_activeColumnIndex];

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
            double columnWidth = Math.Max(100, Settings.Default.UserWidth > 0 ? Settings.Default.UserWidth : 250);

            // --- Part 1: Configure Layout ---
            // Set column widths and visibility based on whether they contain items.
            var numVisibleLeftColumns = 0;
            if (_listLeft1.Any())
            {
                numVisibleLeftColumns++;
                ColLeft1.Width = new GridLength(columnWidth);
                ListBoxLeft1.Visibility = Visibility.Visible;
            }
            else
            {
                ColLeft1.Width = new GridLength(0);
                ListBoxLeft1.Visibility = Visibility.Collapsed;
            }

            if (_listLeft2.Any())
            {
                numVisibleLeftColumns++;
                ColLeft2.Width = new GridLength(columnWidth);
                ListBoxLeft2.Visibility = Visibility.Visible;
            }
            else
            {
                ColLeft2.Width = new GridLength(0);
                ListBoxLeft2.Visibility = Visibility.Collapsed;
            }

            if (_listLeft3.Any())
            {
                numVisibleLeftColumns++;
                ColLeft3.Width = new GridLength(columnWidth);
                ListBoxLeft3.Visibility = Visibility.Visible;
            }
            else
            {
                ColLeft3.Width = new GridLength(0);
                ListBoxLeft3.Visibility = Visibility.Collapsed;
            }

            // The center column is always conceptually present and given width.
            ColCenter.Width = new GridLength(columnWidth);
            ListBoxCenter.Visibility = Visibility.Visible;

            var numVisibleRightColumns = 0;
            if (_listRight.Any())
            {
                numVisibleRightColumns = 1;
                ColRight.Width = new GridLength(columnWidth);
                ListBoxRight.Visibility = Visibility.Visible;
            }
            else
            {
                ColRight.Width = new GridLength(0);
                ListBoxRight.Visibility = Visibility.Collapsed;
            }

            // --- Part 2: Position Window ---
            var numColumns = numVisibleLeftColumns + 1 + numVisibleRightColumns;
            double screenWidth = SystemParameters.PrimaryScreenWidth;

            // Set the correct final width & height
            Width = Math.Min(numColumns * columnWidth, screenWidth * 0.95);
            Border.MaxHeight = SystemParameters.PrimaryScreenHeight * 0.9;

            // Force layout update to get correct ActualHeight for vertical centering.
            UpdateLayout();

            // The anchor is the screen's center. We want the left side of our middle column to be offset from that.
            var centerColumnLeftOffset = (numVisibleLeftColumns * columnWidth);
            Left = Math.Max(0, (SystemParameters.PrimaryScreenWidth / 2.0) - centerColumnLeftOffset);

            // Try to top align the window to 256px top if sufficient space.
            Top = Math.Min(256, (SystemParameters.PrimaryScreenHeight / 2.0) - (ActualHeight / 2.0));
        }

        /// <summary>
        /// Switches the window associated with the selected item.
        /// </summary>
        private void Switch()
        {
            var currentListBox = _listBoxes[_activeColumnIndex];
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
                int nextIndex = _activeColumnIndex - 1;
                // If we moved left from the far-left column, or if we are on the far-right column,
                // jump to the center column to start the rotation.
                if (nextIndex < 0 || _activeColumnIndex == 4)
                {
                    nextIndex = 3;
                }
                SetActiveColumn(nextIndex);
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
                        SetActiveColumn(nextIndex);
                    }
                    return;
                }

                if (key == Key.Right)
                {
                    e.Handled = true;
                    int nextIndex = _activeColumnIndex + 1;
                    if (nextIndex < _listBoxes.Count)
                    {
                        SetActiveColumn(nextIndex);
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
                if (newIndex != -1 && newIndex != _activeColumnIndex)
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
                tb.IsEnabled = true;

                _foregroundWindow = SystemWindow.ForegroundWindow;
                Show();
                Activate();
                Keyboard.Focus(tb);
                LoadData(InitialFocus.NextItem);
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
                tb.IsEnabled = true;

                ActivateAndFocusMainWindow();

                Keyboard.Focus(tb);
                LoadData(e.ShiftDown ? InitialFocus.PreviousItem : InitialFocus.NextItem);

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
            if (_activeColumnIndex != 3)
            {
                SetActiveColumn(3);
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
                    var currentListBox = _listBoxes[_activeColumnIndex];
                    win = currentListBox.SelectedItem as AppWindowViewModel;
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
            var currentListBox = _listBoxes[_activeColumnIndex];
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
            if (_activeColumnIndex >= 0 && _activeColumnIndex <= 2)
            {
                var currentListBox = _listBoxes[_activeColumnIndex];
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
            // Before closing, capture the current state for intelligent UI updates later.
            var originalIndex = _activeColumnIndex;
            var originalListBox = _listBoxes[originalIndex];
            var selectedIndex = originalListBox.SelectedIndex;

            // Calculate the expected index adjustment. We need to know how many windows *before*
            // our selection are being closed, so we can adjust our selection index downward to maintain
            // the cursor's relative position.
            var windowsToCloseSet = new HashSet<AppWindowViewModel>(windowsToClose);
            int adjustment = 0;
            for (int i = 0; i < selectedIndex; i++)
            {
                if (windowsToCloseSet.Contains(originalListBox.Items[i]))
                {
                    adjustment++;
                }
            }

            // Sequentially attempt to close the windows.
            foreach (var win in windowsToClose)
            {
                bool isClosed = await _windowCloser.TryCloseAsync(win);
                if (isClosed)
                {
                    RemoveWindowFromAllLists(win);
                }
                else if (abortOnFailure)
                {
                    // A window failed to close, and we are configured to abort the entire operation.
                    break;
                }
            }

            // After the operation is complete, update the UI.
            if (_unfilteredWindowList.Count == 0)
            {
                HideWindow();
                return;
            }

            if (originalListBox.Items.Count > 0)
            {
                // The column is still active, so update the selection within it.
                // Apply the calculated adjustment to find the new logical index.
                int newSelectedIndex = selectedIndex - adjustment;

                // Clamp the new index to be within the valid range of the now-smaller list.
                originalListBox.SelectedIndex = Math.Max(0, Math.Min(newSelectedIndex, originalListBox.Items.Count - 1));
            }
            else
            {
                // The column we were in is now empty, find the next logical column to focus on.
                bool foundNewColumn = false;
                // Search right, towards the center.
                for (int i = originalIndex + 1; i < _listBoxes.Count; i++)
                {
                    if (_listBoxes[i].Items.Count > 0)
                    {
                        SetActiveColumn(i);
                        foundNewColumn = true;
                        break;
                    }
                }
                // If not found, search left.
                if (!foundNewColumn)
                {
                    for (int i = originalIndex - 1; i >= 0; i--)
                    {
                        if (_listBoxes[i].Items.Count > 0)
                        {
                            SetActiveColumn(i);
                            break;
                        }
                    }
                }
            }

            ScrollSelectedItemIntoView();
        }

        
        private void DismissWindow(object sender, ExecutedRoutedEventArgs e)
        {
            HideWindow();
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
            var currentListBox = _listBoxes[_activeColumnIndex];
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
            var currentListBox = _listBoxes[_activeColumnIndex];
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
            var currentListBox = _listBoxes[_activeColumnIndex];
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

        #endregion

        private enum InitialFocus
        {
            NextItem,
            PreviousItem
        }
    }
}