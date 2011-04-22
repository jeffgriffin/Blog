using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DirectShowLib;
using net.visibleblue.util.IEnumerable;
using Microsoft.Practices.Prism.ViewModel;
using net.visibleblue.util;
using System.Runtime.InteropServices;
using System.Collections.ObjectModel;
using System.Reflection;
using Microsoft.Win32;
using Microsoft.Practices.Prism.Commands;
using System.Windows;
using System.Threading.Tasks;
using System.Threading;
using WPFMediaKit.Threading;
using System.Windows.Threading;
using System.IO;
using System.Windows.Interop;
using System.Runtime.InteropServices.ComTypes;
using System.ComponentModel;

namespace TranscodeToMP4.ViewModel
{
    public class PrimaryViewModel : WorkDispatcherObject, INotifyPropertyChanged
    {
        private static readonly string PASSTHROUGH = "Passthrough";
        private static readonly string PASSTHROUGH_FORMAT = "Passthrough ({0})";

        public DelegateCommand ChooseSourceFileCommand { get; private set; }
        public DelegateCommand TranscodeCommand { get; private set; }
        public DelegateCommand AudioEncoderPropertiesCommand { get; private set; }
        public DelegateCommand VideoEncoderPropertiesCommand { get; private set; }
        public DelegateCommand CancelCommand { get; private set; }

        const int E_ABORT = unchecked((int)0x80004004);
        const int VFW_E_WRONG_STATE = unchecked((int)0x80040227);
        const int S_OK = 0;

        public PrimaryViewModel()
        {
            EnsureThread(ApartmentState.MTA);
            Dispatcher.BeginInvoke(
                (Action)delegate
                {
                    _audioCompressors = DsDevice.GetDevicesOfCat(FilterCategory.AudioCompressorCategory);
                    AudioCompressorNames = new ObservableCollection<string>(
                        Enumerable.Repeat(PASSTHROUGH, 1).Concat(AudioCompressors.Select<DsDevice, string>(dev => dev.Name)));
                    _videoCompressors = DsDevice.GetDevicesOfCat(FilterCategory.VideoCompressorCategory);
                    VideoCompressorNames = new ObservableCollection<string>(
                        Enumerable.Repeat(PASSTHROUGH, 1).Concat(VideoCompressors.Select<DsDevice, string>(dev => dev.Name)));
                    RaisePropertyChanged("VideoCompressors");
                    RaisePropertyChanged("VideoCompressorNames");
                    RaisePropertyChanged("AudioCompressors");
                    RaisePropertyChanged("AudioCompressorNames");
                });

            ChooseSourceFileCommand = new DelegateCommand(ChooseSource);
            TranscodeCommand = new DelegateCommand(Transcode);
            AudioEncoderPropertiesCommand = new DelegateCommand(
                delegate
                {
                    if (_audioEncoder == null)
                        _audioEncoder = CreateAudioCompressorFilter();
                    ShowFilterProperties(_audioEncoder);
                });
            VideoEncoderPropertiesCommand = new DelegateCommand(
                delegate
                {
                    if (_videoEncoder == null)
                        _videoEncoder = CreateVideoCompressorFilter();
                    ShowFilterProperties(_videoEncoder);
                });
            CancelCommand = new DelegateCommand(
                delegate
                {
                    _cancelRequested = true;
                },
                delegate
                {
                    return _isTranscoding;
                });
        }

        IEnumerable<DsDevice> _audioCompressors = null;
        public IEnumerable<DsDevice> AudioCompressors { get { return _audioCompressors; } }
        public ObservableCollection<string> AudioCompressorNames { get; private set; }
        IEnumerable<DsDevice> _videoCompressors = null;
        public IEnumerable<DsDevice> VideoCompressors { get { return _videoCompressors; } }
        public ObservableCollection<string> VideoCompressorNames { get; private set; }

        GraphPlayer _graphPlayer;
        public GraphPlayer GraphPlayer
        {
            set
            {
                _graphPlayer = value;
                if (_graphPlayer != null)
                    _graphPlayer.SetSharedDispatcher(this.Dispatcher);
            }
        }

