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

namespace TranscodeToMP4.ViewModel
{
    public class PrimaryViewModel : NotificationObject
    {
        //public class SampleGrabberCB : ISampleGrabberCB
        //{
        //    public SampleGrabberCB(ISampleGrabber sampleGrabber)
        //    {
        //        AMMediaType media;

        //        // Set the media type to Video/RBG24
        //        media = new AMMediaType();
        //        media.majorType = MediaType.Video;
        //        media.subType = MediaSubType.RGB24;
        //        media.formatType = FormatType.VideoInfo;
        //        int hr = sampleGrabber.SetMediaType(media);
        //        DsError.ThrowExceptionForHR(hr);

        //        DsUtils.FreeAMMediaType(media);
        //        media = null;

        //        // Configure the samplegrabber
        //        sampleGrabber.SetCallback(this, 1);
        //    }

        //    public event Action<IntPtr,int> OnNewSample;

        //    int ISampleGrabberCB.BufferCB(double SampleTime, IntPtr pBuffer, int BufferLen)
        //    {
        //        if (OnNewSample != null)
        //        {
        //            OnNewSample(pBuffer, BufferLen);
        //        }
        //        return 0;
        //    }

        //    int ISampleGrabberCB.SampleCB(double SampleTime, IMediaSample pSample)
        //    {
        //        if (OnNewSample != null)
        //        {
        //            IntPtr pBuff;
        //            int cSize;
        //            pSample.GetPointer(out pBuff);
        //            cSize = pSample.GetSize();
        //            OnNewSample(pBuff, cSize);
        //        }

        //        Marshal.ReleaseComObject(pSample);
        //        return 0;
        //    }
        //}

        private static readonly string PASSTHROUGH = "Passthrough";
        private static readonly string PASSTHROUGH_FORMAT = "Passthrough ({0})";

        private Lazy<IEnumerable<DsDevice>> _audioCompressors;
        private Lazy<ObservableCollection<string>> _audioCompressorNames;
        private Lazy<IEnumerable<DsDevice>> _videoCompressors;
        private Lazy<ObservableCollection<string>> _videoCompressorNames;
        public IEnumerable<DsDevice> AudioCompressors { get { return _audioCompressors.Value; } }
        public ObservableCollection<string> AudioCompressorNames { get { return _audioCompressorNames.Value; } }
        public IEnumerable<DsDevice> VideoCompressors { get { return _videoCompressors.Value; } }
        public ObservableCollection<string> VideoCompressorNames { get { return _videoCompressorNames.Value; } }

        public DelegateCommand ChooseSourceFileCommand { get; private set; }
        public DelegateCommand TranscodeCommand { get; private set; }

        const int E_ABORT = unchecked((int)0x80004004);
        const int VFW_E_WRONG_STATE = unchecked((int)0x80040227);
        const int S_OK = 0;

