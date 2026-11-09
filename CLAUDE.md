# VaKey - Kế hoạch xây dựng chi tiết

> Bộ gõ tiếng Việt - Anh cho Windows, nhẹ, nhanh, miễn phí, trải nghiệm tương tự Unikey.

| Mục                | Nội dung            |
| ------------------ | ------------------- |
| Tên dự án          | VaKey               |
| Nền tảng           | Windows 10/11 (x64) |
| Ngôn ngữ           | C# / .NET 10        |
| Phiên bản mục tiêu | 1.0                 |

---

## 1. Tổng quan

### 1.1 Mục tiêu

- Gõ tiếng Việt chính xác bằng Telex và VNI, xuất Unicode dựng sẵn (NFC).
- Chuyển Việt/Anh tức thì bằng phím tắt.
- Chạy nền nhẹ (RAM < 30 MB, độ trễ < 5 ms mỗi phím).
- Tương thích các ứng dụng phổ biến: Chrome, Brave, Edge, Word, Excel, VS Code, Zalo, Telegram, Notepad, terminal.

### 1.2 Phạm vi bản 1.0

**Có:**

- Kiểu gõ Telex, VNI
- Bảng mã Unicode
- Tray icon, phím tắt chuyển Việt/Anh
- Cửa sổ cài đặt, lưu cấu hình
- Tự khởi động cùng Windows
- Nhớ trạng thái Việt/Anh theo ứng dụng
- Kiểm tra chính tả cơ bản để không biến đổi từ tiếng Anh
- Installer và bản portable

**Chưa có (để sau):**

- macOS / Linux
- Bảng mã TCVN3, VNI-Win (chỉ chuyển mã qua clipboard ở giai đoạn mở rộng)
- Đồng bộ cấu hình qua cloud
- Gợi ý từ / dự đoán văn bản

### 1.3 Nguyên tắc thiết kế

1. **Engine tách biệt hoàn toàn khỏi Windows** để test tự động và tái dùng.
2. **Engine không có trạng thái toàn cục ẩn**, mọi thứ nằm trong một đối tượng `Session`.
3. **Mặc định an toàn**: khi không chắc chắn thì không biến đổi từ.
4. **Không có kết nối mạng ngầm**, không ghi lại nội dung đã gõ ra đĩa.

---

## 2. Kiến trúc

### 2.1 Cấu trúc solution

```
VaKey/
├─ VaKey.sln
├─ src/
│  ├─ VaKey.Core/                 # Engine gõ (thuần C#, netstandard/net8)
│  │  ├─ Engine/
│  │  │  ├─ InputSession.cs       # Buffer + trạng thái từ đang gõ
│  │  │  ├─ ITypingMethod.cs      # Giao diện kiểu gõ
│  │  │  ├─ TelexMethod.cs
│  │  │  ├─ VniMethod.cs
│  │  │  ├─ ToneMarker.cs         # Đặt dấu thanh
│  │  │  └─ EngineResult.cs       # Kết quả: số ký tự xóa + chuỗi chèn
│  │  ├─ Data/
│  │  │  ├─ VowelTable.cs         # Bảng nguyên âm + dấu
│  │  │  └─ Syllable.cs           # Phụ âm đầu, vần, phụ âm cuối
│  │  ├─ Validation/
│  │  │  └─ SyllableValidator.cs  # Kiểm tra âm tiết hợp lệ
│  │  └─ Settings/
│  │     └─ EngineOptions.cs
│  ├─ VaKey.Win/                  # Ứng dụng Windows
│  │  ├─ Native/                  # P/Invoke: hook, SendInput, user32
│  │  ├─ Input/                   # KeyboardHook, KeySender
│  │  ├─ App/                     # Tray, vòng đời ứng dụng
│  │  ├─ UI/                      # Cửa sổ cài đặt
│  │  ├─ Profiles/                # Cấu hình theo ứng dụng
│  │  └─ Config/                  # Đọc/ghi JSON
├─ tests/
│  ├─ VaKey.Core.Tests/           # xUnit cho engine
│  └─ VaKey.Win.Tests/            # Test phần logic không phụ thuộc UI
├─ docs/
│  ├─ typing-spec.md              # Đặc tả quy tắc gõ
│  ├─ compatibility.md            # Bảng tương thích ứng dụng
│  └─ architecture.md
├─ installer/                     # Script Inno Setup
└─ .github/workflows/             # CI
```

