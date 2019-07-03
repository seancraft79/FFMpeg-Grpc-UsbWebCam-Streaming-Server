using FFmpeg.AutoGen;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace FFmpeg_usbCam.FFmpeg.Decoder
{
    public sealed unsafe class VideoStreamDecoder : IDisposable
    {
        private readonly AVCodecContext* _pCodecContext;
        private readonly AVFormatContext* _pFormatContext;
        private readonly int _streamIndex;
        private readonly AVFrame* _pFrame;
        private readonly AVPacket* _pPacket;

        public VideoStreamDecoder(string device)
        {
            _pFormatContext = ffmpeg.avformat_alloc_context();
            var pFormatContext = _pFormatContext;

            //ffmpeg.av_register_all();
            ffmpeg.avdevice_register_all();

            //webcam
            AVInputFormat* iformat = ffmpeg.av_find_input_format("dshow");
            ffmpeg.avformat_open_input(&pFormatContext, device, iformat, null).ThrowExceptionIfError();
            
            //미디어 파일 열기 url주소 또는 파일 이름 필요            
            //ffmpeg.avformat_open_input(&pFormatContext, url, null, null).ThrowExceptionIfError();

            ////미디어 정보 가져옴, blocking 함수라서 network protocol으로 가져올 시, 블락될수도 있슴
            ffmpeg.avformat_find_stream_info(_pFormatContext, null).ThrowExceptionIfError();

            // find the first video stream
            AVStream* pStream = null;

            for (var i = 0; i < _pFormatContext->nb_streams; i++)

                if (_pFormatContext->streams[i]->codec->codec_type == AVMediaType.AVMEDIA_TYPE_VIDEO)
                {
                    pStream = _pFormatContext->streams[i];
                    break;
                }

            if (pStream == null) throw new InvalidOperationException("Could not found video stream.");

            _streamIndex = pStream->index;
            _pCodecContext = pStream->codec;

            var codecId = _pCodecContext->codec_id;
            var pCodec = ffmpeg.avcodec_find_decoder(codecId);  //H264
            if (pCodec == null) throw new InvalidOperationException("Unsupported codec.");

            //open codec
            ffmpeg.avcodec_open2(_pCodecContext, pCodec, null).ThrowExceptionIfError();

            CodecName = ffmpeg.avcodec_get_name(codecId);
            FrameSize = new System.Windows.Size(_pCodecContext->width, _pCodecContext->height);  // 640 480
            PixelFormat = _pCodecContext->pix_fmt;

            _pPacket = ffmpeg.av_packet_alloc();
            _pFrame = ffmpeg.av_frame_alloc();
        }
        public string CodecName { get; }
        public System.Windows.Size FrameSize { get; }
        public AVPixelFormat PixelFormat { get; }

        public void Dispose()
        {
            ffmpeg.av_frame_unref(_pFrame);
            ffmpeg.av_free(_pFrame);

            ffmpeg.av_packet_unref(_pPacket);
            ffmpeg.av_free(_pPacket);

            ffmpeg.avcodec_close(_pCodecContext);
            var pFormatContext = _pFormatContext;
            ffmpeg.avformat_close_input(&pFormatContext);
        }

        public bool TryDecodeNextFrame(out AVFrame frame)
        {
            ffmpeg.av_frame_unref(_pFrame);
            int error;
            do
            {
                try
                {
                    do
                    {
                        error = ffmpeg.av_read_frame(_pFormatContext, _pPacket);
                        if (error == ffmpeg.AVERROR_EOF)
                        {
                            frame = *_pFrame;
                            return false;
                        }

                        error.ThrowExceptionIfError();
                    } while (_pPacket->stream_index != _streamIndex);

                    ffmpeg.avcodec_send_packet(_pCodecContext, _pPacket).ThrowExceptionIfError();
                }
                finally
                {
                    ffmpeg.av_packet_unref(_pPacket);
                }

                error = ffmpeg.avcodec_receive_frame(_pCodecContext, _pFrame);
            } while (error == ffmpeg.AVERROR(ffmpeg.EAGAIN));

            error.ThrowExceptionIfError();
            frame = *_pFrame;
            return true;
        }

        public IReadOnlyDictionary<string, string> GetContextInfo()
        {
            AVDictionaryEntry* tag = null;
            var result = new Dictionary<string, string>();
            while ((tag = ffmpeg.av_dict_get(_pFormatContext->metadata, "", tag, ffmpeg.AV_DICT_IGNORE_SUFFIX)) != null)
            {
                var key = Marshal.PtrToStringAnsi((IntPtr)tag->key);
                var value = Marshal.PtrToStringAnsi((IntPtr)tag->value);
                result.Add(key, value);
            }

            return result;
        }
    }
}
