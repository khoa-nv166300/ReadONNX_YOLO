using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Wpf_MVVM_Read_Onnx_Using_Canvas.Models;

namespace Wpf_MVVM_Read_Onnx_Using_Canvas.ViewModels
{
    public partial class MainViewModel : BaseViewModel
    {

        public MainViewModel()
        {
            LoadOnnxCommand = new RelayCommand(p => LoadOnnx());
            LoadNamesCommand = new RelayCommand(p => LoadNames());
            OpenImageCommand = new RelayCommand(p => OpenImage());
            DetectCommand = new RelayCommand(p => { bAutoRun = false; Detect(); });
            RunCommand = new RelayCommand(p => { bAutoRun = true; RunToggle(); });

        }

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
                    DisplayedImageSource = new BitmapImage(new Uri(_imgPath));
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

            if (!float.TryParse(ConfidenceThreshold, out float conf)) conf = 0.25f;
            if (!float.TryParse(IouThreshold, out float iou)) iou = 0.45f;
            if (!int.TryParse(ImageSize, out int imgSz)) imgSz = 640;

            ItemSourceListData.Clear();
            OverlayDetections.Clear();

            await Task.Run(() => DetectOne(_imgPath, conf, iou, imgSz, CancellationToken.None));
        }

        private void RunToggle()
        {
            if (IsAutoRunning)
            {
                _ctsAutoDetect?.Cancel();
                _ctsAutoDetect?.Dispose();
                _ctsAutoDetect = null;
                IsAutoRunning = false;
                StatusMessage = "Auto Detect stopped.";
            }
            else
            {
                if (_session == null) { StatusMessage = "Load ONNX trước."; return; }

                if (!float.TryParse(ConfidenceThreshold, out float conf)) conf = 0.25f;
                if (!float.TryParse(IouThreshold, out float iou)) iou = 0.45f;
                if (!int.TryParse(ImageSize, out int imgSz)) imgSz = 640;

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

                Task.Run(() =>
                {
                    while (!token.IsCancellationRequested && bAutoRun)
                    {

                        foreach (var filePath in files)
                        {
                            if(bAutoRun == false) break;
                            Thread.Sleep(100);
                            App.Current.Dispatcher.Invoke(() =>
                            {
                                ItemSourceListData.Clear();
                                OverlayDetections.Clear();
                            });
                            if (token.IsCancellationRequested) return;
                            DetectOne(filePath, conf, iou, imgSz, token);
                        }
                    }
                }, token);
            }
        }


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
            Mat src = Cv2.ImRead(imagePath);

            if (src.Empty()) { StatusMessage = "Không đọc được ảnh."; return; }
            int origW = src.Width, origH = src.Height;

            var letter = Letterbox(src, imgSize, imgSize);
            Mat canvas = letter.Item1;
            float ratio = letter.Item2;
            int padW = letter.Item3, padH = letter.Item4;

            //Cv2.CvtColor(canvas, canvas, ColorConversionCodes.BGR2RGB);
            var inputTensor = ToTensorUnsafeBGR(canvas);

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

                var dets = new List<Detections>();
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

                    float bx1 = Clamp(x1 - padW, 0, origW - 1) / ratio;
                    float by1 = Clamp(y1 - padH, 0, origH - 1) / ratio;
                    float bx2 = Clamp(x2 - padW, 0, origW - 1) / ratio;
                    float by2 = Clamp(y2 - padH, 0, origH - 1) / ratio;

                    bx1 = (x1 - padW) / ratio;
                    by1 = (y1 - padH) / ratio;
                    bx2 = (x2 - padW) / ratio;
                    by2 = (y2 - padH) / ratio;

                    bx1 = Clamp(bx1, 0, origW - 1);
                    by1 = Clamp(by1, 0, origH - 1);
                    bx2 = Clamp(bx2, 0, origW - 1);
                    by2 = Clamp(by2, 0, origH - 1);

