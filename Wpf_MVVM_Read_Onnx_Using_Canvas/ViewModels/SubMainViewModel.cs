using Microsoft.ML.OnnxRuntime;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media;
using Wpf_MVVM_Read_Onnx_Using_Canvas.Models;

namespace Wpf_MVVM_Read_Onnx_Using_Canvas.ViewModels
{
    public partial class MainViewModel : BaseViewModel
    {

        private InferenceSession _session;
        private List<string> _names = new List<string>();
        private CancellationTokenSource _ctsAutoDetect;
        private Stopwatch sw = new Stopwatch();

        private string _onnxPath = "";
        private string _imgPath = "";
        private float[] _tensorBuf;
        private int _bufLen;
        private bool bAutoRun = false;

        private string _resultMessage = "";

        public string ResultMessage
        {
            get => _resultMessage;
            set => SetProperty(ref _resultMessage, value);
        }


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

        public Action<List<Detections>, int, int> DrawOverlayAction { get; set; }
        private ObservableCollection<DetectionMessage> _temSourceListData = new ObservableCollection<DetectionMessage>();

        public ObservableCollection<DetectionMessage> ItemSourceListData
        {
            get { return _temSourceListData; }
            set { _temSourceListData = value; OnPropertyChanged(); }
        }
        private ObservableCollection<IGraphicItem> _overlayDetections = new ObservableCollection<IGraphicItem>();

        public ObservableCollection<IGraphicItem> OverlayDetections
        {
            get { return _overlayDetections; }
            set { _overlayDetections = value; OnPropertyChanged(); }
        }

        public ICommand LoadOnnxCommand { get; }
        public ICommand LoadNamesCommand { get; }
        public ICommand OpenImageCommand { get; }
        public ICommand DetectCommand { get; }
        public ICommand RunCommand { get; }
    }
}
