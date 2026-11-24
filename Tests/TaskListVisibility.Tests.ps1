$ErrorActionPreference = 'Stop'
$sourcePath = Join-Path (Split-Path $PSScriptRoot -Parent) 'Tools/TaskListVisibility.cs'
$source = Get-Content -LiteralPath $sourcePath -Raw
$cases = @'
public static class VisibilityRegression
{
    public static void Run()
    {
        var cases = new (string? value, string? username, bool expected)[]
        {
            (null, "admin", false),
            ("", "admin", false),
            ("   ", "admin", false),
            ("all", "anyone", true),
            (" ALL ", null, true),
            ("alice,ADMIN,bob", "admin", true),
            (" alice, , ADMIN, ", " admin ", true),
            ("admin1,bob", "admin", false),
            ("alice,bob", "charlie", false),
            ("alice,bob", null, false),
            (", ,", "", false),
            ("all,bob", "alice", false),
            ("谢光亮@jxhl,admin@JXHL", "ADMIN@jxhl", true)
        };
        foreach (var item in cases)
        {
            if (TaskListVisibility.CanShow(item.value, item.username) != item.expected)
                throw new Exception($"Unexpected visibility: {item.value} / {item.username}");
        }
    }
}
'@
Add-Type -TypeDefinition ("#nullable enable`nusing System;`nusing System.Linq;`n" + $source + "`n" + $cases)
[JXHLJSApp.Tools.VisibilityRegression]::Run()
Write-Host 'PASS: 13 task-list visibility cases.'
