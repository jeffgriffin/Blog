//Copyright (c) 2011, Jeff Griffin
//All rights reserved.

//Redistribution and use in source and binary forms, with or without modification, are permitted provided that the following conditions are met:

//    Redistributions of source code must retain the above copyright notice, this list of conditions and the following disclaimer.
//    Redistributions in binary form must reproduce the above copyright notice, this list of conditions and the following disclaimer in the documentation and/or other materials provided with the distribution.

//THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DirectShowLib;
using net.visibleblue.util.IEnumerable;
using Microsoft.Practices.Prism.ViewModel;
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
using System.Windows.Media;

namespace TranscodeToMP4.Model
{
    public class PrimaryModel : WorkDispatcherObject, INotifyPropertyChanged, IDisposable
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

        private readonly Guid DTFILTER_SUBTYPE = new Guid("46ADBD28-6FD0-4796-93B2-155C51DC048D");
        private readonly Guid DTFILTER_CLASSID = new Guid("09144FD6-BB29-11DB-96F1-005056C00008");

        private readonly Guid ETDTFILTER_SUBTYPE = new Guid("C4C4C4D0-0049-4E2B-98FB-9537F6CE516D");
        private readonly Guid ETDTFILTER_CLASSID = new Guid("C4C4C4F2-0049-4E2B-98FB-9537F6CE516D");

