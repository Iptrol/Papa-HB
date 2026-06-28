using Microsoft.Extensions.Logging;
using Nickvision.Desktop.Helpers;
using Nickvision.Parabolic.Shared.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
namespace Nickvision.Parabolic.Shared.Services;
public class ThumbnailService : IThumbnailService
{
    private readonly ILogger<ThumbnailService> _logger;
    private readonly HttpClient _httpClient;
    private readonly Dictionary<Uri, byte[]> _cache;
    private readonly Dictionary<Uri, Uri> _mediaMap;
    public ThumbnailService(ILogger<ThumbnailService> logger, IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient();
        _cache = [];
        _mediaMap = [];
        _logger.LogDebug("Loading default thumbnail into cache...");
        using var stream = typeof(ThumbnailService).Assembly.GetManifestResourceStream("Nickvision.Parabolic.Shared.Resources.default_thumbnail.jpg");
        if (stream is null)
        {
            _logger.LogWarning("Default thumbnail resource not found. Using empty byte array.");
            _cache[Uri.Empty] = [];
            return;
        }
        using var memoryStream = new MemoryStream();
        stream.CopyTo(memoryStream);
        _cache[Uri.Empty] = memoryStream.ToArray();
        _logger.LogDebug("Loaded default thumbnail into cache.");
    }
    public async Task<byte[]> GetImageBytesAsync(Uri url)
    {
        _logger.LogDebug($"Getting image bytes for url: {url}");
        if (_cache.TryGetValue(url, out var bytes))
        {
            _logger.LogDebug($"Found image bytes for url ({url}) in cache.");
            return bytes;
        }
        await DownloadImageBytesAsync(url);
        return _cache[url];
    }
    public async Task<MemoryStream> GetImageStreamAsync(Uri url)
    {
        _logger.LogDebug($"Getting image stream for url: {url}");
        if (_cache.TryGetValue(url, out var bytes))
        {
            _logger.LogDebug($"Found image stream for url ({url}) in cache.");
            return new MemoryStream(bytes);
        }
        await DownloadImageBytesAsync(url);
        return new MemoryStream(_cache[url]);
    }
    public void MapMedia(Media media)
    {
        if (_cache.ContainsKey(media.Url))
        {
            return;
        }
        if (media.ThumbnailUrl.IsEmpty)
        {
            _cache[media.Url] = _cache[Uri.Empty];
            _logger.LogWarning($"Mapped media url ({media.Url}) to default thumbnail as thumbnail url is empty.");
        }
        else
        {
            _mediaMap[media.Url] = media.ThumbnailUrl;
            _logger.LogDebug($"Mapped media url ({media.Url}) to thumbnail url ({media.ThumbnailUrl}).");
        }
    }
    private async Task DownloadImageBytesAsync(Uri url)
    {
        if (_cache.ContainsKey(url) || url == Uri.Empty)
        {
            return;
        }
        _logger.LogDebug($"Downloading image bytes for url: {url}");
        _mediaMap.TryGetValue(url, out var mappedUrl);
        var targetUrl = mappedUrl ?? url;
        byte[] bytes;
        try
        {
            using var response = await _httpClient.GetAsync(targetUrl);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning($"Failed to download thumbnail for url ({url}): HTTP {(int)response.StatusCode}. Using default thumbnail.");
                _cache[url] = _cache[Uri.Empty];
                if (mappedUrl is not null)
                {
                    _cache[mappedUrl] = _cache[Uri.Empty];
                    _mediaMap.Remove(url);
                }
                return;
            }
            bytes = await response.Content.ReadAsByteArrayAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"Exception downloading thumbnail for url ({url}): {ex.Message}. Using default thumbnail.");
            _cache[url] = _cache[Uri.Empty];
            if (mappedUrl is not null)
            {
                _cache[mappedUrl] = _cache[Uri.Empty];
                _mediaMap.Remove(url);
            }
            return;
        }
        _cache[url] = bytes.Length == 0 ? _cache[Uri.Empty] : bytes;
        if (mappedUrl is not null)
        {
            _cache[mappedUrl] = bytes.Length == 0 ? _cache[Uri.Empty] : bytes;
            _mediaMap.Remove(url);
        }
    }
}
