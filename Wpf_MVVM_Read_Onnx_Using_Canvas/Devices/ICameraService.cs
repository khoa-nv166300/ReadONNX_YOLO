using OpenCvSharp;
using System;
using System.Windows.Media.Imaging;

namespace Wpf_MVVM_Read_Onnx_Using_Canvas.Devices
{
    public interface ICameraService : IDisposable
    {
        event Action<BitmapSource> FrameReceived;
        event Action<Mat> FrameReceived_;
        bool OpenFirstDevice();
        bool StartGrabbing();
        void StopGrabbing();
        void Close();
        bool IsOpened { get; }
        bool IsGrabbing { get; }
        Mat mat { get; set; }
    }
}