        public PrimaryViewModel()
        {
            _audioCompressors = new Lazy<IEnumerable<DsDevice>>(() => DsDevice.GetDevicesOfCat(FilterCategory.AudioCompressorCategory));
            _audioCompressorNames = new Lazy<ObservableCollection<string>>(() => new ObservableCollection<string>(
                Enumerable.Repeat(PASSTHROUGH, 1).Concat(AudioCompressors.Select<DsDevice, string>(dev => dev.Name))));
            _videoCompressors = new Lazy<IEnumerable<DsDevice>>(() => DsDevice.GetDevicesOfCat(FilterCategory.VideoCompressorCategory));
            _videoCompressorNames = new Lazy<ObservableCollection<string>>(() => new ObservableCollection<string>(
                Enumerable.Repeat(PASSTHROUGH, 1).Concat(VideoCompressors.Select<DsDevice, string>(dev => dev.Name))));
            ChooseSourceFileCommand = new DelegateCommand(ChooseSource);
            TranscodeCommand = new DelegateCommand(Transcode);





            //_muxer = CreateFilter(FilterCategory.LegacyAmFilterCategory, "Haali Matroska Muxer");
            //if (_splitter == null)
            //    Log.Error("Haali Matroska Muxer not found.");
            //IPropertyBag muxerPB = (IPropertyBag)_muxer;
            //object pVar = 1;
            //err = muxerPB.Write("FileType", ref pVar);
            //err = _graphBuilder.AddFilter(_muxer, "muxer");

            //Marshal.ReleaseComObject(muxerPB);
            //IFileSinkFilter muxerFS = (IFileSinkFilter)muxerBF;
            //err = muxerFS.SetFileName(_mp4FileName, null);

            //_videoDecoder = CreateFilter(FilterCategory.LegacyAmFilterCategory, "ffdshow Video Decoder");
            //if (_videoDecoder == null)
            //    Log.Error("ffdshow Video Decoder not found.");
            //if (!string.IsNullOrEmpty(_videoCompressorName))
            //{
            //    graphBuilder.AddFilter(videoDecoder, null);
            //    HookPin(splitter, 0, videoDecoder, 0);
            //    videoCompressor = CreateFilter(FilterCategory.VideoCompressorCategory, _videoCompressorName);
            //    int hr = graphBuilder.AddFilter(videoCompressor, "video filter");
            //    HookPin(videoDecoder, 0, videoCompressor, 0);
            //    HookPin(videoCompressor, 0, muxerBF, 0);
            //    DsError.ThrowExceptionForHR(hr);
            //}
            //else
            //    HookPin(splitter, 0, muxerBF, 0);
            //_graphBuilder.RemoveFilter

            //Add the Video compressor filter to the graph
            //_audioDecoder = CreateFilter(FilterCategory.LegacyAmFilterCategory, "ffdshow Audio Decoder");
            //if (_audioDecoder == null)
            //    Log.Error("ffdshow Audio Decoder not found.");
            //IBaseFilter audioCompressor = null;
            //if (!string.IsNullOrEmpty(_audioCompressorName))
            //{
            //    graphBuilder.AddFilter(audioDecoder, null);
            //    HookPin(splitter, 1, audioDecoder, 0);
            //    audioCompressor = CreateFilter(FilterCategory.AudioCompressorCategory, _audioCompressorName);
            //    int hr = graphBuilder.AddFilter(audioCompressor, "audio filter");
            //    HookPin(audioDecoder, 0, audioCompressor, 0);
            //    HookPin(audioCompressor, 0, muxerBF, 1);
            //    DsError.ThrowExceptionForHR(hr);
            //}
            //else
            //    HookPin(splitter, 1, muxerBF, 1);
            //ResetConnections();
        }

        WorkDispatcher _workDispatcher = new WorkDispatcher();

        IBaseFilter _splitter;
        IFileSourceFilter _splitterFS;
        IBaseFilter _pinTeeVideo1;
        IBaseFilter _pinTeeAudio1;
        IBaseFilter _videoDecoder;
        IBaseFilter _pinTeeVideo2;
        IPin _previewPin;
        IGraphBuilder _graphBuilder;
        //IBaseFilter _sampleGrabberBF;
        //ISampleGrabber _sampleGrabber;
        //SampleGrabberCB _stage1SGCB;
        IBaseFilter _audioDecoder;
        IMediaControl _mediaCtrl;
        object _syncGraph = new object();
        private void DestroyGraph()
        {
            if (_graphBuilder == null)
                return;
            Application.Current.Dispatcher.Invoke(
                        delegate
                        {
                            lock (_syncGraph)
                            {
                                _mediaCtrl.Stop();
                                UnHookPin(_pinTeeAudio1, 0, _audioDecoder, 0);
                                Marshal.ReleaseComObject(_audioDecoder);
                                UnHookPin(_splitter, 1, _pinTeeAudio1, 0);
                                Marshal.ReleaseComObject(_pinTeeAudio1);
                                //UnHookPin(_pinTeeVideo2, 0, _sampleGrabberBF, 0);
                                //Marshal.ReleaseComObject(_sampleGrabber);
                                //Marshal.ReleaseComObject(_sampleGrabberBF);
                                UnHookPin(_videoDecoder, 0, _pinTeeVideo2, 0);
                                Marshal.ReleaseComObject(_previewPin);
                                Marshal.ReleaseComObject(_pinTeeVideo2);
                                UnHookPin(_pinTeeVideo1, 0, _videoDecoder, 0);
                                Marshal.ReleaseComObject(_videoDecoder);
                                UnHookPin(_splitter, 0, _pinTeeVideo1, 0);
                                Marshal.ReleaseComObject(_splitter);
                                Marshal.ReleaseComObject(_splitterFS);
                                Marshal.ReleaseComObject(_pinTeeVideo1);
                                Marshal.ReleaseComObject(_graphBuilder);
                                Marshal.ReleaseComObject(_mediaCtrl);
                                _graphBuilder = null;
                                _previewPin = null;

                                RaisePropertyChanged("Graph");
                                RaisePropertyChanged("PreviewPin");
                            }
                        });
        }