        //IBaseFilter _splitter;
        //IFileSourceFilter _splitterFS;
        IBaseFilter _fileSource;
        //IBaseFilter _videoDecoder;
        //IPin _previewPin;
        IGraphBuilder _graphBuilder;
        IMediaControl _mediaCtrl;
        object _syncGraph = new object();
        //IBaseFilter _nullRenderer;
        IBaseFilter _videoRenderer;
        private void DestroyPreviewGraph()
        {
            if (!CheckAccess())
            {
                Dispatcher.BeginInvoke((Action)DestroyPreviewGraph);
                return;
            }
            if (_graphBuilder == null)
                return;

            lock (_syncGraph)
            {
                _mediaCtrl.StopWhenReady();
                //Marshal.ReleaseComObject(_previewPin);
                //Marshal.ReleaseComObject(_nullRenderer);
                Marshal.ReleaseComObject(_videoRenderer);
                //Marshal.ReleaseComObject(_videoDecoder);
                //Marshal.ReleaseComObject(_splitter);
                //Marshal.ReleaseComObject(_splitterFS);
                Marshal.ReleaseComObject(_fileSource);
                Marshal.ReleaseComObject(_graphBuilder);
                Marshal.ReleaseComObject(_mediaCtrl);
                _videoRenderer = null;
                _graphBuilder = null;
                //_previewPin = null;
            }
        }

        IBaseFilter _audioEncoder;
        IBaseFilter _videoEncoder;
        private void DestroyAudioEncoder()
        {
            if (!CheckAccess())
            {
                Dispatcher.BeginInvoke((Action)DestroyAudioEncoder);
                return;
            }
            lock (_syncGraph)
            {
                if (_audioEncoder != null)
                {
                    Marshal.ReleaseComObject(_audioEncoder);
                    _audioEncoder = null;
                }
            }
        }

        private void DestroyVideoEncoder()
        {
            if (!CheckAccess())
            {
                Dispatcher.BeginInvoke((Action)DestroyVideoEncoder);
                return;
            }
            lock (_syncGraph)
            {
                if (_videoEncoder != null)
                {
                    Marshal.ReleaseComObject(_videoEncoder);
                    _videoEncoder = null;
                }
            }
        }

        private void ConstructSharedGraph()
        {
            if (!CheckAccess())
            {
                Dispatcher.BeginInvoke((Action)ConstructSharedGraph);
                return;
            }
            _graphBuilder = (IGraphBuilder)new FilterGraphNoThread();

            //_splitter = CreateFilter(FilterCategory.LegacyAmFilterCategory, "Haali Media Splitter");
            //if (_splitter == null)
            //    Log.Error("Haali Media Splitter not found.");
            //int err = _graphBuilder.AddFilter(_splitter, null);
            //_splitterFS = (IFileSourceFilter)_splitter;
            //_splitterFS.Load(_inputFilePath, null);

            int hr = _graphBuilder.AddSourceFilter(_inputFilePath, _inputFilePath, out _fileSource);
            DsError.ThrowExceptionForHR(hr);

            _mediaCtrl = _graphBuilder as IMediaControl;
        }

