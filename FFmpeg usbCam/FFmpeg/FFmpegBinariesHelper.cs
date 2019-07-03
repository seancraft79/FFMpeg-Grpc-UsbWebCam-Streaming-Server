using System;
using System.IO;
using System.Runtime.InteropServices;

namespace FFmpeg_usbCam.FFmpeg
{
    public class FFmpegBinariesHelper
    {
        private const string LD_LIBRARY_PATH = "LD_LIBRARY_PATH";
        internal static void RegisterFFmpegBinaries()
        {
            switch (Environment.OSVersion.Platform)
            {
                case PlatformID.Win32NT:
                case PlatformID.Win32S:
                case PlatformID.Win32Windows:
                    var current = Environment.CurrentDirectory;
                    var probe = Path.Combine("FFmpeg", "Plugins");
                    while (current != null)
                    {
                        //var ffmpegDirectory = Path.Combine(current, probe);
                        var ffmpegDirectory = Path.Combine(current, probe);
                        if (Directory.Exists(ffmpegDirectory))
                        {
                            RegisterLibrariesSearchPath(ffmpegDirectory);
                            return;
                        }
                        current = Directory.GetParent(current)?.FullName;
                    }
                    break;
                case PlatformID.Unix:
                case PlatformID.MacOSX:
                    var libraryPath = Environment.GetEnvironmentVariable(LD_LIBRARY_PATH);
                    RegisterLibrariesSearchPath(libraryPath);
                    break;
            }
        }

        private static void RegisterLibrariesSearchPath(string path)
        {
            switch (Environment.OSVersion.Platform)
            {
                case PlatformID.Win32NT:
                case PlatformID.Win32S:
                case PlatformID.Win32Windows:
                    SetDllDirectory(path);
                    break;
                case PlatformID.Unix:
                case PlatformID.MacOSX:
                    string currentValue = Environment.GetEnvironmentVariable(LD_LIBRARY_PATH);
                    if (string.IsNullOrWhiteSpace(currentValue) == false && currentValue.Contains(path) == false)
                    {
                        string newValue = currentValue + Path.PathSeparator + path;
                        Environment.SetEnvironmentVariable(LD_LIBRARY_PATH, newValue);
                    }
                    break;
            }
        }

        [DllImport("kernel32", SetLastError = true)]
        private static extern bool SetDllDirectory(string lpPathName);
    }
}
