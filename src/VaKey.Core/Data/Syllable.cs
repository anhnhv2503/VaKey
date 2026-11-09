namespace VaKey.Core.Data;

/// <summary>Một ký tự trong buffer: chữ gốc (thường), dấu phụ, chữ hoa. Thanh điệu thuộc về cả âm tiết.</summary>
public struct VChar
{
    public char Base;
    public Mark Mark;
    public bool Upper;

    /// <summary>Ký tự 'ư' sinh ra từ phím 'w' đứng riêng (Telex), để 'ww' hoàn tác về 'w'.</summary>
    public bool FromW;

    public VChar(char baseLower, Mark mark = Mark.None, bool upper = false)
    {
        Base = baseLower;
        Mark = mark;
        Upper = upper;
        FromW = false;
    }

    public readonly bool IsVowel => VowelTable.IsVowel(Base);

    public static VChar FromKey(char key) => new(char.ToLowerInvariant(key), Mark.None, char.IsUpper(key));

    public override readonly string ToString() => VowelTable.Compose(Base, Mark, Tone.None, Upper).ToString();
}

/// <summary>Vị trí các thành phần âm tiết trong buffer: [phụ âm đầu][cụm nguyên âm][âm cuối].</summary>
public readonly record struct SyllableParts(int InitialLength, int VowelStart, int VowelLength, int Length)
{
    public int FinalStart => VowelStart + VowelLength;
    public bool HasFinal => FinalStart < Length;
    public bool HasVowel => VowelLength > 0;
}

public static class Syllable
{
    /// <summary>
    /// Tách âm tiết. "qu" luôn là phụ âm đầu; "gi" là phụ âm đầu khi theo sau là một nguyên âm khác
    /// (gia, giường), còn "gì", "gìn" thì 'i' là nguyên âm.
    /// </summary>
    public static SyllableParts Parse(ReadOnlySpan<VChar> w)
    {
        int n = w.Length;
        int i = 0;
        while (i < n && !w[i].IsVowel) i++;

        int init = i;
        if (init == 1 && w[0].Base == 'q' && i < n && w[i].Base == 'u' && w[i].Mark == Mark.None)
        {
            init = 2;
        }
        else if (init == 1 && w[0].Base == 'g' && i + 1 < n && w[i].Base == 'i' && w[i].Mark == Mark.None && w[i + 1].IsVowel)
        {
            init = 2;
        }

        int j = init;
        while (j < n && w[j].IsVowel) j++;
        return new SyllableParts(init, init, j - init, n);
    }
}