        private void ConstructPreviewGraph()
        {
            if (!CheckAccess())
            {
                Dispatcher.BeginInvoke((Action)ConstructPreviewGraph);
                return;
            }
            lock (_syncGraph)
            {
                ConstructSharedGraph();
                IBaseFilter nullVideo;
                IBaseFilter nullAudio;
                GetConnectNullRenderers(out nullVideo, out nullAudio);
                IPin compressedVideoPin = GetConnectedOut(nullVideo, 0);
                IPin compressedAudioPin = GetConnectedOut(nullAudio, 0);

                if (compressedVideoPin == null)
                    throw new ApplicationException("Unable to find video stream.");
                string videoSubType = GetPinMediaSubType(compressedVideoPin);
                compressedVideoPin.Disconnect();

                string audioSubType = null;
                if (compressedAudioPin != null)
                {
                    audioSubType = GetPinMediaSubType(compressedAudioPin);
                    compressedAudioPin.Disconnect();
                }
                DisconnectAll(_fileSource);

                _graphBuilder.RemoveFilter(nullVideo);
                _graphBuilder.RemoveFilter(nullAudio);
                Marshal.ReleaseComObject(nullVideo);
                Marshal.ReleaseComObject(nullAudio);

                Application.Current.Dispatcher.BeginInvoke(
                    delegate
                    {
                        //set the passthrough pin media sub type hint
                        if (videoSubType == null)
                            VideoCompressorNames[0] = PASSTHROUGH;
                        else
                            VideoCompressorNames[0] = string.Format(PASSTHROUGH_FORMAT, videoSubType);
                        RaisePropertyChanged("CurrentVideoCompressorName");


                        if (audioSubType == null)
                            AudioCompressorNames[0] = PASSTHROUGH;
                        else
                            AudioCompressorNames[0] = string.Format(PASSTHROUGH_FORMAT, audioSubType);
                        RaisePropertyChanged("CurrentAudioCompressorName");
                    });

                //HACK: for some reason, the standard Video Renderer sets up a better graph than VMR9 does
                IPin decodePin = RenderForDumbPin();
                _videoRenderer = _graphPlayer.CreateRenderer(_graphBuilder);
                HookPinDirect(decodePin, _videoRenderer, 0);
                Marshal.ReleaseComObject(decodePin);

                //terminate the audio pin, so we can read its subtype
                //_nullRenderer = CreateFilter(FilterCategory.LegacyAmFilterCategory, "Null Renderer");
                //if (_nullRenderer == null)
                //    Log.Error("Null Renderer");
                //int err = _graphBuilder.AddFilter(_nullRenderer, null);
                //HookPin(_splitter, 1, _nullRenderer, 0);

                _mediaCtrl.Run();

                
            }
        }

        private void GetConnectNullRenderers(out IBaseFilter nullVideoRenderer, out IBaseFilter nullAudioRenderer)
        {
            nullVideoRenderer = null;
            nullAudioRenderer = null;
            List<IBaseFilter> nullRenderList = new List<IBaseFilter>();
            Random rand = new Random();

            while (true)
            {
                if (nullVideoRenderer != null && nullAudioRenderer != null)
                    return;
                IBaseFilter nullRenderer = (IBaseFilter)new NullRenderer();
                int hr = _graphBuilder.AddFilter(nullRenderer, Convert.ToString(rand.Next()));
                DsError.ThrowExceptionForHR(hr);
                DisconnectAll(_fileSource);
                RenderAll(_fileSource);
                IPin connected = GetConnectedOut(nullRenderer, 0);
                if (connected == null)
                    break;

                nullRenderList.Add(nullRenderer);
                bool anyUnconnected = false;
                IBaseFilter anr = null;
                IBaseFilter vnr = null;
                nullRenderList.Do(
                    delegate(IBaseFilter nr)
                    {
                        IPin co = null;
                        AMMediaType mt = new AMMediaType();
                        try
                        {
                            co = GetConnectedOut(nr, 0);
                            co.ConnectionMediaType(mt);
                            if (mt.majorType == MediaType.Video)
                                vnr = nr;
                            else if (mt.majorType == MediaType.Audio)
                                anr = nr;

                            if (co == null)
                                anyUnconnected = true;
                        }
                        finally
                        {
                            Marshal.ReleaseComObject(co);
                            DsUtils.FreeAMMediaType(mt);
                        }
                    });
                nullAudioRenderer = anr;
                nullVideoRenderer = vnr;
            }
            nullRenderList.Remove(nullAudioRenderer);
            nullRenderList.Remove(nullVideoRenderer);
            while (nullRenderList.Count > 0)
            {
                var nr = nullRenderList[0];
                nullRenderList.RemoveAt(0);
                Marshal.ReleaseComObject(nr);
            }
        }

