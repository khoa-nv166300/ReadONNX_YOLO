using System.Windows.Media;

namespace Wpf_MVVM_Read_Onnx_Using_Canvas.Models
{
    public interface IGraphicItem
    {
         double X { get; set; }
         double Y { get; set; }
         double Width { get; set; }
         double Height { get; set; }
         double StrokeThickness { get; set; }
         Brush Color { get; set; }
         Brush Fill { get; set; }
         string Label { get; set; }
    }

}
