using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;
using Switcheroo.Core.Highlighting;
using Switcheroo.Properties;

namespace Switcheroo.Highlighting
{
    public class HighlightService
    {
        private static readonly XmlSerializer _serializer = new XmlSerializer(typeof(List<HighlightRule>));
        
        private readonly HighlightMatcher _matcher = new HighlightMatcher();

        public List<HighlightRule> Rules { get; private set; }
        public event EventHandler RulesChanged;

        public HighlightService()
        {
            LoadRules();
        }

        private void LoadRules()
        {
            var data = Settings.Default.HighlightRules;
            if (string.IsNullOrEmpty(data))
            {
                Rules = new List<HighlightRule>();
                return;
            }

            try
            {
                using (var reader = new StringReader(data))
                {
                    Rules = (List<HighlightRule>)_serializer.Deserialize(reader);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error loading highlight rules: " + ex.Message);
                Rules = new List<HighlightRule>();
            }
        }

        public void SaveRules()
        {
            try
            {
                using (var writer = new StringWriter())
                {
                    _serializer.Serialize(writer, Rules);
                    Settings.Default.HighlightRules = writer.ToString();
                    Settings.Default.Save();
                }
                RulesChanged?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error saving highlight rules: " + ex.Message);
            }
        }

        public HighlightRule FindMatch(string processTitle, string windowTitle, string className)
        {
            foreach (var rule in Rules)
            {
                if (_matcher.IsMatch(rule, processTitle, windowTitle, className))
                {
                    return rule;
                }
            }
            return null;
        }

        public void AddRule(HighlightRule rule)
        {
            Rules.Add(rule);
            SaveRules();
        }

        public void RemoveRule(HighlightRule rule)
        {
            Rules.Remove(rule);
            SaveRules();
        }

        public void MoveRuleUp(HighlightRule rule)
        {
            int index = Rules.IndexOf(rule);
            if (index > 0)
            {
                Rules.RemoveAt(index);
                Rules.Insert(index - 1, rule);
                SaveRules();
            }
        }

        public void MoveRuleDown(HighlightRule rule)
        {
            int index = Rules.IndexOf(rule);
            if (index >= 0 && index < Rules.Count - 1)
            {
                Rules.RemoveAt(index);
                Rules.Insert(index + 1, rule);
                SaveRules();
            }
        }
    }
}