        public PrimaryModel()
        {
            ChooseSourceFileCommand = new DelegateCommand(ChooseSource);
            TranscodeCommand = new DelegateCommand(Transcode, () => !string.IsNullOrEmpty(InputFilePath) && !_isTranscoding);
            AudioEncoderPropertiesCommand = new DelegateCommand(
                delegate
                {
                    if (Dispatcher.DispatcherThread == null)
                        return;
                    Dispatcher.BeginInvoke((Action)
                        delegate
                        {
                            try
                            {
                                if (_audioEncoder == null)
                                    _audioEncoder = CreateAudioCompressorFilter();
                                ShowFilterProperties(_audioEncoder);
                            }
                            catch (Exception e)
                            {
                                LogError(e);
                            }
                        });
                });
            VideoEncoderPropertiesCommand = new DelegateCommand(
                delegate
                {
                    if (Dispatcher.DispatcherThread == null)
                        return;
                    Dispatcher.BeginInvoke((Action)
                        delegate
                        {
                            try
                            {
                                if (_videoEncoder == null)
                                    _videoEncoder = CreateVideoCompressorFilter();
                                ShowFilterProperties(_videoEncoder);
                            }
                            catch (Exception e)
                            {
                                LogError(e);
                            }
                        });
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
            LogEntries = new ObservableCollection<ILogEntry>();
            ShowPreview = true;
            UseClock = false;
        }

        private void InitDispatcher()
        {
            this.Dispatcher = _graphPlayer.Dispatcher;
            Dispatcher.BeginInvoke(
                (Action)delegate
                {
                    _audioCompressors = DsDevice.GetDevicesOfCat(FilterCategory.AudioCompressorCategory);
                    AudioCompressorItems = new ObservableCollection<CompressorItem>(
                        Enumerable.Repeat(
                        new CompressorItem(
                            PASSTHROUGH,
                            () => CurrentAudioCompressorName = null,
                            () => _currentAudioCompressor == null), 1).Concat(
                            _audioCompressors.Select<DsDevice, CompressorItem>(dev => new CompressorItem(
                                dev.Name,
                                () => CurrentAudioCompressorName = dev.Name,
                                () => _currentAudioCompressor == dev))));
                    _videoCompressors = DsDevice.GetDevicesOfCat(FilterCategory.VideoCompressorCategory);
                    VideoCompressorItems = new ObservableCollection<CompressorItem>(
                        Enumerable.Repeat(
                        new CompressorItem(
                            PASSTHROUGH,
                            () => CurrentVideoCompressorName = null,
                            () => _currentVideoCompressor == null), 1).Concat(
                            _videoCompressors.Select<DsDevice, CompressorItem>(dev => new CompressorItem(
                                dev.Name,
                                () => CurrentVideoCompressorName = dev.Name,
                                () => _currentVideoCompressor == dev))));
                    RaisePropertyChanged("CurrentAudioCompressorName");
                    RaisePropertyChanged("CurrentVideoCompressorName");
                    RaisePropertyChanged("VideoCompressors");
                    RaisePropertyChanged("VideoCompressorNames");
                    RaisePropertyChanged("VideoCompressorItems");
                    RaisePropertyChanged("AudioCompressors");
                    RaisePropertyChanged("AudioCompressorNames");
                    RaisePropertyChanged("AudioCompressorItems");
                });
        }

        public class CompressorItem : NotificationObject
        {
            Func<bool> _isActiveDelegate;

            public CompressorItem(string name, Action activateDelegate, Func<bool> isActiveDelegate)
            {
                Name = name;
                ActivateCommand = new DelegateCommand(activateDelegate);
                _isActiveDelegate = isActiveDelegate;
            }

            private string _name;
            public string Name
            {
                get { return _name; }
                set
                {
                    _name = value;
                    RaisePropertyChanged("Name");
                }
            }

            public void FireIsActiveChanged()
            {
                RaisePropertyChanged("IsActive");
            }

            public DelegateCommand ActivateCommand
            {
                get;
                private set;
            }

            public bool IsActive
            {
                get { return _isActiveDelegate.Invoke(); }
            }
        }

        IEnumerable<DsDevice> _audioCompressors = null;
        public ObservableCollection<CompressorItem> AudioCompressorItems { get; private set; }
        IEnumerable<DsDevice> _videoCompressors = null;
        public ObservableCollection<CompressorItem> VideoCompressorItems { get; private set; }

        GraphPlayer _graphPlayer;
        public GraphPlayer GraphPlayer
        {
            set
            {
                _graphPlayer = value;
                if (value != null && Dispatcher.DispatcherThread == null)
                    InitDispatcher();
            }
        }

        public bool ShowPreview
        {
            get;
            set;
        }

        public bool UseClock
        {
            get;
            set;
        }

        public interface ILogEntry
        {
            System.Windows.Media.Brush Brush { get; set; }
            string Text { get; set; }
        }
        public class LogEntry : ILogEntry
        {
            public Brush Brush { get; set; }
            public String Text { get; set; }
        }
        public class ObservableLogEntry : NotificationObject, ILogEntry
        {
            public Brush Brush { get; set; }
            private string _text;
            public String Text
            {
                get { return _text; }
                set
                {
                    _text = value;
                    RaisePropertyChanged("Text");
                }
            }
        }
        public ObservableCollection<ILogEntry> LogEntries
        {
            get;
            private set;
        }
        private bool CheckBeginInvokeUI<TArg>(Action<TArg> action, TArg arg)
        {
            if (!Thread.CurrentThread.Equals(Application.Current.Dispatcher.Thread))
            {
                Application.Current.Dispatcher.BeginInvoke(action, arg);
                return false;
            }
            return true;
        }
        private void LogError(Exception e)
        {
            if (CheckBeginInvokeUI<Exception>(LogError, e))
            {
                LogEntries.Add(new LogEntry() { Brush = Brushes.Red, Text = e.Message });
                net.visibleblue.util.Log.Error(e);
            }
        }
        private void LogError(string message)
        {
            if (CheckBeginInvokeUI<string>(LogError, message))
            {
                LogEntries.Add(new LogEntry() { Brush = Brushes.Red, Text = message });
                net.visibleblue.util.Log.Error(message);
            }
        }
        private void LogWarning(Exception e)
        {
            if (CheckBeginInvokeUI<Exception>(LogWarning, e))
            {
                LogEntries.Add(new LogEntry() { Brush = Brushes.Yellow, Text = e.Message });
                net.visibleblue.util.Log.Warning(e);
            }
        }
        private void LogWarning(string message)
        {
            if (CheckBeginInvokeUI<string>(LogWarning, message))
            {
                LogEntries.Add(new LogEntry() { Brush = Brushes.Yellow, Text = message });
                net.visibleblue.util.Log.Warning(message);
            }
        }
        private void LogInfo(string message)
        {
            if (CheckBeginInvokeUI<string>(LogInfo, message))
            {
                LogEntries.Add(new LogEntry() { Brush = Brushes.White, Text = message });
                net.visibleblue.util.Log.Info(message);
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
                if (Dispatcher.DispatcherThread == null)
                    return;
                Dispatcher.BeginInvoke((Action)DestroyPreviewGraph);
                return;
            }
            if (_graphBuilder == null)
                return;

            lock (_syncGraph)
            {
                try
                {
                    _mediaCtrl.StopWhenReady();
                    //Marshal.ReleaseComObject(_previewPin);
                    //Marshal.ReleaseComObject(_nullRenderer);
                    if (_videoRenderer != null)
                        Marshal.ReleaseComObject(_videoRenderer);
                    //Marshal.ReleaseComObject(_videoDecoder);
                    //Marshal.ReleaseComObject(_splitter);
                    //Marshal.ReleaseComObject(_splitterFS);
                    Marshal.ReleaseComObject(_fileSource);
                    Marshal.ReleaseComObject(_graphBuilder);
                    Marshal.ReleaseComObject(_mediaCtrl);
                    _dsRotEntry.Dispose();
                    _videoRenderer = null;
                    _graphBuilder = null;
                    //_previewPin = null;
                }
                catch (Exception e)
                {
                    LogError(e);
                }
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
                try
                {
                    if (_audioEncoder != null)
                    {
                        Marshal.ReleaseComObject(_audioEncoder);
                        _audioEncoder = null;
                    }
                }
                catch (Exception e)
                {
                    LogError(e);
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
                try
                {
                    if (_videoEncoder != null)
                    {
                        Marshal.ReleaseComObject(_videoEncoder);
                        _videoEncoder = null;
                    }
                }
                catch (Exception e)
                {
                    LogError(e);
                }
            }
        }

        DsROTEntry _dsRotEntry = null;
        private void ConstructSharedGraph()
        {
            if (!CheckAccess())
            {
                if (Dispatcher.DispatcherThread == null)
                    return;
                Dispatcher.BeginInvoke((Action)ConstructSharedGraph);
                return;
            }
            _graphBuilder = (IGraphBuilder)new FilterGraph();
            _dsRotEntry = new DsROTEntry((IFilterGraph)_graphBuilder);

            //_splitter = CreateFilter(FilterCategory.LegacyAmFilterCategory, "Haali Media Splitter");
            //if (_splitter == null)
            //    Log.Error("Haali Media Splitter not found.");
            //int err = _graphBuilder.AddFilter(_splitter, null);
            //_splitterFS = (IFileSourceFilter)_splitter;
            //_splitterFS.Load(_inputFilePath, null);

            LogInfo("Adding source filter...");
            int hr = _graphBuilder.AddSourceFilter(_inputFilePath, _inputFilePath, out _fileSource);
            DsError.ThrowExceptionForHR(hr);

            _mediaCtrl = _graphBuilder as IMediaControl;
        }

        private void ConstructPreviewGraph()
        {
            if (!CheckAccess())
            {
                if (Dispatcher.DispatcherThread == null)
                    return;
                Dispatcher.BeginInvoke((Action)ConstructPreviewGraph);
                return;
            }
            lock (_syncGraph)
            {
                try
                {
                    ConstructSharedGraph();
                    IBaseFilter nullVideo;
                    IBaseFilter nullAudio;
                    LogInfo("Rendering streams to null...");
                    GetConnectNullRenderers(out nullVideo, out nullAudio);
                    if (nullVideo == null)
                        throw new ApplicationException("Unable to find or filter a video stream from the requested file.");
                    IPin compressedVideoPin = GetConnectedOut(nullVideo, 0);
                    IPin compressedAudioPin = GetConnectedOut(nullAudio, 0);

                    if (compressedVideoPin == null)
                        throw new ApplicationException("Unable to find compressed video stream.");
                    LogInfo("Video stream found.  Finding media subtype...");
                    string videoSubType = GetPinMediaSubType(compressedVideoPin);
                    if (videoSubType == null)
                        LogWarning("Unable to classify video stream.");
                    else
                        LogInfo(string.Format("Video stream subtype found: {0}", videoSubType));
                    int err = compressedVideoPin.Disconnect();
                    DsError.ThrowExceptionForHR(err);

                    string audioSubType = null;
                    if (compressedAudioPin != null)
                    {
                        LogInfo("Audio stream found.  Finding media subtype...");
                        audioSubType = GetPinMediaSubType(compressedAudioPin);
                        if (audioSubType == null)
                            LogWarning("Unable to classify audio stream.");
                        else
                            LogInfo(string.Format("Audio stream subtype found: {0}", audioSubType));
                        //compressedAudioPin.Disconnect();
                    }
                    //DisconnectAll(_fileSource);

                    err = _graphBuilder.RemoveFilter(nullVideo);
                    DsError.ThrowExceptionForHR(err);
                    Marshal.ReleaseComObject(nullVideo);

                    Application.Current.Dispatcher.BeginInvoke(
                        delegate
                        {
                            //set the passthrough pin media sub type hint
                            if (videoSubType == null)
                                VideoCompressorItems[0].Name = PASSTHROUGH;
                            else
                                VideoCompressorItems[0].Name = string.Format(PASSTHROUGH_FORMAT, videoSubType);
                            RaisePropertyChanged("VideoCompressorNames");
                            RaisePropertyChanged("CurrentVideoCompressorName");


                            if (audioSubType == null)
                                AudioCompressorItems[0].Name = PASSTHROUGH;
                            else
                                AudioCompressorItems[0].Name = string.Format(PASSTHROUGH_FORMAT, audioSubType);
                            RaisePropertyChanged("AudioCompressorNames");
                            RaisePropertyChanged("CurrentAudioCompressorName");
                        });

                    LogInfo("Rendering video stream...");
                    //HACK: for some reason, the standard Video Renderer sets up a better graph than VMR9 does
                    IPin decodePin = null;
                    try
                    {
                        decodePin = RenderForDumbPin(compressedVideoPin);
                    }
                    catch (Exception e)
                    {
                        LogWarning(e);
                    }
                    if (decodePin == null)
                        throw new ApplicationException("Unable to render video stream.");
                    _videoRenderer = _graphPlayer.CreateRenderer(_graphBuilder);
                    HookPinDirect(decodePin, _videoRenderer, 0);
                    Marshal.ReleaseComObject(decodePin);

                    //_videoRenderer = _graphPlayer.CreateRenderer(_graphBuilder);
                    //HookPin(compressedVideoPin, _videoRenderer, 0);

                    err = _graphBuilder.RemoveFilter(nullAudio);
                    DsError.ThrowExceptionForHR(err);
                    Marshal.ReleaseComObject(nullAudio);
                    Marshal.ReleaseComObject(compressedVideoPin);
                    Marshal.ReleaseComObject(compressedAudioPin);

                    //terminate the audio pin, so we can read its subtype
                    //_nullRenderer = CreateFilter(FilterCategory.LegacyAmFilterCategory, "Null Renderer");
                    //if (_nullRenderer == null)
                    //    Log.Error("Null Renderer");
                    //int err = _graphBuilder.AddFilter(_nullRenderer, null);
                    //HookPin(_splitter, 1, _nullRenderer, 0);

                    err = _mediaCtrl.Run();
                    DsError.ThrowExceptionForHR(err);
                }
                catch (Exception e)
                {
                    LogError(e);
                }
            }
        }

        const int NULL_RENDER_POOL_SIZE = 5;
        private void GetConnectNullRenderers(out IBaseFilter nullVideoRenderer, out IBaseFilter nullAudioRenderer)
        {
            nullVideoRenderer = null;
            nullAudioRenderer = null;
            List<IBaseFilter> nullRenderPool = new List<IBaseFilter>();
            Random rand = new Random();
            for (int i = 0; i < NULL_RENDER_POOL_SIZE; i++)
            {
                IBaseFilter nullRenderer = (IBaseFilter)new NullRenderer();
                int hr = _graphBuilder.AddFilter(nullRenderer, Convert.ToString(rand.Next()));
                DsError.ThrowExceptionForHR(hr);
                nullRenderPool.Add(nullRenderer);
            }

            RenderAll(_fileSource);

            IBaseFilter anr = null;
            IBaseFilter vnr = null;
            nullRenderPool.Do(
                delegate(IBaseFilter nr)
                {
                    IPin co = null;
                    AMMediaType mt = new AMMediaType();
                    try
                    {
                        co = GetConnectedOut(nr, 0);
                        if (co != null)
                        {
                            co.ConnectionMediaType(mt);
                            if (mt.majorType == MediaType.Video)
                            {
                                AddDtFilterIfNeeded(nr, mt);
                                vnr = nr;
                            }
                            else if (mt.majorType == MediaType.Audio)
                            {
                                AddDtFilterIfNeeded(nr, mt);
                                anr = nr;
                            }
                        }
                    }
                    finally
                    {
                        if (co != null)
                            Marshal.ReleaseComObject(co);
                        DsUtils.FreeAMMediaType(mt);
                    }
                });
            nullAudioRenderer = anr;
            nullVideoRenderer = vnr;
            nullRenderPool.Remove(nullAudioRenderer);
            nullRenderPool.Remove(nullVideoRenderer);
            while (nullRenderPool.Count > 0)
            {
                var nr = nullRenderPool[0];
                _graphBuilder.RemoveFilter(nr);
                nullRenderPool.RemoveAt(0);
                Marshal.ReleaseComObject(nr);
            }
        }

        private void AddDtFilterIfNeeded(IBaseFilter nullRenderer, AMMediaType mediaType)
        {
            IBaseFilter tagDecoder = null;
            if (mediaType.subType.Equals(DTFILTER_SUBTYPE))
                tagDecoder = CreateFilter(DTFILTER_CLASSID);
            else if (mediaType.subType.Equals(ETDTFILTER_SUBTYPE))
                tagDecoder = CreateFilter(ETDTFILTER_CLASSID);
            if (tagDecoder != null)
            {
                _graphBuilder.AddFilter(tagDecoder, null);
                IPin co = GetConnectedOut(nullRenderer, 0);
                co.Disconnect();
                HookPinDirect(co, tagDecoder, 0);
                HookPin(tagDecoder, 0, nullRenderer, 0);
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

        private bool TryConnectToAny(IPin sourcePin, IBaseFilter destinationFilter)
        {
            IEnumPins pinEnum;
            int hr = destinationFilter.EnumPins(out pinEnum);
            DsError.ThrowExceptionForHR(hr);
            IPin[] pins = { null };
            while (pinEnum.Next(pins.Length, pins, IntPtr.Zero) == 0)
            {
                int err = _graphBuilder.Connect(sourcePin, pins[0]);
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
            if (!CheckAccess())
            {
                if (Dispatcher.DispatcherThread == null)
                    return;
                Dispatcher.BeginInvoke((Action)Transcode);
                return;
            }
            lock (_syncGraph)
            {
                try
                {
                    _isTranscoding = true;
                    CancelCommand.RaiseCanExecuteChanged();
                    TranscodeCommand.RaiseCanExecuteChanged();
                    LogInfo("Destroying preview graph...");
                    DestroyPreviewGraph();
                    ConstructSharedGraph();

                    //set sync source to null to allow the graph to progress as quickly as possible
                    IMediaFilter mediaFilter = (IMediaFilter)_graphBuilder;
                    int err;
                    if (!UseClock)
                    {
                        err = mediaFilter.SetSyncSource(null);
                        DsError.ThrowExceptionForHR(err);
                    }

                    LogInfo("Rendering streams to null...");
                    IBaseFilter nullVideo;
                    IBaseFilter nullAudio;
                    GetConnectNullRenderers(out nullVideo, out nullAudio);
                    if (nullVideo == null)
                        throw new ApplicationException("Unable to find or filter a video stream from the requested file.");
                    IPin compressedVideoPin = GetConnectedOut(nullVideo, 0);
                    IPin compressedAudioPin = GetConnectedOut(nullAudio, 0);
                    if (compressedVideoPin == null)
                        throw new ApplicationException("Unable to find compressed video stream.");
                    err = compressedVideoPin.Disconnect();
                    DsError.ThrowExceptionForHR(err);
                    if (compressedAudioPin != null)
                        compressedAudioPin.Disconnect();

                    err = _graphBuilder.RemoveFilter(nullVideo);
                    DsError.ThrowExceptionForHR(err);
                    Marshal.ReleaseComObject(nullVideo);
                    err = _graphBuilder.RemoveFilter(nullAudio);
                    DsError.ThrowExceptionForHR(err);
                    Marshal.ReleaseComObject(nullAudio);

                    LogInfo("Adding Haali Matroska Muxer...");
                    IBaseFilter muxer;
                    muxer = CreateFilter(FilterCategory.LegacyAmFilterCategory, "Haali Matroska Muxer");
                    if (muxer == null)
                        throw new ApplicationException("Haali Matroska Muxer not found.");
                    //set the FileType on the multiplexer to MP4
                    IPropertyBag muxerPB = (IPropertyBag)muxer;
                    object pVar = 1;
                    err = muxerPB.Write("FileType", ref pVar);
                    DsError.ThrowExceptionForHR(err);

                    IFileSinkFilter muxerFS = (IFileSinkFilter)muxer;
                    string tempPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
                    LogInfo(string.Format("Store output in temporary file: {0}", tempPath));
                    err = muxerFS.SetFileName(tempPath, null);
                    DsError.ThrowExceptionForHR(err);

                    IBaseFilter pinTeeVideo = null;
                    if (ShowPreview)
                    {
                        pinTeeVideo = CreateFilter(FilterCategory.LegacyAmFilterCategory, "Infinite Pin Tee Filter");
                        if (pinTeeVideo == null)
                            throw new ApplicationException("Infinite Pin Tee Filter not found.");
                    }

                    if (_currentVideoCompressor != null)
                    {
                        if (ShowPreview)
                        {
                            LogInfo(string.Format("Rendering video through {0} with preview...", _currentVideoCompressor.Name));
                            //we want to use intelligent connect to choose a decoder, but the pin tee isn't going to resolve it.
                            IPin decoderPin = RenderForDumbPin(compressedVideoPin);

                            err = _graphBuilder.AddFilter(pinTeeVideo, "pinTeeVideo");
                            DsError.ThrowExceptionForHR(err);
                            HookPinDirect(decoderPin, pinTeeVideo, 0);

                            _videoRenderer = _graphPlayer.CreateRenderer(_graphBuilder);
                            IPin pinTee0 = DsFindPin.ByDirection(pinTeeVideo, PinDirection.Output, 0);
                            err = TryConnect(pinTee0, _videoRenderer, 0, out pinTee0);
                            DsError.ThrowExceptionForHR(err);
                            if (_videoEncoder == null)
                                _videoEncoder = CreateVideoCompressorFilter();
                            err = _graphBuilder.AddFilter(_videoEncoder, "videoEncoder");
                            DsError.ThrowExceptionForHR(err);
                            Marshal.ReleaseComObject(pinTee0);
                            HookPin(pinTeeVideo, 1, _videoEncoder, 0);
                            Marshal.ReleaseComObject(decoderPin);
                        }
                        else
                        {
                            LogInfo(string.Format("Rendering video through {0} without preview...", _currentVideoCompressor.Name));
                            if (_videoEncoder == null)
                                _videoEncoder = CreateVideoCompressorFilter();
                            err = _graphBuilder.AddFilter(_videoEncoder, "videoEncoder");
                            DsError.ThrowExceptionForHR(err);
                            if (!TryConnectToAny(compressedVideoPin, _videoEncoder))
                                throw new ApplicationException("Unable to connect compressed video to video encoder");
                        }

                        err = _graphBuilder.AddFilter(muxer, "muxer");
                        DsError.ThrowExceptionForHR(err);
                        //HookPin(_videoEncoder, 0, muxer, 0);

                        IPin direct;
                        if (!TryConnectAny(_videoEncoder, muxer, 0, out direct))
                            throw new ApplicationException("Unable to connect encoded video to MP4 container.");
                        Marshal.ReleaseComObject(direct);
                        //_previewPin = DsFindPin.ByDirection(pinTeeVideo, PinDirection.Output, 1);
                    }
                    else if (ShowPreview)
                    {
                        LogInfo("Rendering compressed video with preview...");
                        IPin decoderPin = RenderForDumbPin(compressedVideoPin);
                        PinInfo decoderPinInfo;
                        err = decoderPin.QueryPinInfo(out decoderPinInfo);
                        DsError.ThrowExceptionForHR(err);
                        IBaseFilter decoderFilter = decoderPinInfo.filter;
                        DisconnectAll(decoderFilter);
                        err = compressedVideoPin.Disconnect();
                        DsError.ThrowExceptionForHR(err);

                        err = _graphBuilder.AddFilter(pinTeeVideo, "pinTeeVideo");
                        DsError.ThrowExceptionForHR(err);
                        HookPin(compressedVideoPin, pinTeeVideo, 0);

                        err = _graphBuilder.AddFilter(muxer, "muxer");
                        DsError.ThrowExceptionForHR(err);
                        HookPin(pinTeeVideo, 0, muxer, 0);

                        _videoRenderer = _graphPlayer.CreateRenderer(_graphBuilder);
                        IPin pinTeeOut1 = DsFindPin.ByDirection(pinTeeVideo, PinDirection.Output, 1);
                        TryConnectToAny(pinTeeOut1, decoderFilter);
                        HookPin(decoderPin, _videoRenderer, 0);

                        //HookPin(pinTeeVideo, 1, _videoRenderer, 0);

                        //err = _graphBuilder.Render(decoderPin);
                        //DsError.ThrowExceptionForHR(err);

                        Marshal.ReleaseComObject(pinTeeOut1);
                        Marshal.ReleaseComObject(decoderPin);
                        //_previewPin = DecodeForDumbPin(pinTeeVideo, 0);
                    }
                    else
                    {
                        LogInfo("Rendering compressed video without preview...");
                        err = _graphBuilder.AddFilter(muxer, "muxer");
                        DsError.ThrowExceptionForHR(err);
                        HookPin(compressedVideoPin, muxer, 0);
                    }
                    Marshal.ReleaseComObject(compressedVideoPin);

                    if (compressedAudioPin != null)
                    {
                        if (_currentAudioCompressor != null)
                        {
                            LogInfo(string.Format("Rendering audio through {0}...", _currentAudioCompressor.Name));
                            if (_audioEncoder == null)
                                _audioEncoder = CreateAudioCompressorFilter();
                            err = _graphBuilder.AddFilter(_audioEncoder, "audioEncoder");
                            DsError.ThrowExceptionForHR(err);
                            HookPin(compressedAudioPin, _audioEncoder, 0);
                            HookPinDirect(_audioEncoder, 0, muxer, 1);
                        }
                        else
                        {
                            LogInfo("Rendering compressed audio...");
                            HookPin(compressedAudioPin, muxer, 1);
                        }
                        Marshal.ReleaseComObject(compressedAudioPin);
                    }

                    LogInfo("Filter built.  Running it...");
                    err = _mediaCtrl.Run();
                    DsError.ThrowExceptionForHR(err);
                    ProgressIsIndeterminate = false;
                    RaisePropertyChanged("ProgressIsIndeterminate");
                    var timer = new DispatcherTimer(DispatcherPriority.Background, Application.Current.Dispatcher);
                    timer.Interval = TimeSpan.FromMilliseconds(500);
                    int numElipses = 0;
                    ObservableLogEntry progressLogEntry = new ObservableLogEntry();
                    progressLogEntry.Brush = Brushes.White;
                    CheckBeginInvokeUI<object>(delegate(object obj)
                    {
                        LogEntries.Add(progressLogEntry);
                    }, null);
                    timer.Tick +=
                        delegate
                        {
                            Dispatcher.BeginInvoke(
                                (Action)delegate
                                {
                                    lock (_syncGraph)
                                    {
                                        try
                                        {
                                            if (_graphBuilder == null)
                                                return;
                                            IMediaPosition mediaPos = (IMediaPosition)_graphBuilder;
                                            IMediaEvent medEvent = (IMediaEvent)_graphBuilder;
                                            EventCode eventCode;
                                            double current = 0;
                                            double duration = 0;
                                            int waitRes = S_OK;
                                            if (!_cancelRequested)
                                                waitRes = medEvent.WaitForCompletion(1, out eventCode);
                                            if (_cancelRequested || waitRes == S_OK || waitRes == VFW_E_WRONG_STATE)
                                            {
                                                LogInfo("Finish signaled.  Stopping filter...");
                                                timer.Stop();
                                                try
                                                {
                                                    err = _mediaCtrl.StopWhenReady();

                                                    DestroyAudioEncoder();
                                                    DestroyVideoEncoder();
                                                    if (pinTeeVideo != null)
                                                        Marshal.ReleaseComObject(pinTeeVideo);
                                                    Marshal.ReleaseComObject(muxer);
                                                    Marshal.ReleaseComObject(muxerPB);
                                                    Marshal.ReleaseComObject(muxerFS);
                                                    DestroyPreviewGraph();
                                                    Marshal.ReleaseComObject(mediaFilter);
                                                    Marshal.ReleaseComObject(mediaPos);
                                                    Marshal.ReleaseComObject(medEvent);
                                                }
                                                finally
                                                {
                                                    CheckBeginInvokeUI<object>(delegate(object obj)
                                                    {
                                                        ProgressIsIndeterminate = false;
                                                        RaisePropertyChanged("ProgressIsIndeterminate");
                                                        ProgressValue = 0;
                                                        RaisePropertyChanged("ProgressValue");
                                                        progressLogEntry.Text = "Finished";
                                                        LogInfo("Graph Completed.");
                                                        try
                                                        {
                                                            if (!_cancelRequested)
                                                                ChooseDestination(tempPath);
                                                        }
                                                        catch (Exception e)
                                                        {
                                                            LogError(e);
                                                            return;
                                                        }
                                                        File.Delete(tempPath);
                                                        LogInfo("Temporary file deleted.");
                                                        _cancelRequested = false;
                                                        _isTranscoding = false;
                                                        TranscodeCommand.RaiseCanExecuteChanged();
                                                        CancelCommand.RaiseCanExecuteChanged();
                                                    }, null);
                                                }
                                            }
                                            else if (!_cancelRequested)
                                            {
                                                err = mediaPos.get_CurrentPosition(out current);
                                                err = mediaPos.get_Duration(out duration);
                                            }
                                            if (current < duration && !_cancelRequested)
                                            {
                                                ProgressValue = (current / duration) * 10000;
                                                RaisePropertyChanged("ProgressValue");
                                                if (numElipses == 3)
                                                    numElipses = 0;
                                                else
                                                    numElipses++;
                                                StringBuilder sb = new StringBuilder();
                                                for (int i = 0; i < numElipses; i++)
                                                    sb.Append('.');
                                                var progressLogText = string.Format("Transcoding {0:f2}%{1}", (current / duration) * 100, sb.ToString());
                                                CheckBeginInvokeUI<object>(delegate(object obj)
                                                {
                                                    progressLogEntry.Text = progressLogText;
                                                }, null);
                                            }
                                            else
                                            {
                                                ProgressIsIndeterminate = true;
                                                RaisePropertyChanged("ProgressIsIndeterminate");
                                            }
                                        }
                                        catch (Exception e)
                                        {
                                            LogError(e);
                                        }
                                    }
                                });
                        };
                    timer.Start();
                }
                catch (Exception e)
                {
                    LogError(e);
                }
                finally
                {
                    _cancelRequested = false;
                    _isTranscoding = false;
                    TranscodeCommand.RaiseCanExecuteChanged();
                    CancelCommand.RaiseCanExecuteChanged();
                }
            }
        }

        private IPin RenderForDumbPin(IPin renderPin)
        {
            IPin inPin = null;
            IBaseFilter tempRenderer = null;
            IPin outPin = null;
            try
            {
                tempRenderer = (IBaseFilter)new VideoRenderer();
                int err = _graphBuilder.AddFilter(tempRenderer, null);
                DsError.ThrowExceptionForHR(err);
                err = TryConnect(renderPin, tempRenderer, 0, out outPin);
                if (err != 0)
                {
                    //fall back to vmr
                    err = _graphBuilder.RemoveFilter(tempRenderer);
                    DsError.ThrowExceptionForHR(err);
                    Marshal.ReleaseComObject(tempRenderer);
                    tempRenderer = (IBaseFilter)new VideoMixingRenderer();
                    err = _graphBuilder.AddFilter(tempRenderer, null);
                    DsError.ThrowExceptionForHR(err);
                    err = TryConnect(renderPin, tempRenderer, 0, out outPin);
                    DsError.ThrowExceptionForHR(err);
                }
                outPin.Disconnect();
                return outPin;
            }
            finally
            {
                if (tempRenderer != null)
                {
                    _graphBuilder.RemoveFilter(tempRenderer);
                    Marshal.ReleaseComObject(tempRenderer);
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
                LogInfo(string.Format("File requested: {0}", InputFilePath));

                DestroyPreviewGraph();
                ConstructPreviewGraph();
                TranscodeCommand.RaiseCanExecuteChanged();
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

        private void ChooseDestination(string tempPath)
        {
            SaveFileDialog saveDialog = new SaveFileDialog();
            saveDialog.Title = "Save MP4";
            saveDialog.Filter = "MP4 Files(*.mp4)|*.mp4";
            saveDialog.OverwritePrompt = true;
            if (!saveDialog.ShowDialog().GetValueOrDefault(false) || string.IsNullOrEmpty(saveDialog.FileName))
            {
                LogWarning("Save canceled.");
                return;
            }
            if (File.Exists(saveDialog.FileName))
                File.Delete(saveDialog.FileName);
            File.Move(tempPath, saveDialog.FileName);
            LogInfo(string.Format("Output saved: {0}", saveDialog.FileName));
        }

        DsDevice _currentAudioCompressor = null;
        public string CurrentAudioCompressorName
        {
            get
            {
                if (AudioCompressorItems == null)
                    return null;
                return _currentAudioCompressor == null ? AudioCompressorItems[0].Name : _currentAudioCompressor.Name;
            }
            set
            {
                DestroyAudioEncoder();
                var valueCompressor = _audioCompressors.FirstOrDefault(dev => dev.Name.Equals(value));
                if (valueCompressor == _currentAudioCompressor)
                    return;
                _currentAudioCompressor = valueCompressor;
                RaisePropertyChanged("CurrentAudioCompressorName");
                AudioCompressorItems.Do(item => item.FireIsActiveChanged());
            }
        }

        public IEnumerable<string> AudioCompressorNames
        {
            get
            {
                if (AudioCompressorItems == null)
                    return null;
                return AudioCompressorItems.Select<CompressorItem, string>(item => item.Name);
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
                if (VideoCompressorItems == null)
                    return null;
                return _currentVideoCompressor == null ? VideoCompressorItems[0].Name : _currentVideoCompressor.Name;
            }
            set
            {
                DestroyVideoEncoder();
                var valueCompressor = _videoCompressors.FirstOrDefault(dev => dev.Name.Equals(value));
                if (valueCompressor == _currentVideoCompressor)
                    return;
                _currentVideoCompressor = valueCompressor;
                RaisePropertyChanged("CurrentVideoCompressorName");
                VideoCompressorItems.Do(item => item.FireIsActiveChanged());
            }
        }

        public IEnumerable<string> VideoCompressorNames
        {
            get
            {
                if (VideoCompressorItems == null)
                    return null;
                return VideoCompressorItems.Select<CompressorItem, string>(item => item.Name);
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

        public static IBaseFilter CreateFilter(Guid classId)
        {
            var type = Type.GetTypeFromCLSID(classId);
            return (IBaseFilter)Activator.CreateInstance(type, false);
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

        private void HookPinDirect(IBaseFilter outFilter, int outIndex, IBaseFilter inFilter, int inIndex)
        {
            IPin outPin = DsFindPin.ByDirection(outFilter, PinDirection.Output, outIndex);
            if (outPin == null)
                return;
            HookPinDirect(outPin, inFilter, inIndex);
            Marshal.ReleaseComObject(outPin);
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
                err = OleCreatePropertyFrame(IntPtr.Zero, 0, 0, filterInfo.achName, 1, ref filterObject,
                    caGuid.cElems, caGuid.pElems, 0, 0, IntPtr.Zero);
                DsError.ThrowExceptionForHR(err);

                Marshal.FreeCoTaskMem(caGuid.pElems);
                Marshal.ReleaseComObject(spp);
            }
            else
            {
                IAMVfwCompressDialogs compDialogs = filter as IAMVfwCompressDialogs;
                if (compDialogs != null)
                {
                    err = compDialogs.ShowDialog(VfwCompressDialogs.Config, IntPtr.Zero);
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

        public void Dispose()
        {
            DestroyPreviewGraph();
        }
    }
}
