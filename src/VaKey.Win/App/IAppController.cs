using VaKey.Win.Config;
using VaKey.Win.Input;

namespace VaKey.Win.App;

/// <summary>Những gì cửa sổ giao diện được phép làm với ứng dụng (luôn gọi trên luồng UI).</summary>
internal interface IAppController
{
    AppConfig Config { get; }

    InputState State { get; }

    ConfigStore Store { get; }

    Icon AppIcon { get; }

    /// <summary>Phát trên luồng UI khi trạng thái Việt/Anh thay đổi.</summary>
    event Action<InputState>? StateChanged;

    /// <summary>Phát trên luồng UI sau khi cấu hình được áp dụng.</summary>
    event Action? ConfigChanged;

    void ApplyConfig(AppConfig config);

    void ToggleVietnamese();

    void ShowAdvancedSettings(IWin32Window owner);

    void ShowHelp(IWin32Window? owner);

    void ShowAbout(IWin32Window? owner);

    void Exit();
}
