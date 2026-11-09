using System.Net.Http.Headers;
using System.Text.Json;

namespace VaKey.Win.App;

/// <summary>
/// Kiểm tra cập nhật — CHỈ khi người dùng bấm. Chỉ đọc số phiên bản của bản phát hành mới nhất
/// trên GitHub, không gửi bất kỳ dữ liệu người dùng nào.
/// </summary>
internal static class UpdateChecker
{
    /// <summary>Đổi thành repo thật khi phát hành.</summary>
    public const string Repository = "vakey-app/vakey";

    public static string ReleasesUrl => $"https://github.com/{Repository}/releases";

    public static Version CurrentVersion =>
        typeof(UpdateChecker).Assembly.GetName().Version ?? new Version(1, 0, 0);

    public static async Task<Version?> GetLatestVersionAsync(CancellationToken ct = default)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("VaKey", CurrentVersion.ToString(3)));
        http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

        using var response = await http.GetAsync($"https://api.github.com/repos/{Repository}/releases/latest", ct);
        if (!response.IsSuccessStatusCode) return null;

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        var tag = doc.RootElement.TryGetProperty("tag_name", out var t) ? t.GetString() : null;
        return ParseTag(tag);
    }

    internal static Version? ParseTag(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag)) return null;
        var s = tag.TrimStart('v', 'V');
        int dash = s.IndexOf('-');
        if (dash >= 0) s = s[..dash];
        return Version.TryParse(s, out var v) ? v : null;
    }
}
