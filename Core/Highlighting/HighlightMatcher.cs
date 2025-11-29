using System;
using System.Text.RegularExpressions;

namespace Switcheroo.Core.Highlighting
{
    public class HighlightMatcher
    {
        public bool IsMatch(HighlightRule rule, string processTitle, string windowTitle, string className)
        {
            if (rule == null || string.IsNullOrEmpty(rule.Argument)) return false;

            switch (rule.MatchType)
            {
                case MatchType.ProcessName:
                    // Case-insensitive exact match
                    return string.Equals(processTitle, rule.Argument, StringComparison.OrdinalIgnoreCase);

                case MatchType.WindowClass:
                    return string.Equals(className, rule.Argument, StringComparison.OrdinalIgnoreCase);

                case MatchType.WindowTitleRegex:
                    var regex = rule.GetRegex();
                    if (regex != null)
                    {
                        return regex.IsMatch(windowTitle ?? "");
                    }
                    return false;
            }
            return false;
        }
    }
}
