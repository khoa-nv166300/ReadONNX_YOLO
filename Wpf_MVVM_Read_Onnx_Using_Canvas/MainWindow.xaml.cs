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
    public partial class MainWindow : System.Windows.Window
    {
        MainViewModel MainViewModel { get; set; }
        public MainWindow()
        {
            InitializeComponent();
            MainViewModel = new MainViewModel();
            this.DataContext = MainViewModel;

        }

        protected override void OnClosed(System.EventArgs e)
        {
            MainViewModel.ClosingAction?.Invoke();
            base.OnClosed(e);
        }
    }
}
