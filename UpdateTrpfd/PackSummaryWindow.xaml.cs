using System.Collections.Generic;
using System.Windows;

namespace TrpfdManager
{
    public sealed class PackSummaryItem
    {
        public string Name { get; init; } = "";
        public int FileCount { get; init; }
        public int Wins { get; init; }     // paths this pack wins (last)
        public int Loses { get; init; }    // paths this pack loses (overridden)
        public List<string> Files { get; init; } = new();
    }

    public partial class PackSummaryWindow : Window
    {
        public PackSummaryWindow(IList<PackSummaryItem> items)
        {
            InitializeComponent();
            DataContext = items;
        }
    }
    public partial class PackSummaryWindow : Window
    {
        private void Close_Click(object sender, RoutedEventArgs e) => Close();
    }
}