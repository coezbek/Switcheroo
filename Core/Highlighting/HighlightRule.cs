using System;
using System.Xml.Serialization;
using System.Text.RegularExpressions;

namespace Switcheroo.Core.Highlighting
{
    public enum MatchType
    {
        ProcessName,
        WindowTitleRegex,
        WindowClass
    }

    [Serializable]
    public class HighlightRule
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; }

        public MatchType MatchType { get; set; }

        private string _argument;
        public string Argument
        {
            get { return _argument; }
            set
            {
                if (_argument != value)
                {
                    _argument = value;
                    _cachedRegex = null;
                }
            }
        }

        // Visuals
        public string ColorHex { get; set; } // #RRGGBB or #AARRGGBB
        public string Marker { get; set; } // Text/Emoji

        [XmlIgnore]
        private Regex _cachedRegex;

        public Regex GetRegex()
        {
            if (_cachedRegex == null && MatchType == MatchType.WindowTitleRegex && !string.IsNullOrEmpty(Argument))
            {
                try
                {
                    _cachedRegex = new Regex(Argument, RegexOptions.IgnoreCase);
                }
                catch
                {
                    // Invalid regex, ignore or handle gracefully
                }
            }
            return _cachedRegex;
        }

        public HighlightRule() { }
    }
}