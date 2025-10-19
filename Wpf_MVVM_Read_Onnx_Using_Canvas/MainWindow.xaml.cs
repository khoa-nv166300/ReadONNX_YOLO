using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
using Wpf_MVVM_Read_Onnx_Using_Canvas.ViewModels;

namespace Wpf_MVVM_Read_Onnx_Using_Canvas
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private MainViewModel _viewModel;

        public MainWindow()
        {
            InitializeComponent();

            // 1. Khởi tạo ViewModel và thiết lập DataContext
            _viewModel = new MainViewModel();
            this.DataContext = _viewModel;

            // 2. Gán Action vẽ Overlay từ View cho ViewModel
            _viewModel.DrawOverlayAction = DrawDetections;

            // Xóa các event handlers cũ (như BtnRun.Click += BtnRun_Click)
            // vì chúng đã được thay thế bằng Command Binding trong XAML.
        }

        // Logic UI-specific (Vẽ lên Canvas) được giữ lại trong View
        void DrawDetections(List<MainViewModel.Det> dets, int w, int h)
        {
            Overlay.Children.Clear();

            // Scale Canvas to image size
            Overlay.Width = w; Overlay.Height = h;
            Brush color;
            foreach (var d in dets)
            {
                // Logic chọn màu theo ClassId (cần truy cập _names, nhưng MVVM 
                // không cho phép truy cập _names từ View/Code-behind, 
                // nên cần đơn giản hóa hoặc đưa _names vào ViewModel).
                // Giả định logic chọn màu đơn giản theo ClassId như cũ:
                color = d.ClassId == 0 ? System.Windows.Media.Brushes.Lime :
                        d.ClassId == 1 ? System.Windows.Media.Brushes.DodgerBlue :
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

                // Giả định tên lớp (cls) được quản lý trong ViewModel và không cần ở đây
                string cls = $"{d.ClassName}";
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
        }

        // Tất cả các event handlers cũ (BtnLoadOnnx_Click, BtnOpenImage_Click, v.v.)
        // đã được xóa bỏ.
    }
}