        public IGraphBuilder Graph
        {
            get { return _graphBuilder; }
        }

        public IPin PreviewPin
        {
            get { return _previewPin; }
        }

        private void ConstructGraphStage1()
        {
            Application.Current.Dispatcher.Invoke(
                        delegate
                        {
                            lock (_syncGraph)
                            {
                                _graphBuilder = (IGraphBuilder)new FilterGraph();

                                _splitter = CreateFilter(FilterCategory.LegacyAmFilterCategory, "Haali Media Splitter");
                                if (_splitter == null)
                                    Log.Error("Haali Media Splitter not found.");
                                int err = _graphBuilder.AddFilter(_splitter, null);
                                _splitterFS = (IFileSourceFilter)_splitter;
                                _splitterFS.Load(_inputFilePath, null);

                                _pinTeeVideo1 = CreateFilter(FilterCategory.LegacyAmFilterCategory, "Infinite Pin Tee Filter");
                                _graphBuilder.AddFilter(_pinTeeVideo1, "pinTeeVideo1");
                                if (_pinTeeVideo1 == null)
                                    Log.Error("Infinite Pin Tee Filter not found.");
                                HookPin(_splitter, 0, _pinTeeVideo1, 0);

                                _videoDecoder = CreateFilter(FilterCategory.LegacyAmFilterCategory, "ffdshow Video Decoder");
                                _graphBuilder.AddFilter(_videoDecoder, "videoDecoder");
                                if (_videoDecoder == null)
                                    Log.Error("ffdshow Video Decoder not found.");
                                HookPin(_pinTeeVideo1, 0, _videoDecoder, 0);

                                _pinTeeVideo2 = CreateFilter(FilterCategory.LegacyAmFilterCategory, "Infinite Pin Tee Filter");
                                _graphBuilder.AddFilter(_pinTeeVideo2, "pinTeeVideo2");
                                HookPin(_videoDecoder, 0, _pinTeeVideo2, 0);

                                _previewPin = DsFindPin.ByDirection(_pinTeeVideo2, PinDirection.Output, 0);

                                _pinTeeAudio1 = CreateFilter(FilterCategory.LegacyAmFilterCategory, "Infinite Pin Tee Filter");
                                _graphBuilder.AddFilter(_pinTeeAudio1, "pinTeeAudio1");
                                HookPin(_splitter, 1, _pinTeeAudio1, 0);

                                _audioDecoder = CreateFilter(FilterCategory.LegacyAmFilterCategory, "ffdshow Audio Decoder");
                                _graphBuilder.AddFilter(_audioDecoder, "audioDecoder");
                                if (_audioDecoder == null)
                                    Log.Error("ffdshow Audio Decoder not found.");
                                _graphBuilder.AddFilter(_audioDecoder, null);
                                HookPin(_pinTeeAudio1, 0, _audioDecoder, 0);

                                _mediaCtrl = _graphBuilder as IMediaControl;

                                RaisePropertyChanged("Graph");
                                RaisePropertyChanged("PreviewPin");

                                string subType = GetSplitterPinMediaSubType(0);
                                if (subType == null)
                                    VideoCompressorNames[0] = PASSTHROUGH;
                                else
                                    VideoCompressorNames[0] = string.Format(PASSTHROUGH_FORMAT, subType);
                                RaisePropertyChanged("CurrentVideoCompressorName");

                                subType = GetSplitterPinMediaSubType(1);
                                if (subType == null)
                                    AudioCompressorNames[0] = PASSTHROUGH;
                                else
                                    AudioCompressorNames[0] = string.Format(PASSTHROUGH_FORMAT, subType);
                                RaisePropertyChanged("CurrentAudioCompressorName");
                            }
                        });
        }

