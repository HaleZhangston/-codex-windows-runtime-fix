using System.Runtime.InteropServices;
using System.Security;
using System.Text;

namespace CampusNetAutoLogin;

internal sealed record WifiNetwork(string Ssid, int Signal, bool SecurityEnabled, uint Authentication, uint Cipher, string ExistingProfile);

internal static class WifiManager
{
    private const uint ClientVersion = 2;
    private const uint AvailableIncludeAllAdhoc = 1;
    private const uint ProfileUser = 2;
    private const uint InterfaceOpcodeCurrentConnection = 7;

    public static async Task<List<WifiNetwork>> ScanAsync(CancellationToken cancellationToken = default)
    {
        return await Task.Run(async () =>
        {
            using var client = OpenClient();
            var interfaces = GetInterfaces(client.Handle);
            foreach (var item in interfaces)
            {
                Guid scanId = item.Id;
                WlanScan(client.Handle, ref scanId, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
            }
            await Task.Delay(1400, cancellationToken);

            var results = new Dictionary<string, WifiNetwork>(StringComparer.Ordinal);
            foreach (var item in interfaces)
            {
                IntPtr list = IntPtr.Zero;
                try
                {
                    Guid id = item.Id;
                    Check(WlanGetAvailableNetworkList(client.Handle, ref id, AvailableIncludeAllAdhoc, IntPtr.Zero, out list),
                        "读取附近 Wi-Fi");
                    int count = Marshal.ReadInt32(list);
                    long cursor = list.ToInt64() + 8;
                    int size = Marshal.SizeOf<WLAN_AVAILABLE_NETWORK>();
                    for (int i = 0; i < count; i++)
                    {
                        var network = Marshal.PtrToStructure<WLAN_AVAILABLE_NETWORK>(new IntPtr(cursor + (long)i * size));
                        string ssid = DecodeSsid(network.Dot11Ssid);
                        if (string.IsNullOrWhiteSpace(ssid)) continue;
                        var value = new WifiNetwork(ssid, (int)Math.Min(network.SignalQuality, 100),
                            network.SecurityEnabled != 0, network.DefaultAuthAlgorithm,
                            network.DefaultCipherAlgorithm, network.ProfileName ?? "");
                        if (!results.TryGetValue(ssid, out var old) || value.Signal > old.Signal) results[ssid] = value;
                    }
                }
                finally { if (list != IntPtr.Zero) WlanFreeMemory(list); }
            }
            return results.Values.OrderByDescending(x => x.Signal).ThenBy(x => x.Ssid).ToList();
        }, cancellationToken);
    }

    public static async Task<(bool Success, string Message)> ConnectAsync(
        SavedWifiProfile saved, WifiNetwork? detected, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(saved.Ssid)) return (false, "SSID 为空");
        using var client = OpenClient();
        var interfaces = GetInterfaces(client.Handle);
        if (interfaces.Count == 0) return (false, "没有可用的无线网卡");
        string password = CredentialProtection.Unprotect(saved.EncryptedWifiPassword);
        uint auth = detected?.Authentication ?? saved.Authentication;
        bool secure = detected?.SecurityEnabled ?? saved.SecurityEnabled;

        foreach (var item in interfaces)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string profileName = saved.Ssid;
            if (!secure || !string.IsNullOrEmpty(password))
            {
                string xml = BuildProfileXml(saved.Ssid, password, secure, auth, detected?.Cipher ?? 4);
                Guid id = item.Id;
                uint result = WlanSetProfile(client.Handle, ref id, ProfileUser, xml, null, true,
                    IntPtr.Zero, out uint reason);
                if (result != 0)
                    return (false, $"Windows 无法保存 Wi-Fi 配置（错误 {result}/{reason}）");
            }

            var parameters = new WLAN_CONNECTION_PARAMETERS
            {
                ConnectionMode = 0,
                Profile = profileName,
                Dot11BssType = 3,
                Flags = 0
            };
            Guid connectId = item.Id;
            uint connectResult = WlanConnect(client.Handle, ref connectId, ref parameters, IntPtr.Zero);
            if (connectResult != 0) continue;

            for (int wait = 0; wait < 24; wait++)
            {
                await Task.Delay(500, cancellationToken);
                if (string.Equals(GetCurrentSsid(client.Handle, item.Id), saved.Ssid, StringComparison.Ordinal))
                    return (true, $"已连接 {saved.Ssid}");
            }
        }
        return (false, $"连接 {saved.Ssid} 超时或密码不正确");
    }

    public static string? GetCurrentSsid()
    {
        try
        {
            using var client = OpenClient();
            foreach (var item in GetInterfaces(client.Handle))
            {
                string? ssid = GetCurrentSsid(client.Handle, item.Id);
                if (!string.IsNullOrWhiteSpace(ssid)) return ssid;
            }
        }
        catch { }
        return null;
    }

    private static string? GetCurrentSsid(IntPtr handle, Guid id)
    {
        IntPtr data = IntPtr.Zero;
        try
        {
            uint size;
            uint valueType;
            uint result = WlanQueryInterface(handle, ref id, InterfaceOpcodeCurrentConnection, IntPtr.Zero,
                out size, out data, out valueType);
            if (result != 0 || data == IntPtr.Zero) return null;
            var attributes = Marshal.PtrToStructure<WLAN_CONNECTION_ATTRIBUTES>(data);
            return attributes.State == 1 ? DecodeSsid(attributes.Association.Dot11Ssid) : null;
        }
        finally { if (data != IntPtr.Zero) WlanFreeMemory(data); }
    }

    private static string BuildProfileXml(string ssid, string password, bool secure, uint auth, uint cipher)
    {
        string escapedSsid = SecurityElement.Escape(ssid) ?? ssid;
        string hex = Convert.ToHexString(Encoding.UTF8.GetBytes(ssid));
        if (!secure)
            return $"<?xml version=\"1.0\"?><WLANProfile xmlns=\"http://www.microsoft.com/networking/WLAN/profile/v1\"><name>{escapedSsid}</name><SSIDConfig><SSID><hex>{hex}</hex><name>{escapedSsid}</name></SSID></SSIDConfig><connectionType>ESS</connectionType><connectionMode>auto</connectionMode><MSM><security><authEncryption><authentication>open</authentication><encryption>none</encryption><useOneX>false</useOneX></authEncryption></security></MSM></WLANProfile>";

        string authentication = auth switch { 4 => "WPAPSK", 9 => "WPA3SAE", _ => "WPA2PSK" };
        string encryption = cipher == 2 ? "TKIP" : "AES";
        string escapedPassword = SecurityElement.Escape(password) ?? password;
        return $"<?xml version=\"1.0\"?><WLANProfile xmlns=\"http://www.microsoft.com/networking/WLAN/profile/v1\"><name>{escapedSsid}</name><SSIDConfig><SSID><hex>{hex}</hex><name>{escapedSsid}</name></SSID></SSIDConfig><connectionType>ESS</connectionType><connectionMode>auto</connectionMode><MSM><security><authEncryption><authentication>{authentication}</authentication><encryption>{encryption}</encryption><useOneX>false</useOneX></authEncryption><sharedKey><keyType>passPhrase</keyType><protected>false</protected><keyMaterial>{escapedPassword}</keyMaterial></sharedKey></security></MSM></WLANProfile>";
    }

    private static List<(Guid Id, string Description)> GetInterfaces(IntPtr handle)
    {
        IntPtr list = IntPtr.Zero;
        try
        {
            Check(WlanEnumInterfaces(handle, IntPtr.Zero, out list), "枚举无线网卡");
            int count = Marshal.ReadInt32(list);
            long cursor = list.ToInt64() + 8;
            int size = Marshal.SizeOf<WLAN_INTERFACE_INFO>();
            var result = new List<(Guid, string)>();
            for (int i = 0; i < count; i++)
            {
                var value = Marshal.PtrToStructure<WLAN_INTERFACE_INFO>(new IntPtr(cursor + (long)i * size));
                result.Add((value.InterfaceGuid, value.InterfaceDescription ?? ""));
            }
            return result;
        }
        finally { if (list != IntPtr.Zero) WlanFreeMemory(list); }
    }

    private static WlanClientHandle OpenClient()
    {
        Check(WlanOpenHandle(ClientVersion, IntPtr.Zero, out _, out IntPtr handle), "打开 Windows WLAN 服务");
        return new WlanClientHandle(handle);
    }

    private static string DecodeSsid(DOT11_SSID value)
    {
        if (value.SSID == null || value.SSIDLength == 0) return "";
        int length = (int)Math.Min(value.SSIDLength, 32);
        try { return Encoding.UTF8.GetString(value.SSID, 0, length); }
        catch { return Encoding.Default.GetString(value.SSID, 0, length); }
    }

    private static void Check(uint result, string action)
    {
        if (result != 0) throw new InvalidOperationException($"{action}失败（Windows 错误 {result}）");
    }

    private sealed class WlanClientHandle : IDisposable
    {
        public IntPtr Handle { get; }
        public WlanClientHandle(IntPtr handle) => Handle = handle;
        public void Dispose() { if (Handle != IntPtr.Zero) WlanCloseHandle(Handle, IntPtr.Zero); }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WLAN_INTERFACE_INFO
    {
        public Guid InterfaceGuid;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string InterfaceDescription;
        public uint InterfaceState;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DOT11_SSID
    {
        public uint SSIDLength;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)] public byte[] SSID;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WLAN_AVAILABLE_NETWORK
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string ProfileName;
        public DOT11_SSID Dot11Ssid;
        public uint Dot11BssType;
        public uint NumberOfBssids;
        public int NetworkConnectable;
        public uint NotConnectableReason;
        public uint NumberOfPhyTypes;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)] public uint[] PhyTypes;
        public int MorePhyTypes;
        public uint SignalQuality;
        public int SecurityEnabled;
        public uint DefaultAuthAlgorithm;
        public uint DefaultCipherAlgorithm;
        public uint Flags;
        public uint Reserved;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WLAN_CONNECTION_PARAMETERS
    {
        public uint ConnectionMode;
        [MarshalAs(UnmanagedType.LPWStr)] public string Profile;
        public IntPtr Dot11Ssid;
        public IntPtr DesiredBssidList;
        public uint Dot11BssType;
        public uint Flags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WLAN_ASSOCIATION_ATTRIBUTES
    {
        public DOT11_SSID Dot11Ssid;
        public uint Dot11BssType;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 6)] public byte[] Dot11Bssid;
        public uint Dot11PhyType;
        public uint Dot11PhyIndex;
        public uint SignalQuality;
        public uint RxRate;
        public uint TxRate;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WLAN_CONNECTION_ATTRIBUTES
    {
        public uint State;
        public uint ConnectionMode;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string ProfileName;
        public WLAN_ASSOCIATION_ATTRIBUTES Association;
        public uint SecurityEnabled;
        public int OneXEnabled;
        public uint AuthAlgorithm;
        public uint CipherAlgorithm;
    }

    [DllImport("wlanapi.dll")]
    private static extern uint WlanOpenHandle(uint clientVersion, IntPtr reserved, out uint negotiatedVersion, out IntPtr clientHandle);
    [DllImport("wlanapi.dll")]
    private static extern uint WlanCloseHandle(IntPtr clientHandle, IntPtr reserved);
    [DllImport("wlanapi.dll")]
    private static extern uint WlanEnumInterfaces(IntPtr clientHandle, IntPtr reserved, out IntPtr interfaceList);
    [DllImport("wlanapi.dll")]
    private static extern uint WlanScan(IntPtr clientHandle, ref Guid interfaceGuid, IntPtr dot11Ssid, IntPtr ieData, IntPtr reserved);
    [DllImport("wlanapi.dll")]
    private static extern uint WlanGetAvailableNetworkList(IntPtr clientHandle, ref Guid interfaceGuid, uint flags, IntPtr reserved, out IntPtr availableNetworkList);
    [DllImport("wlanapi.dll", CharSet = CharSet.Unicode)]
    private static extern uint WlanSetProfile(IntPtr clientHandle, ref Guid interfaceGuid, uint flags, string profileXml, string? allUserProfileSecurity, bool overwrite, IntPtr reserved, out uint reasonCode);
    [DllImport("wlanapi.dll", CharSet = CharSet.Unicode)]
    private static extern uint WlanConnect(IntPtr clientHandle, ref Guid interfaceGuid, ref WLAN_CONNECTION_PARAMETERS connectionParameters, IntPtr reserved);
    [DllImport("wlanapi.dll")]
    private static extern uint WlanQueryInterface(IntPtr clientHandle, ref Guid interfaceGuid, uint opcode, IntPtr reserved, out uint dataSize, out IntPtr data, out uint opcodeValueType);
    [DllImport("wlanapi.dll")]
    private static extern void WlanFreeMemory(IntPtr memory);
}