### 2.2 Luồng xử lý một phím

```
Người dùng nhấn phím
        │
        ▼
[WH_KEYBOARD_LL hook]
  - Phím inject? ──► bỏ qua, chuyển tiếp
  - Có Ctrl/Alt/Win? ──► reset buffer, chuyển tiếp
  - Chế độ Anh? ──► chuyển tiếp
        │
        ▼
[InputSession.Process(key)]
  - Cập nhật buffer
  - Áp dụng quy tắc Telex/VNI
  - Kiểm tra âm tiết hợp lệ
        │
        ▼
EngineResult { BackspaceCount, TextToInsert, SwallowKey }
        │
        ▼
[KeySender]
  - Gửi N Backspace
  - Gửi chuỗi Unicode bằng SendInput
  - Nuốt phím gốc nếu cần
```

### 2.3 Giao diện chính của Core

```csharp
public interface ITypingMethod
{
    // Trả về true nếu phím này là phím biến đổi (dấu, mũ, ...)
    bool IsTransformKey(char key);
}

public sealed class EngineResult
{
    public int BackspaceCount { get; init; }
    public string TextToInsert { get; init; } = "";
    public bool SwallowKey { get; init; }
}

public sealed class InputSession
{
    public EngineResult Process(char key);
    public EngineResult Backspace();
    public void Reset();                 // Kết thúc từ, đổi cửa sổ, click chuột
    public string CurrentWord { get; }
}
```

### 2.4 Mô hình âm tiết tiếng Việt

Một âm tiết gồm: **[phụ âm đầu] + [vần: âm đệm + nguyên âm chính + âm cuối] + [thanh điệu]**

- Phụ âm đầu: `b c ch d đ g gh gi h k kh l m n ng ngh nh p ph qu r s t th tr v x`
- Âm đệm: `o`, `u` (ví dụ `hoa`, `quê`)
- Nguyên âm chính: `a ă â e ê i o ô ơ u ư y` và nguyên âm đôi/ba (`ia, ua, ưa, iê, uô, ươ, ...`)
- Âm cuối: `c ch m n ng nh p t` hoặc bán nguyên âm `i y o u`
- Thanh điệu: ngang, sắc, huyền, hỏi, ngã, nặng

---

## 3. Đặc tả quy tắc gõ

### 3.1 Telex

| Gõ                | Ra               | Ghi chú                  |
| ----------------- | ---------------- | ------------------------ |
| `aa`              | `â`              |                          |
| `aw`              | `ă`              |                          |
| `ee`              | `ê`              |                          |
| `oo`              | `ô`              |                          |
| `ow`              | `ơ`              |                          |
| `uw`              | `ư`              |                          |
| `w` đứng riêng    | `ư`              | Có thể tắt trong cài đặt |
| `dd`              | `đ`              |                          |
| `s`               | sắc              |                          |
| `f`               | huyền            |                          |
| `r`               | hỏi              |                          |
| `x`               | ngã              |                          |
| `j`               | nặng             |                          |
| `z`               | xóa dấu thanh    |                          |
| `uow`             | `ươ`             | Gõ tắt                   |
| Lặp phím biến đổi | trả về ký tự gốc | `ss` → `s`, `aaa` → `aa` |

### 3.2 VNI

| Gõ  | Ra            |
| --- | ------------- |
| `1` | sắc           |
| `2` | huyền         |
| `3` | hỏi           |
| `4` | ngã           |
| `5` | nặng          |
| `6` | mũ (`â ê ô`)  |
| `7` | móc (`ơ ư`)   |
| `8` | trăng (`ă`)   |
| `9` | `đ`           |
| `0` | xóa dấu thanh |

### 3.3 Quy tắc đặt dấu thanh

Cho phép chọn 2 kiểu trong cài đặt:

| Từ          | Kiểu mới | Kiểu cũ |
| ----------- | -------- | ------- |
| hoà / hòa   | `hòa`    | `hoà`   |
| thuý / thúy | `thúy`   | `thuý`  |
| khoẻ / khỏe | `khỏe`   | `khoẻ`  |

Quy tắc chung:

1. Nếu chỉ có một nguyên âm, đặt dấu lên nó.
2. Nếu có nguyên âm mang dấu phụ (`ă â ê ô ơ ư`), ưu tiên đặt dấu lên đó (`người`, `được`).
3. Nguyên âm đôi/ba: đặt theo bảng quy tắc, có tùy chọn kiểu mới/cũ cho `oa, oe, uy`.
4. Có âm cuối: đặt dấu lên nguyên âm chính (`toán`, `quyết`).
5. `qu` và `gi` là phụ âm đầu, `u` và `i` không nhận dấu (`quả`, `giường`).

