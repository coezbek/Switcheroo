using System.Collections.Generic;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;

namespace Switcheroo
{
    /// <summary>
    /// A custom AutomationPeer that "lies" to Windows, claiming the window has no children. 
    /// This prevents the UI Automation system (triggered by ContextMenus) from crawling the 
    /// entire Visual Tree, preventing a ~100ms+ lag when 
    /// </summary>
    public class SilentWindowAutomationPeer : WindowAutomationPeer
    {
        public SilentWindowAutomationPeer(Window owner) : base(owner)
        {
        }

        protected override List<AutomationPeer> GetChildrenCore()
        {
            // Return empty list to stop the recursive tree crawl
            return new List<AutomationPeer>();
        }
    }

    /// <summary>
    /// A lightweight ListBox that strips away heavy Automation overhead.
    /// It treats itself as a plain generic UI element rather than a complex List 
    /// that needs to report selection changes to Windows.
    /// </summary>
    public class PerformanceListBox : ListBox
    {
        protected override AutomationPeer OnCreateAutomationPeer()
        {
            // Returning FrameworkElementAutomationPeer instead of ListBoxAutomationPeer
            // prevents "RaiseSelectionEvents" and other heavy logic from running.
            return new FrameworkElementAutomationPeer(this);
        }
    }
}