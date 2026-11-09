# Kiến trúc VaKey

```
VaKey.sln
├─ src/VaKey.Core      Engine gõ, thuần C# (net10.0), không phụ thuộc Windows
├─ src/VaKey.Win       Ứng dụng WinForms (net10.0-windows): hook, SendInput, tray, cài đặt
├─ tests/VaKey.Core.Tests   xUnit: quy tắc gõ, dữ liệu CSV, khứ hồi, hiệu năng
└─ tests/VaKey.Win.Tests    xUnit: KeyProcessor (với bàn phím/ô soạn thảo giả), phím tắt, cấu hình, hồ sơ
```

## VaKey.Core

| Tệp | Vai trò |
| --- | --- |
| `Data/VowelTable.cs` | Bảng nguyên âm × dấu phụ × thanh điệu (NFC), `Compose`/`TryDecompose` |
| `Data/Syllable.cs` | `VChar`, tách âm tiết (`qu`, `gi`) |
| `Validation/SyllableValidator.cs` | Phụ âm đầu, bảng vần, ràng buộc chính tả |
| `Engine/ITypingMethod.cs`, `TelexMethod.cs`, `VniMethod.cs` | Ánh xạ phím → `KeyAction` |
| `Engine/ToneMarker.cs` | Chọn nguyên âm mang dấu thanh |
| `Engine/InputSession.cs` | Buffer từ đang gõ, áp dụng phím, hoàn tác, kiểm tra chính tả, tính `EngineResult` |
| `Settings/EngineOptions.cs` | Kiểu gõ, kiểu dấu, `w`→`ư`, chính tả, ngoại lệ tiếng Anh |

`InputSession` không có trạng thái toàn cục; mỗi phiên độc lập. Hiệu năng: vài micro giây mỗi phím (test
`ProcessingIsFast`).

## VaKey.Win

```
                ┌──────────────── Luồng UI (WinForms) ────────────────┐
                │ VaKeyApp: NotifyIcon, menu, SettingsForm, ConfigStore │
                └──────────┬──────────────────────────▲────────────────┘
               HookThread.Invoke(...)        StateChanged/Toggled (Post về UI)
                ┌──────────▼──────────────────────────┴────────────────┐
                │ HookThread (STA, message loop riêng, ưu tiên cao)     │
                │  WH_KEYBOARD_LL ─► KeyProcessor.OnKey ─► InputSession │
                │  WH_MOUSE_LL    ─► OnMouseDown (reset)                │
                │  WinEvent FOREGROUND ─► OnForegroundChanged           │
                │  WM_TIMER 5s ─► kiểm tra hook còn sống, cài lại       │
                └──────────┬───────────────────────────────────────────┘
                           ▼
                KeySender.SendInput (Backspace + KEYEVENTF_UNICODE, gửi theo gói)
```

- **Mọi** thao tác lên `InputSession`/`KeyProcessor` diễn ra trên luồng hook → không cần khóa.
  Luồng UI gửi lệnh qua `HookThread.Invoke` (hàng đợi + `PostThreadMessage`).
- Phím do VaKey gửi mang `dwExtraInfo = 0x56414B59` ("VAKY") và cờ `LLKHF_INJECTED` → hook bỏ qua.
- Phím bị nuốt thì phím nhả tương ứng cũng bị nuốt.
- Lỗi trong callback không bao giờ làm treo bàn phím: bắt ngoại lệ, reset, chuyển tiếp phím.
- **Hook bị Windows gỡ** (callback quá `LowLevelHooksTimeout`): mỗi 5 giây so sánh `GetLastInputInfo` với lần cuối
  hook nhận sự kiện; nếu có input mà hook không thấy → cài lại (cài mới trước, gỡ cũ sau).

### Chế độ gửi phím (`profiles.json`)

| Chế độ | Cách gửi | Dùng cho |
| --- | --- | --- |
| Standard | Backspace×N + Unicode trong một `SendInput` | Mặc định |
| Slow | Backspace, nghỉ 4 ms, rồi ký tự | Remote Desktop, máy ảo, app chậm |
| Selection | Shift+Left×N rồi gõ đè | Ô địa chỉ có tự hoàn thành |
| Clipboard | Backspace, Ctrl+V, khôi phục clipboard sau 300 ms | Phương án cuối |

### Phím tắt

- Chỉ phím bổ trợ (mặc định `Ctrl+Shift`): kích hoạt khi **nhả**, nếu giữa chừng không có phím nào khác
  (để `Ctrl+Shift+T` vẫn hoạt động bình thường).
- Phím bổ trợ + phím (`Alt+Z`, `Ctrl+Space`): kích hoạt khi nhấn, phím bị nuốt; với Alt/Win gửi thêm một phím giả
  để ứng dụng không mở menu.

### Cấu hình

- `%AppData%\VaKey\config.json` (bản portable: `Data\config.json` cạnh exe nếu có `portable.txt`).
- Có trường `version`; `ConfigStore.Migrate` nâng cấp cấu hình cũ. File hỏng được sao lưu thành `config.bad.json`.
- Ghi nguyên tử: file tạm rồi đổi tên.

### Quyền riêng tư

- Buffer chỉ chứa từ hiện tại, xóa khi kết thúc từ. Không ghi phím ra đĩa.
- Log chẩn đoán tắt mặc định, chỉ ghi sự kiện hệ thống.
- Không có kết nối mạng nào trừ khi người dùng bấm "Kiểm tra cập nhật" (chỉ đọc số phiên bản từ GitHub).
