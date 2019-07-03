﻿using System;
using System.Runtime.InteropServices;
using FFmpeg.AutoGen;

namespace FFmpeg_usbCam.FFmpeg
{
    internal static class FFmpegHelper
    {
        public static unsafe string Av_strerror(int error)
        {
            var bufferSize = 1024;
            var buffer = stackalloc byte[bufferSize];
            ffmpeg.av_strerror(error, buffer, (ulong)bufferSize);
            var message = Marshal.PtrToStringAnsi((IntPtr)buffer);
            return message;
        }

        public static int ThrowExceptionIfError(this int error)
        {
            if (error < 0) throw new ApplicationException(Av_strerror(error));
            return error;
        }
    }
}
