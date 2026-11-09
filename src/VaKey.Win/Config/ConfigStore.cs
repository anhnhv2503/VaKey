using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace VaKey.Win.Config;

/// <summary>Đọc/ghi config.json. Ghi nguyên tử (file tạm + đổi tên), tự nâng cấp phiên bản cấu hình.</summary>
public sealed class ConfigStore
{
    public const string PortableMarker = "portable.txt";

    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public ConfigStore(string directory)
    {
        Directory = directory;
    }

    public string Directory { get; }

    public string ConfigPath => Path.Combine(Directory, "config.json");

    public string ProfilesPath => Path.Combine(Directory, "profiles.json");

    public string LogDirectory => Path.Combine(Directory, "logs");

    /// <summary>Bản portable nếu có file portable.txt cạnh exe; khi đó cấu hình nằm trong thư mục Data.</summary>
    public static ConfigStore CreateDefault()
    {
        var exeDir = AppContext.BaseDirectory;
        if (File.Exists(Path.Combine(exeDir, PortableMarker)))
        {
            return new ConfigStore(Path.Combine(exeDir, "Data"));
        }

        return new ConfigStore(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VaKey"));
    }

    public bool IsPortable => Path.GetFullPath(Directory)
        .StartsWith(Path.GetFullPath(AppContext.BaseDirectory), StringComparison.OrdinalIgnoreCase);

    /// <summary>false ở lần chạy đầu tiên (chưa từng lưu cấu hình).</summary>
    public bool Exists => File.Exists(ConfigPath);

    public AppConfig Load()
    {
        if (!File.Exists(ConfigPath)) return new AppConfig();

        try
        {
            var node = JsonNode.Parse(File.ReadAllText(ConfigPath), documentOptions: new JsonDocumentOptions
            {
                CommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
            }) as JsonObject ?? throw new JsonException("Cấu hình không phải object");

            Migrate(node);
            var config = node.Deserialize<AppConfig>(JsonOptions) ?? new AppConfig();
            config.AppStates = new Dictionary<string, bool>(config.AppStates ?? [], StringComparer.OrdinalIgnoreCase);
            config.ExcludedApps ??= [];
            config.EnglishExceptions ??= [];
            config.Version = AppConfig.CurrentVersion;
            return config;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            // Cấu hình hỏng: giữ lại bản sao để người dùng kiểm tra, dùng mặc định.
            TryBackupCorrupted();
            Diagnostics.Log.Error("Không đọc được config.json, dùng mặc định", ex);
            return new AppConfig();
        }
    }

    public void Save(AppConfig config)
    {
        System.IO.Directory.CreateDirectory(Directory);
        config.Version = AppConfig.CurrentVersion;
        var tmp = ConfigPath + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(config, JsonOptions));
        File.Move(tmp, ConfigPath, overwrite: true);
    }

    /// <summary>Nâng cấp cấu hình cũ lên phiên bản hiện tại. Thêm bước mới khi tăng CurrentVersion.</summary>
    internal static void Migrate(JsonObject node)
    {
        int version = node["version"]?.GetValue<int>() ?? node["Version"]?.GetValue<int>() ?? 0;

        if (version < 1)
        {
            // Phiên bản 0 (bản thử nghiệm) dùng "method" thay vì "inputMethod".
            if (node["method"] is { } method && node["inputMethod"] is null)
            {
                node.Remove("method");
                node["inputMethod"] = method.DeepClone();
            }
        }

        node["version"] = AppConfig.CurrentVersion;
    }

    private void TryBackupCorrupted()
    {
        try
        {
            File.Copy(ConfigPath, Path.Combine(Directory, "config.bad.json"), overwrite: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
