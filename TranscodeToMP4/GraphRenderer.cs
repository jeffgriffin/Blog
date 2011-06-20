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
using WPFMediaKit.DirectShow.Controls;
using System.Windows;
using TranscodeToMP4.Model;
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
            EnsureThread(System.Threading.ApartmentState.MTA);
        }

        /// <summary>
        /// This method exposes a surface allocated renderer to the caller
        /// </summary>
        public IBaseFilter CreateRenderer(IGraphBuilder graph)
        {
            if (!CheckAccess())
            {
                Dispatcher.BeginInvoke((Action)(()=>CreateRenderer(graph)));
            }
            IBaseFilter renderer = CreateVideoRenderer(WPFMediaKit.DirectShow.MediaPlayers.VideoRendererType.VideoMixingRenderer9, graph);

            //var mixer = renderer as IVMRMixerControl9;

            //if (mixer != null)
            //{
            //    VMR9MixerPrefs dwPrefs;
            //    mixer.GetMixingPrefs(out dwPrefs);
            //    dwPrefs &= ~VMR9MixerPrefs.RenderTargetMask;
            //    dwPrefs |= VMR9MixerPrefs.RenderTargetRGB;
            //    mixer.SetMixingPrefs(dwPrefs);
            //}

            return renderer;
        }
    }
}
