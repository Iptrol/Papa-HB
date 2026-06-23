using Nickvision.Desktop.Globalization;
using System;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Nickvision.Parabolic.Shared.Models;

public class Format : IComparable<Format>, IEquatable<Format>
{
    public static Format BestVideo { get; }
    public static Format BestAudio { get; }
    public static Format WorstVideo { get; }
    public static Format WorstAudio { get; }
    public static Format NoneVideo { get; }
    public static Format NoneAudio { get; }

    public string Id { get; }
    public string Protocol { get; }
    public string Extension { get; }
    public ulong Bytes { get; }
    public MediaType Type { get; }
    public double? Bitrate { get; }
    public string? AudioLanguage { get; }
    public bool HasAudioDescription { get; }
    public VideoCodec? VideoCodec { get; }
    public AudioCodec? AudioCodec { get; }
    public VideoResolution? VideoResolution { get; }
    public FrameRate? FrameRate { get; }

    public bool ContainsAudio => Type == MediaType.Audio || Bitrate.HasValue || !string.IsNullOrEmpty(AudioLanguage) || HasAudioDescription || AudioCodec.HasValue;

    static Format()
    {
        BestVideo = new Format("BEST_VIDEO", "BEST", MediaType.Video);
        BestAudio = new Format("BEST_AUDIO", "BEST", MediaType.Audio);
        WorstVideo = new Format("WORST_VIDEO", "WORST", MediaType.Video);
        WorstAudio = new Format("WORST_AUDIO", "WORST", MediaType.Audio);
        NoneVideo = new Format("NONE_VIDEO", "NONE", MediaType.Video);
        NoneAudio = new Format("NONE_AUDIO", "NONE", MediaType.Audio);
    }

    private Format(string id, string protocol, MediaType type)
    {
        Id = id;
        Protocol = protocol;
        Extension = string.Empty;
        Bytes = 0u;
        Type = type;
        Bitrate = null;
        AudioLanguage = null;
        HasAudioDescription = false;
        VideoCodec = null;
        AudioCodec = null;
        VideoResolution = null;
        FrameRate = null;
    }

    public Format(JsonElement ytdlp, ITranslationService translator) : this(string.Empty, string.Empty, MediaType.Video)
    {
        if (ytdlp.TryGetProperty("format_id", out var idProperty) && idProperty.ValueKind != JsonValueKind.Null)
        {
            Id = idProperty.GetString() ?? string.Empty;
        }
        if (ytdlp.TryGetProperty("protocol", out var protocolProperty) && protocolProperty.ValueKind != JsonValueKind.Null)
        {
            Protocol = protocolProperty.GetString() ?? string.Empty;
        }
        if (ytdlp.TryGetProperty("ext", out var extensionProperty) && extensionProperty.ValueKind != JsonValueKind.Null)
        {
            Extension = extensionProperty.GetString() ?? string.Empty;
        }
        if (ytdlp.TryGetProperty("filesize", out var filesizeProprty) && filesizeProprty.ValueKind != JsonValueKind.Null && filesizeProprty.TryGetUInt64(out var bytes))
        {
            Bytes = bytes;
        }
        if (ytdlp.TryGetProperty("tbr", out var bitrateProperty) && bitrateProperty.ValueKind != JsonValueKind.Null && bitrateProperty.TryGetDouble(out var bitrate))
        {
            Bitrate = bitrate;
        }
        var note = string.Empty;
        var resolution = string.Empty;
        if (ytdlp.TryGetProperty("format_note", out var noteProperty) && noteProperty.ValueKind != JsonValueKind.Null)
        {
            note = noteProperty.GetString() ?? string.Empty;
        }
        if (ytdlp.TryGetProperty("resolution", out var resolutionProprety) && resolutionProprety.ValueKind != JsonValueKind.Null)
        {
            resolution = resolutionProprety.GetString() ?? string.Empty;
        }
        if (resolution == "audio only")
        {
            Type = MediaType.Audio;
        }
        else
        {
            Type = note == "storyboard" ? MediaType.Image : MediaType.Video;
            VideoResolution = VideoResolution.Parse(resolution, translator);
        }
        var language = string.Empty;
        if (ytdlp.TryGetProperty("language", out var languageProperty) && languageProperty.ValueKind != JsonValueKind.Null)
        {
            language = languageProperty.GetString() ?? string.Empty;
        }
        if (!string.IsNullOrEmpty(language))
        {
            AudioLanguage = language;
            if (Id.ToLower().Contains("audiodesc"))
            {
                HasAudioDescription = true;
            }
        }
        if (ytdlp.TryGetProperty("vcodec", out var videoCodecProprety) && videoCodecProprety.ValueKind != JsonValueKind.Null)
        {
            VideoCodec = (videoCodecProprety.GetString()?.ToLower() ?? string.Empty) switch
            {
                var x when x.Contains("vp09") || x.Contains("vp9") => Models.VideoCodec.VP9,
                var x when x.Contains("av01") => Models.VideoCodec.AV01,
                var x when x.Contains("avc1") || x.Contains("h264") => Models.VideoCodec.H264,
                var x when x.Contains("hevc") || x.Contains("h265") => Models.VideoCodec.H265,
                "none" => null,
                _ => null
            };
        }
        if (ytdlp.TryGetProperty("acodec", out var audioCodecProperty) && audioCodecProperty.ValueKind != JsonValueKind.Null)
        {
            AudioCodec = (audioCodecProperty.GetString()?.ToLower() ?? string.Empty) switch
            {
                var x when x.Contains("flac") || x.Contains("alac") => Models.AudioCodec.FLAC,
                var x when x.Contains("wav") || x.Contains("aiff") => Models.AudioCodec.WAV,
                var x when x.Contains("opus") => Models.AudioCodec.OPUS,
                var x when x.Contains("aac") => Models.AudioCodec.AAC,
                var x when x.Contains("mp4a") => Models.AudioCodec.MP4A,
                var x when x.Contains("mp3") => Models.AudioCodec.MP3,
                "none" => null,
                _ => null
            };
        }
        if (ytdlp.TryGetProperty("fps", out var fpsProperty) && fpsProperty.ValueKind != JsonValueKind.Null && fpsProperty.TryGetDouble(out var fps))
        {
            FrameRate = fps switch
            {
                24.0 => Models.FrameRate.Fps24,
                30.0 => Models.FrameRate.Fps30,
                60.0 => Models.FrameRate.Fps60,
                _ => null
            };
        }
    }

