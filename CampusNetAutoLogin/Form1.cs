using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using System.Net;
using System.Text.Json;

namespace CampusNetAutoLogin;

public partial class Form1 : Form
{
    private readonly bool _autoMode;
    private readonly bool _silentMode;
    private readonly AppSettings _settings = AppSettings.Load();
    private readonly ListView _wifiList = new();
    private readonly WebView2 _browser = new();
    private readonly TextBox _ssid = new();
    private readonly TextBox _wifiPassword = new();
    private readonly TextBox _portalUsername = new();
    private readonly TextBox _portalPassword = new();
    private readonly TextBox _portalUrl = new();
    private readonly NumericUpDown _priority = new();
    private readonly CheckBox _networkAuto = new();
    private readonly CheckBox _autoStart = new();
    private readonly CheckBox _autoSubmit = new();
    private readonly Label _statusTitle = new();
    private readonly Label _statusDetail = new();
    private readonly Button _scanButton = new();
    private readonly Button _connectButton = new();
    private readonly TabControl _tabs = new();
    private readonly Dictionary<string, WifiNetwork> _detected = new(StringComparer.Ordinal);
    private SavedWifiProfile? _activePortalProfile;
    private TaskCompletionSource<bool>? _portalCompletion;
    private CancellationTokenSource? _operationCancellation;
    private bool _scriptRunning;

    private static readonly Color Navy = Color.FromArgb(17, 33, 61);
    private static readonly Color Blue = Color.FromArgb(37, 99, 235);
    private static readonly Color Teal = Color.FromArgb(13, 148, 136);
    private static readonly Color Orange = Color.FromArgb(217, 119, 6);
    private static readonly Color Red = Color.FromArgb(220, 38, 38);
    private static readonly Color Surface = Color.FromArgb(244, 247, 251);
    private static readonly Color Muted = Color.FromArgb(100, 116, 139);

    public Form1(bool autoMode = false, bool silentMode = false)
    {
        _autoMode = autoMode;
        _silentMode = silentMode;
        InitializeComponent();
        BuildInterface();
        Shown += FormShown;
        FormClosing += (_, _) => _operationCancellation?.Cancel();
    }

    private void InitializeComponent()
    {
        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = Surface;
        ClientSize = new Size(1220, 790);
        MinimumSize = new Size(1020, 680);
        StartPosition = FormStartPosition.CenterScreen;
        Text = "校园网 Wi-Fi 自动连接";
        Font = new Font("Microsoft YaHei UI", 9.5F);
    }

