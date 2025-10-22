using System.Windows.Media;

namespace Wpf_MVVM_Read_Onnx_Using_Canvas.Models
{
    public class GraphicItem : IGraphicItem
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public double StrokeThickness { get; set; }
        public Brush Color { get; set; }
        public Brush Fill { get; set; }
        public string Label { get; set; }

    }
    public class TextBlockItem : GraphicItem { }
    public class RectangleItem : GraphicItem { }
  

}
