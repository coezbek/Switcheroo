using System.Collections.Generic;
using System.Linq;
using Switcheroo.Core;
using Switcheroo.Core.Matchers;

namespace Switcheroo
{
    public static class TitleFormatter
    {
        // Formats titles for display, applying highlighting and stripping common suffixes
        public static void FormatTitlesForDisplay(List<AppWindowViewModel> windows)
        {
            var highlighter = new XamlHighlighter();

            var processGroups = windows.GroupBy(w => w.ProcessTitle).ToList();
            foreach (var group in processGroups)
            {
                var windowsInGroup = group.ToList();
                var titles = windowsInGroup.Select(w => w.WindowTitle).ToList();
                var commonSuffix = titles
                    .Select(t => t.Contains(" - ") ? t.Substring(t.LastIndexOf(" - ")) : null)
                    .Where(s => !string.IsNullOrEmpty(s))
                    .GroupBy(s => s)
                    .OrderByDescending(g => g.Count())
                    .FirstOrDefault()?.Key;
                                    
                if (commonSuffix != null && titles.Count(t => t.EndsWith(commonSuffix)) < titles.Count / 2.0)
                    commonSuffix = null;


                foreach (var window in windowsInGroup)
                {
                    // Determine display title for window:
                    var displayTitle = window.WindowTitle;
                    if (commonSuffix != null && window.WindowTitle.EndsWith(commonSuffix))
                    {
                        var prefix = window.WindowTitle.Substring(0, window.WindowTitle.Length - commonSuffix.Length).TrimEnd();
                        if (prefix.Length > 0)
                            displayTitle = prefix;
                    }

                    window.FormattedTitle = highlighter.Highlight(new[] { new StringPart(displayTitle) });

                    // Determine process title for window:
                    var processTitleToShow = window.ProcessTitle;
                    
                    // If process title is all caps or all lower caps, convert to title case for better readability
                    if (processTitleToShow.All(c => !char.IsLetter(c) || char.IsUpper(c)) || 
                        processTitleToShow.All(c => !char.IsLetter(c) || char.IsLower(c)))
                        processTitleToShow = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(processTitleToShow.ToLower());

                    window.FormattedProcessTitle = highlighter.Highlight(new[] { new StringPart(processTitleToShow) });
                }
            }
        }
    }
}