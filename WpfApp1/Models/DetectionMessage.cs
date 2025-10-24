using System;
using System.Windows.Media;

namespace WpfApp1.Models
{
    public class DetectionMessage
    {
        public DetectionMessage(string message)
        {
            Message = message;
            Color = Brushes.White;
            DateTime = DateTime.Now;
        }

        public DetectionMessage(string message, Brush color)
        {
            Message = message;
            Color = color;
            DateTime = DateTime.Now;
        }

        public DateTime DateTime { get; set; }
        public string Message { get; set; }
        public Brush Color { get; set; }
    }
}