                    dets.Add(new Detections
                    {
                        X1 = bx1,
                        Y1 = by1,
                        X2 = bx2,
                        Y2 = by2,
                        ClassId = bestClass,
                        Score = bestScore,
                        ClassName = _names != null ? _names[bestClass] : "",
                    });
                }

                var finalDets = NMS(dets, iouThres);

                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    try
                    {
                        DisplayedImageSource = new BitmapImage(new Uri(imagePath));
                        finalDets.ForEach(d =>
                        {
                            ItemSourceListData.Add(new DetectionMessage(d.ToString(),
                                (d.ClassId == 0) ? Brushes.Cyan : (d.ClassId == 1) ? Brushes.DodgerBlue : Brushes.Red
                                ));

                            OverlayDetections.Add(new GraphicItem()
                            {
                                X = d.X1,
                                Y = d.Y1,
                                Width = Math.Abs(d.X1 - d.X2),
                                Height = Math.Abs(d.Y1 - d.Y2),
                                Color = (d.ClassId == 0) ? Brushes.Cyan : (d.ClassId == 1) ? Brushes.DodgerBlue : Brushes.Red,
                                StrokeThickness = 3,
                                Fill = Brushes.Transparent,
                            });

                            OverlayDetections.Add(new TextBlockItem()
                            {
                                X = d.X1,
                                Y = d.Y1 - 20,
                                Color = (d.ClassId == 0) ? Brushes.Cyan : (d.ClassId == 1) ? Brushes.DodgerBlue : Brushes.Red,
                                Label = $" {d.ClassName} ({d.Score:P1})"
                            });
                        });
                    }
                    catch (Exception)
                    {
                    }

                    DrawOverlayAction?.Invoke(finalDets, origW, origH);
                });
                ResultMessage = $"Detections: {finalDets.Count} ";
            }
        }
        void DetectOne(Mat _mat, float confThres, float iouThres, int imgSize, CancellationToken token)
        {
            if (token.IsCancellationRequested) return;
            sw.Restart();

            Mat src = _mat.Clone();

            if (src.Empty()) { StatusMessage = "Không đọc được ảnh."; return; }
            int origW = src.Width, origH = src.Height;

            var letter = Letterbox(src, imgSize, imgSize);
            Mat canvas = letter.Item1;
            float ratio = letter.Item2;
            int padW = letter.Item3, padH = letter.Item4;

            var inputTensor = ToTensorUnsafeBGR(canvas);

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

                var dets = new List<Detections>();
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

                    float bx1 = Clamp(x1 - padW, 0, origW - 1) / ratio;
                    float by1 = Clamp(y1 - padH, 0, origH - 1) / ratio;
                    float bx2 = Clamp(x2 - padW, 0, origW - 1) / ratio;
                    float by2 = Clamp(y2 - padH, 0, origH - 1) / ratio;

                    bx1 = (x1 - padW) / ratio;
                    by1 = (y1 - padH) / ratio;
                    bx2 = (x2 - padW) / ratio;
                    by2 = (y2 - padH) / ratio;

                    bx1 = Clamp(bx1, 0, origW - 1);
                    by1 = Clamp(by1, 0, origH - 1);
                    bx2 = Clamp(bx2, 0, origW - 1);
                    by2 = Clamp(by2, 0, origH - 1);

                    dets.Add(new Detections
                    {
                        X1 = bx1,
                        Y1 = by1,
                        X2 = bx2,
                        Y2 = by2,
                        ClassId = bestClass,
                        Score = bestScore,
                        ClassName = _names != null ? _names[bestClass] : "",
                    });
                }

                var finalDets = NMS(dets, iouThres);

                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    try
                    {
                        DisplayedImageSource = _mat.ToWriteableBitmap();
                        finalDets.ForEach(d =>
                        {
                            ItemSourceListData.Add(new DetectionMessage(d.ToString(),
                                (d.ClassId == 0) ? Brushes.Cyan : (d.ClassId == 1) ? Brushes.DodgerBlue : Brushes.Red
                                ));
                        });
                    }
                    catch (Exception)
                    {
                    }

                    DrawOverlayAction?.Invoke(finalDets, origW, origH);
                });
                ResultMessage = $"Detections: {finalDets.Count} ";
            }
        }

        private  float Clamp(float v, float min, float max)
            => v < min ? min : (v > max ? max : v);

        private unsafe DenseTensor<float> ToTensorUnsafeBGR(Mat bgr)
        {
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

        private  Tuple<Mat, float, int, int> Letterbox(Mat src, int targetW, int targetH)
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

        private  float IoU(Detections a, Detections b)
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

        private  List<Detections> NMS(List<Detections> dets, float iouThres)
        {
            var res = new List<Detections>();
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
