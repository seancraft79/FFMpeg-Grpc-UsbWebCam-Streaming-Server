using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using FFmpeg.AutoGen;
using FFmpeg_usbCam.FFmpeg.Decoder;
using System.Drawing;
using System.IO;
using FFmpeg_usbCam.FFmpeg;
using System.Collections.Generic;
using Grpc.Core;
using System.Threading.Tasks;
using Rtspstream;
using System.Reflection;

namespace FFmpeg_usbCam
{
    /// <summary>
    /// MainWindow.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class MainWindow : Window
    {
        // TODO : Set name of WebCam device -> video="Name of WebCam device"
        const string device = "video=HD Pro Webcam C920";

        Thread thread;
        ThreadStart ts;
        Dispatcher dispatcher = Application.Current.Dispatcher;

        static byte[] currentBitmapData;

        private bool activeThread;      //rtsp thread 활성화 유무

        public MainWindow()
        {
            InitializeComponent();

            //비디오 프레임 디코딩 thread 생성
            ts = new ThreadStart(DecodeAllFramesToImages);
            thread = new Thread(ts);

            //FFmpeg dll 파일 참조 경로 설정
            FFmpegBinariesHelper.RegisterFFmpegBinaries();
            //SetupLogging();

            activeThread = true;
        }

        private void Play_Button_Click(object sender, RoutedEventArgs e)
        {
            //thread 시작 
            if (thread.ThreadState == ThreadState.Unstarted)
            {
                thread.Start();
                StartServer();
            }
        }

        private static unsafe void SetupLogging()
        {
            ffmpeg.av_log_set_level(ffmpeg.AV_LOG_VERBOSE);

            // do not convert to local function
            av_log_set_callback_callback logCallback = (p0, level, format, vl) =>
            {
                if (level > ffmpeg.av_log_get_level()) return;

                var lineSize = 1024;
                var lineBuffer = stackalloc byte[lineSize];
                var printPrefix = 1;
                ffmpeg.av_log_format_line(p0, level, format, vl, lineBuffer, lineSize, &printPrefix);
                var line = Marshal.PtrToStringAnsi((IntPtr)lineBuffer);
            };

            ffmpeg.av_log_set_callback(logCallback);
        }

        private unsafe void DecodeAllFramesToImages()
        {
            using (var vsd = new VideoStreamDecoder(device))
            {
                //Console.WriteLine($"codec name: {vsd.CodecName}");

                var info = vsd.GetContextInfo();
                info.ToList().ForEach(x => Console.WriteLine($"{x.Key} = {x.Value}"));

                var sourceSize = vsd.FrameSize;
                var sourcePixelFormat = vsd.PixelFormat;
                var destinationSize = sourceSize;
                var destinationPixelFormat = AVPixelFormat.AV_PIX_FMT_BGR24;
                using (var vfc = new VideoFrameConverter(sourceSize, sourcePixelFormat, destinationSize, destinationPixelFormat))
                {
                    var frameNumber = 0;
                    while (vsd.TryDecodeNextFrame(out var frame) && activeThread)
                    {
                        var convertedFrame = vfc.Convert(frame);

                        Bitmap bitmap;

                        bitmap = new Bitmap(convertedFrame.width, convertedFrame.height, convertedFrame.linesize[0], System.Drawing.Imaging.PixelFormat.Format24bppRgb, (IntPtr)convertedFrame.data[0]);
                        BitmapToImageSource(bitmap);

                        frameNumber++;
                    }
                }
            }
        }

        int index = 0;
        void BitmapToImageSource(Bitmap bitmap)
        {
            //Dispatcher to access UI thread
            dispatcher.BeginInvoke((Action)(() =>
            {
                using (MemoryStream memory = new MemoryStream())
                {
                    if (thread.IsAlive)
                    {
                        //bitmap.Save(memory, System.Drawing.Imaging.ImageFormat.Bmp);
                        bitmap.Save(memory, System.Drawing.Imaging.ImageFormat.Jpeg);
                        memory.Position = 0;
                        BitmapImage bitmapimage = new BitmapImage();
                        bitmapimage.BeginInit();
                        bitmapimage.CacheOption = BitmapCacheOption.OnLoad;

                        //bitmapimage.CreateOptions = BitmapCreateOptions.IgnoreImageCache;

                        bitmapimage.StreamSource = memory;
                        bitmapimage.EndInit();

                        image.Source = bitmapimage;     //image 컨트롤에 웹캠 이미지 표시


                        // TOTO : TEST
                        //Interlocked.Exchange(ref currentBitmapData, getTestImgData());



                        // Get byte data from bitmap
                        Interlocked.Exchange(ref currentBitmapData, memory.ToArray());
                        //Console.WriteLine($"CurrentData Length => {currentBitmapData.Length}");
                    }
                }
            }));

        }

        byte[] getTestImgData()
        {
            var projectPath = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location.Substring(0, Assembly.GetEntryAssembly().Location.IndexOf("bin\\")));
            string path = Path.Combine(projectPath, "Iamges", $"car{index++}.png");
            if (index > 5) index = 0;
            var imgData = File.ReadAllBytes(path);
            return imgData;
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (thread.IsAlive)
            {
                activeThread = false;
                thread.Join();
            }
        }

        const string HOST = "localhost";
        const int PORT = 1004;

        static int currentIndex = 0;
        static List<byte[]> imageByteList = new List<byte[]>();

        static void StartServer()
        {
            Console.WriteLine("Start Server");

            Server server = new Server
            {
                Services = { Rtspstream.Rtspstream.BindService(new RtspStreamImpl()) },
                Ports = { new ServerPort(HOST, PORT, ServerCredentials.Insecure) }
            };
            server.Start();

            //server.ShutdownAsync().Wait();
        }

        class RtspStreamImpl : Rtspstream.Rtspstream.RtspstreamBase
        {
            public override Task<StreamData> GetStreaming(AuthToken request, ServerCallContext context)
            {
                var streamData = GetStreamData(request);
                return Task.FromResult(streamData);
            }

            //public async override Task GetStreaming(AuthToken request, IServerStreamWriter<StreamData> responseStream, ServerCallContext context)
            //{
            //    var streamData = GetStreamData(request);
            //    await responseStream.WriteAsync(streamData);
            //}

            static StreamData GetStreamData(AuthToken authToken)
            {
                var data = new StreamData();
                //data.Authtoken = new AuthToken { Token = $"servertoken{currentIndex}" };
                data.Token = authToken.Token;
                data.Channel = authToken.Channel;

                //data.Image = Google.Protobuf.ByteString.CopyFrom(imageByteList[currentIndex++]);

                if (currentBitmapData != null)
                {
                    data.Image = Google.Protobuf.ByteString.CopyFrom(currentBitmapData);
                    Console.WriteLine($"CurrentData => {currentBitmapData.Length}, CurrentIamge => {data.Image.Length}");
                }
                else Console.WriteLine("Current Data is NULL");

                if (data.Image == null)
                    data.Image = Google.Protobuf.ByteString.CopyFrom(imageByteList[0]);   // In case of current RewFrameByte is null

                if (data.Image != null)
                    Console.WriteLine($"Send data length => {data.Image.Length}");
                else Console.WriteLine("Send data NULL");

                if (currentIndex > imageByteList.Count - 1) currentIndex = 0;

                return data;
            }
        }
    }
}
