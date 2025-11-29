using System.Windows.Media;

namespace Switcheroo.Highlighting
{
    public class ConfiguredColor
    {
        public string Name { get; set; }
        public Color Color { get; set; }
        public string Hex => Color.ToString();

        public ConfiguredColor(string name, Color color)
        {
            Name = name;
            Color = color;
        }

        public ConfiguredColor(string name, string hex)
        {
            Name = name;
            try
            {
                Color = (Color)ColorConverter.ConvertFromString(hex);
            }
            catch
            {
                Color = Colors.Transparent;
            }
        }
    }
}