    private void BuildInterface()
    {
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1 };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 92));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 82));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var banner = new Panel { Dock = DockStyle.Fill, BackColor = Navy };
        banner.Controls.Add(new Label { Text = "Wi-Fi 自动连接与校园网认证", ForeColor = Color.White, Font = new Font("Microsoft YaHei UI", 19F, FontStyle.Bold), AutoSize = true, Location = new Point(28, 17) });
        banner.Controls.Add(new Label { Text = "扫描附近网络 · 失败自动切换 · 网页认证自动填写", ForeColor = Color.FromArgb(191, 219, 254), AutoSize = true, Location = new Point(31, 57) });
        var secure = new Label { Text = "密码由 Windows 当前用户加密", ForeColor = Color.FromArgb(167, 243, 208), AutoSize = true, Anchor = AnchorStyles.Top | AnchorStyles.Right, Location = new Point(950, 34) };
        banner.Resize += (_, _) => secure.Left = banner.ClientSize.Width - secure.Width - 28;
        banner.Controls.Add(secure);

        var status = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Margin = new Padding(20, 10, 20, 8) };
        _statusTitle.Text = "准备扫描附近 Wi-Fi";
        _statusTitle.Font = new Font("Microsoft YaHei UI", 11.5F, FontStyle.Bold);
        _statusTitle.ForeColor = Navy;
        _statusTitle.AutoSize = true;
        _statusTitle.Location = new Point(22, 10);
        _statusDetail.Text = "选择网络后可保存 Wi-Fi 密码和该网络对应的网页认证资料。";
        _statusDetail.ForeColor = Muted;
        _statusDetail.AutoSize = true;
        _statusDetail.Location = new Point(23, 39);
        status.Controls.AddRange([_statusTitle, _statusDetail]);

        var split = new SplitContainer { Dock = DockStyle.Fill, SplitterDistance = 440, SplitterWidth = 8, Margin = new Padding(20, 0, 20, 18), BackColor = Surface };
        split.Panel1.BackColor = Color.White;
        split.Panel2.BackColor = Color.White;
        BuildNetworkPanel(split.Panel1);
        BuildRightPanel(split.Panel2);

        root.Controls.Add(banner, 0, 0);
        root.Controls.Add(status, 0, 1);
        root.Controls.Add(split, 0, 2);
        Controls.Add(root);
    }

    private void BuildNetworkPanel(Control parent)
    {
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(16), RowCount = 4, ColumnCount = 1 };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));

        var top = new Panel { Dock = DockStyle.Fill };
        top.Controls.Add(new Label { Text = "附近和已保存的网络", Font = new Font("Microsoft YaHei UI", 13F, FontStyle.Bold), ForeColor = Navy, AutoSize = true, Location = new Point(0, 6) });
        _scanButton.Text = "重新扫描";
        StyleButton(_scanButton, Blue, Color.White);
        _scanButton.Size = new Size(96, 34);
        _scanButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _scanButton.Location = new Point(310, 0);
        top.Resize += (_, _) => _scanButton.Left = top.ClientSize.Width - _scanButton.Width;
        _scanButton.Click += async (_, _) => await ScanAsync();
        top.Controls.Add(_scanButton);

        _wifiList.Dock = DockStyle.Fill;
        _wifiList.View = View.Details;
        _wifiList.FullRowSelect = true;
        _wifiList.HideSelection = false;
        _wifiList.Columns.Add("网络名称", 185);
        _wifiList.Columns.Add("信号", 65);
        _wifiList.Columns.Add("安全", 70);
        _wifiList.Columns.Add("状态", 80);
        _wifiList.SelectedIndexChanged += (_, _) => LoadSelectedProfile();

        var buttons = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(0, 8, 0, 0) };
        buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        _connectButton.Text = "连接所选网络";
        StyleButton(_connectButton, Teal, Color.White);
        _connectButton.Margin = new Padding(0, 0, 5, 0);
        _connectButton.Click += async (_, _) => await ConnectSelectedAsync();
        var delete = new Button { Text = "删除已保存", Dock = DockStyle.Fill, Margin = new Padding(5, 0, 0, 0) };
        StyleButton(delete, Color.FromArgb(226, 232, 240), Navy);
        delete.Click += (_, _) => DeleteSelected();
        buttons.Controls.Add(_connectButton, 0, 0);
        buttons.Controls.Add(delete, 1, 0);

        _autoStart.Text = "登录 Windows 后自动扫描、连接和认证";
        _autoStart.Checked = _settings.AutoStart || StartupManager.IsEnabled();
        _autoStart.Dock = DockStyle.Fill;
        _autoStart.CheckedChanged += (_, _) => { _settings.AutoStart = _autoStart.Checked; _settings.Save(); StartupManager.SetEnabled(_autoStart.Checked); };

        layout.Controls.Add(top, 0, 0);
        layout.Controls.Add(_wifiList, 0, 1);
        layout.Controls.Add(buttons, 0, 2);
        layout.Controls.Add(_autoStart, 0, 3);
        parent.Controls.Add(layout);
    }

    private void BuildRightPanel(Control parent)
    {
        _tabs.Dock = DockStyle.Fill;
        var settingsTab = new TabPage("网络资料") { BackColor = Color.White, Padding = new Padding(22, 16, 22, 14) };
        var browserTab = new TabPage("校园网认证浏览器") { BackColor = Color.White, Padding = new Padding(4) };
        _tabs.TabPages.AddRange([settingsTab, browserTab]);

        var form = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 10 };
        form.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        form.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        form.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        for (int i = 1; i <= 7; i++) form.RowStyles.Add(new RowStyle(SizeType.Absolute, 66));
        form.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        form.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        form.Controls.Add(new Label { Text = "所选网络的连接与认证资料", Font = new Font("Microsoft YaHei UI", 13F, FontStyle.Bold), ForeColor = Navy, Dock = DockStyle.Fill }, 0, 0);
        form.SetColumnSpan(form.GetControlFromPosition(0, 0)!, 2);
        AddField(form, "Wi-Fi 名称（SSID）", _ssid, 1, 0);
        AddField(form, "尝试优先级（小的先试）", _priority, 1, 1);
        _wifiPassword.UseSystemPasswordChar = true;
        AddField(form, "Wi-Fi 密码（开放网络留空）", _wifiPassword, 2, 0);
        AddField(form, "网页认证入口", _portalUrl, 2, 1);
        AddField(form, "校园网用户名", _portalUsername, 3, 0);
        _portalPassword.UseSystemPasswordChar = true;
        AddField(form, "校园网密码", _portalPassword, 3, 1);

        _networkAuto.Text = "扫描到此网络时自动连接";
        _networkAuto.Checked = true;
        _networkAuto.Dock = DockStyle.Fill;
        _autoSubmit.Text = "网页识别成功后自动点击确认";
        _autoSubmit.Checked = _settings.AutoSubmit;
        _autoSubmit.Dock = DockStyle.Fill;
        form.Controls.Add(_networkAuto, 0, 4);
        form.Controls.Add(_autoSubmit, 1, 4);

        var save = new Button { Text = "保存或更新这个网络", Dock = DockStyle.Fill, Margin = new Padding(0, 8, 5, 8) };
        StyleButton(save, Blue, Color.White);
        save.Click += (_, _) => SaveProfile(true);
        var reveal = new Button { Text = "显示 / 隐藏两个密码", Dock = DockStyle.Fill, Margin = new Padding(5, 8, 0, 8) };
        StyleButton(reveal, Color.FromArgb(226, 232, 240), Navy);
        reveal.Click += (_, _) => { _wifiPassword.UseSystemPasswordChar = !_wifiPassword.UseSystemPasswordChar; _portalPassword.UseSystemPasswordChar = !_portalPassword.UseSystemPasswordChar; };
        form.Controls.Add(save, 0, 5);
        form.Controls.Add(reveal, 1, 5);

        var note = new Label
        {
            Text = "说明：普通 WPA/WPA2/WPA3 个人网络可直接保存密码。企业 802.1X 网络需先由 Windows 建好配置，本软件会复用；网页认证资料是每个 Wi-Fi 独立保存的。",
            ForeColor = Muted, Dock = DockStyle.Fill, AutoSize = false
        };
        form.Controls.Add(note, 0, 7);
        form.SetColumnSpan(note, 2);
        settingsTab.Controls.Add(form);

        _browser.Dock = DockStyle.Fill;
        _browser.DefaultBackgroundColor = Color.White;
        browserTab.Controls.Add(_browser);
        parent.Controls.Add(_tabs);
    }

    private static void AddField(TableLayoutPanel form, string label, Control control, int row, int column)
    {
        var panel = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, Padding = new Padding(column == 0 ? 0 : 5, 0, column == 0 ? 5 : 0, 4) };
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 23));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        panel.Controls.Add(new Label { Text = label, ForeColor = Muted, Dock = DockStyle.Fill }, 0, 0);
        control.Dock = DockStyle.Fill;
        if (control is TextBox box) { box.BorderStyle = BorderStyle.FixedSingle; box.Font = new Font("Microsoft YaHei UI", 10F); }
        if (control is NumericUpDown num) { num.Minimum = 1; num.Maximum = 999; num.Value = 10; }
        panel.Controls.Add(control, 0, 1);
        form.Controls.Add(panel, column, row);
    }

    private static void StyleButton(Button button, Color background, Color foreground)
    {
        button.BackColor = background;
        button.ForeColor = foreground;
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 0;
        button.Cursor = Cursors.Hand;
        button.Dock = button.Dock == DockStyle.None ? DockStyle.None : DockStyle.Fill;
    }

    private async void FormShown(object? sender, EventArgs e)
    {
        try
        {
            await EnsureBrowserAsync();
            if (_autoMode && _silentMode) { Opacity = 0; ShowInTaskbar = false; Hide(); }
            await ScanAsync();
            if (_settings.Networks.Count > 0) await RunAutoConnectAsync();
            else await RefreshNetworkStateAsync();
        }
        catch (Exception ex) { RevealWindow(); SetStatus("初始化失败", ex.Message, Red); }
    }

    private async Task EnsureBrowserAsync()
    {
        if (_browser.CoreWebView2 != null) return;
        string dataPath = Path.Combine(AppSettings.DataDirectory, "WebView2");
        Directory.CreateDirectory(dataPath);
        var environment = await CoreWebView2Environment.CreateAsync(null, dataPath, new CoreWebView2EnvironmentOptions("--no-proxy-server"));
        await _browser.EnsureCoreWebView2Async(environment);
        var core = _browser.CoreWebView2 ?? throw new InvalidOperationException("WebView2 初始化失败");
        core.Settings.AreDevToolsEnabled = false;
        core.Settings.IsStatusBarEnabled = false;
        try { core.Settings.IsPasswordAutosaveEnabled = false; core.Settings.IsGeneralAutofillEnabled = false; } catch { }
        core.NavigationCompleted += BrowserNavigationCompleted;
    }

    private async Task ScanAsync()
    {
        _scanButton.Enabled = false;
        SetStatus("正在扫描附近 Wi-Fi", "请稍候，Windows 正在更新可用网络列表…", Blue);
        try
        {
            var networks = await WifiManager.ScanAsync();
            _detected.Clear();
            foreach (var network in networks) _detected[network.Ssid] = network;
            RefreshNetworkList();
            SetStatus($"扫描完成：发现 {networks.Count} 个网络", $"当前连接：{WifiManager.GetCurrentSsid() ?? "未连接 Wi-Fi"}", Teal);
        }
        catch (Exception ex) { SetStatus("Wi-Fi 扫描失败", ex.Message, Red); }
        finally { _scanButton.Enabled = true; }
    }

    private void RefreshNetworkList(string? selectSsid = null)
    {
        selectSsid ??= _wifiList.SelectedItems.Count > 0 ? _wifiList.SelectedItems[0].Text : null;
        _wifiList.BeginUpdate();
        _wifiList.Items.Clear();
        var names = _detected.Keys.Union(_settings.Networks.Select(x => x.Ssid), StringComparer.Ordinal)
            .OrderBy(x => _settings.Networks.FirstOrDefault(p => p.Ssid == x)?.Priority ?? 999)
            .ThenByDescending(x => _detected.TryGetValue(x, out var n) ? n.Signal : -1);
        string? current = WifiManager.GetCurrentSsid();
        foreach (string name in names)
        {
            _detected.TryGetValue(name, out var network);
            bool saved = _settings.Networks.Any(x => x.Ssid == name);
            string state = string.Equals(current, name, StringComparison.Ordinal) ? "已连接" : saved ? "已保存" : "附近";
            var item = new ListViewItem([name, network == null ? "—" : $"{network.Signal}%", network == null ? "—" : network.SecurityEnabled ? "加密" : "开放", state]);
            item.Tag = name;
            if (saved) item.ForeColor = Blue;
            _wifiList.Items.Add(item);
            if (name == selectSsid) item.Selected = true;
        }
        _wifiList.EndUpdate();
    }

    private void LoadSelectedProfile()
    {
        if (_wifiList.SelectedItems.Count == 0) return;
        string ssid = _wifiList.SelectedItems[0].Text;
        _ssid.Text = ssid;
        var saved = _settings.Networks.FirstOrDefault(x => x.Ssid == ssid);
        _wifiPassword.Text = Unprotect(saved?.EncryptedWifiPassword);
        _portalUsername.Text = saved?.PortalUsername ?? "";
        _portalPassword.Text = Unprotect(saved?.EncryptedPortalPassword);
        _portalUrl.Text = saved?.PortalUrl ?? "http://10.200.84.3/a79.htm";
        _priority.Value = Math.Clamp(saved?.Priority ?? 10, 1, 999);
        _networkAuto.Checked = saved?.AutoConnect ?? true;
        _autoSubmit.Checked = _settings.AutoSubmit;
    }

    private bool SaveProfile(bool announce)
    {
        string ssid = _ssid.Text.Trim();
        if (string.IsNullOrWhiteSpace(ssid)) { SetStatus("请选择或填写 Wi-Fi 名称", "先扫描并选择网络，或手动填写 SSID。", Orange); return false; }
        _detected.TryGetValue(ssid, out var detected);
        bool secure = detected?.SecurityEnabled ?? !string.IsNullOrEmpty(_wifiPassword.Text);
        var saved = _settings.Networks.FirstOrDefault(x => x.Ssid == ssid);
        if (saved == null) { saved = new SavedWifiProfile { Ssid = ssid }; _settings.Networks.Add(saved); }
        saved.EncryptedWifiPassword = string.IsNullOrEmpty(_wifiPassword.Text) ? saved.EncryptedWifiPassword : CredentialProtection.Protect(_wifiPassword.Text);
        saved.SecurityEnabled = secure;
        saved.Authentication = detected?.Authentication ?? saved.Authentication;
        saved.PortalUsername = _portalUsername.Text.Trim();
        saved.EncryptedPortalPassword = string.IsNullOrEmpty(_portalPassword.Text) ? saved.EncryptedPortalPassword : CredentialProtection.Protect(_portalPassword.Text);
        saved.PortalUrl = _portalUrl.Text.Trim();
        saved.Priority = (int)_priority.Value;
        saved.AutoConnect = _networkAuto.Checked;
        _settings.AutoSubmit = _autoSubmit.Checked;
        _settings.Save();
        StartupManager.SetEnabled(_settings.AutoStart);
        RefreshNetworkList(ssid);
        if (announce) SetStatus($"已保存 {ssid}", "Wi-Fi 与网页认证密码均已由当前 Windows 用户加密。", Teal);
        return true;
    }

    private void DeleteSelected()
    {
        if (_wifiList.SelectedItems.Count == 0) return;
        string ssid = _wifiList.SelectedItems[0].Text;
        _settings.Networks.RemoveAll(x => x.Ssid == ssid);
        _settings.Save();
        RefreshNetworkList();
        SetStatus($"已删除 {ssid} 的软件配置", "Windows 自身保存的 Wi-Fi 配置未被删除。", Orange);
    }

    private async Task ConnectSelectedAsync()
    {
        if (!SaveProfile(false)) return;
        var profile = _settings.Networks.First(x => x.Ssid == _ssid.Text.Trim());
        _operationCancellation?.Cancel();
        _operationCancellation = new CancellationTokenSource();
        _connectButton.Enabled = false;
        try
        {
            bool ok = await TryProfileAsync(profile, _operationCancellation.Token);
            if (!ok) SetStatus($"未能完成 {profile.Ssid} 的联网", "请检查 Wi-Fi 密码、信号或认证页面提示。", Orange);
        }
        finally { _connectButton.Enabled = true; RefreshNetworkList(profile.Ssid); }
    }

    private async Task RunAutoConnectAsync()
    {
        if (await CheckInternetAsync())
        {
            SetStatus("当前网络已经连通", $"Wi-Fi：{WifiManager.GetCurrentSsid() ?? "非无线连接"}，无需切换。", Teal);
            if (_silentMode) Close();
            return;
        }
        if (_settings.Networks.Count == 0)
        {
            RevealWindow();
            SetStatus("还没有保存的网络", "扫描后选择 Wi-Fi，填写资料并保存。", Orange);
            return;
        }

        _operationCancellation = new CancellationTokenSource();
        List<SavedWifiProfile> candidates = [];
        for (int attempt = 0; attempt < 10; attempt++)
        {
            string? current = WifiManager.GetCurrentSsid();
            candidates = _settings.Networks.Where(x => x.AutoConnect)
                .Where(x => _detected.ContainsKey(x.Ssid) || string.Equals(current, x.Ssid, StringComparison.Ordinal))
                .OrderBy(x => x.Priority)
                .ThenByDescending(x => _detected.TryGetValue(x.Ssid, out var n) ? n.Signal : 0)
                .ToList();
            if (candidates.Count > 0) break;

            SetStatus("等待无线网络就绪", $"开机网络准备中，正在进行第 {attempt + 1}/10 次检测…", Blue);
            await Task.Delay(5000, _operationCancellation.Token);
            await ScanAsync();
        }

        foreach (var profile in candidates)
        {
            if (await TryProfileAsync(profile, _operationCancellation.Token))
            {
                if (_silentMode) Close();
                return;
            }
            SetStatus($"{profile.Ssid} 失败，尝试下一个", "程序会按优先级继续尝试附近已保存网络。", Orange);
        }
        RevealWindow();
        SetStatus("所有已保存网络均未连通", candidates.Count == 0 ? "等待 50 秒后仍未发现已保存网络。" : "请检查密码、信号或网页认证资料。", Red);
    }

    private async Task<bool> TryProfileAsync(SavedWifiProfile profile, CancellationToken token)
    {
        bool alreadyConnected = string.Equals(WifiManager.GetCurrentSsid(), profile.Ssid, StringComparison.Ordinal);
        if (!alreadyConnected)
        {
            SetStatus($"正在连接 {profile.Ssid}", "正在将网络交给 Windows WLAN 服务…", Blue);
            _detected.TryGetValue(profile.Ssid, out var detected);
            var connected = await WifiManager.ConnectAsync(profile, detected, token);
            if (!connected.Success)
            {
                SetStatus($"{profile.Ssid} 连接失败", connected.Message, Orange);
                return false;
            }
            RefreshNetworkList(profile.Ssid);
        }
        else
        {
            SetStatus($"已连接 {profile.Ssid}", "正在检测是否可以直连外网…", Blue);
        }

        if (await CheckInternetAsync())
        {
            SetStatus($"{profile.Ssid} 已连通外网", "无需网页认证。", Teal);
            return true;
        }
        if (string.IsNullOrWhiteSpace(profile.PortalUsername) || string.IsNullOrEmpty(profile.EncryptedPortalPassword))
        {
            SetStatus($"{profile.Ssid} 需要网页认证", "该网络尚未保存校园网用户名或密码。", Orange);
            RevealWindow();
            return false;
        }
        return await AuthenticatePortalAsync(profile, token);
    }
    private async Task<bool> AuthenticatePortalAsync(SavedWifiProfile profile, CancellationToken token)
    {
        await EnsureBrowserAsync();
        _activePortalProfile = profile;
        _portalCompletion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _scriptRunning = false;
        string url = Uri.TryCreate(profile.PortalUrl, UriKind.Absolute, out _) ? profile.PortalUrl : "http://www.msftconnecttest.com/redirect";
        SetStatus($"{profile.Ssid} 正在网页认证", "即将自动填写用户名和密码并确认。", Blue);
        _browser.CoreWebView2.Navigate(url);
        using var registration = token.Register(() => _portalCompletion.TrySetCanceled(token));
        _ = MonitorPortalInternetAsync(_portalCompletion, token);
        Task completed = await Task.WhenAny(_portalCompletion.Task, Task.Delay(TimeSpan.FromSeconds(45), token));
        if (completed != _portalCompletion.Task) { _portalCompletion.TrySetResult(false); RevealWindow(); }
        bool result = await _portalCompletion.Task;
        _activePortalProfile = null;
        return result;
    }

    private async void BrowserNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        if (_activePortalProfile == null || _portalCompletion == null || _scriptRunning || !e.IsSuccess) return;
        _scriptRunning = true;
        try
        {
            await Task.Delay(700);
            string script = PortalAutomation.BuildScript(_activePortalProfile.PortalUsername,
                Unprotect(_activePortalProfile.EncryptedPortalPassword), _settings.AutoSubmit);
            string raw = await _browser.CoreWebView2.ExecuteScriptAsync(script);
            string json = JsonSerializer.Deserialize<string>(raw) ?? "{}";
            using var doc = JsonDocument.Parse(json);
            string status = doc.RootElement.TryGetProperty("status", out var value) ? value.GetString() ?? "" : "";
            if (status == "submitted") SetStatus("网页资料已填写并确认", "正在等待外网连通…", Blue);
            else if (status.StartsWith("filled", StringComparison.Ordinal))
            {
                SetStatus("网页资料已自动填写", "请在认证浏览器中检查并手动确认。", Orange);
                RevealWindow();
                _tabs.SelectedIndex = 1;
            }
            else
            {
                SetStatus("未识别到标准认证表单", "已打开认证浏览器，可手动处理验证码或特殊选项。", Orange);
                RevealWindow();
                _tabs.SelectedIndex = 1;
            }
        }
        catch (Exception ex) { SetStatus("网页自动填写失败", ex.Message, Red); RevealWindow(); }
        finally { _scriptRunning = false; }
    }

    private async Task MonitorPortalInternetAsync(TaskCompletionSource<bool> completion, CancellationToken token)
    {
        for (int i = 0; i < 22 && !completion.Task.IsCompleted; i++)
        {
            await Task.Delay(2000, token);
            if (await CheckInternetAsync())
            {
                SetStatus("校园网认证成功", "外网已经直连可用。", Teal);
                completion.TrySetResult(true);
                return;
            }
        }
        completion.TrySetResult(false);
    }

    private async Task RefreshNetworkStateAsync()
    {
        if (await CheckInternetAsync()) SetStatus("当前网络已连通", $"Wi-Fi：{WifiManager.GetCurrentSsid() ?? "非无线连接"}", Teal);
    }

    private async Task<bool> CheckInternetAsync()
    {
        try
        {
            using var handler = new HttpClientHandler { UseProxy = false, AllowAutoRedirect = false, AutomaticDecompression = DecompressionMethods.All };
            using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(5) };
            using var response = await client.GetAsync(_settings.ProbeUrl);
            if (response.StatusCode != HttpStatusCode.OK) return false;
            return (await response.Content.ReadAsStringAsync()).Contains("Microsoft Connect Test", StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }

    private static string Unprotect(string? encrypted)
    {
        if (string.IsNullOrEmpty(encrypted)) return "";
        try { return CredentialProtection.Unprotect(encrypted); } catch { return ""; }
    }

    private void RevealWindow()
    {
        if (InvokeRequired) { BeginInvoke(RevealWindow); return; }
        Opacity = 1;
        ShowInTaskbar = true;
        Show();
        WindowState = FormWindowState.Normal;
        Activate();
    }

    private void SetStatus(string title, string detail, Color color)
    {
        if (InvokeRequired) { BeginInvoke(() => SetStatus(title, detail, color)); return; }
        _statusTitle.Text = title;
        _statusDetail.Text = detail;
        _statusTitle.ForeColor = color == Teal ? Teal : color == Red ? Red : Navy;
        AppLog.Write($"{title} | {detail}");
    }
}