### 3.4 Kết thúc từ

Buffer được reset khi gặp: Space, Enter, Tab, dấu câu, phím mũi tên, Home/End, click chuột, đổi cửa sổ, tổ hợp phím có Ctrl/Alt/Win.

### 3.5 Backspace

- Backspace xóa một ký tự trong buffer, cập nhật trạng thái dấu tương ứng.
- Nếu người dùng gõ `viêt` rồi xóa `t`, buffer phải về đúng `viê`.

---

## 4. Các giai đoạn thực hiện

### Giai đoạn 0: Chuẩn bị (2-3 ngày)

**Công việc**

- [ ] Tạo repo GitHub, chọn giấy phép (gợi ý MIT hoặc GPLv3)
- [ ] Tạo solution và 3 project (Core, Win, Tests)
- [ ] Cấu hình CI: build + chạy test trên mỗi pull request
- [ ] Viết `docs/typing-spec.md` từ mục 3 của tài liệu này
- [ ] Thu thập dữ liệu test:
  - Danh sách âm tiết tiếng Việt hợp lệ (khoảng 7.000 âm tiết)
  - 500+ từ/cụm từ tiếng Việt thông dụng kèm chuỗi phím Telex và VNI
  - 300+ từ tiếng Anh dễ bị gõ nhầm (`window`, `process`, `where`, `static`, `class`, `tax`...)
- [ ] Chốt các quyết định kỹ thuật: .NET 8, WinForms, JSON config

**Đầu ra:** repo chạy được CI, tài liệu đặc tả, bộ dữ liệu test.

---

### Giai đoạn 1: Engine gõ (1-2 tuần)

**Tuần 1: Nền tảng**

- [ ] Bảng nguyên âm: mọi tổ hợp nguyên âm + dấu mũ/móc + dấu thanh (Unicode NFC)
- [ ] Hàm tách âm tiết: phụ âm đầu / vần / phụ âm cuối
- [ ] `InputSession` với buffer và `Reset()`
- [ ] `TelexMethod`: dấu mũ, móc, trăng, `dd`
- [ ] `ToneMarker`: đặt và xóa dấu thanh theo quy tắc mục 3.3
- [ ] `EngineResult`: tính số Backspace và chuỗi chèn tối thiểu

**Tuần 2: Hoàn thiện**

- [ ] `VniMethod`
- [ ] Hoàn tác khi gõ lặp (`ss`, `aaa`, `ddd`)
- [ ] Xử lý Backspace trong buffer
- [ ] `SyllableValidator`: không biến đổi khi từ không thể là âm tiết tiếng Việt
- [ ] Tùy chọn: kiểu đặt dấu mới/cũ, bật/tắt `w` thành `ư`, bật/tắt kiểm tra chính tả
- [ ] Test hồi quy lớn, so sánh kết quả với Unikey trên cùng bộ chuỗi phím

**Unit test bắt buộc (ví dụ)**

| Chuỗi phím (Telex) | Kết quả                   |
| ------------------ | ------------------------- |
| `Vieetj Nam`       | `Việt Nam`                |
| `nguoiwf`          | `người`                   |
| `dduwowcj`         | `được`                    |
| `hoaf`             | `hòa` (kiểu mới)          |
| `quar`             | `quả`                     |
| `giuwowngf`        | `giường`                  |
| `ass`              | `as`                      |
| `window`           | `window` (không biến đổi) |

**Tiêu chí hoàn thành**

- ≥ 95% test pass ngay khi kết thúc giai đoạn, 100% với các trường hợp thường gặp
- Mỗi lần xử lý phím dưới 1 ms trong benchmark
- Độ phủ code của Core ≥ 85%

---

### Giai đoạn 2: Hook bàn phím và xuất ký tự (1 tuần)

**Công việc**

- [ ] `KeyboardHook` dùng `WH_KEYBOARD_LL`, chạy trên thread riêng có message loop
- [ ] Bỏ qua phím inject (`LLKHF_INJECTED`) và phím có dấu hiệu từ chính VaKey (extra info riêng)
- [ ] `KeySender`:
  - Gửi Backspace bằng `SendInput`
  - Gửi ký tự Unicode bằng `KEYEVENTF_UNICODE`
  - Gửi theo gói (một lần `SendInput` cho nhiều sự kiện) để tránh chèn xen
