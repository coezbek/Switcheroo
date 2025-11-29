using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using Switcheroo.Core.Highlighting;

namespace Switcheroo.Highlighting
{
    public partial class HighlightConfigWindow : Window
    {
        private readonly HighlightService _service;
        private HighlightRule _currentRule;

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

        public HighlightConfigWindow(HighlightService service)
        {
            InitializeComponent();
            _service = service;

            CmbType.ItemsSource = Enum.GetValues(typeof(MatchType));

            var colors = new List<ConfiguredColor>
            {
                new ConfiguredColor("None", Colors.Transparent),
                new ConfiguredColor("Red", Colors.Red),
                new ConfiguredColor("Orange", Colors.Orange),
                new ConfiguredColor("Green", Colors.Green),
                new ConfiguredColor("Blue", Colors.Blue),
                new ConfiguredColor("Purple", Colors.Purple),
            };
            CmbColor.ItemsSource = colors;

            RefreshList();
            
            Theme.Changed += Theme_Changed;
            Closed += (s, e) => Theme.Changed -= Theme_Changed;
        }

        private void Theme_Changed(object sender, EventArgs e)
        {
            UpdateWindowChrome();
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            UpdateWindowChrome();
        }

        private void UpdateWindowChrome()
        {
            try
            {
                var hwnd = new WindowInteropHelper(this).Handle;
                // Check if we are using the dark theme resource
                bool isDark = Theme.IsUsingDarkTheme(); 
                int useImmersiveDarkMode = isDark ? 1 : 0;
                DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref useImmersiveDarkMode, sizeof(int));
            }
            catch
            {
                // DWM API might not be available or failed
            }
        }

        private void RefreshList()
        {
            // 1. Capture the rule we want to keep selected BEFORE clearing the list
            var ruleToRestore = _currentRule;

            // 2. Reset the source
            RulesList.ItemsSource = null;
            RulesList.ItemsSource = _service.Rules;

            // 3. Restore the selection using the local variable
            if (ruleToRestore != null && _service.Rules.Contains(ruleToRestore))
            {
                RulesList.SelectedItem = ruleToRestore;
            }
        }

        public void SelectRule(HighlightRule rule)
        {
            if (_service.Rules.Contains(rule))
            {
                RulesList.SelectedItem = rule;
            }
        }

        public void SetupNewRule(HighlightRule rule)
        {
            _service.AddRule(rule);
            RefreshList();
            SelectRule(rule);
        }

        private void RulesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _currentRule = RulesList.SelectedItem as HighlightRule;
            EditorPanel.IsEnabled = _currentRule != null;

            if (_currentRule != null)
            {
                TxtName.Text = _currentRule.Name;
                CmbType.SelectedItem = _currentRule.MatchType;
                TxtPattern.Text = _currentRule.Argument;
                TxtMarker.Text = _currentRule.Marker;

                try
                {
                    Color ruleColor = (Color)ColorConverter.ConvertFromString(_currentRule.ColorHex);

                    if (ruleColor.A == 0)
                        // If fully transparent, select "None"
                        CmbColor.SelectedIndex = 0;
                    else
                    {
                        // Force Alpha to 255 (Opaque) to match the Dropdown items
                        ruleColor.A = 255;
                        CmbColor.SelectedValue = ruleColor.ToString();
                    }
                }
                catch 
                {
                    CmbColor.SelectedIndex = 0;
                }

                // Fallback: If no color matches (e.g. null, empty, or different transparent code), select None
                if (CmbColor.SelectedIndex == -1)
                    CmbColor.SelectedIndex = 0;
            }
        }

        private void BtnAdd_Click(object sender, RoutedEventArgs e)
        {
            var rule = new HighlightRule
            {
                Name = "New Rule",
                MatchType = MatchType.ProcessName,
                ColorHex = Colors.Transparent.ToString()
            };
            _service.AddRule(rule);
            RefreshList();
            RulesList.SelectedItem = rule;
        }

        private void BtnRemove_Click(object sender, RoutedEventArgs e)
        {
            if (RulesList.SelectedItem is HighlightRule rule)
            {
                int selectedIndex = RulesList.SelectedIndex;
                _service.RemoveRule(rule);
                _currentRule = null;
                RefreshList();

                if (_service.Rules.Count > 0)
                {
                    // Select the item at the same index (next item), or the last item if we removed the last one
                    RulesList.SelectedIndex = Math.Min(selectedIndex, _service.Rules.Count - 1);
                }
            }
        }

        private void BtnUp_Click(object sender, RoutedEventArgs e)
        {
            if (RulesList.SelectedItem is HighlightRule rule)
            {
                _service.MoveRuleUp(rule);
                RefreshList();
                RulesList.SelectedItem = rule;
            }
        }

        private void BtnDown_Click(object sender, RoutedEventArgs e)
        {
            if (RulesList.SelectedItem is HighlightRule rule)
            {
                _service.MoveRuleDown(rule);
                RefreshList();
                RulesList.SelectedItem = rule;
            }
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            if (_currentRule != null)
            {
                if ((MatchType)CmbType.SelectedItem == MatchType.WindowTitleRegex)
                {
                    try
                    {
                        new System.Text.RegularExpressions.Regex(TxtPattern.Text);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Invalid Regex Pattern: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                }
                _currentRule.Name = TxtName.Text;
                _currentRule.MatchType = (MatchType)CmbType.SelectedItem;
                _currentRule.Argument = TxtPattern.Text;
                _currentRule.Marker = TxtMarker.Text;
                _currentRule.ColorHex = (string)CmbColor.SelectedValue;

                _service.SaveRules();
                RefreshList();
                RulesList.SelectedItem = _currentRule;
            }
        }

        private void Emoji_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn)
            {
                if (btn.Content is Emoji.Wpf.TextBlock emojiBlock)
                {
                    TxtMarker.Text = emojiBlock.Text;
                }
                else
                {
                    TxtMarker.Text = btn.Content?.ToString();
                }
            }
        }
    }
}