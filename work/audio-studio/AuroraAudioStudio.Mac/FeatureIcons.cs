using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace AuroraAudioStudio.Mac;

public sealed partial class MainWindow
{
    // Vector equivalents avoid depending on Windows-only Segoe Fluent glyphs.
    // https://docs.avaloniaui.net/api/avalonia/controls/shapes/path
    private static Control FeatureIcon(string feature)
    {
        var data = feature switch
        {
            "music" => "M8,16 V5 L18,3 V14 M8,7 L18,5 M8,16 A3,2 0 1 1 2,16 A3,2 0 1 1 8,16 M18,14 A3,2 0 1 1 12,14 A3,2 0 1 1 18,14",
            "voice" => "M7,4 A3,3 0 0 1 13,4 V10 A3,3 0 0 1 7,10 Z M4,9 V10 A6,6 0 0 0 16,10 V9 M10,16 V20 M6,20 H14",
            "singing" => "M6,9 A5,5 0 0 1 14,3 A5,5 0 0 1 12,11 L6,18 L2,20 L3,16 Z M6,7 L13,12 M14,15 Q19,15 19,20",
            "separation" => "M3,6 H19 M3,14 H19 M7,2 V10 M15,10 V18 M5,4 H9 V8 H5 Z M13,12 H17 V16 H13 Z",
            "transcription" => "M2,4 H20 V18 H2 Z M8,4 V18 M14,4 V18 M6,4 V11 H10 V4 M12,4 V11 H16 V4",
            _ => "M3,4 H19 A1,1 0 0 1 20,5 V17 A1,1 0 0 1 19,18 H3 A1,1 0 0 1 2,17 V5 A1,1 0 0 1 3,4 M5,11 H10 M13,11 H17 M5,14 H17"
        };
        return new Avalonia.Controls.Shapes.Path { Data = Geometry.Parse(data), Stroke = Accent, StrokeThickness = 1.5,
            Width = 22, Height = 22, Stretch = Stretch.Uniform, VerticalAlignment = VerticalAlignment.Center, IsHitTestVisible = false };
    }
}