- [ ] Xử lý modifier: Ctrl, Alt, Win → reset buffer và chuyển tiếp phím
- [ ] Chuyển trạng thái khi: đổi cửa sổ foreground (`SetWinEventHook`), click chuột (hook chuột), phím điều hướng
- [ ] Xử lý Caps Lock và Shift đúng cho chữ hoa (`VIỆT`, `Việt`)
- [ ] Cơ chế an toàn: nếu hook bị Windows gỡ do timeout, tự cài lại

**Thử nghiệm thủ công**
Notepad, Chrome (Google Docs), Word, VS Code, Zalo.

**Tiêu chí hoàn thành**

- Gõ đúng đoạn văn 500 từ liên tục trong 5 ứng dụng trên
- Không lặp ký tự, không mất ký tự, không treo bàn phím

---

### Giai đoạn 3: Giao diện và trải nghiệm (1 tuần)

**Công việc**

- [ ] Tray icon: biểu tượng `V` (xanh) / `E` (đỏ), tooltip, menu chuột phải
- [ ] Phím tắt chuyển Việt/Anh: mặc định `Ctrl+Shift`, cho phép đổi thành `Alt+Z`, `Ctrl+Space`...
- [ ] Cửa sổ cài đặt:
  - Kiểu gõ: Telex / VNI
  - Bảng mã: Unicode
  - Kiểu đặt dấu: mới / cũ
  - Kiểm tra chính tả: bật/tắt
  - Khởi động cùng Windows
  - Phím tắt
  - Danh sách ứng dụng loại trừ
- [ ] Nhớ trạng thái Việt/Anh theo từng ứng dụng
- [ ] Lưu cấu hình tại `%AppData%\VaKey\config.json` (kèm số phiên bản để nâng cấp cấu hình)
- [ ] Chỉ cho chạy một instance (Mutex)
- [ ] Âm báo hoặc thông báo nhỏ khi chuyển chế độ (tùy chọn)

**Tiêu chí hoàn thành:** bản alpha có thể dùng hằng ngày.

---

### Giai đoạn 4: Tương thích và độ ổn định (1-2 tuần)

**Công việc**

- [ ] Bảng tương thích theo tiến trình (`profiles.json`) với các chế độ gửi phím:
  - **Standard**: Backspace + Unicode (mặc định)
  - **Slow**: thêm độ trễ nhỏ giữa các sự kiện (cho app chậm)
  - **Selection**: chọn ký tự bằng Shift+Left rồi ghi đè (cho ô địa chỉ trình duyệt có autocomplete)
  - **Clipboard**: dán chuỗi (phương án cuối, khôi phục clipboard sau đó)
- [ ] Xử lý ô địa chỉ trình duyệt có gợi ý tự điền (dễ sinh ký tự thừa)
- [ ] Xử lý Remote Desktop, máy ảo, terminal (Windows Terminal, cmd, PowerShell)
- [ ] Danh sách app tắt bộ gõ tự động (game toàn màn hình, IDE tùy chọn)
- [ ] Ứng dụng chạy quyền Administrator: phát hiện và cảnh báo, hỗ trợ chạy VaKey bằng admin (tác vụ lên lịch khi đăng nhập)
- [ ] Kiểm tra rò rỉ bộ nhớ và handle khi chạy 8+ giờ
- [ ] Đo hiệu năng: độ trễ từ lúc nhấn phím đến lúc ký tự xuất hiện < 5 ms, RAM < 30 MB, CPU nhàn rỗi ≈ 0%
- [ ] Ghi log chẩn đoán (tắt mặc định, không lưu nội dung gõ)
- [ ] Thử trên Windows 10 và 11, DPI cao, nhiều màn hình, bố cục bàn phím khác

**Ma trận kiểm thử tối thiểu**

| Nhóm            | Ứng dụng                                           |
| --------------- | -------------------------------------------------- |
| Trình duyệt     | Chrome, Edge, Firefox                              |
| Văn phòng       | Word, Excel, PowerPoint, LibreOffice               |
| Soạn thảo / IDE | Notepad, VS Code, Visual Studio, JetBrains         |
| Chat            | Zalo, Telegram, Messenger, Discord, Slack          |
| Terminal        | Windows Terminal, cmd, PowerShell                  |
| Khác            | Remote Desktop, VMware/VirtualBox, Ghi chú Windows |

