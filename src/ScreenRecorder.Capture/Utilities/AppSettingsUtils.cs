using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Media.MediaProperties;
using Windows.Storage;
using ScreenRecorder.Capture.Utilities;

namespace ScreenRecorder.Capture.Utilities;

public struct AppSettings
{
    public uint Width;
    public uint Height;
    public uint Bitrate;
    public uint FrameRate;
    public bool IncludeCursor;
}

public static class ApplicationSettings
{
    public static AppSettings GetCachedSettings(this AppSettings appSettings)
    {
        var localSettings = ApplicationData.Current.LocalSettings;
        appSettings = new AppSettings
        {
            Width = 1920,
            Height = 1080,
            Bitrate = 18000000,
            FrameRate = 60,
            IncludeCursor = true
        };

        // Resolution
        if (localSettings.Values.TryGetValue(nameof(AppSettings.Width), out var width) &&
            localSettings.Values.TryGetValue(nameof(AppSettings.Height), out var height))
        {
            appSettings.Width = (uint)width;
            appSettings.Height = (uint)height;
        }
        // Support the old settings
        else if (localSettings.Values.TryGetValue("UseSourceSize", out var useSourceSize) &&
            (bool)useSourceSize == true)
        {
            appSettings.Width = 0;
            appSettings.Height = 0;
        }
        else if (localSettings.Values.TryGetValue("Quality", out var quality))
        {
            var videoQuality = EnumsUtils.ParseEnumValue<VideoEncodingQuality>((string)quality);

            var temp = MediaEncodingProfile.CreateMp4(videoQuality);
            appSettings.Width = temp.Video.Width;
            appSettings.Height = temp.Video.Height;
        }

        // Bitrate
        if (localSettings.Values.TryGetValue(nameof(AppSettings.Bitrate), out var bitrate))
        {
            appSettings.Bitrate = (uint)bitrate;
        }
        // Suppor the old setting
        else if (localSettings.Values.TryGetValue("Quality", out var quality))
        {
            var videoQuality = EnumsUtils.ParseEnumValue<VideoEncodingQuality>((string)quality);

            var temp = MediaEncodingProfile.CreateMp4(videoQuality);
            appSettings.Bitrate = temp.Video.Bitrate;
        }

        // Frame rate
        if (localSettings.Values.TryGetValue(nameof(AppSettings.FrameRate), out var frameRate))
        {
            appSettings.FrameRate = (uint)frameRate;
        }

        // Include cursor
        if (localSettings.Values.TryGetValue(nameof(AppSettings.IncludeCursor), out var includeCursor))
        {
            appSettings.IncludeCursor = (bool)includeCursor;
        }

        return appSettings;
    }

}
