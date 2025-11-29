using System;
using System.Windows;
using Microsoft.Win32;
using Switcheroo.Properties;

namespace Switcheroo
{
    public static class Theme
    {
        public enum Mode { Light, Dark, System }

        public static event EventHandler Changed;

        public static void Apply()
        {
            Mode mode = GetMode();
            if (mode == Mode.System)
            {
                mode = IsSystemInDarkMode() ? Mode.Dark : Mode.Light;
            }

            string dictUri = mode == Mode.Dark
                ? "/Switcheroo;component/Themes/Dark.xaml"
                : "/Switcheroo;component/Themes/Light.xaml";

            // Get the application resources
            var dicts = Application.Current.Resources.MergedDictionaries;

            // Clear existing theme dictionaries
            dicts.Clear();

            // Add the new theme
            dicts.Add(new ResourceDictionary { Source = new Uri(dictUri, UriKind.Relative) });

            Changed?.Invoke(null, EventArgs.Empty);
        }

        public static bool IsUsingDarkTheme()
        {
            Mode mode = GetMode();
            if (mode == Mode.System)
            {
                return IsSystemInDarkMode();
            }
            return mode == Mode.Dark;
        }

        private static Mode GetMode()
        {
            if (!Enum.TryParse(Settings.Default.Theme, out Mode mode))
                return Mode.System;
            return mode;
        }

        private static bool IsSystemInDarkMode()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    return (key?.GetValue("AppsUseLightTheme") is int val) && val == 0;
                }
            }
            catch { }
            return false;
        }
    }
}