    [JsonConstructor]
    internal Format(string id, string protocol, string extension, ulong bytes, MediaType type, double? bitrate, string? audioLanguage, bool hasAudioDescription, VideoCodec? videoCodec, AudioCodec? audioCodec, VideoResolution? videoResolution, FrameRate? frameRate)
    {
        Id = id;
        Protocol = protocol;
        Extension = extension;
        Bytes = bytes;
        Type = type;
        Bitrate = bitrate;
        AudioLanguage = audioLanguage;
        HasAudioDescription = hasAudioDescription;
        VideoCodec = videoCodec;
        AudioCodec = audioCodec;
        VideoResolution = videoResolution;
        FrameRate = frameRate;
    }

    public int CompareTo(Format? other)
    {
        if (other is null)
        {
            return 1;
        }
        var resolutionCompare = VideoResolution?.CompareTo(other.VideoResolution) ?? 0;
        var languageCompare = string.Compare(AudioLanguage, other.AudioLanguage, StringComparison.OrdinalIgnoreCase);
        var bitrateCompare = Bitrate.HasValue && other.Bitrate.HasValue ? Bitrate.Value.CompareTo(other.Bitrate.Value) : 0;
        if (resolutionCompare != 0)
        {
            return resolutionCompare;
        }
        else
        {
            if (Type == MediaType.Video)
            {
                if (languageCompare != 0)
                {
                    return languageCompare;
                }
                else if (bitrateCompare != 0)
                {
                    return bitrateCompare;
                }
                else
                {
                    return Id.CompareTo(other.Id);
                }
            }
            else
            {
                if (bitrateCompare != 0)
                {
                    return bitrateCompare;
                }
                else if (languageCompare != 0)
                {
                    return languageCompare;
                }
                else
                {
                    return Id.CompareTo(other.Id);
                }
            }
        }
    }

    public override bool Equals(object? obj) => obj is Format other && Equals(other);

    public bool Equals(Format? other) => other is not null && Id == other.Id;

    public override int GetHashCode() => Id.GetHashCode();

    public override string ToString() => ToString(null);

    public string ToString(ITranslationService? translator)
    {
        // Спеціальні формати
        switch (Id)
        {
            case "BEST_VIDEO":
            case "BEST_AUDIO":
                return translator?._("Best") ?? "Найкраще";
            case "WORST_VIDEO":
            case "WORST_AUDIO":
                return translator?._("Worst") ?? "Найгірше";
            case "NONE_VIDEO":
            case "NONE_AUDIO":
                return translator?._("None") ?? "Немає";
        }

        var result = new StringBuilder();

        if (Type == MediaType.Video)
        {
            // Роздільна здатність
            if (VideoResolution is not null)
            {
                var height = VideoResolution.Height;
                var label = height switch
                {
                    144 => "144p",
                    240 => "240p",
                    360 => "360p",
                    480 => "480p",
                    720 => "720p",
                    1080 => "1080p",
                    1440 => "1440p",
                    >= 2160 => "4K",
                    _ => $"{height}p"
                };
                result.Append(label);
            }
        }
        else if (Type == MediaType.Audio)
        {
            // Бітрейт аудіо
            if (Bitrate.HasValue)
            {
                result.Append($"{(int)Bitrate.Value}k");
            }
        }

        // Розмір файлу
        if (Bytes > 0)
        {
            const double mib = 1024d * 1024d;
            const double gib = 1024d * 1024d * 1024d;
            string sizeLabel;
            if (Bytes > gib)
            {
                sizeLabel = $"{Bytes / gib:0.0} ГБ";
            }
            else
            {
                sizeLabel = $"{(int)(Bytes / mib)} МБ";
            }
            if (result.Length > 0)
            {
                result.Append(" | ");
            }
            result.Append(sizeLabel);
        }

        return result.Length > 0 ? result.ToString() : Id;
    }

    public static bool operator >(Format left, Format right) => left.CompareTo(right) > 0;
    public static bool operator <(Format left, Format right) => left.CompareTo(right) < 0;
    public static bool operator >=(Format left, Format right) => left.CompareTo(right) >= 0;
    public static bool operator <=(Format left, Format right) => left.CompareTo(right) <= 0;
    public static bool operator ==(Format left, Format right) => left.Equals(right);
    public static bool operator !=(Format left, Format right) => !left.Equals(right);
}
