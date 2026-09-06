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
        throw "Unable to parse runtime ID from staging directory: $Name"
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
            throw 'Long-path destination file was not created.'
        }

        $copied = [System.IO.File]::ReadAllBytes("\\?\$targetFile")
        if ([System.Text.Encoding]::UTF8.GetString($copied) -ne 'codex-runtime-fix-self-test') {
            throw 'Long-path file content verification failed.'
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
        throw "Runtime ID parsing test failed: $runtimeId"
    }

    $tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("codex-runtime-list-selftest-" + [guid]::NewGuid().ToString('N'))
    try {
        New-Item -ItemType Directory -Path (Join-Path $tempRoot 'a\b') -Force | Out-Null
        Set-Content -LiteralPath (Join-Path $tempRoot 'a\b\one.txt') -Value '1' -Encoding UTF8
        Set-Content -LiteralPath (Join-Path $tempRoot 'two.txt') -Value '2' -Encoding UTF8

        $files = @(Get-RelativeFileList -Root $tempRoot)
        if ($files.Count -ne 2) {
            throw "Relative path enumeration test failed. File count: $($files.Count)"
        }
        if ($files -notcontains 'a\b\one.txt' -or $files -notcontains 'two.txt') {
            throw 'Relative path enumeration test failed.'
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
    Write-RepairLog 'Starting Codex Desktop runtime repair.'

    $pkg = Get-AppxPackage -Name 'OpenAI.Codex' | Select-Object -First 1
    if (-not $pkg) {
        throw 'OpenAI.Codex MSIX package was not found.'
    }

    $installLocation = [string]$pkg.InstallLocation
    Write-RepairLog "Detected Codex Desktop version: $($pkg.Version)"

    $src = Join-Path $installLocation 'app\resources\cua_node'
    $root = Join-Path $env:LOCALAPPDATA 'OpenAI\Codex\runtimes\cua_node'

    if (-not (Test-Path -LiteralPath $src)) {
        throw "cua_node was not found in the installed Codex package: $src"
    }
    if (-not (Test-Path -LiteralPath (Join-Path $src 'bin\node.exe'))) {
        throw 'Installed cua_node is missing bin\node.exe.'
    }
    if (-not (Test-Path -LiteralPath (Join-Path $src 'bin\node_repl.exe'))) {
        throw 'Installed cua_node is missing bin\node_repl.exe.'
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
    Write-RepairLog 'Started Codex normally and waiting for a main window.'

    for ($i = 0; $i -lt 12; $i++) {
        Start-Sleep -Seconds 1
        if (Get-CodexWindowProcess) {
            Write-RepairLog 'Codex created a main window normally. No repair is required.'
            exit 0
        }
    }

    Stop-CodexDesktopProcesses
    Start-Sleep -Seconds 1

    $latestStage = Get-ChildItem -LiteralPath $root -Directory -Force -ErrorAction SilentlyContinue | Where-Object {
        $_.Name -like '.staging-*' -and $_.LastWriteTime -ge $launchTime.AddSeconds(-2)
    } | Sort-Object LastWriteTime -Descending | Select-Object -First 1

    if (-not $latestStage) {
        throw 'This launch did not create a fresh cua_node .staging-* directory. The current failure may not be the runtime-staging issue targeted by this script.'
    }

    $runtimeId = Get-RuntimeIdFromStageName -Name $latestStage.Name
    $dst = Join-Path $root $runtimeId
    Write-RepairLog "Detected runtime ID: $runtimeId"

    New-Item -ItemType Directory -Path $dst -Force | Out-Null

    Write-RepairLog 'Running xcopy /E /I /Y /G /H.'
    & "$env:SystemRoot\System32\xcopy.exe" "$src\*" "$dst\" /E /I /Y /G /H /Q | Out-Null
    Write-RepairLog "xcopy exit code: $LASTEXITCODE"

    Write-RepairLog 'Running Robocopy to fill deep directory trees.'
    & "$env:SystemRoot\System32\robocopy.exe" "$src" "$dst" /E /COPY:DAT /DCOPY:DAT /R:0 /W:0 /NFL /NDL /NJH /NJS /NP | Out-Null
    Write-RepairLog "Robocopy exit code: $LASTEXITCODE"

    $srcFiles = @(Get-RelativeFileList -Root $src)
    $dstFiles = @(Get-RelativeFileList -Root $dst)
    $dstLookup = @{}
    foreach ($relativePath in $dstFiles) {
        $dstLookup[$relativePath] = $true
    }

    $missing = @($srcFiles | Where-Object { -not $dstLookup.ContainsKey($_) })
    Write-RepairLog "Initial integrity check: source=$($srcFiles.Count); target=$($dstFiles.Count); missing=$($missing.Count)."

    foreach ($relativePath in $missing) {
        $sourceFile = Join-Path $src $relativePath
        $targetFile = Join-Path $dst $relativePath
        $targetDirectory = Split-Path -Parent $targetFile

        [System.IO.Directory]::CreateDirectory("\\?\$targetDirectory") | Out-Null

        try {
            $bytes = [System.IO.File]::ReadAllBytes("\\?\$sourceFile")
            [System.IO.File]::WriteAllBytes("\\?\$targetFile", $bytes)
            Write-RepairLog "Filled missing file: $relativePath"
        }
        catch {
            Write-RepairLog "Failed to fill missing file: $relativePath; $($_.Exception.Message)"
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
    Write-RepairLog "Final integrity check: source=$($srcFiles.Count); target=$($dstFilesAfter.Count); missing=$($missingAfter.Count); node.exe=$nodeOk; node_repl.exe=$nodeReplOk."

    if (-not $nodeOk -or -not $nodeReplOk -or $missingAfter.Count -gt 0) {
        foreach ($relativePath in ($missingAfter | Select-Object -First 20)) {
            Write-RepairLog "Still missing: $relativePath"
        }
        throw 'The runtime could not be reconstructed completely.'
    }

    Get-ChildItem -LiteralPath $root -Directory -Force -ErrorAction SilentlyContinue | Where-Object {
        $_.Name -like ".staging-$runtimeId-*"
    } | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue

    Write-RepairLog "Runtime $runtimeId is complete. Failed staging directories for this runtime ID were cleaned."

    Stop-CodexDesktopProcesses
    Start-Sleep -Seconds 1
    Start-Process -FilePath 'explorer.exe' -ArgumentList $appUri
    Write-RepairLog 'Restarted Codex Desktop.'

    for ($i = 0; $i -lt 20; $i++) {
        Start-Sleep -Seconds 1
        if (Get-CodexWindowProcess) {
            Write-RepairLog 'Repair succeeded: Codex created a main window.'
            exit 0
        }
    }

    throw 'The runtime is complete, but Codex still did not create a main window after restart.'
}
catch {
    Write-RepairLog "Repair failed: $($_.Exception.Message)"
    Write-Host "Log: $logFile"
    exit 1
}