        private bool TryConnectAny(IBaseFilter sourcefilter, IBaseFilter destinationFilter, int destinationPinIndex, out IPin connectedDirectlyTo)
        {
            connectedDirectlyTo = null;
            IEnumPins pinEnum;
            int hr = sourcefilter.EnumPins(out pinEnum);
            DsError.ThrowExceptionForHR(hr);
            IPin[] pins = { null };
            while (pinEnum.Next(pins.Length, pins, IntPtr.Zero) == 0)
            {
                int err = TryConnect(pins[0], destinationFilter, destinationPinIndex, out connectedDirectlyTo);
                if (err == 0)
                    return true;
                Marshal.ReleaseComObject(pins[0]);
            }
            return false;
        }

        private void RenderAll(IBaseFilter filter)
        {
            IEnumPins pinEnum;
            int hr = filter.EnumPins(out pinEnum);
            DsError.ThrowExceptionForHR(hr);
            IPin[] pins = { null };
            while (pinEnum.Next(pins.Length, pins, IntPtr.Zero) == 0)
            {
                _graphBuilder.Render(pins[0]);
                Marshal.ReleaseComObject(pins[0]);
            }
        }

        private void DisconnectAll(IBaseFilter filter)
        {
            IEnumPins pinEnum;
            int hr = filter.EnumPins(out pinEnum);
            DsError.ThrowExceptionForHR(hr);
            IPin[] pins = { null };
            while (pinEnum.Next(pins.Length, pins, IntPtr.Zero) == 0)
            {
                pins[0].Disconnect();
                Marshal.ReleaseComObject(pins[0]);
            }
        }

        private int TryConnect(IPin pin, IBaseFilter inFilter, int inFilterIndex, out IPin connectedDirectlyTo)
        {
            IPin rendererInPin = null;
            try
            {
                rendererInPin = DsFindPin.ByDirection(inFilter, PinDirection.Input, inFilterIndex);
                int err = _graphBuilder.Connect(pin, rendererInPin);
                if (err != 0)
                {
                    connectedDirectlyTo = null;
                    return err;
                }
                err = rendererInPin.ConnectedTo(out connectedDirectlyTo);
                if (err != 0)
                    return err;
                return 0;
            }
            finally
            {
                if (rendererInPin != null)
                    Marshal.ReleaseComObject(rendererInPin);
            }
        }

        private IPin GetConnectedOut(IBaseFilter filter, int inPinIndex)
        {
            IPin inPin = null;
            try
            {
                inPin = DsFindPin.ByDirection(filter, PinDirection.Input, inPinIndex);
                IPin outPin;
                inPin.ConnectedTo(out outPin);
                return outPin;
            }
            finally
            {
                if (inPin != null)
                    Marshal.ReleaseComObject(inPin);
            }
        }

        private string GetPinMediaSubType(IPin pin)
        {
            AMMediaType mt = new AMMediaType();
            pin.ConnectionMediaType(mt);
            var memberinfo = typeof(MediaSubType).GetMembers(BindingFlags.Public | BindingFlags.Static).Concat(
                typeof(MissingMediaSubTypes).GetMembers(BindingFlags.Public | BindingFlags.Static));
            var match = memberinfo.FirstOrDefault(mi => mi is FieldInfo && ((FieldInfo)mi).GetValue(null).Equals(mt.subType));
            return match == null ? null : match.Name;
        }

