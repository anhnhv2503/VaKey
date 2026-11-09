# VaKey

Bộ gõ tiếng Việt cho Windows 10/11 — nhẹ, nhanh, miễn phí, mã nguồn mở. Trải nghiệm tương tự Unikey.

- Telex và VNI, xuất Unicode dựng sẵn (NFC)
- Chuyển Việt/Anh bằng `Ctrl+Shift` (đổi được: `Alt+Z`, `Ctrl+Space`...)
- Kiểm tra chính tả: không biến đổi từ tiếng Anh như `window`, `class`, `password`
- Nhớ trạng thái Việt/Anh theo từng ứng dụng, danh sách ứng dụng loại trừ
- Đặt dấu kiểu mới (`hòa`) hoặc kiểu cũ (`hoà`)
- Không lưu nội dung gõ, không kết nối mạng ngầm

## Cài đặt

- **Installer**: tải `VaKey-x.y.z-setup-win-x64.exe` từ trang Releases và chạy. Mặc định cài cho người dùng hiện tại,
  không cần quyền Admin; có tùy chọn "Khởi động cùng Windows". Gỡ cài đặt trong Settings → Apps.
- **Portable**: giải nén `VaKey-x.y.z-portable-win-x64.zip`, chạy `VaKey.exe`. Cấu hình lưu trong thư mục `Data` cạnh exe.

Cả hai bản đều đã kèm .NET runtime, không cần cài thêm gì.

## Sử dụng

Mở VaKey sẽ hiện **bảng điều khiển**:

- Nút lớn **V Tiếng Việt / E English** — bấm để chuyển chế độ.
- **Điều khiển**: bảng mã, kiểu gõ (Telex/VNI), phím chuyển (Ctrl+Shift, Alt+Z hoặc tự đặt).
- **Mở rộng**: đặt dấu kiểu mới/cũ, kiểm tra chính tả, nhớ theo ứng dụng, khởi động cùng Windows, quyền Admin...
- **Đóng** ẩn bảng (VaKey vẫn chạy ở khay hệ thống), **Kết thúc** thoát hẳn.

Mọi thay đổi có hiệu lực ngay. Mở lại VaKey từ Start menu (hoặc click phải biểu tượng khay → Bảng điều khiển) để hiện lại bảng.

Biểu tượng ở khay hệ thống: **V** xanh = tiếng Việt, **E** đỏ = tiếng Anh.
Click trái để chuyển chế độ, click phải để mở menu.

### Telex

| Gõ        | Ra  |     | Gõ    | Ra        |
| --------- | --- | --- | ----- | --------- |
| `aa`      | â   |     | `s`   | sắc (á)   |
| `aw`      | ă   |     | `f`   | huyền (à) |
| `ee`      | ê   |     | `r`   | hỏi (ả)   |
| `oo`      | ô   |     | `x`   | ngã (ã)   |
| `ow`      | ơ   |     | `j`   | nặng (ạ)  |
| `uw`, `w` | ư   |     | `z`   | xóa dấu   |
| `dd`      | đ   |     | `uow` | ươ        |

Gõ lặp để lấy lại ký tự gốc: `ss` → `s`, `aaa` → `aa`, `ww` → `w`.
Ví dụ: `Vieetj Nam` → Việt Nam, `nguoiwf` → người, `dduwowcj` → được.

### VNI

| Phím     | 1   | 2     | 3   | 4   | 5    | 6     | 7   | 8   | 9   | 0       |
| -------- | --- | ----- | --- | --- | ---- | ----- | --- | --- | --- | ------- |
| Tác dụng | sắc | huyền | hỏi | ngã | nặng | â ê ô | ơ ư | ă   | đ   | xóa dấu |

Ví dụ: `Vie65t Nam` → Việt Nam, `nguoi72` → người.

## Xử lý sự cố

| Vấn đề                                            | Cách xử lý                                                                                   |
| ------------------------------------------------- | -------------------------------------------------------------------------------------------- |
| Không gõ được trong một ứng dụng chạy quyền Admin | Menu → **Chạy lại với quyền Admin**, hoặc Cài đặt → Hệ thống → "Khởi động với quyền Admin".  |
| Dấu bị gõ đúp (`aâ`, `ááá`)                       | Đang có bộ gõ khác chạy (Unikey, EVKey, OpenKey, bộ gõ Windows). Tắt bộ gõ kia.              |
| Từ tiếng Anh bị đổi (`this` → `thí`)              | Thêm từ vào "Từ tiếng Anh không biến đổi" trong Cài đặt, hoặc chuyển sang chế độ E.          |
| Trong game toàn màn hình bị gõ dấu                | Menu → "Luôn tắt trong game.exe".                                                            |
| Ô địa chỉ trình duyệt sinh ký tự thừa             | Đổi chế độ gửi của trình duyệt thành `Selection` trong `profiles.json`.                      |
| Remote Desktop / máy ảo mất ký tự                 | Chế độ `Slow` (đã bật sẵn cho mstsc, VMware, VirtualBox).                                    |
| Antivirus cảnh báo                                | Bộ gõ dùng hook bàn phím giống mọi bộ gõ khác. Mã nguồn công khai; bản phát hành được ký số. |

Báo lỗi: tạo GitHub Issue theo mẫu, kèm chuỗi phím đã gõ và tên ứng dụng. Có thể bật log chẩn đoán
(Cài đặt → Hệ thống); log **không** chứa nội dung gõ.

## Phát triển

```powershell
dotnet build VaKey.sln
dotnet test VaKey.sln
dotnet run --project src/VaKey.Win
winget install JRSoftware.InnoSetup         # một lần, để tạo file cài đặt
./build/publish.ps1 -Version 1.0.0          # artifacts/: setup.exe + portable zip
pwsh ./build/make-icon.ps1                  # tạo lại biểu tượng ứng dụng
```

Tài liệu: [đặc tả quy tắc gõ](docs/typing-spec.md) · [kiến trúc](docs/architecture.md) ·
[bảng tương thích](docs/compatibility.md) · [kế hoạch](CLAUDE.md)

Mỗi lỗi được báo cần thêm thành một test hồi quy (`tests/VaKey.Core.Tests/Data/telex.csv` hoặc `vni.csv`) trước khi sửa.

## Giấy phép

MIT

## Tác giả

Nguyen Ha Viet Anh - BugMaker
