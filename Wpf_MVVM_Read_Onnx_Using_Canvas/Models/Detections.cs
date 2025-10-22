using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Wpf_MVVM_Read_Onnx_Using_Canvas.Models
{
    public class Detections
    {
        public float X1, Y1, X2, Y2;
        public int ClassId;
        public float Score;
        public string ClassName;
        override public string ToString()
        {
            return $"{ClassName} ({Score:P1}) [{X1:N0}, {Y1:N0}, {X2:N0}, {Y2:N0}]";
        }
    }
}
