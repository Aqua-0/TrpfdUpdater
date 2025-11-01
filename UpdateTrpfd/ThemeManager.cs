using System;

namespace TrpfdManager;
internal static class ThemeManager
{
    public static void Apply(bool dark)
    {
        var app = System.Windows.Application.Current; // explicit WPF
        if (app is null) return;

        var uri = new Uri(dark ? "Themes/Dark.xaml" : "Themes/Light.xaml", UriKind.Relative);
        var rd = new System.Windows.ResourceDictionary { Source = uri };

        var md = app.Resources.MergedDictionaries;
        if (md.Count == 0) md.Add(rd); else md[0] = rd;
    }
}