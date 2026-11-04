param([Parameter(Mandatory)][string]$ApkPath, [Parameter(Mandatory)][string]$AssemblyPath)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [IO.Compression.ZipFile]::OpenRead((Resolve-Path -LiteralPath $ApkPath))
try {
    $entry = $archive.GetEntry('assemblies/JXHLJSApp.dll')
    if (!$entry) { throw 'APK does not contain the application assembly; do not install this incomplete package.' }
    if (!($archive.Entries | Where-Object { $_.FullName -match '^lib/[^/]+/libmonosgen-2\.0\.so$' })) { throw 'APK is missing the Android managed runtime.' }
    $stream = $entry.Open()
    try {
        $embeddedHash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($stream))
    } finally { $stream.Dispose() }
    $expectedHash = (Get-FileHash -LiteralPath $AssemblyPath -Algorithm SHA256).Hash
    if ($embeddedHash -ne $expectedHash) { throw 'APK assembly does not match the latest compiled source.' }
    Write-Output "PASS: APK embeds the exact current application assembly and Android runtime ($embeddedHash)"
} finally { $archive.Dispose() }