        bool _cancelRequested = false;
        bool _isTranscoding = false;
        private void Transcode()
        {
            //if (!CheckAccess())
            //{
            //    Dispatcher.BeginInvoke((Action)Transcode);
            //    return;
            //}
            //lock (_syncGraph)
            //{
            //    _isTranscoding = true;
            //    CancelCommand.RaiseCanExecuteChanged();
            //    DestroyPreviewGraph();
            //    ConstructSharedGraph();

            //    //set sync source to null to allow the graph to progress as quickly as possible
            //    IMediaFilter mediaFilter = (IMediaFilter)_graphBuilder;
            //    mediaFilter.SetSyncSource(null);

            //    IBaseFilter muxer;
            //    muxer = CreateFilter(FilterCategory.LegacyAmFilterCategory, "Haali Matroska Muxer");
            //    if (muxer == null)
            //        Log.Error("Haali Matroska Muxer not found.");
            //    IPropertyBag muxerPB = (IPropertyBag)muxer;
            //    object pVar = 1;
            //    int err = muxerPB.Write("FileType", ref pVar);

            //    IFileSinkFilter muxerFS = (IFileSinkFilter)muxer;
            //    string tempPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            //    err = muxerFS.SetFileName(tempPath, null);

            //    IBaseFilter pinTeeVideo = CreateFilter(FilterCategory.LegacyAmFilterCategory, "Infinite Pin Tee Filter");
            //    if (pinTeeVideo == null)
            //        Log.Error("Infinite Pin Tee Filter not found.");

            //    if (_currentVideoCompressor != null)
            //    {
            //        //we want to use intelligent connect to choose a decoder, but the pin tee isn't going to resolve it.
            //        IPin decoderPin = DecodeForDumbPin(_splitter, 0);

            //        _graphBuilder.AddFilter(pinTeeVideo, "pinTeeVideo");
            //        HookPinDirect(decoderPin, pinTeeVideo, 0);
            //        Marshal.ReleaseComObject(decoderPin);

            //        if (_videoEncoder == null)
            //            _videoEncoder = CreateVideoCompressorFilter();
            //        _graphBuilder.AddFilter(_videoEncoder, "videoEncoder");
            //        HookPin(pinTeeVideo, 0, _videoEncoder, 0);
            //        err = _graphBuilder.AddFilter(muxer, "muxer");
            //        HookPin(_videoEncoder, 0, muxer, 0);

            //        _videoRenderer = _graphPlayer.CreateRenderer(_graphBuilder);
            //        HookPin(pinTeeVideo, 1, _videoRenderer, 0);
            //        //_previewPin = DsFindPin.ByDirection(pinTeeVideo, PinDirection.Output, 1);
            //    }
            //    else
            //    {
            //        _graphBuilder.AddFilter(pinTeeVideo, "pinTeeVideo");
            //        HookPin(_splitter, 0, pinTeeVideo, 0);
            //        err = _graphBuilder.AddFilter(muxer, "muxer");
            //        HookPin(pinTeeVideo, 0, muxer, 0);

            //        IPin decoderPin = DecodeForDumbPin(pinTeeVideo, 1);
            //        _videoRenderer = _graphPlayer.CreateRenderer(_graphBuilder);
            //        HookPin(decoderPin, _videoRenderer, 0);
            //        Marshal.ReleaseComObject(decoderPin);
            //        //_previewPin = DecodeForDumbPin(pinTeeVideo, 0);
            //    }

            //    if (_currentAudioCompressor != null)
            //    {
            //        if (_audioEncoder == null)
            //            _audioEncoder = CreateAudioCompressorFilter();
            //        _graphBuilder.AddFilter(_audioEncoder, "audioEncoder");
            //        HookPin(_splitter, 1, _audioEncoder, 0);
            //        HookPin(_audioEncoder, 0, muxer, 1);
            //    }
            //    else
            //        HookPin(_splitter, 1, muxer, 1);

            //    IMediaPosition mediaPos = (IMediaPosition)_graphBuilder;
            //    IMediaEvent medEvent = (IMediaEvent)_graphBuilder;
            //    EventCode eventCode;
            //    err = _mediaCtrl.Run();
            //    ProgressIsIndeterminate = false;
            //    RaisePropertyChanged("ProgressIsIndeterminate");
            //    var timer = new DispatcherTimer(DispatcherPriority.Background);
            //    timer.Interval = TimeSpan.FromMilliseconds(500);
            //    timer.Tick +=
            //        delegate
            //        {
            //            double current = 0;
            //            double duration = 0;
            //            if (!_cancelRequested)
            //            {
            //                err = mediaPos.get_CurrentPosition(out current);
            //                err = mediaPos.get_Duration(out duration);
            //            }
            //            if (current < duration && !_cancelRequested)
            //            {
            //                ProgressValue = (current / duration) * 10000;
            //                RaisePropertyChanged("ProgressValue");
            //            }
            //            else
            //            {
            //                ProgressValue = 0;
            //                RaisePropertyChanged("ProgressValue");
            //                ProgressIsIndeterminate = true;
            //                RaisePropertyChanged("ProgressIsIndeterminate");
            //                int waitRes = S_OK;
            //                if (!_cancelRequested)
            //                    waitRes = medEvent.WaitForCompletion(1, out eventCode);
            //                if (waitRes == S_OK || waitRes == VFW_E_WRONG_STATE)
            //                {
            //                    timer.Stop();
            //                    err = _mediaCtrl.StopWhenReady();
            //                    ProgressIsIndeterminate = false;
            //                    RaisePropertyChanged("ProgressIsIndeterminate");

            //                    DestroyAudioEncoder();
            //                    DestroyVideoEncoder();
            //                    Marshal.ReleaseComObject(pinTeeVideo);
            //                    Marshal.ReleaseComObject(muxer);
            //                    Marshal.ReleaseComObject(muxerPB);
            //                    Marshal.ReleaseComObject(muxerFS);
            //                    DestroyPreviewGraph();
            //                    Marshal.ReleaseComObject(mediaFilter);
            //                    Marshal.ReleaseComObject(mediaPos);
            //                    Marshal.ReleaseComObject(medEvent);
            //                    _cancelRequested = false;
            //                    _isTranscoding = false;
            //                    CancelCommand.RaiseCanExecuteChanged();
            //                }
            //            }
            //        };
            //    timer.Start();
            //}
        }

