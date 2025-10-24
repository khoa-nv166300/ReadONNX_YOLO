using OpenCvSharp;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace WpfApp1.Devices
{
    //public class HikCameraService : ICameraService
    //{
    //    public event Action<BitmapSource> FrameReceived;
    //    public event Action<Mat> FrameReceived_;
    //    public Mat mat { get; set; }
    //    Stopwatch sw { get; }

    //    private MyCamera _cam = new MyCamera();
    //    private bool _opened, _grabbing;

    //    public bool IsOpened => _opened;
    //    public bool IsGrabbing => _grabbing;

    //    // Callback kiểu OnImageCallback (không block UI thread)
    //    private MyCamera.cbOutputExdelegate _imageCallback;
    //    public HikCameraService()
    //    {
    //        sw = new Stopwatch();
    //    }
    //    public bool OpenFirstDevice()
    //    {
    //        MyCamera.MV_CC_DEVICE_INFO_LIST list = new MyCamera.MV_CC_DEVICE_INFO_LIST();
    //        int nRet = MyCamera.MV_CC_EnumDevices_NET(MyCamera.MV_GIGE_DEVICE | MyCamera.MV_USB_DEVICE, ref list);
    //        if (nRet != MyCamera.MV_OK || list.nDeviceNum == 0) return false;

    //        var stDevInfo = (MyCamera.MV_CC_DEVICE_INFO)Marshal.PtrToStructure(list.pDeviceInfo[0], typeof(MyCamera.MV_CC_DEVICE_INFO));
    //        nRet = _cam.MV_CC_CreateDevice_NET(ref stDevInfo);
    //        if (nRet != MyCamera.MV_OK) return false;

    //        nRet = _cam.MV_CC_OpenDevice_NET();
    //        if (nRet != MyCamera.MV_OK)
    //        {
    //            _cam.MV_CC_DestroyDevice_NET();
    //            return false;
    //        }

    //        // Set trigger mode OFF (free-run) để demo
    //        _cam.MV_CC_SetEnumValue_NET("TriggerMode", (int)MyCamera.MV_CAM_TRIGGER_MODE.MV_TRIGGER_MODE_OFF);

    //        // Chọn PixelFormat (ưu tiên BGR8 cho WPF; nếu không có, dùng Mono8)
    //        // Không phải camera nào cũng hỗ trợ, nên thử đặt và bỏ qua lỗi.
    //        _cam.MV_CC_SetEnumValue_NET("PixelFormat", (uint)MyCamera.MvGvspPixelType.PixelType_Gvsp_BGR8_Packed);

    //        _imageCallback = new MyCamera.cbOutputExdelegate(OnImageCallback);
    //        //_imageCallback = new MyCamera.cbOutputExdelegate(OnImageCallback_);
    //        _opened = true;
    //        return true;
    //    }

    //    public bool StartGrabbing()
    //    {
    //        if (!_opened) return false;

    //        // Đăng ký callback nhận frame
    //        int nRet = _cam.MV_CC_RegisterImageCallBackEx_NET(_imageCallback, IntPtr.Zero);
    //        if (nRet != MyCamera.MV_OK) return false;

    //        nRet = _cam.MV_CC_StartGrabbing_NET();
    //        if (nRet != MyCamera.MV_OK) return false;

    //        _grabbing = true;
    //        return true;
    //    }

    //    public void StopGrabbing()
    //    {
    //        if (!_grabbing) return;
    //        _cam.MV_CC_StopGrabbing_NET();
    //        _grabbing = false;
    //    }

    //    public void Close()
    //    {
    //        if (_grabbing) StopGrabbing();
    //        if (_opened)
    //        {
    //            _cam.MV_CC_CloseDevice_NET();
    //            _cam.MV_CC_DestroyDevice_NET();
    //            _opened = false;
    //        }
    //    }

    //    public void Dispose() => Close();

    //    // === CALLBACK NHẬN ẢNH ===
    //    private void OnImageCallback(IntPtr pData, ref MyCamera.MV_FRAME_OUT_INFO_EX frameInfo, IntPtr pUser)
    //    {
    //        sw.Restart();
    //        try
    //        {
    //            // Xác định PixelFormat
    //            var pt = (MyCamera.MvGvspPixelType)frameInfo.enPixelType;

    //            // Chuẩn bị WriteableBitmap cho WPF
    //            BitmapSource bmpSrc;

    //            if (pt == MyCamera.MvGvspPixelType.PixelType_Gvsp_BGR8_Packed)
    //            {
    //                // BGR24 → WPF dùng PixelFormats.Bgr24
    //                int width = (int)frameInfo.nWidth;
    //                int height = (int)frameInfo.nHeight;
    //                int stride = width * 3;

    //                // Copy unmanaged -> managed
    //                byte[] buffer = new byte[stride * height];
    //                Marshal.Copy(pData, buffer, 0, buffer.Length);

    //                bmpSrc = BitmapSource.Create(
    //                    width, height, 96, 96,
    //                    PixelFormats.Bgr24,
    //                    null,
    //                    buffer, stride);

    //                mat = new Mat(height, width, MatType.CV_8UC3, pData);
    //            }
    //            else if (pt == MyCamera.MvGvspPixelType.PixelType_Gvsp_Mono8)
    //            {
    //                // MONO8 → Gray8
    //                int width = (int)frameInfo.nWidth;
    //                int height = (int)frameInfo.nHeight;
    //                int stride = width;

    //                byte[] buffer = new byte[stride * height];
    //                Marshal.Copy(pData, buffer, 0, buffer.Length);

    //                bmpSrc = BitmapSource.Create(
    //                    width, height, 96, 96,
    //                    PixelFormats.Gray8,
    //                    null,
    //                    buffer, stride);
    //                mat = new Mat(height, width, MatType.CV_8UC1, pData);

    //            }
    //            else
    //            {
    //                // Các định dạng Bayer/khác → chuyển về BGR8 bằng ConvertPixelType
    //                bmpSrc = ConvertToBgr24(pData, frameInfo);
    //                mat = ConvertToBGR8(pData, frameInfo);
    //            }

    //            // Đẩy về UI: WPF yêu cầu marshal lên UI thread
    //            if (bmpSrc != null)
    //            {
    //                bmpSrc.Freeze();
    //                Application.Current?.Dispatcher?.BeginInvoke(new Action(() =>
    //                {
    //                    FrameReceived?.Invoke(bmpSrc);
    //                }));
    //            }
    //        }
    //        catch (Exception ex)
    //        {
    //            MessageBox.Show($"Error in OnImageCallback_ {ex.Message}");
    //        }
    //    }

    //    private BitmapSource ConvertToBgr24(IntPtr pData, MyCamera.MV_FRAME_OUT_INFO_EX info)
    //    {
    //        // Dùng MV_CC_ConvertPixelType chuyển về BGR8
    //        int w = (int)info.nWidth;
    //        int h = (int)info.nHeight;

    //        var dstSize = w * h * 3; // BGR24
    //        IntPtr dstBuf = Marshal.AllocHGlobal(dstSize);

    //        try
    //        {
    //            MyCamera.MV_PIXEL_CONVERT_PARAM c = new MyCamera.MV_PIXEL_CONVERT_PARAM
    //            {
    //                nWidth = info.nWidth,
    //                nHeight = info.nHeight,
    //                pSrcData = pData,
    //                nSrcDataLen = info.nFrameLen,
    //                enSrcPixelType = info.enPixelType,
    //                enDstPixelType = MyCamera.MvGvspPixelType.PixelType_Gvsp_BGR8_Packed,
    //                pDstBuffer = dstBuf,
    //                nDstBufferSize = (uint)dstSize
    //            };

    //            int nRet = _cam.MV_CC_ConvertPixelType_NET(ref c);
    //            if (nRet != MyCamera.MV_OK) return null;

    //            byte[] managed = new byte[dstSize];
    //            Marshal.Copy(dstBuf, managed, 0, dstSize);

    //            return BitmapSource.Create(
    //                w, h, 96, 96,
    //                PixelFormats.Bgr24,
    //                null,
    //                managed, w * 3);
    //        }
    //        finally
    //        {
    //            Marshal.FreeHGlobal(dstBuf);
    //        }
    //    }

    //    private Mat ConvertToBGR8(IntPtr pData, MyCamera.MV_FRAME_OUT_INFO_EX info)
    //    {
    //        int w = (int)info.nWidth;
    //        int h = (int)info.nHeight;
    //        int dstSize = w * h * 3;

    //        IntPtr dstBuf = Marshal.AllocHGlobal(dstSize);
    //        try
    //        {
    //            MyCamera.MV_PIXEL_CONVERT_PARAM param = new MyCamera.MV_PIXEL_CONVERT_PARAM
    //            {
    //                nWidth = info.nWidth,
    //                nHeight = info.nHeight,
    //                pSrcData = pData,
    //                nSrcDataLen = info.nFrameLen,
    //                enSrcPixelType = info.enPixelType,
    //                enDstPixelType = MyCamera.MvGvspPixelType.PixelType_Gvsp_BGR8_Packed,
    //                pDstBuffer = dstBuf,
    //                nDstBufferSize = (uint)dstSize
    //            };
    //            _cam.MV_CC_ConvertPixelType_NET(ref param);

    //            return new Mat(h, w, MatType.CV_8UC3, dstBuf).Clone();
    //        }
    //        finally
    //        {
    //            Marshal.FreeHGlobal(dstBuf);
    //        }
    //    }
    //}
}

