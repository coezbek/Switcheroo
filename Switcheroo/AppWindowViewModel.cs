using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using Switcheroo.Core;
using Switcheroo.Core.Highlighting;

namespace Switcheroo
{
    public class AppWindowViewModel : INotifyPropertyChanged, IWindowText
    {
        public AppWindowViewModel(AppWindow appWindow)
        {
            AppWindow = appWindow;
        }

        public AppWindow AppWindow { get; private set; }

        #region IWindowText Members

        public string WindowTitle
        {
            get { return AppWindow.Title; }
        }

        public string ProcessTitle
        {
            get { return AppWindow.ProcessTitle; }
        }

        #endregion

        #region Bindable properties

        public IntPtr HWnd
        {
            get { return AppWindow.HWnd; }
        }

        private string _formattedTitle;

        public string FormattedTitle
        {
            get { return _formattedTitle; }
            set
            {
                _formattedTitle = value;
                NotifyOfPropertyChange(() => FormattedTitle);
            }
        }

        private string _formattedProcessTitle;

        public string FormattedProcessTitle
        {
            get { return _formattedProcessTitle; }
            set
            {
                _formattedProcessTitle = value;
                NotifyOfPropertyChange(() => FormattedProcessTitle);
            }
        }

        private bool _isBeingClosed = false;

        public bool IsBeingClosed
        {
            get { return _isBeingClosed; }
            set
            {
                _isBeingClosed = value;
                NotifyOfPropertyChange(() => IsBeingClosed);
            }
        }

        // Highlighting support
        private Brush _highlightBackgroundBrush = Brushes.Transparent;
        public Brush HighlightBackgroundBrush
        {
            get { return _highlightBackgroundBrush; }
            set { _highlightBackgroundBrush = value; NotifyOfPropertyChange(() => HighlightBackgroundBrush); }
        }

        private Brush _highlightSolidBrush = Brushes.Transparent;
        public Brush HighlightSolidBrush
        {
            get { return _highlightSolidBrush; }
            set { _highlightSolidBrush = value; NotifyOfPropertyChange(() => HighlightSolidBrush); }
        }

        private Brush _highlightBorderBrush = Brushes.Transparent;
        public Brush HighlightBorderBrush
        {
            get { return _highlightBorderBrush; }
            set { _highlightBorderBrush = value; NotifyOfPropertyChange(() => HighlightBorderBrush); }
        }

        private System.Windows.Thickness _highlightBorderThickness = new System.Windows.Thickness(0);
        public System.Windows.Thickness HighlightBorderThickness
        {
            get { return _highlightBorderThickness; }
            set { _highlightBorderThickness = value; NotifyOfPropertyChange(() => HighlightBorderThickness); }
        }

        private string _marker;
        public string Marker
        {
            get { return _marker; }
            set { _marker = value; NotifyOfPropertyChange(() => Marker); }
        }

        public void ApplyHighlight(HighlightRule rule, bool notify = true)
        {
            // Default values
            Brush bg = Brushes.Transparent;
            Brush solid = Brushes.Transparent;
            Brush border = Brushes.Transparent;
            System.Windows.Thickness thickness = new System.Windows.Thickness(0);
            string marker = null;

            if (rule != null)
            {
                if (!string.IsNullOrEmpty(rule.ColorHex))
                {
                    try
                    {
                        var color = (Color)ColorConverter.ConvertFromString(rule.ColorHex);

                        if (color.A != 0)
                        {
                            // Transparent for normal state (matches Hex from ContextMenu which uses 0x40/64 alpha)
                            var b = new SolidColorBrush(Color.FromArgb(0x40, color.R, color.G, color.B));
                            b.Freeze();
                            bg = b;

                            // Solid for selected state (covers the system selection color)
                            var s = new SolidColorBrush(Color.FromArgb(0x80, color.R, color.G, color.B));
                            s.Freeze();
                            solid = s;

                            // Border to be Opaque (0xFF) so it is visible
                            var br = new SolidColorBrush(Color.FromArgb(0xFF, color.R, color.G, color.B));
                            br.Freeze();
                            border = br;
                            
                            thickness = new System.Windows.Thickness(1);
                        }
                    }
                    catch { }
                }
                marker = rule.Marker;
            }

            // Normalize empty string to null for UI triggers
            if (string.IsNullOrEmpty(marker)) 
                marker = null;

            if (notify)
            {
                HighlightBackgroundBrush = bg;
                HighlightSolidBrush = solid;
                HighlightBorderBrush = border;
                HighlightBorderThickness = thickness;
                Marker = marker;
            }
            else
            {
                _highlightBackgroundBrush = bg;
                _highlightSolidBrush = solid;
                _highlightBorderBrush = border;
                _highlightBorderThickness = thickness;
                _marker = marker;
            }
        }

        #endregion

        #region INotifyPropertyChanged Members

        public event PropertyChangedEventHandler PropertyChanged;

        private void NotifyOfPropertyChange<T>(Expression<Func<T>> property)
        {
            var handler = PropertyChanged;
            if (handler != null)
                handler(this, new PropertyChangedEventArgs(GetPropertyName(property)));
        }

        private string GetPropertyName<T>(Expression<Func<T>> property)
        {
            var lambda = (LambdaExpression) property;

            MemberExpression memberExpression;
            if (lambda.Body is UnaryExpression)
            {
                var unaryExpression = (UnaryExpression) lambda.Body;
                memberExpression = (MemberExpression) unaryExpression.Operand;
            }
            else
            {
                memberExpression = (MemberExpression) lambda.Body;
            }

            return memberExpression.Member.Name;
        }

        #endregion
    }
}