        private IPin RenderForDumbPin()
        {
            IPin inPin = null;
            IBaseFilter tempRenderer = null;
            try
            {
                tempRenderer = (IBaseFilter)new VideoRenderer();
                _graphBuilder.AddFilter(tempRenderer, null);
                RenderAll(_fileSource);
                //HookPin(sourcePin, tempRenderer, 0);
                inPin = DsFindPin.ByDirection(tempRenderer, PinDirection.Input, 0);
                IPin outPin;
                int hr = inPin.ConnectedTo(out outPin);
                DsError.ThrowExceptionForHR(hr);
                outPin.Disconnect();
                return outPin;
            }
            finally
            {
                if (tempRenderer != null)
                {
                    Marshal.ReleaseComObject(tempRenderer);
                    _graphBuilder.RemoveFilter(tempRenderer);
                }
                if (inPin != null)
                    Marshal.ReleaseComObject(inPin);
            }
        }

        private IPin DecodeForDumbPin(IBaseFilter source, int outPinIndex)
        {
            IPin inPin = null;
            IBaseFilter tempRenderer = null;
            try
            {
                tempRenderer = (IBaseFilter)new VideoRenderer();
                _graphBuilder.AddFilter(tempRenderer, null);

                HookPin(source, outPinIndex, tempRenderer, 0);
                inPin = DsFindPin.ByDirection(tempRenderer, PinDirection.Input, 0);
                IPin outPin;
                int hr = inPin.ConnectedTo(out outPin);
                DsError.ThrowExceptionForHR(hr);
                outPin.Disconnect();
                return outPin;
            }
            finally
            {
                if (tempRenderer != null)
                {
                    Marshal.ReleaseComObject(tempRenderer);
                    _graphBuilder.RemoveFilter(tempRenderer);
                }
                if (inPin != null)
                    Marshal.ReleaseComObject(inPin);
            }
        }

        public double ProgressMax
        {
            get { return 10000; }
        }

        public double ProgressMin
        {
            get { return 0; }
        }

        public double ProgressValue
        {
            get;
            private set;
        }

        public bool ProgressIsIndeterminate
        {
            get;
            private set;
        }

        public event Action<IntPtr, int> NewStage1Sample;


        private string _inputFilePath;
        public string InputFilePath
        {
            get { return _inputFilePath; }
            set
            {
                _inputFilePath = value;
                RaisePropertyChanged("InputFilePath");

                DestroyPreviewGraph();
                ConstructPreviewGraph();
            }
        }

