using System;
using System.Collections.Generic;
using System.Linq;
using Switcheroo.Core;
using Switcheroo.Core.Matchers;

namespace Switcheroo
{
    public static class TitleFormatter
    {
        public static bool Anonymize { get; set; }

        private static readonly string[] LoremWords = 
        { 
            "lorem", "ipsum", "dolor", "sit", "amet", "consectetur", "adipiscing", "elit", 
            "sed", "do", "eiusmod", "tempor", "incididunt", "ut", "labore", "et", "dolore", 
            "magna", "aliqua", "ut", "enim", "ad", "minim", "veniam", "quis", "nostrud", 
            "exercitation", "ullamco", "laboris", "nisi", "ut", "aliquip", "ex", "ea", 
            "commodo", "consequat", "duis", "aute", "irure", "dolor", "in", "reprehenderit", 
            "in", "voluptate", "velit", "esse", "cillum", "dolore", "eu", "fugiat", "nulla", 
            "pariatur" 
        };

        public static string GetFakeTitle(int seed)
        {
            var rnd = new Random(seed);
            int words = rnd.Next(3, 8);
            var result = new List<string>();
            for (int i = 0; i < words; i++)
            {
                result.Add(LoremWords[rnd.Next(LoremWords.Length)]);
            }
            var sentence = string.Join(" ", result);
            return char.ToUpper(sentence[0]) + sentence.Substring(1);
        }

        // Formats titles for display, applying highlighting and stripping common suffixes
        public static void FormatTitlesForDisplay(List<AppWindowViewModel> windows)
        {
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
                    if (Anonymize)
                    {
                        window.FormattedTitle = GetFakeTitle(window.HWnd.ToInt32());
                    }
                    else
                    {
                        // Determine display title for window:
                        var displayTitle = window.WindowTitle;
                        if (commonSuffix != null && window.WindowTitle.EndsWith(commonSuffix))
                        {
                            var prefix = window.WindowTitle.Substring(0, window.WindowTitle.Length - commonSuffix.Length).TrimEnd();
                            if (prefix.Length > 0)
                                displayTitle = prefix;
                        }

                        window.FormattedTitle = displayTitle;
                    }

                    // Determine process title for window:
                    var processTitleToShow = window.ProcessTitle;
                    
                    // If process title is all caps or all lower caps, convert to title case for better readability
                    if (processTitleToShow.All(c => !char.IsLetter(c) || char.IsUpper(c)) || 
                        processTitleToShow.All(c => !char.IsLetter(c) || char.IsLower(c)))
                        processTitleToShow = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(processTitleToShow.ToLower());

                    window.FormattedProcessTitle = processTitleToShow;
                }
            }
        }
    }
}