         private void Run()
        {
            Application.Current.Dispatcher.Invoke(
                        delegate
                        {
                            lock (_syncGraph)
                            {
                                _mediaCtrl.Run();
                            }
                        });
         }

        private void Transcode()
        {
            Application.Current.Dispatcher.Invoke(
                delegate
                {
                    lock (_syncGraph)
                    {
                        DestroyGraph();
                        ConstructGraphStage1();


                        IBaseFilter muxer;
                        muxer = CreateFilter(FilterCategory.LegacyAmFilterCategory, "Haali Matroska Muxer");
                        if (muxer == null)
                            Log.Error("Haali Matroska Muxer not found.");
                        IPropertyBag muxerPB = (IPropertyBag)muxer;
                        object pVar = 1;
                        int err = muxerPB.Write("FileType", ref pVar);
                        err = _graphBuilder.AddFilter(muxer, "muxer");

                        //Marshal.ReleaseComObject(muxerPB);
                        IFileSinkFilter muxerFS = (IFileSinkFilter)muxer;
                        string tempPath = Path.GetTempFileName();
                        err = muxerFS.SetFileName(tempPath, null);

                        IBaseFilter videoEncoder = null;
                        if (_currentVideoCompressor != null)
                        {
                            videoEncoder = CreateVideoCompressorFilter();
                            _graphBuilder.AddFilter(videoEncoder, "videoEncoder");
                            HookPin(_pinTeeVideo2, 1, videoEncoder, 0);
                            HookPin(videoEncoder, 0, muxer, 0);
                        }
                        else
                            HookPin(_pinTeeVideo1, 1, muxer, 0);

                        IBaseFilter audioEncoder = null;
                        if (_currentAudioCompressor != null)
                        {
                            audioEncoder = CreateVideoCompressorFilter();
                            _graphBuilder.AddFilter(audioEncoder, "audioEncoder");
                            HookPin(_audioDecoder, 0, audioEncoder, 0);
                            HookPin(audioEncoder, 0, muxer, 1);
                        }
                        else
                            HookPin(_pinTeeAudio1, 1, muxer, 1);

                        IMediaPosition mediaPos = (IMediaPosition)_graphBuilder;
                        IMediaEvent medEvent = (IMediaEvent)_graphBuilder;
                        EventCode eventCode;
                        IntPtr eventHandle;
                        medEvent.GetEventHandle(out eventHandle);
                        ManualResetEvent mre = new ManualResetEvent(false);
                        mre.SafeWaitHandle = new Microsoft.Win32.SafeHandles.SafeWaitHandle(eventHandle, true);
                        err = _mediaCtrl.Run();
                        ProgressIsIndeterminate = false;
                        RaisePropertyChanged("ProgressIsIndeterminate");
                        var timer = new DispatcherTimer(DispatcherPriority.Background);
                        timer.Interval = TimeSpan.FromMilliseconds(500);
                        timer.Tick +=
                            delegate
                            {
                                double current;
                                double duration;
                                err = mediaPos.get_CurrentPosition(out current);
                                err = mediaPos.get_Duration(out duration);
                                if(current<duration)
                                {
                                    ProgressValue = (current / duration) * 10000;
                                    RaisePropertyChanged("ProgressValue");
                                }
                                else if(current >= duration)
                                {
                                    ProgressValue = 0;
                                    RaisePropertyChanged("ProgressValue");
                                    ProgressIsIndeterminate = true;
                                    RaisePropertyChanged("ProgressIsIndeterminate");
                                    int waitRes = medEvent.WaitForCompletion(1, out eventCode);
                                    if (waitRes == S_OK || waitRes == VFW_E_WRONG_STATE)
                                    {
                                        timer.Stop();
                                        err = _mediaCtrl.Stop();
                                        ProgressIsIndeterminate = false;
                                        RaisePropertyChanged("ProgressIsIndeterminate");
                                    }
                                }
                                /*if (destroy)
                                {
                                    if (videoEncoder != null)
                                    {
                                        _graphBuilder.RemoveFilter(videoEncoder);
                                        Marshal.ReleaseComObject(videoEncoder);
                                    }
                                    if (audioEncoder != null)
                                    {
                                        _graphBuilder.RemoveFilter(audioEncoder);
                                        Marshal.ReleaseComObject(audioEncoder);
                                    }
                                    _graphBuilder.RemoveFilter(muxer);
                                    Marshal.ReleaseComObject(muxer);
                                }*/
                            };
                        timer.Start();
                    }
                });
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

        //private void SetConfigParms(ICaptureGraphBuilder2 capGraph, IBaseFilter capFilter, int iFrameRate, int iWidth, int iHeight)
        //{
        //    int hr;
        //    object o;
        //    AMMediaType media;

        //    // Find the stream config interface
        //    hr = capGraph.FindInterface(
        //        PinCategory.Capture, MediaType.Video, capFilter, typeof(IAMStreamConfig).GUID, out o);

        //    IAMStreamConfig videoStreamConfig = o as IAMStreamConfig;
        //    if (videoStreamConfig == null)
        //    {
        //        throw new Exception("Failed to get IAMStreamConfig");
        //    }

        //    // Get the existing format block
        //    hr = videoStreamConfig.GetFormat(out media);
        //    DsError.ThrowExceptionForHR(hr);

        //    // copy out the videoinfoheader
        //    VideoInfoHeader v = new VideoInfoHeader();
        //    Marshal.PtrToStructure(media.formatPtr, v);

        //    // if overriding the framerate, set the frame rate
        //    if (iFrameRate > 0)
        //    {
        //        v.AvgTimePerFrame = 10000000 / iFrameRate;
        //    }

        //    // if overriding the width, set the width
        //    if (iWidth > 0)
        //    {
        //        v.BmiHeader.Width = iWidth;
        //    }

        //    // if overriding the Height, set the Height
        //    if (iHeight > 0)
        //    {
        //        v.BmiHeader.Height = iHeight;
        //    }

        //    // Copy the media structure back
        //    Marshal.StructureToPtr(v, media.formatPtr, false);

        //    // Set the new format
        //    hr = videoStreamConfig.SetFormat(media);
        //    DsError.ThrowExceptionForHR(hr);

        //    DsUtils.FreeAMMediaType(media);
        //    media = null;
        //}

        public event Action<IntPtr, int> NewStage1Sample;


        private string _inputFilePath;
        public string InputFilePath
        {
            get { return _inputFilePath; }
            set
            {
                _inputFilePath = value;
                RaisePropertyChanged("InputFilePath");

                DestroyGraph();
                ConstructGraphStage1();
                Run();
            }
        }

        private void ChooseSource()
        {
            OpenFileDialog mtsDialog = new OpenFileDialog();
            mtsDialog.Title = "Open Media File";
            mtsDialog.Filter = "Media Files(*.mp4;*.m4v;*.mkv;*.mts;*mt2s;*.tod;*.ogg;*.ogm;*.avi)|*.mp4;*.m4v;*.mkv;*.mts;*mt2s;*.tod;*.ogg;*.ogm;*.avi";
            if (!mtsDialog.ShowDialog().GetValueOrDefault(false) || string.IsNullOrEmpty(mtsDialog.FileName))
                return;
            InputFilePath = mtsDialog.FileName;
        }

        DsDevice _currentAudioCompressor = null;
        public string CurrentAudioCompressorName
        {
            get
            {
                return _currentAudioCompressor == null ? AudioCompressorNames[0] : _currentAudioCompressor.Name;
            }
            set
            {
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
                return _currentVideoCompressor == null ? VideoCompressorNames[0] : _currentVideoCompressor.Name;
            }
            set
            {
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

        private IBaseFilter CreateFilter(Guid category, string friendlyname)
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
            IPin outPin = DsFindPin.ByDirection(outFilter, PinDirection.Output, outIndex);
            if (outPin == null)
                return;
            else
                outPin.Disconnect();
            IPin inPin = DsFindPin.ByDirection(inFilter, PinDirection.Input, inIndex);
            if (inPin == null)
                return;
            inPin.Disconnect();
            int err = outPin.Connect(inPin, null);
            DsError.ThrowExceptionForHR(err);
            Marshal.ReleaseComObject(outPin);
            Marshal.ReleaseComObject(inPin);
        }

        private void UnHookPin(IBaseFilter outFilter, int outIndex, IBaseFilter inFilter, int inIndex)
        {
            IPin outPin = DsFindPin.ByDirection(outFilter, PinDirection.Output, outIndex);
            if (outPin != null)
            {
                outPin.Disconnect();
                Marshal.ReleaseComObject(outPin);
            }
            IPin inPin = DsFindPin.ByDirection(inFilter, PinDirection.Input, inIndex);
            if (inPin != null)
            {
                inPin.Disconnect();
                Marshal.ReleaseComObject(inPin);
            }
        }

        private string GetSplitterPinMediaSubType(int pinIndex)
        {
            IPin videoOut = DsFindPin.ByDirection(_splitter, PinDirection.Output, pinIndex);
            if (videoOut == null)
                return "No Media";
            AMMediaType mt = new AMMediaType();
            videoOut.ConnectionMediaType(mt);
            var memberinfo = typeof(MediaSubType).GetMembers(BindingFlags.Public | BindingFlags.Static).Concat(
                typeof(MissingMediaSubTypes).GetMembers(BindingFlags.Public | BindingFlags.Static));
            var match = memberinfo.FirstOrDefault(mi => mi is FieldInfo && ((FieldInfo)mi).GetValue(null).Equals(mt.subType));
            Marshal.ReleaseComObject(videoOut);
            return match == null ? null : match.Name;
        }

        //public void Work(object state)
        //{
        //    //Create the Graph
        //    IGraphBuilder graphBuilder = (IGraphBuilder)new FilterGraph();

        //    toolStripStatusLabel1.Text = _mtsFileName;
        //    IBaseFilter splitter;
        //    //graphBuilder.AddSourceFilter(_mtsFileName, "Haali Media Splitter", out splitter);
        //    splitter = CreateFilter(FilterCategory.LegacyAmFilterCategory, "Haali Media Splitter");
        //    int err = graphBuilder.AddFilter(splitter, null);
        //    IFileSourceFilter splitterFS = (IFileSourceFilter)splitter;
        //    splitterFS.Load(_mtsFileName, null);

        //    IBaseFilter muxerBF = CreateFilter(FilterCategory.LegacyAmFilterCategory, "Haali Matroska Muxer");
        //    IPropertyBag muxerPB = (IPropertyBag)muxerBF;
        //    object pVar = 1;
        //    err = muxerPB.Write("FileType", ref pVar);
        //    err = graphBuilder.AddFilter(muxerBF, "muxer");
        //    IFileSinkFilter muxerFS = (IFileSinkFilter)muxerBF;
        //    err = muxerFS.SetFileName(_mp4FileName, null);

        //    //Add the Video input device to the graph
        //    IBaseFilter videoDecoder = CreateFilter(FilterCategory.LegacyAmFilterCategory, "ffdshow Video Decoder");
        //    IBaseFilter videoCompressor = null;
        //    if (!string.IsNullOrEmpty(_videoCompressorName))
        //    {
        //        graphBuilder.AddFilter(videoDecoder, null);
        //        HookPin(splitter, 0, videoDecoder, 0);
        //        videoCompressor = CreateFilter(FilterCategory.VideoCompressorCategory, _videoCompressorName);
        //        int hr = graphBuilder.AddFilter(videoCompressor, "video filter");
        //        HookPin(videoDecoder, 0, videoCompressor, 0);
        //        HookPin(videoCompressor, 0, muxerBF, 0);
        //        DsError.ThrowExceptionForHR(hr);
        //    }
        //    else
        //        HookPin(splitter, 0, muxerBF, 0);


        //    //Add the Video compressor filter to the graph
        //    IBaseFilter audioDecoder = CreateFilter(FilterCategory.LegacyAmFilterCategory, "ffdshow Audio Decoder");
        //    IBaseFilter audioCompressor = null;
        //    if (!string.IsNullOrEmpty(_audioCompressorName))
        //    {
        //        graphBuilder.AddFilter(audioDecoder, null);
        //        HookPin(splitter, 1, audioDecoder, 0);
        //        audioCompressor = CreateFilter(FilterCategory.AudioCompressorCategory, _audioCompressorName);
        //        int hr = graphBuilder.AddFilter(audioCompressor, "audio filter");
        //        HookPin(audioDecoder, 0, audioCompressor, 0);
        //        HookPin(audioCompressor, 0, muxerBF, 1);
        //        DsError.ThrowExceptionForHR(hr);
        //    }
        //    else
        //        HookPin(splitter, 1, muxerBF, 1);

        //    //err = graphBuilder.RenderFile(_mtsFileName, null);
        //    IMediaControl mediaCtrl = graphBuilder as IMediaControl;
        //    IMediaPosition mediaPos = (IMediaPosition)graphBuilder;
        //    IMediaEvent medEvent = (IMediaEvent)graphBuilder;
        //    EventCode eventCode;
        //    IntPtr eventHandle;
        //    medEvent.GetEventHandle(out eventHandle);
        //    ManualResetEvent mre = new ManualResetEvent(false);
        //    mre.SafeWaitHandle = new Microsoft.Win32.SafeHandles.SafeWaitHandle(eventHandle, true);
        //    err = mediaCtrl.Run();
        //    double current;
        //    double duration;
        //    while (true)
        //    {
        //        err = mediaPos.get_CurrentPosition(out current);
        //        err = mediaPos.get_Duration(out duration);
        //        if (current >= duration)
        //            break;
        //        this.BeginInvoke((Action<double, double>)delegate(double currentPos, double dur)
        //        {
        //            toolStripStatusLabel1.Text = "Transcoding...";
        //            toolStripProgressBar1.Visible = true;
        //            toolStripProgressBar1.Style = ProgressBarStyle.Blocks;
        //            toolStripProgressBar1.Minimum = 0;
        //            toolStripProgressBar1.Maximum = 10000;
        //            toolStripProgressBar1.Value = System.Convert.ToInt32(Math.Ceiling((currentPos / dur) * 10000));
        //        }, current, duration);
        //        //medEvent.WaitForCompletion(500, out eventCode);
        //        //mre.WaitOne(500);
        //        Thread.Sleep(100);
        //    }
        //    this.Invoke((Action)delegate()
        //    {
        //        toolStripStatusLabel1.Text = "Writing file...";
        //        toolStripProgressBar1.Visible = true;
        //        //toolStripProgressBar1.Value = 0;
        //        //toolStripProgressBar1.Minimum = 0;
        //        //toolStripProgressBar1.Maximum = 100;
        //        toolStripProgressBar1.Style = ProgressBarStyle.Marquee;
        //        toolStripProgressBar1.MarqueeAnimationSpeed = 100;
        //    });
        //    err = medEvent.WaitForCompletion(int.MaxValue, out eventCode);
        //    err = mediaCtrl.Stop();

        //    Marshal.ReleaseComObject(splitter);
        //    Marshal.ReleaseComObject(splitterFS);
        //    Marshal.ReleaseComObject(audioDecoder);
        //    Marshal.ReleaseComObject(videoDecoder);
        //    if (videoCompressor != null)
        //        Marshal.ReleaseComObject(videoCompressor);
        //    if (audioCompressor != null)
        //        Marshal.ReleaseComObject(audioCompressor);
        //    Marshal.ReleaseComObject(muxerBF);
        //    Marshal.ReleaseComObject(muxerFS);
        //    Marshal.ReleaseComObject(muxerPB);
        //    Marshal.ReleaseComObject(mediaCtrl);
        //    Marshal.ReleaseComObject(mediaPos);
        //    Marshal.ReleaseComObject(medEvent);
        //    Marshal.ReleaseComObject(graphBuilder);

        //    this.BeginInvoke((Action)delegate()
        //    {
        //        toolStripStatusLabel1.Text = _mtsFileName;
        //        toolStripProgressBar1.Visible = false;
        //        button4.Enabled = true;
        //    });
        //}
    }
}
