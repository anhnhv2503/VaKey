using System.Text.Json;
using VaKey.Win.Config;
using VaKey.Win.Input;

namespace VaKey.Win.Profiles;

/// <summary>Hồ sơ tương thích cho một tiến trình.</summary>
public sealed class AppProfile
{
    public string Process { get; set; } = "";

    public SendMode SendMode { get; set; } = SendMode.Standard;

    /// <summary>Tự tắt bộ gõ trong ứng dụng này (game, ...).</summary>
    public bool Disabled { get; set; }

    public string? Note { get; set; }
}

public sealed class ProfileFile
{
    public int Version { get; set; } = 1;

    public List<AppProfile> Profiles { get; set; } = [];
}

/// <summary>Tập hồ sơ theo tiến trình, tra cứu theo tên tiến trình đã chuẩn hóa.</summary>
public sealed class ProfileSet
{
    private readonly Dictionary<string, AppProfile> _byProcess = new(StringComparer.OrdinalIgnoreCase);

    public ProfileSet(IEnumerable<AppProfile> profiles)
    {
        foreach (var p in profiles)
        {
            if (string.IsNullOrWhiteSpace(p.Process)) continue;
            _byProcess[AppConfig.NormalizeProcessName(p.Process)] = p;
        }
    }

    public static ProfileSet Empty { get; } = new([]);

    public IReadOnlyCollection<AppProfile> All => _byProcess.Values;

    public SendMode GetSendMode(string? process) =>
        process is not null && _byProcess.TryGetValue(process, out var p) ? p.SendMode : SendMode.Standard;

    public bool IsDisabled(string? process) =>
        process is not null && _byProcess.TryGetValue(process, out var p) && p.Disabled;

    /// <summary>
    /// Đọc profiles.json của người dùng; nếu chưa có thì dùng profiles.default.json đi kèm ứng dụng
    /// và sao chép ra để người dùng sửa.
    /// </summary>
    public static ProfileSet Load(string userPath, string defaultPath)
    {
        try
        {
            if (!File.Exists(userPath) && File.Exists(defaultPath))
            {
                System.IO.Directory.CreateDirectory(Path.GetDirectoryName(userPath)!);
                File.Copy(defaultPath, userPath);
            }

            var path = File.Exists(userPath) ? userPath : defaultPath;
            if (!File.Exists(path)) return Empty;
            return Parse(File.ReadAllText(path));
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            Diagnostics.Log.Error("Không đọc được profiles.json", ex);
            return Empty;
        }
    }

    public static ProfileSet Parse(string json)
    {
        var file = JsonSerializer.Deserialize<ProfileFile>(json, ConfigStore.JsonOptions) ?? new ProfileFile();
        return new ProfileSet(file.Profiles ?? []);
    }
}
