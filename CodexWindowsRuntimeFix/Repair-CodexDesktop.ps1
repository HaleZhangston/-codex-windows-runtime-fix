param(
    [switch]$SelfTest
)

$ErrorActionPreference = 'Stop'

function Get-RelativeFileList {
    param([Parameter(Mandatory)][string]$Root)

    Get-ChildItem -LiteralPath $Root -Recurse -File -Force -ErrorAction SilentlyContinue | ForEach-Object {
        $_.FullName.Substring($Root.Length).TrimStart('\')
    }
}

function Get-RuntimeIdFromStageName {
    param([Parameter(Mandatory)][string]$Name)

    $match = [regex]::Match($Name, '^\.staging-([^-]+)-')
    if (-not $match.Success) {
        throw "无法从 staging 目录名解析 runtime ID：$Name"
    }

    return $match.Groups[1].Value
}

function Test-LongPathByteCopy {
    $tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("codex-runtime-fix-selftest-" + [guid]::NewGuid().ToString('N'))
    $sourceRoot = Join-Path $tempRoot 'source'
    $targetRoot = Join-Path $tempRoot 'target'

    try {
        $segments = 1..14 | ForEach-Object { 'segment_' + ('x' * 18) }
        $relative = ($segments -join '\') + '\sample.js'
        $sourceFile = Join-Path $sourceRoot $relative
        $targetFile = Join-Path $targetRoot $relative
        $sourceDir = Split-Path -Parent $sourceFile
        $targetDir = Split-Path -Parent $targetFile

        [System.IO.Directory]::CreateDirectory("\\?\$sourceDir") | Out-Null
        [System.IO.Directory]::CreateDirectory("\\?\$targetDir") | Out-Null

        $payload = [System.Text.Encoding]::UTF8.GetBytes('codex-runtime-fix-self-test')
        [System.IO.File]::WriteAllBytes("\\?\$sourceFile", $payload)
        $bytes = [System.IO.File]::ReadAllBytes("\\?\$sourceFile")
        [System.IO.File]::WriteAllBytes("\\?\$targetFile", $bytes)

        if (-not [System.IO.File]::Exists("\\?\$targetFile")) {
            throw '长路径目标文件未创建。'
        }

        $copied = [System.IO.File]::ReadAllBytes("\\?\$targetFile")
        if ([System.Text.Encoding]::UTF8.GetString($copied) -ne 'codex-runtime-fix-self-test') {
            throw '长路径文件内容校验失败。'
        }
    }
    finally {
        if (Test-Path -LiteralPath $tempRoot) {
            Remove-Item -LiteralPath $tempRoot -Recurse -Force -ErrorAction SilentlyContinue
        }
    }
}

function Invoke-SelfTest {
    $sample = '.staging-440c4f095d41ea30-RcH05o'
    $runtimeId = Get-RuntimeIdFromStageName -Name $sample

    if ($runtimeId -ne '440c4f095d41ea30') {
        throw "runtime ID 解析测试失败：$runtimeId"
    }

    $tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("codex-runtime-list-selftest-" + [guid]::NewGuid().ToString('N'))
    try {
        New-Item -ItemType Directory -Path (Join-Path $tempRoot 'a\b') -Force | Out-Null
        Set-Content -LiteralPath (Join-Path $tempRoot 'a\b\one.txt') -Value '1' -Encoding UTF8
        Set-Content -LiteralPath (Join-Path $tempRoot 'two.txt') -Value '2' -Encoding UTF8

        $files = @(Get-RelativeFileList -Root $tempRoot)
        if ($files.Count -ne 2) {
            throw "相对路径枚举测试失败，文件数：$($files.Count)"
        }
        if ($files -notcontains 'a\b\one.txt' -or $files -notcontains 'two.txt') {
            throw '相对路径枚举测试失败。'
        }
    }
    finally {
        if (Test-Path -LiteralPath $tempRoot) {
            Remove-Item -LiteralPath $tempRoot -Recurse -Force -ErrorAction SilentlyContinue
        }
    }

    Test-LongPathByteCopy
    Write-Host 'Self-test passed.'
}

if ($SelfTest) {
    Invoke-SelfTest
    exit 0
}

$logDir = Join-Path $env:LOCALAPPDATA 'OpenAI\Codex'
New-Item -ItemType Directory -Path $logDir -Force | Out-Null
$logFile = Join-Path $logDir 'one-click-runtime-repair.log'

function Write-RepairLog {
    param([Parameter(Mandatory)][string]$Message)

    $time = Get-Date -Format 'yyyy-MM-dd HH:mm:ss'
    $line = "[$time] $Message"
    Write-Host $line
    Add-Content -LiteralPath $logFile -Value $line -Encoding UTF8
}

try {
    Write-RepairLog '开始 Codex Desktop runtime 修复。'

    $pkg = Get-AppxPackage -Name 'OpenAI.Codex' | Select-Object -First 1
    if (-not $pkg) {
        throw '没有找到 OpenAI.Codex MSIX 包。'
    }

    $installLocation = [string]$pkg.InstallLocation
    Write-RepairLog "当前 Codex Desktop 版本：$($pkg.Version)"

    $src = Join-Path $installLocation 'app\resources\cua_node'
    $root = Join-Path $env:LOCALAPPDATA 'OpenAI\Codex\runtimes\cua_node'

    if (-not (Test-Path -LiteralPath $src)) {
        throw "找不到当前 Codex 安装包中的 cua_node：$src"
    }
    if (-not (Test-Path -LiteralPath (Join-Path $src 'bin\node.exe'))) {
        throw '官方 cua_node 中缺少 bin\node.exe。'
    }
    if (-not (Test-Path -LiteralPath (Join-Path $src 'bin\node_repl.exe'))) {
        throw '官方 cua_node 中缺少 bin\node_repl.exe。'
    }

    New-Item -ItemType Directory -Path $root -Force | Out-Null

    function Get-CodexDesktopProcesses {
        Get-Process -Name 'ChatGPT' -ErrorAction SilentlyContinue | Where-Object {
            try {
                $_.Path -and $_.Path.StartsWith($installLocation, [System.StringComparison]::OrdinalIgnoreCase)
            }
            catch {
                $false
            }
        }
    }

    function Stop-CodexDesktopProcesses {
        @(Get-CodexDesktopProcesses) | ForEach-Object {
            Stop-Process -Id $_.Id -Force -ErrorAction SilentlyContinue
        }
    }

    function Get-CodexWindowProcess {
        @(Get-CodexDesktopProcesses) | Where-Object {
            $_.MainWindowHandle -ne 0
        } | Select-Object -First 1
    }

    Stop-CodexDesktopProcesses
    Start-Sleep -Seconds 1

    $launchTime = Get-Date
    $appUri = "shell:AppsFolder\$($pkg.PackageFamilyName)!App"

    Start-Process -FilePath 'explorer.exe' -ArgumentList $appUri
    Write-RepairLog '已尝试正常启动 Codex，等待主窗口。'

    for ($i = 0; $i -lt 12; $i++) {
        Start-Sleep -Seconds 1
        if (Get-CodexWindowProcess) {
            Write-RepairLog 'Codex 已正常创建主窗口，无需修复。'
            exit 0
        }
    }

    Stop-CodexDesktopProcesses
    Start-Sleep -Seconds 1

    $latestStage = Get-ChildItem -LiteralPath $root -Directory -Force -ErrorAction SilentlyContinue | Where-Object {
        $_.Name -like '.staging-*' -and $_.LastWriteTime -ge $launchTime.AddSeconds(-2)
    } | Sort-Object LastWriteTime -Descending | Select-Object -First 1

    if (-not $latestStage) {
        throw '本次启动没有生成新的 cua_node .staging-* 目录；当前故障可能不是此脚本针对的 runtime 复制问题。'
    }

    $runtimeId = Get-RuntimeIdFromStageName -Name $latestStage.Name
    $dst = Join-Path $root $runtimeId
    Write-RepairLog "检测到 runtime ID：$runtimeId"

    New-Item -ItemType Directory -Path $dst -Force | Out-Null

    Write-RepairLog '开始 xcopy /E /I /Y /G /H。'
    & "$env:SystemRoot\System32\xcopy.exe" "$src\*" "$dst\" /E /I /Y /G /H /Q | Out-Null
    Write-RepairLog "xcopy 返回码：$LASTEXITCODE"

    Write-RepairLog '开始 Robocopy 补齐深层目录。'
    & "$env:SystemRoot\System32\robocopy.exe" "$src" "$dst" /E /COPY:DAT /DCOPY:DAT /R:0 /W:0 /NFL /NDL /NJH /NJS /NP | Out-Null
    Write-RepairLog "Robocopy 返回码：$LASTEXITCODE"

    $srcFiles = @(Get-RelativeFileList -Root $src)
    $dstFiles = @(Get-RelativeFileList -Root $dst)
    $dstLookup = @{}
    foreach ($relativePath in $dstFiles) {
        $dstLookup[$relativePath] = $true
    }

    $missing = @($srcFiles | Where-Object { -not $dstLookup.ContainsKey($_) })
    Write-RepairLog "第一次校验：官方源 $($srcFiles.Count)；目标 $($dstFiles.Count)；缺失 $($missing.Count)。"

    foreach ($relativePath in $missing) {
        $sourceFile = Join-Path $src $relativePath
        $targetFile = Join-Path $dst $relativePath
        $targetDirectory = Split-Path -Parent $targetFile

        [System.IO.Directory]::CreateDirectory("\\?\$targetDirectory") | Out-Null

        try {
            $bytes = [System.IO.File]::ReadAllBytes("\\?\$sourceFile")
            [System.IO.File]::WriteAllBytes("\\?\$targetFile", $bytes)
            Write-RepairLog "已补齐：$relativePath"
        }
        catch {
            Write-RepairLog "补齐失败：$relativePath；$($_.Exception.Message)"
        }
    }

    $dstFilesAfter = @(Get-RelativeFileList -Root $dst)
    $dstLookupAfter = @{}
    foreach ($relativePath in $dstFilesAfter) {
        $dstLookupAfter[$relativePath] = $true
    }
    $missingAfter = @($srcFiles | Where-Object { -not $dstLookupAfter.ContainsKey($_) })

    $nodeOk = Test-Path -LiteralPath (Join-Path $dst 'bin\node.exe')
    $nodeReplOk = Test-Path -LiteralPath (Join-Path $dst 'bin\node_repl.exe')
    Write-RepairLog "最终校验：官方源 $($srcFiles.Count)；目标 $($dstFilesAfter.Count)；缺失 $($missingAfter.Count)；node.exe=$nodeOk；node_repl.exe=$nodeReplOk。"

    if (-not $nodeOk -or -not $nodeReplOk -or $missingAfter.Count -gt 0) {
        foreach ($relativePath in ($missingAfter | Select-Object -First 20)) {
            Write-RepairLog "仍缺文件：$relativePath"
        }
        throw 'runtime 未能完整补齐。'
    }

    Get-ChildItem -LiteralPath $root -Directory -Force -ErrorAction SilentlyContinue | Where-Object {
        $_.Name -like ".staging-$runtimeId-*"
    } | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue

    Write-RepairLog "runtime $runtimeId 已完整修复，并清理本版本失败 staging。"

    Stop-CodexDesktopProcesses
    Start-Sleep -Seconds 1
    Start-Process -FilePath 'explorer.exe' -ArgumentList $appUri
    Write-RepairLog '已重新启动 Codex Desktop。'

    for ($i = 0; $i -lt 20; $i++) {
        Start-Sleep -Seconds 1
        if (Get-CodexWindowProcess) {
            Write-RepairLog '修复成功：Codex 已创建主窗口。'
            exit 0
        }
    }

    throw 'runtime 已完整，但 Codex 重新启动后仍未检测到主窗口。'
}
catch {
    Write-RepairLog "修复失败：$($_.Exception.Message)"
    Write-Host "日志：$logFile"
    exit 1
}
