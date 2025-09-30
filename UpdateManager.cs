using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

public class UpdateManifest
{
    public string? name { get; set; }
    public string? version { get; set; }
    public string? notes { get; set; }
    public string? url { get; set; }
    public string? sha256 { get; set; }
    public string? publishedAt { get; set; }
}

public class UpdateManager
{
    private readonly HttpClient _http;
    public UpdateManager()
    {
        _http = new HttpClient() { Timeout = TimeSpan.FromSeconds(30) };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("NowPlayingPopup-Updater/1.0");
    }

    public async Task<UpdateManifest?> GetRemoteManifestAsync(string manifestUrl)
    {
        try
        {
            var s = await _http.GetStringAsync(manifestUrl);
            return JsonSerializer.Deserialize<UpdateManifest>(s, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch
        {
            return null;
        }
    }

    public static Version? GetLocalVersion()
    {
        try
        {
            return Assembly.GetEntryAssembly()?.GetName().Version;
        }
        catch { return null; }
    }

    public static bool IsNewer(string remoteVersion, Version? local)
    {
        if (local == null) return true;
        if (!Version.TryParse(remoteVersion, out var rv)) return false;
        return rv > local;
    }

    public async Task<string?> DownloadFileAsync(string url, IProgress<double>? progress = null, CancellationToken ct = default)
    {
        var tmp = Path.Combine(Path.GetTempPath(), Path.GetFileName(new Uri(url).LocalPath));
        try
        {
            using var resp = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
            resp.EnsureSuccessStatusCode();
            var total = resp.Content.Headers.ContentLength ?? -1L;
            using var stream = await resp.Content.ReadAsStreamAsync(ct);
            using var fs = File.Create(tmp);
            var buffer = new byte[81920];
            long read = 0;
            int r;
            while ((r = await stream.ReadAsync(buffer, ct)) > 0)
            {
                await fs.WriteAsync(buffer.AsMemory(0, r), ct);
                read += r;
                if (total > 0 && progress != null) progress.Report((double)read / total * 100.0);
            }
            return tmp;
        }
        catch
        {
            try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }
            return null;
        }
    }

    public static string ComputeSha256(string filePath)
    {
        using var sha = SHA256.Create();
        using var fs = File.OpenRead(filePath);
        var hash = sha.ComputeHash(fs);
        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
    }

    public static bool VerifySha256(string filePath, string expectedHash)
    {
        if (string.IsNullOrWhiteSpace(expectedHash)) return true;
        var got = ComputeSha256(filePath);
        return string.Equals(got, expectedHash.Trim().ToLowerInvariant(), StringComparison.OrdinalIgnoreCase);
    }
}
