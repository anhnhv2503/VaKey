# Đặc tả quy tắc gõ VaKey

Tài liệu này mô tả chính xác hành vi của `VaKey.Core`. Mọi quy tắc ở đây đều có test tương ứng trong
`tests/VaKey.Core.Tests`. Khi sửa hành vi, cập nhật tài liệu và test cùng lúc.

## 1. Mô hình

- Engine giữ **từ đang gõ** dưới dạng danh sách ký tự `VChar { chữ gốc, dấu phụ, chữ hoa }`.
- **Thanh điệu thuộc về cả âm tiết**, không gắn vào một ký tự. Vị trí dấu thanh được tính lại mỗi lần hiển thị,
  nên dấu tự di chuyển khi từ thay đổi (`hoán` + Backspace → `hóa`; `nguoif` + `w` → `người`).
- Engine cũng giữ **chuỗi phím gốc** để trả lại khi từ không phải tiếng Việt.
- Mỗi phím trả về `EngineResult { BackspaceCount, TextToInsert, SwallowKey }` — phần khác biệt tối thiểu
  giữa từ cũ và từ mới. Nếu chỉ là thêm đúng ký tự vừa gõ thì `SwallowKey = false` (phím gốc đi thẳng tới ứng dụng).

Âm tiết = `[phụ âm đầu] + [cụm nguyên âm] + [âm cuối]`:

- `qu` luôn là phụ âm đầu (`quả`: dấu trên `a`).
- `gi` là phụ âm đầu khi theo sau là nguyên âm khác (`gia`, `giường`); `gì`, `gìn` thì `i` là nguyên âm.

## 2. Telex

| Phím | Tác dụng | Ghi chú |
| --- | --- | --- |
| `aa` `ee` `oo` | `â` `ê` `ô` | Áp lên nguyên âm cùng chữ gần nhất trong cụm nguyên âm, kể cả khi đã gõ âm cuối (`tran` + `a` → `trân`). `ươ` + `o` → `uô`. |
| `aw` | `ă` | `oa` + `w` → `oă` (`hoặc`). |
| `ow` `uw` | `ơ` `ư` | `uo` + `w` → `ươ`. Nếu `uo` ở cuối từ thì thành `uơ` (`thuở`, `huơ`) và tự chuyển thành `ươ` khi gõ tiếp âm cuối (`tuơ` + `n` → `tươn`). `ua` + `w` → `ưa` (`mưa`). |
| `w` đứng riêng | `ư` | Khi từ chưa có nguyên âm (`w` → `ư`, `nhw` → `như`). Tắt được trong cài đặt. |
| `dd` | `đ` | Áp lên `d` đầu từ. |
| `s f r x j` | sắc, huyền, hỏi, ngã, nặng | Cần có nguyên âm; không thì là chữ thường. |
| `z` | xóa dấu thanh | Không có dấu thanh thì là chữ thường. |

### Gõ lặp (hoàn tác)

Gõ lại phím biến đổi khi biến đổi đó đã có sẵn → bỏ biến đổi và thêm ký tự gốc; từ đó trở đi từ hiện tại
được gõ nguyên văn: `ss`→`s` (`ass` → `as`), `aaa` → `aa`, `ddd` → `dd`, `ww` → `w`, `aww` → `aw`.

Nếu sau khi hoàn tác người dùng **gõ tiếp** và từ không còn là tiếng Việt, trả lại đúng chuỗi phím:
`password` → `password` (không phải `pasword`), `array` → `array`.

## 3. VNI

| Phím | Tác dụng |
| --- | --- |
| `1 2 3 4 5` | sắc, huyền, hỏi, ngã, nặng |
| `6` | mũ: nguyên âm `a/e/o` cuối cùng của cụm; `uo`/`ươ` → `uô` |
| `7` | móc: `uo` → `ươ` (cuối từ: `uơ`), nếu không thì `u`/`o` đầu tiên |
| `8` | trăng: `a` |
| `9` | `đ` |
| `0` | xóa dấu thanh |

Chữ số không áp dụng được thì là ký tự thường (`2024`, `x2`). Gõ lặp: `a11` → `a1`, `a66` → `a6`.

## 4. Đặt dấu thanh

Theo thứ tự ưu tiên:

1. Một nguyên âm → đặt lên nó.
2. Có nguyên âm mang dấu phụ (`ă â ê ô ơ ư`) → nguyên âm mang dấu phụ cuối cùng (`người` → `ơ`, `việt` → `ê`, `mưa` → `ư`, `rượu` → `ơ`).
3. Hai nguyên âm, có âm cuối → nguyên âm thứ hai (`toán`, `hoàng`, `huýnh`).
4. Hai nguyên âm, không âm cuối:
   - `oa`, `oe`, `uy`: **kiểu mới** → nguyên âm đầu (`hòa`, `khỏe`, `thủy`); **kiểu cũ** → nguyên âm thứ hai (`hoà`, `khoẻ`, `thuỷ`).
   - Còn lại → nguyên âm đầu (`mía`, `múa`, `tài`, `cháo`).
5. Ba nguyên âm → nguyên âm giữa (`ngoài`, `khuỷu`, `ngoẹo`).

## 5. Kiểm tra chính tả

Khi bật (mặc định), sau mỗi phím engine kiểm tra từ có thể là **tiền tố** của một âm tiết tiếng Việt:

- Phụ âm đầu thuộc `b c ch d đ g gh gi h k kh l m n ng ngh nh p ph qu r s t th tr v x` (đang gõ thì chấp nhận cả `q`).
- Vần (cụm nguyên âm + âm cuối) là tiền tố của một vần trong bảng `SyllableValidator.KnownRhymes`.
  Nguyên âm chưa có dấu phụ khớp với cả dạng có dấu (`uo` khớp `uô`, `ươ`).
- `k gh ngh` chỉ đứng trước `i e ê y`; `c ng` không đứng trước `i e ê y`; `g` không đứng trước `e`.
- Âm cuối tắc `c ch p t` chỉ đi với thanh sắc hoặc nặng.

Xử lý:

- Phím biến đổi làm từ sai chính tả → coi phím đó là chữ thường.
- Từ trở nên sai chính tả → **trả về đúng chuỗi phím gốc** và gõ nguyên văn đến hết từ
  (`window`: `ư` → `ưi` → `ưin` sai → `win` → `window`).

### Giới hạn đã biết

Từ tiếng Anh trùng với âm tiết tiếng Việt hợp lệ thì không thể tự nhận ra (Unikey cũng vậy):
`this` → `thí`, `these` → `thế`, `tax` → `tã`, `if` → `ì`, `var` → `vả`. Cách xử lý:

- Thêm từ vào danh sách **"Từ tiếng Anh không biến đổi"** trong cài đặt (`EngineOptions.EnglishExceptions`), hoặc
- Chuyển sang chế độ tiếng Anh khi gõ văn bản tiếng Anh dài.

Danh sách đầy đủ được kiểm thử: `tests/VaKey.Core.Tests/Data/english-collisions.txt`.

## 6. Kết thúc từ

Engine reset khi nhận ký tự không thuộc từ (Space, dấu câu, chữ số với Telex). Phần Windows reset thêm khi:
Enter, Tab, phím mũi tên, Home/End, Delete, Esc, click chuột, đổi cửa sổ, tổ hợp có Ctrl/Alt/Win.

## 7. Backspace

Xóa một ký tự hiển thị khỏi buffer. Dấu thanh được đặt lại đúng chỗ (`hoán` → `hóa`); nếu không còn nguyên âm
thì bỏ dấu thanh. Khi buffer rỗng, Backspace được chuyển tiếp nguyên vẹn.
