using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Microsoft.Win32;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Wpf_MVVM_Read_Onnx.Model;

namespace Wpf_MVVM_Read_Onnx.ViewModel
{
    public class MainViewModel : ViewModelBase
    {
        private readonly CameraModel _cameraModel;
        private Mat mat;
        private bool bResult = false;
        private bool bTrigger = false;


        // ============= Fields/Properties cho Data Binding =============

        private InferenceSession _session;
        private List<string> _names = new List<string>();
        private CancellationTokenSource _ctsAutoDetect;
        private Stopwatch sw = new Stopwatch();

        // Tương đương với các biến private trong code-behind
        private string _onnxPath = "";
        private string _imgPath = "";
        private float[] _tensorBuf;
        private int _bufLen;

        private string _statusMessage = "";
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        private bool _isGpuEnabled = false;
        public bool IsGpuEnabled
        {
            get => _isGpuEnabled;
            set
            {
                if (SetProperty(ref _isGpuEnabled, value))
                {
                    // Tùy chọn: Tải lại session khi GPU thay đổi
                    CreateSession();
                }
            }
        }

        private string _confidenceThreshold = "0.25";
        public string ConfidenceThreshold
        {
            get => _confidenceThreshold;
            set => SetProperty(ref _confidenceThreshold, value);
        }

        private string _iouThreshold = "0.45";
        public string IouThreshold
        {
            get => _iouThreshold;
            set => SetProperty(ref _iouThreshold, value);
        }

        private string _imageSize = "640";
        public string ImageSize
        {
            get => _imageSize;
            set => SetProperty(ref _imageSize, value);
        }

        private ImageSource _displayedImageSource;
        public ImageSource DisplayedImageSource
        {
            get => _displayedImageSource;
            set => SetProperty(ref _displayedImageSource, value);
        }

        private bool _isAutoRunning = false;
        public bool IsAutoRunning
        {
            get => _isAutoRunning;
            set => SetProperty(ref _isAutoRunning, value);
        }

        public Action<List<Det>, int, int> DrawOverlayAction { get; set; }

        // ============= Commands =============

        public ICommand LoadOnnxCommand { get; }
        public ICommand LoadNamesCommand { get; }
        public ICommand OpenImageCommand { get; }
        public ICommand DetectCommand { get; }
        public ICommand TriggerCommand { get; }
        public ICommand RunCommand { get; }

        public MainViewModel()
        {
            _cameraModel = new CameraModel();
            this._cameraModel.Grab();

            LoadOnnxCommand = new RelayCommand(p => LoadOnnx());
            LoadNamesCommand = new RelayCommand(p => LoadNames());
            OpenImageCommand = new RelayCommand(p => OpenImage());
            DetectCommand = new RelayCommand(p => Detect());
            TriggerCommand = new RelayCommand(p => OnTrigger());
            RunCommand = new RelayCommand(p => OnRun());
            //RunCommand = new RelayCommand(p => RunToggle());

            CDefines.CloseChanged += CDefines_OnCloseChanged;
        }

        private void CDefines_OnCloseChanged(object sender, EventArgs e)
        {
            this._cameraModel.Dispose();
        }

        // ============= Command Implementations =============

