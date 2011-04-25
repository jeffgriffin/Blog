using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DirectShowLib;
using WPFMediaKit.DirectShow.Controls;
using System.Windows;
using TranscodeToMP4.ViewModel;
using System.Runtime.InteropServices;
using WPFMediaKit.Threading;
using System.Windows.Data;

namespace TranscodeToMP4
{
    public class GraphPlayerElement : MediaElementBase
    {
        protected override WPFMediaKit.DirectShow.MediaPlayers.MediaPlayerBase OnRequestMediaPlayer()
        {
            return new GraphPlayer();
        }

        public static readonly DependencyProperty GraphPlayerProperty =
            DependencyProperty.Register("GraphPlayer", typeof(GraphPlayer), typeof(GraphPlayerElement),
            new PropertyMetadata());

        public GraphPlayer GraphPlayer
        {
            get { return (GraphPlayer)GetValue(GraphPlayerProperty); }
            set { SetValue(GraphPlayerProperty, value); }
        }

        protected override void OnInitialized(EventArgs e)
        {
            base.OnInitialized(e);
            GraphPlayer = (GraphPlayer)MediaPlayerBase;
        }
    }

    public class GraphPlayer : WPFMediaKit.DirectShow.MediaPlayers.MediaPlayerBase
    {
        public GraphPlayer()
        {
            EnsureThread(System.Threading.ApartmentState.STA);
        }

        public IBaseFilter CreateRenderer(IGraphBuilder graph)
        {
            if (!CheckAccess())
            {
                Dispatcher.BeginInvoke((Action)(()=>CreateRenderer(graph)));
            }

            return CreateVideoRenderer(WPFMediaKit.DirectShow.MediaPlayers.VideoRendererType.VideoMixingRenderer9, graph);
        }
    }
}
