using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace WpfApp1
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : System.Windows.Window
    {
        string _onnxPath = "";
        string _imgPath = "";
        List<string> _names = new List<string>();
        InferenceSession _session;
        bool _useGpu = false;
        float[] _tensorBuf;   // cache buffer
        int _bufLen;
        bool bAuto = false;

        public MainWindow()
        {
            InitializeComponent();
            BtnRun.Click += BtnRun_Click; ;
        }

        private void BtnRun_Click(object sender, RoutedEventArgs e)
        {
            bAuto = true;
            if (_session == null) { MessageBox.Show("Load ONNX trước."); return; }
            //if (string.IsNullOrEmpty(_imgPath)) { MessageBox.Show("Chọn ảnh trước."); return; }
            string FolderPath = @"C:\Users\khoa\Pictures\Data_LimeStone\raw\images";
            var files = Directory.GetFiles(FolderPath).ToList();
            _useGpu = ChkGpu.IsChecked == true;
            float conf = ParseFloat(TbConf.Text, 0.25f);
            float iou = ParseFloat(TbIou.Text, 0.45f);
            int imgSz = (int)ParseFloat(TbImg.Text, 640);

            Thread t1 = new Thread(() =>
            {
                while (bAuto)
                {
                        files.ForEach(x =>
                        {
                            if (!bAuto) return;
                            DetectOne(x, conf, iou, imgSz);
                            //Thread.Sleep(1000);
                        });
                }
            });
            t1.IsBackground = true;
            t1.Start();
        }

        // ======= UI events =======
        private void BtnLoadOnnx_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "ONNX|*.onnx" };
            if (dlg.ShowDialog() == true)
            {
                _onnxPath = dlg.FileName;
                CreateSession();
            }
        }

        private void BtnLoadNames_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "classes.txt|*.txt|All|*.*" };
            if (dlg.ShowDialog() == true)
            {
                _names = File.ReadAllLines(dlg.FileName)
                             .Where(l => !string.IsNullOrWhiteSpace(l)).ToList();
                TxtStatus.Text = $"Loaded classes: {_names.Count}";
            }
        }

        private void BtnOpenImage_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "Images|*.jpg;*.jpeg;*.png;*.bmp;*.tif;*.tiff;*.webp" };
            if (dlg.ShowDialog() == true)
            {
                Overlay.Children.Clear();
                _imgPath = dlg.FileName;
                var bmp = new BitmapImage(new Uri(_imgPath));
                ImgView.Source = bmp;

                // match overlay size
                Overlay.Width = bmp.PixelWidth;
                Overlay.Height = bmp.PixelHeight;
            }
        }

        private void BtnDetect_Click(object sender, RoutedEventArgs e)
        {
            bAuto = false;
            Console.WriteLine($"b_Auto: {bAuto}");
            if (_session == null) { MessageBox.Show("Load ONNX trước."); return; }
            if (string.IsNullOrEmpty(_imgPath)) { MessageBox.Show("Chọn ảnh trước."); return; }

            _useGpu = ChkGpu.IsChecked == true;
            float conf = ParseFloat(TbConf.Text, 0.25f);
            float iou = ParseFloat(TbIou.Text, 0.45f);
            int imgSz = (int)ParseFloat(TbImg.Text, 640);

            DetectOne(_imgPath, conf, iou, imgSz);
        }

        // ======= Core =======
        void CreateSession()
        {
            try
            {
                _session?.Dispose();
                var opt = new SessionOptions();
                if (ChkGpu.IsChecked == true)
                {
                    try { opt.AppendExecutionProvider_CUDA(); }
                    catch { TxtStatus.Text = "CUDA provider không khả dụng. Dùng CPU."; }
                }
                _session = new InferenceSession(_onnxPath, opt);
                TxtStatus.Text = $"ONNX loaded: {_onnxPath}";
            }
            catch (Exception ex)
            {
                TxtStatus.Text = "ERR load ONNX: " + ex.Message;
            }
        }
        Stopwatch sw = new Stopwatch();
        void DetectOne(string imagePath, float confThres, float iouThres, int imgSize)
        {
            sw.Restart();
            // 1) Read original with OpenCV
            Mat src = Cv2.ImRead(imagePath);

            if (src.Empty()) { TxtStatus.Text = "Không đọc được ảnh."; return; }
            int origW = src.Width, origH = src.Height;

            // 2) Letterbox to (imgSize,imgSize) with padding
            var letter = Letterbox(src, imgSize, imgSize);
            Mat canvas = letter.Item1;         // BGR (imgSize,imgSize)
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
                App.Current.Dispatcher.Invoke(() =>
                {
                    TxtStatus.Text = $"Inference time ~ {ms:0} ms";
                });
            }
            catch (Exception ex)
            {
                App.Current.Dispatcher.Invoke(() =>
                {
                    TxtStatus.Text = "ERR run: " + ex.Message;
                });
                results?.Dispose();
                return;
            }

            var output = results.First(v => v.Name == outputName).AsTensor<float>();
            results.Dispose();

            float[] arr = output.ToArray();
            int[] dims = output.Dimensions.ToArray();  // [1,84,8400] or [1,8400,84]

            int numBoxes, numOutputs; bool transposed;
            if (dims.Length == 3)
            {
                if (dims[1] < dims[2]) { numOutputs = dims[1]; numBoxes = dims[2]; transposed = false; }
                else { numBoxes = dims[1]; numOutputs = dims[2]; transposed = true; }
            }
            else
            {
                App.Current.Dispatcher.Invoke(() =>
                {
                    //TxtStatus.Text = "Output tensor shape lạ.";
                });
                return;
            }

            int numClasses = Math.Max(1, numOutputs - 4);

            // 5) Parse + filter by conf
            var dets = new List<Det>();
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
                float bx1 = (x1 - padW) / ratio;
                float by1 = (y1 - padH) / ratio;
                float bx2 = (x2 - padW) / ratio;
                float by2 = (y2 - padH) / ratio;

                bx1 = Clamp(bx1, 0, origW - 1);
                by1 = Clamp(by1, 0, origH - 1);
                bx2 = Clamp(bx2, 0, origW - 1);
                by2 = Clamp(by2, 0, origH - 1);

                dets.Add(new Det { X1 = bx1, Y1 = by1, X2 = bx2, Y2 = by2, ClassId = bestClass, Score = bestScore });
            }

            // 6) NMS
            var finalDets = NMS(dets, iouThres);

            // 7) Show
            App.Current.Dispatcher.Invoke(() =>
            {
                var bmp = new BitmapImage(new Uri(imagePath));
                ImgView.Source = bmp;

                DrawDetections(finalDets, origW, origH);
            });
            Console.WriteLine($"Tacttime Detect: {sw.ElapsedMilliseconds}ms");
        }

        // ======= Rendering =======
        void DrawDetections(List<Det> dets, int w, int h)
        {
            Overlay.Children.Clear();

            // Scale Canvas to image size
            Overlay.Width = w; Overlay.Height = h;
            Brush color = System.Windows.Media.Brushes.Lime;
            foreach (var d in dets)
            {
                color = d.ClassId == 0 ? System.Windows.Media.Brushes.Lime :
                        d.ClassId == 1 ? System.Windows.Media.Brushes.Red :
                                         System.Windows.Media.Brushes.Red;
                var rect = new Rectangle
                {
                    Stroke = color,
                    StrokeThickness = 2,
                    Width = Math.Max(1, d.X2 - d.X1),
                    Height = Math.Max(1, d.Y2 - d.Y1)
                };
                Canvas.SetLeft(rect, d.X1);
                Canvas.SetTop(rect, d.Y1);
                Overlay.Children.Add(rect);

                string cls = (d.ClassId >= 0 && d.ClassId < _names.Count) ? _names[d.ClassId] : $"cls{d.ClassId}";
                var tb = new TextBlock
                {
                    Text = $"{cls} {d.Score:0.00}",
                    Foreground = color,
                    Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(150, 0, 0, 0)),
                    FontSize = 14,
                    Padding = new Thickness(4, 1, 4, 1)
                };
                // place label above rect
                Canvas.SetLeft(tb, d.X1);
                Canvas.SetTop(tb, Math.Max(0, d.Y1 - 22));
                Overlay.Children.Add(tb);
            }
            //TxtStatus.Text = $"Detections: {dets.Count}";
        }

        // ======= Helpers =======
        static float ParseFloat(string s, float defv)
            => float.TryParse(s, out var v) ? v : defv;

        static float Clamp(float v, float min, float max)
            => v < min ? min : (v > max ? max : v);

        unsafe DenseTensor<float> ToTensorUnsafeBGR(Mat bgr)
        {
            if (bgr.Empty()) throw new ArgumentException("Mat rỗng");
            if (bgr.Type() != MatType.CV_8UC3)
                throw new ArgumentException("Yêu cầu CV_8UC3 (3 kênh, 8-bit)");

            int h = bgr.Rows;
            int w = bgr.Cols;
            int step = (int)bgr.Step();   // số byte mỗi hàng (có padding)
            int len = 1 * 3 * h * w;
            if (_tensorBuf == null || _bufLen != len)
            {
                _tensorBuf = new float[len];
                _bufLen = len;
            }

            byte* basePtr = (byte*)bgr.Data;   // con trỏ đầu ảnh
                                               // NCHW: [1, R, H, W], [1, G, H, W], [1, B, H, W]
            int cStride = h * w;               // số phần tử mỗi kênh
            float inv255 = 1.0f / 255.0f;

            // Lặp hàng → cột, đọc BGR, ghi R,G,B theo NCHW
            for (int y = 0; y < h; y++)
            {
                byte* row = basePtr + y * step;  // đầu hàng y
                int rowOff = y * w;
                for (int x = 0; x < w; x++)
                {
                    int src = x * 3;
                    byte b = row[src + 0];
                    byte g = row[src + 1];
                    byte r = row[src + 2];

                    // vị trí trong tensor
                    int idx = rowOff + x;
                    _tensorBuf[0 * cStride + idx] = r * inv255; // R
                    _tensorBuf[1 * cStride + idx] = g * inv255; // G
                    _tensorBuf[2 * cStride + idx] = b * inv255; // B
                }
            }

            return new DenseTensor<float>(_tensorBuf, new[] { 1, 3, h, w });
        }

        static Tuple<Mat, float, int, int> Letterbox(Mat src, int targetW, int targetH)
        {
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

        class Det
        {
            public float X1, Y1, X2, Y2;
            public int ClassId;
            public float Score;
        }

        static float IoU(Det a, Det b)
        {
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

        static List<Det> NMS(List<Det> dets, float iouThres)
        {
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
