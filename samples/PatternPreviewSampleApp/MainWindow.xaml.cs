using System.Windows;

namespace PatternPreviewSampleApp;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        PatTextBox.Text = "*ANSI31, 45 degree lines\n45,0,0,0,4"; // simple sample
    }

    private void OnInteractionChanged(object? sender, System.EventArgs e)
    {
        ViewStatus.Text = $"Zoom {Preview.Zoom:0.00}x, pan ({Preview.PanOffset.X:0.0}, {Preview.PanOffset.Y:0.0})";
    }

    private void OnApply(object sender, RoutedEventArgs e)
    {
        Preview.PatRawText = PatTextBox.Text;
        Preview.PatPatternName = string.IsNullOrWhiteSpace(PatternNameBox.Text) ? null : PatternNameBox.Text.Trim();
    }
}
