using System;
using System.Collections.Generic;
using System.ComponentModel;
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
using Wpf_MVVM_Read_Onnx.ViewModel;

namespace Wpf_MVVM_Read_Onnx
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

            _viewModel = new MainViewModel();
            this.DataContext = _viewModel;

            _viewModel.DrawOverlayAction = DrawDetections;

        }
        protected override void OnClosing(CancelEventArgs e)
        {
            CDefines.OnCloseChanged();
            base.OnClosing(e);
        }
        void DrawDetections(List<MainViewModel.Det> dets, int w, int h)
        {
            Overlay.Children.Clear();

            // Scale Canvas to image size
            Overlay.Width = w; Overlay.Height = h;
            Brush color;
            foreach (var d in dets)
            {
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

                string cls = $"{d.ClassName}";
                var tb = new TextBlock
                {
                    Text = $"{cls} {d.Score:0.00}",
                    Foreground = color,
                    Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(150, 0, 0, 0)),
                    FontSize = 14,
                    Padding = new Thickness(4, 1, 4, 1)
                };
                Canvas.SetLeft(tb, d.X1);
                Canvas.SetTop(tb, Math.Max(0, d.Y1 - 22));
                Overlay.Children.Add(tb);
            }
        }

    }
}

