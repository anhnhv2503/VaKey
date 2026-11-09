using System.Runtime.InteropServices;
using VaKey.Win.Config;
using static VaKey.Win.Native.NativeMethods;

namespace VaKey.Win.Input;

/// <summary>Tra tên tiến trình và quyền admin theo PID, có bộ nhớ đệm nhỏ.</summary>
internal sealed class ProcessInfoCache
{
    private const int MaxEntries = 256;
    private readonly Dictionary<uint, (string? Name, bool Elevated)> _cache = [];
    private readonly char[] _pathBuffer = new char[1024];

    public static bool SelfElevated { get; } = IsElevated(GetCurrentProcess(), ownsHandle: false) ?? false;

    public (string? Name, bool Elevated) Get(uint pid)
    {
        if (_cache.TryGetValue(pid, out var cached)) return cached;
        if (_cache.Count >= MaxEntries) _cache.Clear();

        string? name = null;
        bool elevated = false;
        var handle = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
        if (handle != IntPtr.Zero)
        {
            uint size = (uint)_pathBuffer.Length;
            if (QueryFullProcessImageNameW(handle, 0, _pathBuffer, ref size))
            {
                name = AppConfig.NormalizeProcessName(new string(_pathBuffer, 0, (int)size));
            }

            // Không mở được token của tiến trình khác thường nghĩa là nó chạy ở mức quyền cao hơn.
            elevated = IsElevated(handle, ownsHandle: true) ?? true;
        }

        var result = (name, elevated);
        _cache[pid] = result;
        return result;
    }

    private static bool? IsElevated(IntPtr process, bool ownsHandle)
    {
        try
        {
            if (!OpenProcessToken(process, TOKEN_QUERY, out var token)) return null;
            try
            {
                return GetTokenInformation(token, TokenElevation, out int value, sizeof(int), out _) ? value != 0 : null;
            }
            finally
            {
                CloseHandle(token);
            }
        }
        finally
        {
            if (ownsHandle) CloseHandle(process);
        }
    }

    internal static string Describe(int error) => Marshal.GetPInvokeErrorMessage(error);
}