        private void ChooseSource()
        {
            OpenFileDialog mtsDialog = new OpenFileDialog();
            mtsDialog.Title = "Open Media File";
            mtsDialog.Filter = "Media Files(*.mp4;*.m4v;*.mkv;*.mts;*mt2s;*.tod;*.ogg;*.ogm;*.avi;*.mov)|*.mp4;*.m4v;*.mkv;*.mts;*mt2s;*.tod;*.ogg;*.ogm;*.avi;*.mov|All files (*.*)|*.*";
            if (!mtsDialog.ShowDialog().GetValueOrDefault(false) || string.IsNullOrEmpty(mtsDialog.FileName))
                return;
            InputFilePath = mtsDialog.FileName;
        }

        DsDevice _currentAudioCompressor = null;
        public string CurrentAudioCompressorName
        {
            get
            {
                if (AudioCompressorNames == null)
                    return null;
                return _currentAudioCompressor == null ? AudioCompressorNames[0] : _currentAudioCompressor.Name;
            }
            set
            {
                DestroyAudioEncoder();
                var valueCompressor = AudioCompressors.FirstOrDefault(dev => dev.Name.Equals(value));
                if (valueCompressor == _currentAudioCompressor)
                    return;
                _currentAudioCompressor = valueCompressor;
                RaisePropertyChanged("CurrentAudioCompressorName");
            }
        }

        private IBaseFilter CreateAudioCompressorFilter()
        {
            //MediaSubType
            //FormatType
            Guid iid = typeof(IBaseFilter).GUID;
            object source = null;
            if (_currentAudioCompressor == null)
                return null;
            _currentAudioCompressor.Mon.BindToObject(null, null, ref iid, out source);
            return (IBaseFilter)source;
        }

        DsDevice _currentVideoCompressor = null;
        public string CurrentVideoCompressorName
        {
            get
            {
                if (VideoCompressorNames == null)
                    return null;
                return _currentVideoCompressor == null ? VideoCompressorNames[0] : _currentVideoCompressor.Name;
            }
            set
            {
                DestroyVideoEncoder();
                var valueCompressor = VideoCompressors.FirstOrDefault(dev => dev.Name.Equals(value));
                if (valueCompressor == _currentVideoCompressor)
                    return;
                _currentVideoCompressor = valueCompressor;
                RaisePropertyChanged("CurrentVideoCompressorName");
            }
        }

        private IBaseFilter CreateVideoCompressorFilter()
        {
            //MediaSubType
            //FormatType
            Guid iid = typeof(IBaseFilter).GUID;
            object filter = null;
            if (_currentVideoCompressor == null)
                return null;
            _currentVideoCompressor.Mon.BindToObject(null, null, ref iid, out filter);
            return (IBaseFilter)filter;
        }

        public static IBaseFilter CreateFilter(Guid category, string friendlyname)
        {
            object filter = null;
            Guid iid = typeof(IBaseFilter).GUID;
            DsDevice deviceMatch = DsDevice.GetDevicesOfCat(category).FirstOrDefault(dev => dev.Name.Equals(friendlyname));
            if (deviceMatch == null)
                return null;
            deviceMatch.Mon.BindToObject(null, null, ref iid, out filter);
            return (IBaseFilter)filter;
        }

        private void HookPin(IBaseFilter outFilter, int outIndex, IBaseFilter inFilter, int inIndex)
        {
            HookPin(_graphBuilder, outFilter, outIndex, inFilter, inIndex);
        }

        public static void HookPin(IGraphBuilder graph, IBaseFilter outFilter, int outIndex, IBaseFilter inFilter, int inIndex)
        {
            IPin outPin = DsFindPin.ByDirection(outFilter, PinDirection.Output, outIndex);
            if (outPin == null)
                return;
            else
                outPin.Disconnect();
            IPin inPin = DsFindPin.ByDirection(inFilter, PinDirection.Input, inIndex);
            if (inPin == null)
                return;
            inPin.Disconnect();
            int err = graph.Connect(outPin, inPin);
            DsError.ThrowExceptionForHR(err);
            Marshal.ReleaseComObject(outPin);
            Marshal.ReleaseComObject(inPin);
        }

