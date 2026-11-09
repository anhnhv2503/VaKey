# Bảng tương thích ứng dụng

Trạng thái: ✅ đạt · ⚠️ đạt với hồ sơ đặc biệt · ❌ lỗi · ⏳ chưa kiểm tra.

> Bảng này được điền khi chạy ma trận kiểm thử thủ công (Giai đoạn 2 và 4). Bộ test tự động chỉ kiểm tra
> engine và logic hook với bàn phím giả; hook thật bỏ qua phím inject nên không thể tự động gõ thử trong app thật.

Kịch bản cho mỗi ứng dụng: gõ đoạn văn mẫu 500 từ (Telex và VNI), gõ chữ hoa/Caps Lock, Backspace giữa từ,
click chuột giữa từ, chuyển Việt/Anh, gõ trong ô địa chỉ/ô tìm kiếm.

| Nhóm | Ứng dụng | Tiến trình | Win 10 | Win 11 | Chế độ gửi | Ghi chú |
| --- | --- | --- | --- | --- | --- | --- |
| Trình duyệt | Chrome | chrome.exe | ⏳ | ⏳ | Standard | Kiểm tra ô địa chỉ (tự hoàn thành), Google Docs |
| | Edge | msedge.exe | ⏳ | ⏳ | Standard | |
| | Firefox | firefox.exe | ⏳ | ⏳ | Standard | |
| | Brave | brave.exe | ⏳ | ⏳ | Standard | |
| Văn phòng | Word | winword.exe | ⏳ | ⏳ | Standard | Tắt AutoCorrect nếu bị đổi chữ |
| | Excel | excel.exe | ⏳ | ⏳ | Standard | Gõ trực tiếp vào ô |
| | PowerPoint | powerpnt.exe | ⏳ | ⏳ | Standard | |
| | LibreOffice | soffice.bin | ⏳ | ⏳ | Standard | |
| Soạn thảo / IDE | Notepad | notepad.exe | ⏳ | ⏳ | Standard | |
| | VS Code | code.exe | ⏳ | ⏳ | Standard | Gợi ý code có thể chen vào |
| | Visual Studio | devenv.exe | ⏳ | ⏳ | Standard | |
| | JetBrains | idea64.exe, rider64.exe | ⏳ | ⏳ | Standard | |
| Chat | Zalo | zalo.exe | ⏳ | ⏳ | Standard | |
| | Telegram | telegram.exe | ⏳ | ⏳ | Standard | |
| | Messenger | messenger.exe | ⏳ | ⏳ | Standard | |
| | Discord | discord.exe | ⏳ | ⏳ | Standard | |
| | Slack | slack.exe | ⏳ | ⏳ | Standard | |
| Terminal | Windows Terminal | windowsterminal.exe | ⏳ | ⏳ | Standard | |
| | cmd / PowerShell | conhost.exe | ⏳ | ⏳ | Standard | |
| Khác | Remote Desktop | mstsc.exe | ⏳ | ⏳ | Slow | Hồ sơ mặc định |
| | VMware / VirtualBox | vmware.exe, virtualboxvm.exe | ⏳ | ⏳ | Slow | Hồ sơ mặc định |
| | Ghi chú Windows | | ⏳ | ⏳ | Standard | |

## Đổi chế độ gửi cho một ứng dụng

Sửa `%AppData%\VaKey\profiles.json` (mở bằng Cài đặt → Hệ thống → "Mở thư mục cấu hình"):

```json
{ "process": "chrome.exe", "sendMode": "Selection" }
```

Sau đó mở Cài đặt và bấm Áp dụng (hoặc khởi động lại VaKey).
