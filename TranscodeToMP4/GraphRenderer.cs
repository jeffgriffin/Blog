using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DirectShowLib;
using WPFMediaKit.DirectShow.Controls;
using System.Windows;

namespace TranscodeToMP4
{
    public class GraphRendererElement : MediaElementBase
    {
        protected override WPFMediaKit.DirectShow.MediaPlayers.MediaPlayerBase OnRequestMediaPlayer()
        {
            return new GraphRenderer();
        }

        public static readonly DependencyProperty GraphProperty =
            DependencyProperty.Register("Graph", typeof(IGraphBuilder), typeof(GraphRendererElement),
                new FrameworkPropertyMetadata(null, new PropertyChangedCallback(OnGraphChanged)));

        public IGraphBuilder Graph
        {
            get { return (IGraphBuilder)GetValue(GraphProperty); }
            set { SetValue(GraphProperty, value); }
        }

        private static void OnGraphChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((GraphRendererElement)d).OnGraphChanged(e);
        }

        protected virtual void OnGraphChanged(DependencyPropertyChangedEventArgs e)
        {
            ((GraphRenderer)MediaPlayerBase).Graph = (IGraphBuilder)e.NewValue;
            ((GraphRenderer)MediaPlayerBase).ProcessBindings();
        }

        public static readonly DependencyProperty PinProperty =
            DependencyProperty.Register("Pin", typeof(IPin), typeof(GraphRendererElement),
                new FrameworkPropertyMetadata(null, new PropertyChangedCallback(OnPinChanged)));

        public IPin Pin
        {
            get { return (IPin)GetValue(PinProperty); }
            set { SetValue(PinProperty, value); }
        }

        private static void OnPinChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((GraphRendererElement)d).OnPinChanged(e);
        }

        protected virtual void OnPinChanged(DependencyPropertyChangedEventArgs e)
        {
            ((GraphRenderer)MediaPlayerBase).Pin = (IPin)e.NewValue;
            ((GraphRenderer)MediaPlayerBase).ProcessBindings();
        }
    }

    public class GraphRenderer : WPFMediaKit.DirectShow.MediaPlayers.MediaPlayerBase
    {
        public IGraphBuilder Graph
        {
            get;
            set;
        }

        public IPin Pin
        {
            get;
            set;
        }

        public void ProcessBindings()
        {
            if (Graph == null || Pin == null)
                return;

            IBaseFilter renderer = CreateVideoRenderer(WPFMediaKit.DirectShow.MediaPlayers.VideoRendererType.VideoMixingRenderer9, Graph);

            var filterGraph = Graph as IFilterGraph2;

            if (filterGraph == null)
                throw new Exception("Could not QueryInterface for the IFilterGraph2");

            IBaseFilter sourceFilter;

            var mixer = renderer as IVMRMixerControl9;

            if (mixer != null)
            {
                VMR9MixerPrefs dwPrefs;
                mixer.GetMixingPrefs(out dwPrefs);
                dwPrefs &= ~VMR9MixerPrefs.RenderTargetMask;
                dwPrefs |= VMR9MixerPrefs.RenderTargetRGB;
                //mixer.SetMixingPrefs(dwPrefs);
            }

            int hr = filterGraph.RenderEx(Pin, AMRenderExFlags.RenderToExistingRenderers, IntPtr.Zero);
            DsError.ThrowExceptionForHR(hr);
        }
    }
}
