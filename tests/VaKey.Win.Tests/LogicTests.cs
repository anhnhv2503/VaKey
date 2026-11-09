using System.Text.Json.Nodes;
using VaKey.Core.Settings;
using VaKey.Win.App;
using VaKey.Win.Config;
using VaKey.Win.Input;
using VaKey.Win.Profiles;

namespace VaKey.Win.Tests;

public class HotkeyTests
{
    [Theory]
    [InlineData("Ctrl+Shift", "Ctrl+Shift")]
    [InlineData("shift + ctrl", "Ctrl+Shift")]
    [InlineData("Alt+Z", "Alt+Z")]
    [InlineData("Ctrl+Space", "Ctrl+Space")]
    [InlineData("ctrl+alt+v", "Ctrl+Alt+V")]
    [InlineData("Win+F5", "Win+F5")]
    [InlineData("Ctrl+`", "Ctrl+`")]
    public void ParsesAndFormats(string text, string expected)
    {
        Assert.True(HotkeyDefinition.TryParse(text, out var h));
        Assert.Equal(expected, h.ToString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("Shift")]
    [InlineData("Z")]
    [InlineData("Ctrl+A+B")]
    [InlineData("Ctrl+Foo")]
    public void RejectsInvalid(string text) => Assert.False(HotkeyDefinition.TryParse(text, out _));

    [Fact]
    public void FallsBackToDefault() =>
        Assert.Equal(HotkeyDefinition.Default, HotkeyDefinition.ParseOrDefault("nonsense"));

    [Fact]
    public void AllPresetsParse()
    {
        foreach (var p in HotkeyDefinition.Presets) Assert.True(HotkeyDefinition.TryParse(p, out _), p);
    }
}

public class KeyTranslatorTests
{
    [Theory]
    [InlineData(0x41u, false, false, 'a')]
    [InlineData(0x41u, true, false, 'A')]
    [InlineData(0x41u, false, true, 'A')]
    [InlineData(0x41u, true, true, 'a')]
    [InlineData(0x35u, false, false, '5')]
    [InlineData(0x35u, true, false, '\0')]
    [InlineData(0x20u, false, false, '\0')]
    [InlineData(0xBEu, false, false, '\0')]
    public void Translates(uint vk, bool shift, bool caps, char expected) =>
        Assert.Equal(expected, KeyTranslator.Translate(vk, shift, caps));
}

public sealed class ConfigStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "vakey-test-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public void MissingFileGivesDefaults()
    {
        var c = new ConfigStore(_dir).Load();
        Assert.Equal(InputMethodKind.Telex, c.InputMethod);
        Assert.Equal("Ctrl+Shift", c.ToggleHotkey);
        Assert.True(c.SpellCheck);
    }

    [Fact]
    public void RoundTrips()
    {
        var store = new ConfigStore(_dir);
        var c = new AppConfig
        {
            InputMethod = InputMethodKind.Vni,
            ToneStyle = ToneStyle.Old,
            ExcludedApps = ["game.exe"],
            EnglishExceptions = ["tax"],
        };
        c.AppStates["code.exe"] = false;
        store.Save(c);

        var loaded = store.Load();
        Assert.Equal(InputMethodKind.Vni, loaded.InputMethod);
        Assert.Equal(ToneStyle.Old, loaded.ToneStyle);
        Assert.Equal(["game.exe"], loaded.ExcludedApps);
        Assert.False(loaded.AppStates["CODE.EXE"]);
        Assert.Equal(AppConfig.CurrentVersion, loaded.Version);
        Assert.Contains("\"inputMethod\": \"Vni\"", File.ReadAllText(store.ConfigPath));
    }

    [Fact]
    public void CorruptedFileIsBackedUp()
    {
        Directory.CreateDirectory(_dir);
        var store = new ConfigStore(_dir);
        File.WriteAllText(store.ConfigPath, "{ not json");
        var c = store.Load();
        Assert.Equal(InputMethodKind.Telex, c.InputMethod);
        Assert.True(File.Exists(Path.Combine(_dir, "config.bad.json")));
    }

    [Fact]
    public void MigratesVersionZero()
    {
        Directory.CreateDirectory(_dir);
        var store = new ConfigStore(_dir);
        File.WriteAllText(store.ConfigPath, """{ "method": "Vni", "spellCheck": false }""");
        var c = store.Load();
        Assert.Equal(InputMethodKind.Vni, c.InputMethod);
        Assert.False(c.SpellCheck);

        var node = new JsonObject { ["version"] = 1 };
        ConfigStore.Migrate(node);
        Assert.Equal(AppConfig.CurrentVersion, node["version"]!.GetValue<int>());
    }

    [Fact]
    public void EngineOptionsFromConfig()
    {
        var o = new AppConfig { InputMethod = InputMethodKind.Vni, SpellCheck = false, EnglishExceptions = ["tax"] }.ToEngineOptions();
        Assert.Equal(InputMethodKind.Vni, o.Method);
        Assert.False(o.SpellCheck);
        Assert.Contains("tax", o.EnglishExceptions);
    }
}

public class ProfileTests
{
    [Theory]
    [InlineData("Chrome", "chrome.exe")]
    [InlineData(@"C:\Apps\Code.EXE", "code.exe")]
    [InlineData(" zalo.exe ", "zalo.exe")]
    public void NormalizesProcessNames(string input, string expected) =>
        Assert.Equal(expected, AppConfig.NormalizeProcessName(input));

    [Fact]
    public void ParsesProfilesWithComments()
    {
        var set = ProfileSet.Parse("""
            {
              // comment
              "version": 1,
              "profiles": [
                { "process": "mstsc.exe", "sendMode": "Slow" },
                { "process": "game", "disabled": true },
              ]
            }
            """);
        Assert.Equal(SendMode.Slow, set.GetSendMode("mstsc.exe"));
        Assert.Equal(SendMode.Standard, set.GetSendMode("notepad.exe"));
        Assert.Equal(SendMode.Standard, set.GetSendMode(null));
        Assert.True(set.IsDisabled("game.exe"));
    }

    [Fact]
    public void BundledDefaultProfilesAreValid()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "profiles.default.json");
        var set = ProfileSet.Parse(File.ReadAllText(path));
        Assert.Equal(SendMode.Slow, set.GetSendMode("mstsc.exe"));
    }
}

public class UpdateCheckerTests
{
    [Theory]
    [InlineData("v1.2.3", "1.2.3")]
    [InlineData("1.0.0-beta.1", "1.0.0")]
    [InlineData(null, null)]
    [InlineData("garbage", null)]
    public void ParsesTags(string? tag, string? expected) =>
        Assert.Equal(expected is null ? null : Version.Parse(expected), UpdateChecker.ParseTag(tag));
}