**Tiêu chí hoàn thành:** bản beta, không lỗi nghiêm trọng trên ma trận trên.

---

### Giai đoạn 5: Tính năng mở rộng (1-2 tuần, tùy chọn)

- [ ] **Gõ tắt (macro)**: bảng tự định nghĩa (`ko` → `không`), nhập/xuất file
- [ ] **Chuyển mã clipboard**: Unicode ↔ TCVN3 ↔ VNI-Win, có phím tắt
- [ ] **Khôi phục phím gốc** khi gõ sai (nhấn Esc hoặc Backspace ngay sau khi biến đổi để trở lại chuỗi phím ban đầu)
- [ ] **Tự viết hoa** đầu câu (tùy chọn)
- [ ] **Gõ tắt từ tiếng Anh** không bị biến đổi (từ điển ngoại lệ do người dùng thêm)
- [ ] **Chế độ tạm tắt** khi giữ một phím (ví dụ giữ Ctrl trái)

---

### Giai đoạn 6: Phát hành (1 tuần)

- [ ] Installer (Inno Setup) và bản portable (zip)
- [ ] Bản build self-contained hoặc framework-dependent (cân nhắc dung lượng)
- [ ] Ký số mã nguồn (code signing) để giảm cảnh báo SmartScreen/antivirus
- [ ] Nộp mẫu cho các hãng antivirus phổ biến để xác nhận không phải mã độc
- [ ] README: giới thiệu, ảnh/GIF demo, hướng dẫn cài đặt, bảng phím Telex/VNI
- [ ] Hướng dẫn xử lý sự cố thường gặp (không gõ được trong app admin, trong game...)
- [ ] Quy trình nhận lỗi: mẫu GitHub Issue, hướng dẫn thu thập log
- [ ] Kiểm tra cập nhật (đọc phiên bản từ GitHub Releases, không gửi dữ liệu người dùng)
- [ ] Phát hành bản beta cho 10-20 người dùng thử, thu phản hồi 1-2 tuần
- [ ] Phát hành 1.0

---

## 5. Lịch trình tổng quan

| Giai đoạn           | Thời gian    | Đầu ra                         |
| ------------------- | ------------ | ------------------------------ |
| 0. Chuẩn bị         | 2-3 ngày     | Repo, CI, đặc tả, dữ liệu test |
| 1. Engine           | 1-2 tuần     | VaKey.Core + test              |
| 2. Hook / SendInput | 1 tuần       | Gõ được trong các app cơ bản   |
| 3. UI               | 1 tuần       | Bản alpha                      |
| 4. Tương thích      | 1-2 tuần     | Bản beta                       |
| 5. Mở rộng          | 1-2 tuần     | Macro, chuyển mã               |
| 6. Phát hành        | 1 tuần       | VaKey 1.0                      |
| **Tổng**            | **6-9 tuần** |                                |

### Các mốc (milestone)

| Mốc              | Điều kiện đạt                                                      |
| ---------------- | ------------------------------------------------------------------ |
| M1 - Engine xong | Test Telex/VNI pass, benchmark đạt                                 |
| M2 - Gõ được     | Hook + SendInput chạy ổn trong 5 app                               |
| M3 - Alpha       | Có UI, cấu hình, dùng hằng ngày được                               |
| M4 - Beta        | Qua ma trận tương thích, người dùng thử không báo lỗi nghiêm trọng |
| M5 - 1.0         | Installer, ký số, tài liệu, quy trình hỗ trợ                       |

---

## 6. Chiến lược kiểm thử

| Loại              | Công cụ                       | Nội dung                                   |
| ----------------- | ----------------------------- | ------------------------------------------ |
| Unit test         | xUnit                         | Engine: từng quy tắc gõ, đặt dấu, hoàn tác |
| Test theo dữ liệu | xUnit Theory + file CSV       | Hàng nghìn cặp (chuỗi phím → kết quả)      |
| Property-based    | FsCheck (tùy chọn)            | Gõ rồi xóa luôn về đúng trạng thái trước   |
| Benchmark         | BenchmarkDotNet               | Thời gian xử lý mỗi phím, cấp phát bộ nhớ  |
| Tích hợp          | Script tự động hóa / thủ công | Gõ trong app thật theo ma trận             |
| Soak test         | Chạy 8-24 giờ                 | Rò rỉ bộ nhớ, hook bị gỡ                   |

