using System.Windows;

namespace PatternPreviewSampleApp;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        PatTextBox.Text = "*ANSI31, 45 degree lines\n45,0,0,0,4"; // simple sample
    }

    private void OnApply(object sender, RoutedEventArgs e)
    {
        Preview.PatRawText = PatTextBox.Text;
        Preview.UsePatternMaker = true;
        Preview.PatPatternName = string.IsNullOrWhiteSpace(PatternNameBox.Text) ? null : PatternNameBox.Text.Trim();
    }
}
