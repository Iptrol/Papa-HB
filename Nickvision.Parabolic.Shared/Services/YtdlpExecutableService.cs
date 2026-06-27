using Microsoft.Extensions.Logging;
using Nickvision.Desktop.Application;
using Nickvision.Desktop.Filesystem;
using Nickvision.Desktop.Keyring;
using Nickvision.Desktop.Network;
using Nickvision.Desktop.System;
using Nickvision.Parabolic.Shared.Helpers;
using Nickvision.Parabolic.Shared.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Nickvision.Parabolic.Shared.Services;

public class YtdlpExecutableService : DependencyExecutableService, IYtdlpExecutableService
{
    private static readonly AppVersion YtdlpBundledVersion;
    private static readonly string YtdlpAssetName;
    private static readonly string[] PartialDownloadFilePatterns;

    private readonly IDenoExecutableService _denoExecutableService;

    static YtdlpExecutableService()
    {
        if (OperatingSystem.IsLinux())
        {
            YtdlpBundledVersion = new AppVersion(Desktop.System.Environment.DeploymentMode == DeploymentMode.Local ? "0.0.0" : "2026.03.17");
        }
        else
        {
            YtdlpBundledVersion = new AppVersion("2026.03.17");
        }
        if (OperatingSystem.IsWindows())
        {
            YtdlpAssetName = RuntimeInformation.OSArchitecture == Architecture.Arm64 ? "yt-dlp_arm64.exe" : "yt-dlp.exe";
        }
        else if (OperatingSystem.IsLinux())
        {
            YtdlpAssetName = RuntimeInformation.OSArchitecture == Architecture.Arm64 ? "yt-dlp_linux_aarch64" : "yt-dlp_linux";
        }
        else if (OperatingSystem.IsMacOS())
        {
            YtdlpAssetName = "yt-dlp_macos";
        }
        else
        {
            YtdlpAssetName = "yt-dlp";
        }
        PartialDownloadFilePatterns = ["*.part*", "*.vtt", "*.srt", "*.ass", "*.lrc"];
    }

    public YtdlpExecutableService(ILogger<YtdlpExecutableService> logger, ILogger<UpdaterService> updaterLogger, IConfigurationService configurationService, IDenoExecutableService denoExecutableService, IHttpClientFactory httpClientFactory)
        : base(logger, "yt-dlp", YtdlpBundledVersion, YtdlpAssetName, configurationService, new UpdaterService(updaterLogger, "yt-dlp", "yt-dlp", httpClientFactory.CreateClient()), new UpdaterService(updaterLogger, "yt-dlp", "yt-dlp-nightly-builds", httpClientFactory.CreateClient()))
    {
        _denoExecutableService = denoExecutableService;
    }

    public IReadOnlyList<string> GetDiscoveryProcessArguments(Uri url, Credential? credential)
    {
        var pluginsDir = Path.Combine(Desktop.System.Environment.ExecutingDirectory, "plugins");
        var arguments = new List<string>(23)
        {
            url.ToString(),
            "--ignore-config",
            "--dump-single-json",
            "--skip-download",
            "--ignore-errors",
            "--no-warnings",
            "--ffmpeg-location",
            Desktop.System.Environment.FindDependency("ffmpeg") ?? "ffmpeg",
            "--js-runtimes",
            $"deno:{_denoExecutableService.ExecutablePath ?? "deno"}",
            "--paths",
            $"temp:{UserDirectories.Cache}"
        };
        if (Directory.Exists(pluginsDir))
        {
            arguments.Add("--plugin-dir");
            arguments.Add(pluginsDir);
        }
        if (_configurationService.LimitCharacters)
        {
            arguments.Add("--windows-filenames");
        }
        if (!string.IsNullOrEmpty(_configurationService.ProxyUrl))
        {
            arguments.Add("--proxy");
            arguments.Add(_configurationService.ProxyUrl);
        }
        if (credential is not null)
        {
            if (!string.IsNullOrEmpty(credential.Username) && !string.IsNullOrEmpty(credential.Password))
            {
                arguments.Add("--username");
                arguments.Add(credential.Username);
                arguments.Add("--password");
                arguments.Add(credential.Password);
            }
            else if (!string.IsNullOrEmpty(credential.Password))
            {
                arguments.Add("--video-password");
                arguments.Add(credential.Password);
            }
        }
        if (_configurationService.CookiesBrowser != Browser.None)
        {
            arguments.Add("--cookies-from-browser");
            arguments.Add(CookiesFromBrowserArgument);
        }
        else if (File.Exists(_configurationService.CookiesPath))
        {
            arguments.Add("--cookies");
            arguments.Add(_configurationService.CookiesPath);
        }
        arguments.AddRange(_configurationService.YtdlpDiscoveryArgs.SplitCommandLine());
        return arguments;
    }

    public Process GetDownloadProcess(DownloadOptions downloadOptions)
    {
        var pluginsDir = Path.Combine(Desktop.System.Environment.ExecutingDirectory, "plugins");
        Directory.CreateDirectory(downloadOptions.SaveFolder);
        var arguments = new List<string>(128)
        {
            downloadOptions.Url.ToString(),
            "--ignore-config",
            "--verbose",
            "--no-warnings",
            "--progress",
            "--newline",
            "--progress-template",
            "[Parabolic] Progress;%(progress.status)s;%(progress.downloaded_bytes)s;%(progress.total_bytes)s;%(progress.total_bytes_estimate)s;%(progress.speed)s;%(progress.eta)s",
            "--progress-delta",
            ".75",
            "-t",
            "sleep",
            "--no-mtime",
            "--no-embed-info-json",
            "--ffmpeg-location",
            Desktop.System.Environment.FindDependency("ffmpeg") ?? "ffmpeg",
            "--js-runtimes",
            $"deno:{_denoExecutableService.ExecutablePath ?? "deno"}",
            "--paths",
            downloadOptions.SaveFolder,
            "--paths",
            $"temp:{downloadOptions.SaveFolder}",
            "--output",
            $"{downloadOptions.SaveFilename}.%(ext)s",
            "--output",
            $"chapter:%(section_number)03d - {downloadOptions.SaveFilename}.%(ext)s",
            "--print",
