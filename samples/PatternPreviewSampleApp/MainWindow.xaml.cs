using System.IO;
using System.Windows;
using FillPatternPreview.Imaging;
using Microsoft.Win32;

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

    private void OnSaveThumbnail(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog { Filter = "PNG image (*.png)|*.png", FileName = "pattern-thumbnail.png" };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            // No control involved: the pattern text goes straight to the thumbnail generator.
            var png = PatternThumbnail.RenderPng(
                PatTextBox.Text,
                string.IsNullOrWhiteSpace(PatternNameBox.Text) ? null : PatternNameBox.Text.Trim(),
                new PatternThumbnailOptions
                {
                    LineBrush = Preview.LineBrush,
                    TilePattern = Preview.TilePattern,
                    FirstTileBrush = Preview.HighlightFirstTile ? Preview.FirstTileBrush : null,
                });
            File.WriteAllBytes(dialog.FileName, png);
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, ex.Message, "Save thumbnail", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
