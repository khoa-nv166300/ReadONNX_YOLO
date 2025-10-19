using System;

namespace Wpf_MVVM_Read_Onnx
{
    public enum ESequence
    {
        Pending,
        Error,
        Complete,
        Accepted,
        Denied,
        Disabled,
    }
    public static class CDefines
    {
        public static event EventHandler<ESequence> SequenceChanged;
        public static event EventHandler CloseChanged;
        public static void OnSequenceChanged(ESequence eSequence)
        {
            SequenceChanged?.Invoke(null, eSequence);
        }
        public static void OnCloseChanged()
        {
            CloseChanged?.Invoke(null, new EventArgs());
        }
    }
}
