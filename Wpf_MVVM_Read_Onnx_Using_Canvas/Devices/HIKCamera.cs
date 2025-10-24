using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Wpf_MVVM_Read_Onnx_Using_Canvas.Devices
{
    public class HikCameraService : IDisposable
    {
        private MyCamera _cam = new MyCamera();
        private bool _opened, _grabbing;
        private MyCamera.cbOutputExdelegate _callback;
        public Mat mat;
        public event Action<BitmapSource> FrameReceived;
        public List<CameraInfo> CameraList { get; private set; } = new List<CameraInfo>();

        public bool Enumerate()
        {
            MyCamera.MV_CC_DEVICE_INFO_LIST list = new MyCamera.MV_CC_DEVICE_INFO_LIST();
            int ret = MyCamera.MV_CC_EnumDevices_NET(MyCamera.MV_GIGE_DEVICE | MyCamera.MV_USB_DEVICE, ref list);
            if (ret != MyCamera.MV_OK) return false;
            CameraList.Clear();

            for (int i = 0; i < list.nDeviceNum; i++)
            {
                var dev = (MyCamera.MV_CC_DEVICE_INFO)
                    Marshal.PtrToStructure(list.pDeviceInfo[i], typeof(MyCamera.MV_CC_DEVICE_INFO));
                string name = $"{i + 1}: {((MyCamera.MV_GIGE_DEVICE_INFO_EX)MyCamera.ByteToStruct(dev.SpecialInfo.stGigEInfo, typeof(MyCamera.MV_GIGE_DEVICE_INFO_EX))).chModelName}";
                CameraList.Add(new CameraInfo { Index = i, Name = string.IsNullOrWhiteSpace(name) ? $"Cam{i + 1}" : name });
            }
            return true;
        }

        public bool Open(CameraInfo camInfo)
        {
            if (camInfo == null) return false;
            MyCamera.MV_CC_DEVICE_INFO_LIST list = new MyCamera.MV_CC_DEVICE_INFO_LIST();
            MyCamera.MV_CC_EnumDevices_NET(MyCamera.MV_GIGE_DEVICE | MyCamera.MV_USB_DEVICE, ref list);
            if (list.nDeviceNum <= camInfo.Index) return false;

            var pDevInfo = (MyCamera.MV_CC_DEVICE_INFO)Marshal.PtrToStructure(list.pDeviceInfo[camInfo.Index], typeof(MyCamera.MV_CC_DEVICE_INFO));
            _cam.MV_CC_CreateDevice_NET(ref pDevInfo);
            if (_cam.MV_CC_OpenDevice_NET() != MyCamera.MV_OK) return false;

            _cam.MV_CC_SetEnumValue_NET("AcquisitionMode", (int)MyCamera.MV_CAM_ACQUISITION_MODE.MV_ACQ_MODE_CONTINUOUS);
            _cam.MV_CC_SetEnumValue_NET("TriggerMode", (int)MyCamera.MV_CAM_TRIGGER_MODE.MV_TRIGGER_MODE_OFF);
            _cam.MV_CC_SetEnumValue_NET("PixelFormat", (uint)MyCamera.MvGvspPixelType.PixelType_Gvsp_BGR8_Packed);
            _callback = new MyCamera.cbOutputExdelegate(OnFrame);
            _opened = true;
            return true;
        }

        public void Start()
        {
            if (!_opened) return;
            _cam.MV_CC_RegisterImageCallBackEx_NET(_callback, IntPtr.Zero);
            _cam.MV_CC_StartGrabbing_NET();
            _grabbing = true;
        }

        public void Stop()
        {
            if (_grabbing)
            {
                _cam.MV_CC_StopGrabbing_NET();
                _grabbing = false;
            }
        }

        public void Close()
        {
            Stop();
            if (_opened)
            {
                _cam.MV_CC_CloseDevice_NET();
                _cam.MV_CC_DestroyDevice_NET();
                _opened = false;
            }
        }

        private void OnFrame(IntPtr pData, ref MyCamera.MV_FRAME_OUT_INFO_EX frameInfo, IntPtr pUser)
        {
            try
            {
                // Xác định PixelFormat
                var pt = (MyCamera.MvGvspPixelType)frameInfo.enPixelType;

                // Chuẩn bị WriteableBitmap cho WPF
                BitmapSource bmpSrc;

                if (pt == MyCamera.MvGvspPixelType.PixelType_Gvsp_BGR8_Packed)
                {
                    // BGR24 → WPF dùng PixelFormats.Bgr24
                    int width = (int)frameInfo.nWidth;
                    int height = (int)frameInfo.nHeight;
                    int stride = width * 3;

                    // Copy unmanaged -> managed
                    byte[] buffer = new byte[stride * height];
                    Marshal.Copy(pData, buffer, 0, buffer.Length);

                    bmpSrc = BitmapSource.Create(
                        width, height, 96, 96,
                        PixelFormats.Bgr24,
                        null,
                        buffer, stride);
                    mat = new Mat(height, width, MatType.CV_8UC3, pData);
                    Console.WriteLine("On Frame BGR24");
                }
                else if (pt == MyCamera.MvGvspPixelType.PixelType_Gvsp_Mono8)
                {
                    // MONO8 → Gray8
                    int width = (int)frameInfo.nWidth;
                    int height = (int)frameInfo.nHeight;
                    int stride = width;

                    byte[] buffer = new byte[stride * height];
                    Marshal.Copy(pData, buffer, 0, buffer.Length);

                    bmpSrc = BitmapSource.Create(
                        width, height, 96, 96,
                        PixelFormats.Gray8,
                        null,
                        buffer, stride);
                    mat = new Mat(height, width, MatType.CV_8UC1, pData);
                    //Console.WriteLine("On Frame MONO");

                }
                else
                {
                    // Các định dạng Bayer/khác → chuyển về BGR8 bằng ConvertPixelType
                    bmpSrc = ConvertToBgr24(pData, frameInfo);
                    mat = ConvertToBGR8(pData, frameInfo);
                    Console.WriteLine("On Frame BGR8");

                }

                // Đẩy về UI: WPF yêu cầu marshal lên UI thread
                if (bmpSrc != null)
                {
                    bmpSrc.Freeze();
                    Application.Current?.Dispatcher?.BeginInvoke(new Action(() =>
                    {
                        FrameReceived?.Invoke(bmpSrc);
                    }));
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error in OnImageCallback_ {ex.Message}");
            }
        }
        private BitmapSource ConvertToBgr24(IntPtr pData, MyCamera.MV_FRAME_OUT_INFO_EX info)
        {
            // Dùng MV_CC_ConvertPixelType chuyển về BGR8
            int w = (int)info.nWidth;
            int h = (int)info.nHeight;

            var dstSize = w * h * 3; // BGR24
            IntPtr dstBuf = Marshal.AllocHGlobal(dstSize);

            try
            {
                MyCamera.MV_PIXEL_CONVERT_PARAM c = new MyCamera.MV_PIXEL_CONVERT_PARAM
                {
                    nWidth = info.nWidth,
                    nHeight = info.nHeight,
                    pSrcData = pData,
                    nSrcDataLen = info.nFrameLen,
                    enSrcPixelType = info.enPixelType,
                    enDstPixelType = MyCamera.MvGvspPixelType.PixelType_Gvsp_BGR8_Packed,
                    pDstBuffer = dstBuf,
                    nDstBufferSize = (uint)dstSize
                };

                int nRet = _cam.MV_CC_ConvertPixelType_NET(ref c);
                if (nRet != MyCamera.MV_OK) return null;

                byte[] managed = new byte[dstSize];
                Marshal.Copy(dstBuf, managed, 0, dstSize);

                return BitmapSource.Create(
                    w, h, 96, 96,
                    PixelFormats.Bgr24,
                    null,
                    managed, w * 3);
            }
            finally
            {
                Marshal.FreeHGlobal(dstBuf);
            }
        }
        private Mat ConvertToBGR8(IntPtr pData, MyCamera.MV_FRAME_OUT_INFO_EX info)
        {
            int w = (int)info.nWidth;
            int h = (int)info.nHeight;
            int dstSize = w * h * 3;

            IntPtr dstBuf = Marshal.AllocHGlobal(dstSize);
            try
            {
                MyCamera.MV_PIXEL_CONVERT_PARAM param = new MyCamera.MV_PIXEL_CONVERT_PARAM
                {
                    nWidth = info.nWidth,
                    nHeight = info.nHeight,
                    pSrcData = pData,
                    nSrcDataLen = info.nFrameLen,
                    enSrcPixelType = info.enPixelType,
                    enDstPixelType = MyCamera.MvGvspPixelType.PixelType_Gvsp_BGR8_Packed,
                    pDstBuffer = dstBuf,
                    nDstBufferSize = (uint)dstSize
                };
                _cam.MV_CC_ConvertPixelType_NET(ref param);

                return new Mat(h, w, MatType.CV_8UC3, dstBuf).Clone();
            }
            finally
            {
                Marshal.FreeHGlobal(dstBuf);
            }
        }
        public void Dispose() => Close();
    }
}
