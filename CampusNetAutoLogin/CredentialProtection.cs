using System.Runtime.InteropServices;
using System.Text;

namespace CampusNetAutoLogin;

internal static class CredentialProtection
{
    [StructLayout(LayoutKind.Sequential)]
    private struct DataBlob
    {
        public int Size;
        public IntPtr Data;
    }

    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CryptProtectData(
        ref DataBlob dataIn, string description, IntPtr entropy,
        IntPtr reserved, IntPtr prompt, int flags, out DataBlob dataOut);

    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CryptUnprotectData(
        ref DataBlob dataIn, IntPtr description, IntPtr entropy,
        IntPtr reserved, IntPtr prompt, int flags, out DataBlob dataOut);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr LocalFree(IntPtr memory);

    private const int CryptProtectUiForbidden = 0x1;

    public static string Protect(string plainText)
    {
        if (string.IsNullOrEmpty(plainText)) return "";
        byte[] bytes = Encoding.UTF8.GetBytes(plainText);
        DataBlob input = ToBlob(bytes);
        try
        {
            if (!CryptProtectData(ref input, "CampusNetAutoLogin", IntPtr.Zero,
                    IntPtr.Zero, IntPtr.Zero, CryptProtectUiForbidden, out DataBlob output))
                throw new InvalidOperationException($"Windows 密码加密失败：{Marshal.GetLastWin32Error()}");
            try
            {
                byte[] protectedBytes = new byte[output.Size];
                Marshal.Copy(output.Data, protectedBytes, 0, output.Size);
                return Convert.ToBase64String(protectedBytes);
            }
            finally { LocalFree(output.Data); }
        }
        finally { Marshal.FreeHGlobal(input.Data); }
    }

    public static string Unprotect(string protectedText)
    {
        if (string.IsNullOrWhiteSpace(protectedText)) return "";
        byte[] bytes = Convert.FromBase64String(protectedText);
        DataBlob input = ToBlob(bytes);
        try
        {
            if (!CryptUnprotectData(ref input, IntPtr.Zero, IntPtr.Zero,
                    IntPtr.Zero, IntPtr.Zero, CryptProtectUiForbidden, out DataBlob output))
                throw new InvalidOperationException($"Windows 密码解密失败：{Marshal.GetLastWin32Error()}");
            try
            {
                byte[] plainBytes = new byte[output.Size];
                Marshal.Copy(output.Data, plainBytes, 0, output.Size);
                return Encoding.UTF8.GetString(plainBytes);
            }
            finally { LocalFree(output.Data); }
        }
        finally { Marshal.FreeHGlobal(input.Data); }
    }

    private static DataBlob ToBlob(byte[] bytes)
    {
        IntPtr data = Marshal.AllocHGlobal(bytes.Length);
        Marshal.Copy(bytes, 0, data, bytes.Length);
        return new DataBlob { Size = bytes.Length, Data = data };
    }
}