Mỗi lỗi người dùng báo cần được thêm thành một test hồi quy trước khi sửa.

---

## 7. Rủi ro và biện pháp

| Rủi ro                                               | Mức độ     | Biện pháp                                                        |
| ---------------------------------------------------- | ---------- | ---------------------------------------------------------------- |
| Lỗi Backspace/Unicode ở một số ứng dụng              | Cao        | Hồ sơ theo tiến trình, nhiều chế độ gửi phím                     |
| Antivirus báo nhầm vì hook phím                      | Cao        | Mã nguồn mở, ký số, nộp mẫu xác nhận, không dùng mạng ngầm       |
| Đặt dấu sai ở trường hợp đặc biệt                    | Trung bình | Bộ test lớn, so sánh với Unikey                                  |
| Từ tiếng Anh bị biến đổi                             | Trung bình | Kiểm tra âm tiết, từ điển ngoại lệ                               |
| App quyền Admin không nhận phím                      | Trung bình | Phát hiện + cảnh báo, tùy chọn chạy admin                        |
| Hook bị Windows gỡ khi xử lý chậm                    | Trung bình | Xử lý nhanh, không làm việc nặng trong callback, tự cài lại hook |
| Xung đột với bộ gõ khác (Unikey, EVKey, Windows IME) | Thấp       | Phát hiện tiến trình xung đột và nhắc người dùng                 |
| Giới hạn của hook so với IME chính thống             | Dài hạn    | Nghiên cứu TSF (C++) cho phiên bản 2.x                           |

---

## 8. Bảo mật và quyền riêng tư

- Không lưu nội dung gõ ra đĩa, không ghi log phím.
- Buffer chỉ giữ từ hiện tại và bị xóa khi kết thúc từ.
- Không gửi dữ liệu qua mạng. Kiểm tra cập nhật chỉ đọc số phiên bản, có thể tắt.
- Không ghi nhận khi đang nhập vào ô mật khẩu (nhận diện cửa sổ/ô password nếu có thể, hoặc cho người dùng đặt danh sách loại trừ).
- Mã nguồn công khai để cộng đồng kiểm tra.

---

## 9. Đội ngũ và phân công (nếu làm nhóm)

| Vai trò           | Trách nhiệm                           |
| ----------------- | ------------------------------------- |
| Engine developer  | Core, test, so sánh với Unikey        |
| Windows developer | Hook, SendInput, tương thích ứng dụng |
| UI / UX           | Tray, cài đặt, icon, tài liệu         |
| QA                | Ma trận ứng dụng, soak test, báo lỗi  |

Một người làm có thể đi tuần tự theo các giai đoạn ở mục 4.

---

## 10. Tiêu chí hoàn thành bản 1.0

- [ ] Gõ đúng Telex và VNI trong ít nhất 10 ứng dụng phổ biến
- [ ] Độ trễ không cảm nhận được (< 5 ms), RAM < 30 MB
- [ ] Chuyển Việt/Anh tức thì, nhớ trạng thái theo ứng dụng
- [ ] Chạy liên tục 8+ giờ không treo, không mất phím, không rò rỉ bộ nhớ
- [ ] Có installer và bản portable, đã ký số
- [ ] Có tài liệu hướng dẫn và quy trình nhận lỗi
- [ ] Bộ test của Core pass 100%, độ phủ ≥ 85%

---

## 11. Lộ trình sau 1.0

| Phiên bản | Nội dung                                                                       |
| --------- | ------------------------------------------------------------------------------ |
| 1.1       | Macro nâng cao, nhập/xuất cấu hình, thêm hồ sơ ứng dụng                        |
| 1.2       | Chuyển mã đầy đủ, gõ tắt thông minh                                            |
| 2.0       | Nghiên cứu TSF để trở thành IME chính thống, ổn định hơn với mọi ứng dụng      |
| 3.0       | Tách Core để chạy trên macOS (CGEventTap/InputMethodKit) và Linux (IBus/Fcitx) |

---

## 12. Bước tiếp theo ngay lập tức

1. Tạo repo và solution, bật CI.
2. Viết `typing-spec.md` hoàn chỉnh từ mục 3.
3. Bắt đầu `VaKey.Core`: bảng nguyên âm và `InputSession` với vài test Telex đầu tiên (`aa`, `aw`, `dd`, `s f r x j`).
