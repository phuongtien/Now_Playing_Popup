using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using ThreadingTimer = System.Threading.Timer;


namespace NowPlayingPopup.Services
{
    /// <summary>
    /// Monitors system volume changes
    /// </summary>
    public class VolumeMonitor : IDisposable
    {
        private IAudioEndpointVolume? _audioEndpointVolume;
        private System.Threading.Timer? _volumeTimer;
        private int _lastVolumePercent = -1;
        private bool _isDisposed;

        public event EventHandler<VolumeChangedEventArgs>? VolumeChanged;

        public bool Initialize(int pollIntervalMs = 2000)
        {
            try
            {
                if (!InitializeAudioEndpoint()) return false;

                _volumeTimer = new System.Threading.Timer(OnVolumeTimerElapsed, null, 0, pollIntervalMs);
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"VolumeMonitor initialization failed: {ex}");
                return false;
            }
        }

        private bool InitializeAudioEndpoint()
        {
            try
            {
                var devEnum = new MMDeviceEnumeratorComObject() as IMMDeviceEnumerator;
                if (devEnum == null) return false;

                int hr = devEnum.GetDefaultAudioEndpoint(EDataFlow.eRender, ERole.eMultimedia, out IMMDevice device);
                if (hr != 0 || device == null) return false;

                var iid = typeof(IAudioEndpointVolume).GUID;
                const int CLSCTX_ALL = 23;
                device.Activate(ref iid, CLSCTX_ALL, IntPtr.Zero, out object volumeObj);

                _audioEndpointVolume = volumeObj as IAudioEndpointVolume;
                return _audioEndpointVolume != null;
            }
            catch
            {
                return false;
            }
        }

        private void OnVolumeTimerElapsed(object? state)
        {
            if (_isDisposed || _audioEndpointVolume == null) return;

            try
            {
                int hr = _audioEndpointVolume.GetMasterVolumeLevelScalar(out float level);
                if (hr != 0) return;

                var volumePercent = (int)Math.Round(level * 100);
                if (volumePercent != _lastVolumePercent)
                {
                    _lastVolumePercent = volumePercent;
                    VolumeChanged?.Invoke(this, new VolumeChangedEventArgs(volumePercent));
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Volume poll error: {ex}");
            }
        }

        public int GetCurrentVolumePercent()
        {
            try
            {
                if (_audioEndpointVolume == null) return -1;
                int hr = _audioEndpointVolume.GetMasterVolumeLevelScalar(out float level);
                return hr == 0 ? (int)Math.Round(level * 100) : -1;
            }
            catch
            {
                return -1;
            }
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;

            _volumeTimer?.Dispose();
            _audioEndpointVolume = null;
        }

        #region COM Interfaces

        [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
        private class MMDeviceEnumeratorComObject { }

        private enum EDataFlow { eRender = 0, eCapture = 1, eAll = 2 }
        private enum ERole { eConsole = 0, eMultimedia = 1, eCommunications = 2 }

        [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMDeviceEnumerator
        {
            int NotImpl1();
            int GetDefaultAudioEndpoint(EDataFlow dataFlow, ERole role, out IMMDevice ppDevice);
        }

        [Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMDevice
        {
            int Activate([In] ref Guid iid, int dwClsCtx, IntPtr pActivationParams,
                        [MarshalAs(UnmanagedType.IUnknown)] out object ppInterface);
        }

        [Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IAudioEndpointVolume
        {
            int RegisterControlChangeNotify(IntPtr pNotify);
            int UnregisterControlChangeNotify(IntPtr pNotify);
            int GetChannelCount(out uint pnChannelCount);
            int SetMasterVolumeLevel(float fLevelDB, Guid pguidEventContext);
            int SetMasterVolumeLevelScalar(float fLevel, Guid pguidEventContext);
            int GetMasterVolumeLevel(out float pfLevelDB);
            int GetMasterVolumeLevelScalar(out float pfLevel);
        }

        #endregion
    }

    public class VolumeChangedEventArgs : EventArgs
    {
        public int VolumePercent { get; }

        public VolumeChangedEventArgs(int volumePercent)
        {
            VolumePercent = volumePercent;
        }
    }
}