        private void HookPinDirect(IPin outPin, IBaseFilter inFilter, int inIndex)
        {
            outPin.Disconnect();
            IPin inPin = DsFindPin.ByDirection(inFilter, PinDirection.Input, inIndex);
            if (inPin == null)
                return;
            inPin.Disconnect();
            int err = _graphBuilder.ConnectDirect(outPin, inPin, null);
            DsError.ThrowExceptionForHR(err);
            Marshal.ReleaseComObject(inPin);
        }

        private void HookPin(IPin outPin, IBaseFilter inFilter, int inIndex)
        {
            HookPin(_graphBuilder, outPin, inFilter, inIndex);
        }

        public static void HookPin(IGraphBuilder graph, IPin outPin, IBaseFilter inFilter, int inIndex)
        {
            outPin.Disconnect();
            IPin inPin = DsFindPin.ByDirection(inFilter, PinDirection.Input, inIndex);
            if (inPin == null)
                return;
            inPin.Disconnect();
            int err = graph.Connect(outPin, inPin);
            DsError.ThrowExceptionForHR(err);
            Marshal.ReleaseComObject(inPin);
        }

        //private string GetSplitterPinMediaSubType(int pinIndex)
        //{
        //    IPin videoOut = DsFindPin.ByDirection(_splitter, PinDirection.Output, pinIndex);
        //    if (videoOut == null)
        //        return "No Media";
        //    AMMediaType mt = new AMMediaType();
        //    videoOut.ConnectionMediaType(mt);
        //    var memberinfo = typeof(MediaSubType).GetMembers(BindingFlags.Public | BindingFlags.Static).Concat(
        //        typeof(MissingMediaSubTypes).GetMembers(BindingFlags.Public | BindingFlags.Static));
        //    var match = memberinfo.FirstOrDefault(mi => mi is FieldInfo && ((FieldInfo)mi).GetValue(null).Equals(mt.subType));
        //    Marshal.ReleaseComObject(videoOut);
        //    return match == null ? null : match.Name;
        //}

        [DllImport("oleaut32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
        public static extern int OleCreatePropertyFrame(
            IntPtr hwndOwner,
            int x,
            int y,
            [MarshalAs(UnmanagedType.LPWStr)] string lpszCaption,
            int cObjects,
            [MarshalAs(UnmanagedType.Interface, ArraySubType = UnmanagedType.IUnknown)] 
			ref object ppUnk,
            int cPages,
            IntPtr lpPageClsID,
            int lcid,
            int dwReserved,
            IntPtr lpvReserved);

        private static void ShowFilterProperties(IBaseFilter filter)
        {
            int err;
            ISpecifyPropertyPages spp = filter as ISpecifyPropertyPages;
            if (spp != null)
            {
                FilterInfo filterInfo;
                err = filter.QueryFilterInfo(out filterInfo);
                DsError.ThrowExceptionForHR(err);

                DsCAUUID caGuid;
                err = spp.GetPages(out caGuid);
                DsError.ThrowExceptionForHR(err);

                object filterObject = (object)filter;
                err = OleCreatePropertyFrame(new WindowInteropHelper(Application.Current.MainWindow).Handle
                    , 0, 0, filterInfo.achName, 1, ref filterObject, caGuid.cElems, caGuid.pElems, 0, 0, IntPtr.Zero);
                DsError.ThrowExceptionForHR(err);

                Marshal.FreeCoTaskMem(caGuid.pElems);
                Marshal.ReleaseComObject(spp);
            }
            else
            {
                IAMVfwCompressDialogs compDialogs = filter as IAMVfwCompressDialogs;
                if (compDialogs != null)
                {
                    err = compDialogs.ShowDialog(VfwCompressDialogs.Config,
                        new WindowInteropHelper(Application.Current.MainWindow).Handle);
                    DsError.ThrowExceptionForHR(err);
                }
            }
        }
        public event PropertyChangedEventHandler PropertyChanged;
        private void RaisePropertyChanged(string property)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(property));
        }
    }
}
