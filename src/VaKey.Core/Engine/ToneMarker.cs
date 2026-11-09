using VaKey.Core.Data;
using VaKey.Core.Settings;

namespace VaKey.Core.Engine;

/// <summary>Chọn nguyên âm mang dấu thanh theo quy tắc chính tả (mục 3.3 của đặc tả).</summary>
public static class ToneMarker
{
    /// <summary>Chỉ số ký tự trong <paramref name="w"/> nhận dấu thanh, hoặc -1 nếu không có nguyên âm.</summary>
    public static int FindTonePosition(ReadOnlySpan<VChar> w, in SyllableParts p, ToneStyle style)
    {
        int n = p.VowelLength;
        int vs = p.VowelStart;
        if (n == 0) return -1;
        if (n == 1) return vs;

        // Ưu tiên nguyên âm mang dấu phụ (ươ → ơ, iê → ê, ưa → ư).
        int lastMarked = -1;
        for (int k = vs; k < vs + n; k++)
        {
            if (w[k].Mark != Mark.None) lastMarked = k;
        }

        if (lastMarked >= 0) return lastMarked;

        if (n == 2)
        {
            if (p.HasFinal) return vs + 1;          // toán, hoàng, huýnh

            char a = w[vs].Base, b = w[vs + 1].Base;
            bool special = (a, b) is ('o', 'a') or ('o', 'e') or ('u', 'y');
            if (special) return style == ToneStyle.Old ? vs + 1 : vs; // hoà/hòa, thuý/thúy

            return vs;                               // mía, múa, tài, cháo
        }

        return vs + 1;                               // ngoài, khuỷu, ngoẹo
    }
}