        private void LoadOnnx()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "ONNX|*.onnx" };
            if (dlg.ShowDialog() == true)
            {
                _onnxPath = dlg.FileName;
                CreateSession();
            }
        }

        private void LoadNames()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "classes.txt|*.txt|All|*.*" };
            if (dlg.ShowDialog() == true)
            {
                _names = File.ReadAllLines(dlg.FileName)
                             .Where(l => !string.IsNullOrWhiteSpace(l)).ToList();
                StatusMessage = $"Loaded classes: {_names.Count}";
            }
        }

        private void OpenImage()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "Images|*.jpg;*.jpeg;*.png;*.bmp;*.tif;*.tiff;*.webp" };
            if (dlg.ShowDialog() == true)
            {
                _imgPath = dlg.FileName;
                try
                {
                    //DisplayedImageSource = new BitmapImage(new Uri(_imgPath));
                    DisplayedImageSource = (new Mat(_imgPath)).ToWriteableBitmap();

                }
                catch (Exception ex)
                {
                    StatusMessage = "ERR load image: " + ex.Message;
                }
            }
        }

        private async void Detect()
        {
            if (_session == null) { StatusMessage = "Load ONNX trước."; return; }
            if (string.IsNullOrEmpty(_imgPath)) { StatusMessage = "Chọn ảnh trước."; return; }

            // Chuyển việc parsing từ UI thread sang async
            if (!float.TryParse(ConfidenceThreshold, out float conf)) conf = 0.25f;
            if (!float.TryParse(IouThreshold, out float iou)) iou = 0.45f;
            if (!int.TryParse(ImageSize, out int imgSz)) imgSz = 640;

            await Task.Run(() => DetectOne(_imgPath, conf, iou, imgSz, CancellationToken.None));
        }
        private async void OnTrigger()
        {
            bTrigger = true;
            if (_cameraModel != null)
            {
                sw.Restart();
                this.mat = this._cameraModel.ImgGrab;

                if (mat != null)
                {
                    if (_session == null) { StatusMessage = "Load ONNX trước."; return; }
                    if (string.IsNullOrEmpty(_imgPath)) { StatusMessage = "Chọn ảnh trước."; return; }

                    if (!float.TryParse(ConfidenceThreshold, out float conf)) conf = 0.25f;
                    if (!float.TryParse(IouThreshold, out float iou)) iou = 0.45f;
                    if (!int.TryParse(ImageSize, out int imgSz)) imgSz = 640;
                    await Task.Run(() => DetectOne(mat, conf, iou, imgSz, CancellationToken.None));

                    //DisplayedImageSource = mat.ToWriteableBitmap();
                    StatusMessage = $"Trigger: {sw.ElapsedMilliseconds}ms";
                    CDefines.OnSequenceChanged(ESequence.Accepted);
                }
                else
                {
                    StatusMessage = $"Trigger: Fail";
                    CDefines.OnSequenceChanged(ESequence.Error);
                }
            }
            else
            {
                StatusMessage = $"Trigger: Fail";
                CDefines.OnSequenceChanged(ESequence.Error);
            }
        }

        private void RunToggle()
        {
            if (IsAutoRunning)
            {
                // Stop
                _ctsAutoDetect?.Cancel();
                _ctsAutoDetect?.Dispose();
                _ctsAutoDetect = null;
                IsAutoRunning = false;
                StatusMessage = "Auto Detect stopped.";
            }
            else
            {
                // Start
                if (_session == null) { StatusMessage = "Load ONNX trước."; return; }

                if (!float.TryParse(ConfidenceThreshold, out float conf)) conf = 0.25f;
                if (!float.TryParse(IouThreshold, out float iou)) iou = 0.45f;
                if (!int.TryParse(ImageSize, out int imgSz)) imgSz = 640;

                // Thay thế FolderPath cứng
                string folderPath = @"C:\Users\khoa\Pictures\Data_LimeStone\raw\images";
                if (!Directory.Exists(folderPath))
                {
                    StatusMessage = "ERR: Thư mục ảnh không tồn tại: " + folderPath;
                    return;
                }
                var files = Directory.GetFiles(folderPath).ToList();
                if (!files.Any())
                {
                    StatusMessage = "ERR: Thư mục không có ảnh.";
                    return;
                }

                _ctsAutoDetect = new CancellationTokenSource();
                CancellationToken token = _ctsAutoDetect.Token;
                IsAutoRunning = true;
                StatusMessage = "Auto Detect started...";

                // Bắt đầu luồng/Task nền
                Task.Run(() =>
                {
                    while (!token.IsCancellationRequested)
                    {
                        foreach (var filePath in files)
                        {
                            if (token.IsCancellationRequested) return;
                            DetectOne(filePath, conf, iou, imgSz, token);
                            // Thread.Sleep(1000); // Bỏ comment nếu muốn có độ trễ
                        }
                    }
                }, token);
            }
        }
        private void OnRun()
        {
            sw.Restart();
            bTrigger = false;

            if (_session == null) { StatusMessage = "Load ONNX trước."; return; }
            if (string.IsNullOrEmpty(_imgPath)) { StatusMessage = "Chọn ảnh trước."; return; }

            if (!float.TryParse(ConfidenceThreshold, out float conf)) conf = 0.25f;
            if (!float.TryParse(IouThreshold, out float iou)) iou = 0.45f;
            if (!int.TryParse(ImageSize, out int imgSz)) imgSz = 640;

            Thread t1 = new Thread(() =>
            {

                while (this.mat != null && bTrigger == false)
                {
                   
                    Thread.Sleep(3);
                    if (_cameraModel != null)
                    {
                        sw.Restart();
                        this.mat = this._cameraModel.ImgGrab;

                        if (mat != null)
                        {
                            App.Current.Dispatcher.Invoke(new Action(() =>
                            {
                                //DisplayedImageSource = mat.ToWriteableBitmap();
                                DetectOne(mat, conf, iou, imgSz, CancellationToken.None);
                            }));

                            StatusMessage = $"Run: {sw.ElapsedMilliseconds}ms, {1.0 / sw.ElapsedMilliseconds * 1000:F0}fps";
                        }
                        else
                        {
                            StatusMessage = $"Trigger: Fail";
                        }
                    }
                    else
                    {
                        StatusMessage = $"Trigger: Fail";
                    }
                }
            });
            t1.IsBackground = true;
            t1.Start();
        }
        // ============= Core Logic (Chuyển từ Code-behind) =============

        void CreateSession()
        {
            try
            {
                _session?.Dispose();
                var opt = new SessionOptions();
                if (IsGpuEnabled)
                {
                    try { opt.AppendExecutionProvider_CUDA(); }
                    catch { StatusMessage = "CUDA provider không khả dụng. Dùng CPU."; }
                }
                _session = new InferenceSession(_onnxPath, opt);
                StatusMessage = $"ONNX loaded: {_onnxPath}";
            }
            catch (Exception ex)
            {
                StatusMessage = "ERR load ONNX: " + ex.Message;
            }
        }

        void DetectOne(string imagePath, float confThres, float iouThres, int imgSize, CancellationToken token)
        {
            if (token.IsCancellationRequested) return;
            sw.Restart();
            // 1) Read original with OpenCV
            Mat src = Cv2.ImRead(imagePath);

            if (src.Empty()) { StatusMessage = "Không đọc được ảnh."; return; }
            int origW = src.Width, origH = src.Height;

            // 2) Letterbox
            var letter = Letterbox(src, imgSize, imgSize);
            Mat canvas = letter.Item1;
            float ratio = letter.Item2;
            int padW = letter.Item3, padH = letter.Item4;

            // 3) BGR->RGB, normalize 0..1, HWC->NCHW
            Cv2.CvtColor(canvas, canvas, ColorConversionCodes.BGR2RGB);
            var inputTensor = ToTensorUnsafeBGR(canvas);

            // 4) Prepare inputs/outputs
            string inputName = _session.InputMetadata.Keys.First();
            string outputName = _session.OutputMetadata.Keys.First();

            var inputs = new List<NamedOnnxValue> {
                NamedOnnxValue.CreateFromTensor(inputName, inputTensor)
            };

            IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results = null;
            try
            {
                var t0 = DateTime.Now;
                results = _session.Run(inputs);
                var ms = (DateTime.Now - t0).TotalMilliseconds;
                // Cập nhật trạng thái
                StatusMessage = $"Inference time ~ {ms:0} ms";
            }
            catch (Exception ex)
            {
                StatusMessage = "ERR run: " + ex.Message;
                results?.Dispose();
                return;
            }
            if (token.IsCancellationRequested) return;

            using (results)
            {
                var output = results.First(v => v.Name == outputName).AsTensor<float>();

                float[] arr = output.ToArray();
                int[] dims = output.Dimensions.ToArray();

                int numBoxes, numOutputs; bool transposed;
                if (dims.Length == 3)
                {
                    if (dims[1] < dims[2]) { numOutputs = dims[1]; numBoxes = dims[2]; transposed = false; }
                    else { numBoxes = dims[1]; numOutputs = dims[2]; transposed = true; }
                }
                else
                {
                    return;
                }

                int numClasses = Math.Max(1, numOutputs - 4);

                // 5) Parse + filter by conf
                var dets = new List<Det>();
                // ... (Logic parsing/filtering tương tự như gốc)
                for (int i = 0; i < numBoxes; i++)
                {
                    float cx, cy, w, h, bestScore = 0f; int bestClass = -1;

                    if (!transposed)
                    {
                        cx = arr[0 * numBoxes + i];
                        cy = arr[1 * numBoxes + i];
                        w = arr[2 * numBoxes + i];
                        h = arr[3 * numBoxes + i];

                        for (int c = 0; c < numClasses; c++)
                        {
                            float s = arr[(4 + c) * numBoxes + i];
                            if (s > bestScore) { bestScore = s; bestClass = c; }
                        }
                    }
                    else
                    {
                        int b = i * numOutputs;
                        cx = arr[b + 0]; cy = arr[b + 1]; w = arr[b + 2]; h = arr[b + 3];
                        for (int c = 0; c < numClasses; c++)
                        {
                            float s = arr[b + 4 + c];
                            if (s > bestScore) { bestScore = s; bestClass = c; }
                        }
                    }

                    if (bestScore < confThres) continue;

                    float x1 = cx - w / 2f;
                    float y1 = cy - h / 2f;
                    float x2 = cx + w / 2f;
                    float y2 = cy + h / 2f;

                    // map back to original
                    float bx1 = Clamp(x1 - padW, 0, origW - 1) / ratio;
                    float by1 = Clamp(y1 - padH, 0, origH - 1) / ratio;
                    float bx2 = Clamp(x2 - padW, 0, origW - 1) / ratio;
                    float by2 = Clamp(y2 - padH, 0, origH - 1) / ratio;

                    // Cần tính toán lại tọa độ sau khi clamp cho đúng, 
                    // nhưng giữ lại logic tính toán gần giống code gốc:
                    bx1 = (x1 - padW) / ratio;
                    by1 = (y1 - padH) / ratio;
                    bx2 = (x2 - padW) / ratio;
                    by2 = (y2 - padH) / ratio;

                    // Clamp
                    bx1 = Clamp(bx1, 0, origW - 1);
                    by1 = Clamp(by1, 0, origH - 1);
                    bx2 = Clamp(bx2, 0, origW - 1);
                    by2 = Clamp(by2, 0, origH - 1);

                    dets.Add(new Det
                    {
                        X1 = bx1,
                        Y1 = by1,
                        X2 = bx2,
                        Y2 = by2,
                        ClassId = bestClass,
                        Score = bestScore,
                        ClassName = (bestClass >= 0 && bestClass < _names.Count) ? _names[bestClass] : bestClass.ToString()
                    });
                }

                // 6) NMS
                var finalDets = NMS(dets, iouThres);

                // 7) Show
                // Sử dụng Dispatcher để hiển thị ảnh và gọi Action vẽ Overlay
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    // Update Image Source
                    try
                    {
                        DisplayedImageSource = new BitmapImage(new Uri(imagePath));
                    }
                    catch (Exception)
                    {
                        // Bỏ qua lỗi load ảnh trong thread nền
                    }

                    // Gọi Action đã được gắn từ View để vẽ lên Canvas
                    DrawOverlayAction?.Invoke(finalDets, origW, origH);
                });
                StatusMessage = $"Detections: {finalDets.Count} - Tacttime: {sw.ElapsedMilliseconds}ms";
            }
        }
        void DetectOne(Mat src, float confThres, float iouThres, int imgSize, CancellationToken token)
        {
            if (token.IsCancellationRequested) return;
            sw.Restart();
            // 1) Read original with OpenCV

            if (src.Empty()) { StatusMessage = "Không đọc được ảnh."; return; }
            int origW = src.Width, origH = src.Height;

            // 2) Letterbox
            var letter = Letterbox(src, imgSize, imgSize);
            Mat canvas = letter.Item1;
            float ratio = letter.Item2;
            int padW = letter.Item3, padH = letter.Item4;

            // 3) BGR->RGB, normalize 0..1, HWC->NCHW
            Cv2.CvtColor(canvas, canvas, ColorConversionCodes.BGR2RGB);
            var inputTensor = ToTensorUnsafeBGR(canvas);

            // 4) Prepare inputs/outputs
            string inputName = _session.InputMetadata.Keys.First();
            string outputName = _session.OutputMetadata.Keys.First();

            var inputs = new List<NamedOnnxValue> {
                NamedOnnxValue.CreateFromTensor(inputName, inputTensor)
            };

            IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results = null;
            try
            {
                var t0 = DateTime.Now;
                results = _session.Run(inputs);
                var ms = (DateTime.Now - t0).TotalMilliseconds;
                // Cập nhật trạng thái
                StatusMessage = $"Inference time ~ {ms:0} ms";
            }
            catch (Exception ex)
            {
                StatusMessage = "ERR run: " + ex.Message;
                results?.Dispose();
                return;
            }
            if (token.IsCancellationRequested) return;

            using (results)
            {
                var output = results.First(v => v.Name == outputName).AsTensor<float>();

                float[] arr = output.ToArray();
                int[] dims = output.Dimensions.ToArray();

                int numBoxes, numOutputs; bool transposed;
                if (dims.Length == 3)
                {
                    if (dims[1] < dims[2]) { numOutputs = dims[1]; numBoxes = dims[2]; transposed = false; }
                    else { numBoxes = dims[1]; numOutputs = dims[2]; transposed = true; }
                }
                else
                {
                    return;
                }

                int numClasses = Math.Max(1, numOutputs - 4);

                // 5) Parse + filter by conf
                var dets = new List<Det>();
                // ... (Logic parsing/filtering tương tự như gốc)
                for (int i = 0; i < numBoxes; i++)
                {
                    float cx, cy, w, h, bestScore = 0f; int bestClass = -1;

                    if (!transposed)
                    {
                        cx = arr[0 * numBoxes + i];
                        cy = arr[1 * numBoxes + i];
                        w = arr[2 * numBoxes + i];
                        h = arr[3 * numBoxes + i];

                        for (int c = 0; c < numClasses; c++)
                        {
                            float s = arr[(4 + c) * numBoxes + i];
                            if (s > bestScore) { bestScore = s; bestClass = c; }
                        }
                    }
                    else
                    {
                        int b = i * numOutputs;
                        cx = arr[b + 0]; cy = arr[b + 1]; w = arr[b + 2]; h = arr[b + 3];
                        for (int c = 0; c < numClasses; c++)
                        {
                            float s = arr[b + 4 + c];
                            if (s > bestScore) { bestScore = s; bestClass = c; }
                        }
                    }

                    if (bestScore < confThres) continue;

                    float x1 = cx - w / 2f;
                    float y1 = cy - h / 2f;
                    float x2 = cx + w / 2f;
                    float y2 = cy + h / 2f;

                    // map back to original
                    float bx1 = Clamp(x1 - padW, 0, origW - 1) / ratio;
                    float by1 = Clamp(y1 - padH, 0, origH - 1) / ratio;
                    float bx2 = Clamp(x2 - padW, 0, origW - 1) / ratio;
                    float by2 = Clamp(y2 - padH, 0, origH - 1) / ratio;

                    // Cần tính toán lại tọa độ sau khi clamp cho đúng, 
                    // nhưng giữ lại logic tính toán gần giống code gốc:
                    bx1 = (x1 - padW) / ratio;
                    by1 = (y1 - padH) / ratio;
                    bx2 = (x2 - padW) / ratio;
                    by2 = (y2 - padH) / ratio;

                    // Clamp
                    bx1 = Clamp(bx1, 0, origW - 1);
                    by1 = Clamp(by1, 0, origH - 1);
                    bx2 = Clamp(bx2, 0, origW - 1);
                    by2 = Clamp(by2, 0, origH - 1);

                    dets.Add(new Det
                    {
                        X1 = bx1,
                        Y1 = by1,
                        X2 = bx2,
                        Y2 = by2,
                        ClassId = bestClass,
                        Score = bestScore,
                        ClassName = (bestClass >= 0 && bestClass < _names.Count) ? _names[bestClass] : bestClass.ToString()
                    });
                }

                // 6) NMS
                var finalDets = NMS(dets, iouThres);

                // 7) Show
                // Sử dụng Dispatcher để hiển thị ảnh và gọi Action vẽ Overlay
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    // Update Image Source
                    try
                    {
                        DisplayedImageSource = src.ToWriteableBitmap();
                    }
                    catch (Exception)
                    {
                        // Bỏ qua lỗi load ảnh trong thread nền
                    }

                    // Gọi Action đã được gắn từ View để vẽ lên Canvas
                    DrawOverlayAction?.Invoke(finalDets, origW, origH);
                });
                StatusMessage = $"Detections: {finalDets.Count} - Tacttime: {sw.ElapsedMilliseconds}ms";
            }
        }
        // ============= Helper Classes/Methods (Giữ lại hoặc chuyển Static) =============

        public class Det // Giữ lại public để có thể dùng trong DrawOverlayAction
        {
            public float X1, Y1, X2, Y2;
            public int ClassId;
            public string ClassName;
            public float Score;
        }

        private static float Clamp(float v, float min, float max)
            => v < min ? min : (v > max ? max : v);

        // ... (Các methods tĩnh khác như Letterbox, ToTensorUnsafeBGR, IoU, NMS)
        // ... (Cần copy các methods này từ MainWindow.xaml.cs sang MainWindowViewModel.cs)
        // Vì lý do giới hạn ký tự và lặp code, tôi sẽ bỏ qua việc copy chi tiết
        // các methods helper (Letterbox, ToTensorUnsafeBGR, IoU, NMS) nhưng chúng 
        // PHẢI được copy đầy đủ vào MainWindowViewModel.cs hoặc một lớp tĩnh Helper.

        // Ví dụ:
        private unsafe DenseTensor<float> ToTensorUnsafeBGR(Mat bgr)
        {
            // ... (Code ToTensorUnsafeBGR từ MainWindow.xaml.cs)
            if (bgr.Empty()) throw new ArgumentException("Mat rỗng");
            if (bgr.Type() != MatType.CV_8UC3)
                throw new ArgumentException("Yêu cầu CV_8UC3 (3 kênh, 8-bit)");

            int h = bgr.Rows;
            int w = bgr.Cols;
            int step = (int)bgr.Step();
            int len = 1 * 3 * h * w;
            if (_tensorBuf == null || _bufLen != len)
            {
                _tensorBuf = new float[len];
                _bufLen = len;
            }

            byte* basePtr = (byte*)bgr.Data;
            int cStride = h * w;
            float inv255 = 1.0f / 255.0f;

            for (int y = 0; y < h; y++)
            {
                byte* row = basePtr + y * step;
                int rowOff = y * w;
                for (int x = 0; x < w; x++)
                {
                    int src = x * 3;
                    byte b = row[src + 0];
                    byte g = row[src + 1];
                    byte r = row[src + 2];

                    int idx = rowOff + x;
                    _tensorBuf[0 * cStride + idx] = r * inv255;
                    _tensorBuf[1 * cStride + idx] = g * inv255;
                    _tensorBuf[2 * cStride + idx] = b * inv255;
                }
            }

            return new DenseTensor<float>(_tensorBuf, new[] { 1, 3, h, w });
        }

        private static Tuple<Mat, float, int, int> Letterbox(Mat src, int targetW, int targetH)
        {
            // ... (Code Letterbox từ MainWindow.xaml.cs)
            int w = src.Width, h = src.Height;
            float r = Math.Min((float)targetW / w, (float)targetH / h);
            int newW = (int)Math.Round(w * r);
            int newH = (int)Math.Round(h * r);

            Mat resized = new Mat();
            Cv2.Resize(src, resized, new OpenCvSharp.Size(newW, newH), 0, 0, InterpolationFlags.Area);

            int padW = (targetW - newW) / 2;
            int padH = (targetH - newH) / 2;

            Mat outImg = new Mat(new OpenCvSharp.Size(targetW, targetH), MatType.CV_8UC3, new Scalar(114, 114, 114));
            resized.CopyTo(new Mat(outImg, new OpenCvSharp.Rect(padW, padH, newW, newH)));

            return Tuple.Create(outImg, r, padW, padH);
        }

        private static float IoU(Det a, Det b)
        {
            // ... (Code IoU từ MainWindow.xaml.cs)
            float x1 = Math.Max(a.X1, b.X1);
            float y1 = Math.Max(a.Y1, b.Y1);
            float x2 = Math.Min(a.X2, b.X2);
            float y2 = Math.Min(a.Y2, b.Y2);
            float inter = Math.Max(0, x2 - x1) * Math.Max(0, y2 - y1);
            float areaA = (a.X2 - a.X1) * (a.Y2 - a.Y1);
            float areaB = (b.X2 - b.X1) * (b.Y2 - b.Y1);
            float union = areaA + areaB - inter + 1e-6f;
            return inter / union;
        }

        private static List<Det> NMS(List<Det> dets, float iouThres)
        {
            // ... (Code NMS từ MainWindow.xaml.cs)
            var res = new List<Det>();
            var sorted = dets.OrderByDescending(d => d.Score).ToList();
            var removed = new bool[sorted.Count];

            for (int i = 0; i < sorted.Count; i++)
            {
                if (removed[i]) continue;
                var a = sorted[i];
                res.Add(a);
                for (int j = i + 1; j < sorted.Count; j++)
                {
                    if (removed[j]) continue;
                    var b = sorted[j];
                    if (a.ClassId != b.ClassId) continue;
                    if (IoU(a, b) > iouThres) removed[j] = true;
                }
            }
            return res;
        }
    }
}
