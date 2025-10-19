using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace WpfApp1
{
    public class OnnxInferenceSample
    {
        public void Run(string modelPath, string imagePath)
        {
            // 1. Load model
            var session = new InferenceSession(modelPath);

            // 2. Tiền xử lý ảnh
            var inputTensor = PreprocessImage(imagePath);

            // 3. Tạo input cho model
            var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("input", inputTensor) // "input" tùy thuộc model
        };

            // 4. Chạy inference
            using (var results = session.Run(inputs))
            {
                // 5. Trích xuất kết quả
                foreach (var r in results)
                {
                    if (r.Name == "boxes")
                    {
                        var boxes = r.AsTensor<float>().ToArray(); // [N,4]
                                                                   // Xử lý boxes...
                    }
                    else if (r.Name == "labels" || r.Name == "classes")
                    {
                        var classes = r.AsTensor<long>().ToArray(); // [N]
                                                                    // Xử lý class...
                    }
                    else if (r.Name == "scores")
                    {
                        var scores = r.AsTensor<float>().ToArray(); // [N]
                                                                    // Xử lý scores...
                    }
                }
            }
        }

        // Hàm ví dụ tiền xử lý ảnh, bạn cần tùy chỉnh cho model cụ thể
        private Tensor<float> PreprocessImage(string imagePath)
        {
            // Đọc ảnh, resize, normalize, chuyển sang tensor [1,3,H,W]
            // Đây chỉ là khung, bạn phải điều chỉnh cho đúng với model của bạn
            Bitmap bmp = new Bitmap(imagePath);
            Bitmap resized = new Bitmap(bmp, new System.Drawing.Size(640, 640)); // ví dụ 640x640
            var tensor = new DenseTensor<float>(new[] { 1, 3, 640, 640 });
            for (int y = 0; y < 640; y++)
            {
                for (int x = 0; x < 640; x++)
                {
                    System.Drawing.Color c = resized.GetPixel(x, y);
                    tensor[0, 0, y, x] = c.R / 255.0f;
                    tensor[0, 1, y, x] = c.G / 255.0f;
                    tensor[0, 2, y, x] = c.B / 255.0f;
                }
            }
            return tensor;
        }
    }
}