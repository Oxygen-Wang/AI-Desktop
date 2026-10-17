# Code guard. Compares code\src with code\code-budget.json and fails when a rule
# gets worse. Only things with a real failure story in this repository are checked.
#
#   powershell -ExecutionPolicy Bypass -File .\code\check-code.ps1
#   powershell -ExecutionPolicy Bypass -File .\code\check-code.ps1 -Accept
#
# Keep this file ASCII-only: Windows PowerShell 5.1 reads scripts with the ANSI code
# page, so non-ASCII text would be mangled.
#
# The rule is "do not add a new violation", not "stay under a global total". A total
# alone punishes whoever fixes a large file (the total drops, the next author gets
# headroom) and rewards whoever adds to a file that was already bad.
param(
    [switch]$Accept,
    [switch]$Quiet
)

$ErrorActionPreference = 'Stop'
$codeRoot = $PSScriptRoot
$repoRoot = Split-Path -Parent $codeRoot
$budgetPath = Join-Path $codeRoot 'code-budget.json'

function Stop-Guard([string]$message) {
    Write-Output ('FAIL: ' + $message)
    [Environment]::Exit(1)
}

# ---------------------------------------------------------------------------
# Baseline
# ---------------------------------------------------------------------------
if (!(Test-Path -LiteralPath $budgetPath)) { Stop-Guard ('missing baseline ' + $budgetPath) }

# JavaScriptSerializer is used instead of ConvertFrom-Json because it needs no extra
# module and reports bad JSON instead of guessing.
Add-Type -AssemblyName 'System.Web.Extensions'

function Read-JsonFile([string]$path) {
    $text = [IO.File]::ReadAllText($path, [Text.Encoding]::UTF8)
    $serializer = New-Object System.Web.Script.Serialization.JavaScriptSerializer
    $serializer.MaxJsonLength = 16777216
    return $serializer.DeserializeObject($text)
}

function Get-Field($object, [string]$name) {
    if ($null -eq $object) { return $null }
    $dict = $object -as [System.Collections.IDictionary]
    if ($null -eq $dict) { return $null }
    foreach ($key in $dict.Keys) { if ([string]$key -ceq $name) { return $dict[$key] } }
    return $null
}

function Get-Count($object, [string]$name, [int]$fallback) {
    $value = Get-Field $object $name
    if ($null -eq $value) { return $fallback }
    return [int]$value
}

try { $baseline = Read-JsonFile $budgetPath }
catch { Stop-Guard ('baseline is not valid JSON: ' + $_.Exception.Message) }
if ($null -eq $baseline) { Stop-Guard 'baseline is empty' }

$limitBlock = Get-Field $baseline 'limits'
$legacyBlock = Get-Field $baseline 'legacy'
if ($null -eq $limitBlock) { Stop-Guard 'baseline has no limits section' }

$limitLine = Get-Count $limitBlock 'lineLength' 300
$limitDepth = Get-Count $limitBlock 'nestedBlockDepth' 8
$limitFile = Get-Count $limitBlock 'fileLines' 900
$limitDup = Get-Count $limitBlock 'duplicateFile' 0

# A file listed here keeps exactly the number of violations recorded in legacyCounts.
# An empty list is the normal state and means no file may have that violation at all.
#
# The baseline stores each rule as { files: [...], counts: {...} }. Accepting a plain
# list too keeps a hand-written or older baseline readable instead of failing with a
# confusing "0 known files".
function Get-Legacy([string]$name) {
    $set = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
    $entry = Get-Field $legacyBlock $name
    $items = Get-Field $entry 'files'
    if ($null -eq $items) { $items = $entry }
    if ($null -ne $items) {
        foreach ($item in $items) {
            $text = [string]$item
            if (-not [String]::IsNullOrEmpty($text)) { [void]$set.Add($text) }
        }
    }
    return (New-Object PSObject -Property @{ Files = $set })
}

# Per-file allowance written next to the legacy list. A baseline without it (an older
# file, or one edited by hand) falls back to "one violation per listed file", which is
# stricter than recording nothing and never silently allows unlimited growth.
function Get-LegacyBounds([string]$name) {
    $map = New-Object 'System.Collections.Generic.Dictionary[string,int]' ([StringComparer]::OrdinalIgnoreCase)
    $entry = Get-Field $legacyBlock $name
    $counts = Get-Field $entry 'counts'
    if ($null -ne $counts) {
        $dict = $counts -as [System.Collections.IDictionary]
        if ($null -ne $dict) {
            foreach ($key in $dict.Keys) { $map[[string]$key] = [int]$dict[$key] }
        }
    }
    return (New-Object PSObject -Property @{ Counts = $map })
}

$legacyLong = Get-Legacy 'longLines'
$legacyDepth = Get-Legacy 'deepBlocks'
$legacyCatch = Get-Legacy 'emptyCatch'
$legacyBig = Get-Legacy 'oversizedFiles'
$boundsLong = (Get-LegacyBounds 'longLines').Counts
$boundsDepth = (Get-LegacyBounds 'deepBlocks').Counts
$boundsCatch = (Get-LegacyBounds 'emptyCatch').Counts
$boundsBig = (Get-LegacyBounds 'oversizedFiles').Counts

# ---------------------------------------------------------------------------
# Collect
# ---------------------------------------------------------------------------
function Split-Lines([string]$path) {
    # .NET returns a real array even for a one-line file; the pipeline cmdlet would
    # collapse it into a single string and every index access would read characters.
    $lines = [IO.File]::ReadAllLines($path, [Text.Encoding]::UTF8)
    return (New-Object PSObject -Property @{ Lines = $lines })
}

$base = [IO.Path]::GetFullPath($repoRoot).TrimEnd('\') + '\'
$sourceFiles = Get-ChildItem -LiteralPath (Join-Path $codeRoot 'src') -Recurse -File -Filter '*.cs'

$longRows = New-Object 'System.Collections.Generic.List[object]'
$depthRows = New-Object 'System.Collections.Generic.List[object]'
$catchRows = New-Object 'System.Collections.Generic.List[object]'
$fileRows = New-Object 'System.Collections.Generic.List[object]'
$hashByFile = New-Object 'System.Collections.Generic.Dictionary[string,string]' ([StringComparer]::OrdinalIgnoreCase)

foreach ($file in $sourceFiles) {
    $relative = [IO.Path]::GetFullPath($file.FullName).Replace($base, '').Replace('\', '/')
    $lines = (Split-Lines $file.FullName).Lines
    if ($lines.Length -gt $limitFile) {
        [void]$fileRows.Add([ordered]@{ File = $relative; Line = 0; Size = $lines.Length })
    }
    if (-not $relative.StartsWith('code/src/tests/')) {
        $hashByFile[(Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash] = $relative
    }
    $depth = 0
    for ($i = 0; $i -lt $lines.Length; $i++) {
        $line = $lines[$i]
        $number = $i + 1
        # Bytes, not characters: a line of Chinese comments costs as much to read as
        # a line of Latin text, and the baseline was measured the same way.
        $bytes = [Text.Encoding]::UTF8.GetByteCount($line)
        if ($bytes -gt $limitLine) {
            [void]$longRows.Add([ordered]@{ File = $relative; Line = $number; Size = $bytes })
        }
        # A catch block that does nothing is a swallowed error, however it is written.
        # Comments are stripped first so the documented exception
        # (`catch (InvalidOperationException) { // reason }`) is not flagged.
        #
        # Every occurrence on the line is counted, not just the first. A line that
        # packs three `catch{}` into one method would otherwise cost the same as one,
        # and this project has several of those. Counting per match also means the
        # number is exactly what a reader would count by hand.
        if ($line -match '\bcatch\b') {
            $block = $line -replace '//.*$', ''
            foreach ($hit in [regex]::Matches($block, '\bcatch\b[^{]*\{\s*\}')) {
                [void]$catchRows.Add([ordered]@{ File = $relative; Line = $number; Size = 0 })
            }
        }
        # Brace depth only on code written one statement per line; a very long line
        # reports a meaningless depth and is already reported as a long line.
        if ($bytes -le $limitLine) {
            $code = $line -replace '//.*$', ''
            $code = $code -replace '"[^"]*"', '""'
            $code = $code -replace "'[^']*'", "''"
            foreach ($ch in $code.ToCharArray()) {
                if ($ch -eq '{') {
                    $depth++
                    if ($depth -gt $limitDepth) {
                        [void]$depthRows.Add([ordered]@{ File = $relative; Line = $number; Size = $depth })
                    }
                } elseif ($ch -eq '}') {
                    if ($depth -gt 0) { $depth-- }
                }
            }
        }
    }
}

# A copy of the application source outside code\src means two versions of the same
# module are being edited. Only an identical content hash counts: a test that builds
# a similar object is not a duplicate of the program.
$duplicates = New-Object 'System.Collections.Generic.List[object]'
foreach ($scan in @('code', 'tmp', 'tools', 'output')) {
    $dir = Join-Path $repoRoot $scan
    if (!(Test-Path -LiteralPath $dir)) { continue }
    foreach ($candidate in Get-ChildItem -LiteralPath $dir -Recurse -File -Filter '*.cs' -ErrorAction SilentlyContinue) {
        $relative = [IO.Path]::GetFullPath($candidate.FullName).Replace($base, '').Replace('\', '/')
        if ($relative.StartsWith('code/src/')) { continue }
        if ($relative -match '/(obj|bin)/') { continue }
        $copyOf = ''
        if ($hashByFile.TryGetValue((Get-FileHash -LiteralPath $candidate.FullName -Algorithm SHA256).Hash, [ref]$copyOf)) {
            [void]$duplicates.Add([ordered]@{ File = $relative; CopyOf = $copyOf })
        }
    }
}

# ---------------------------------------------------------------------------
# Compare
# ---------------------------------------------------------------------------
$problems = New-Object 'System.Collections.Generic.List[string]'

function Add-Problem([string]$line) { [void]$problems.Add($line) }

# Reports only violations in files that are not already listed in the baseline.
# A file that became clean is reported separately so the list can shrink.
#
# $bounds maps a file to the number of violations it is allowed to keep. Without that
# per-file number, "already listed" would mean "listed once, forever unlimited": a file
# with one old long line could then gain fifty more and the guard would stay silent.
function Test-Budget([string]$label, $rows, $legacy, $bounds, [string]$hint) {
    $perFile = @{}
    foreach ($row in $rows) {
        $path = [string]$row['File']
        if ($perFile.ContainsKey($path)) { $perFile[$path] = $perFile[$path] + 1 } else { $perFile[$path] = 1 }
    }
    $newRows = New-Object 'System.Collections.Generic.List[object]'
    $seenByFile = @{}
    foreach ($row in $rows) {
        $path = [string]$row['File']
        if (-not $legacy.Files.Contains($path)) {
            [void]$newRows.Add($row)
            continue
        }
        $seen = 0
        if ($seenByFile.ContainsKey($path)) { $seen = $seenByFile[$path] }
        $seenByFile[$path] = $seen + 1
        $allowed = 0
        if ($null -ne $bounds -and $bounds.ContainsKey($path)) { $allowed = [int]$bounds[$path] }
        # The first $allowed rows of a listed file are the existing damage; anything
        # beyond that was written after the baseline was recorded.
        if ($seen -ge $allowed) { [void]$newRows.Add($row) }
    }
    if ($newRows.Count -eq 0) {
        Write-Output ('  ok   {0}: no new violation ({1} known file(s), {2} now)' -f `
            $label, $legacy.Files.Count, $rows.Count)
    } else {
        Add-Problem ('{0}: {1} new violation(s), {2}' -f $label, $newRows.Count, $hint)
        for ($i = 0; $i -lt $newRows.Count -and $i -lt 8; $i++) {
            $row = $newRows[$i]
            if ($row['Line'] -gt 0) {
                Add-Problem ('    {0}:{1} ({2})' -f $row['File'], $row['Line'], $row['Size'])
            } else {
                Add-Problem ('    {0} ({1} lines)' -f $row['File'], $row['Size'])
            }
        }
        if ($newRows.Count -gt 8) { Add-Problem ('    ... {0} more' -f ($newRows.Count - 8)) }
    }
    foreach ($file in $legacy.Files) {
        if (-not $perFile.ContainsKey($file)) {
            Write-Output ('  tidy {0}: {1} is clean now, remove it from legacy in code-budget.json' -f $label, $file)
            continue
        }
        $allowed = 0
        if ($null -ne $bounds -and $bounds.ContainsKey($file)) { $allowed = [int]$bounds[$file] }
        if ($perFile[$file] -lt $allowed) {
            Write-Output ('  tidy {0}: {1} went from {2} to {3}, run with -Accept to record it' -f $label, $file, $allowed, $perFile[$file])
        }
    }
    # The totals must reconcile. Everything counted is either a violation in an
    # unlisted file, or something over the allowance in a listed file. If the books do
    # not balance, the total is lying and it is cheaper to say so than to debug later.
    $accounted = 0
    foreach ($file in $perFile.Keys) {
        $allowed = 0
        if ($null -ne $bounds -and $bounds.ContainsKey($file)) { $allowed = [int]$bounds[$file] }
        if ($perFile[$file] -gt $allowed) { $accounted += $perFile[$file] - $allowed }
    }
    if ($accounted -ne $newRows.Count) {
        Write-Output ('  note {0}: counted {1} but accounted for {2}; treat the total as suspect' -f `
            $label, $rows.Count, $accounted)
    }
    return $newRows.Count
}

function Count-ByFile($rows) {
    $map = New-Object 'System.Collections.Generic.Dictionary[string,int]' ([StringComparer]::OrdinalIgnoreCase)
    $order = New-Object 'System.Collections.Generic.List[string]'
    foreach ($row in $rows) {
        $path = [string]$row['File']
        if ($map.ContainsKey($path)) {
            $map[$path] = $map[$path] + 1
        } else {
            $map[$path] = 1
            [void]$order.Add($path)
        }
    }
    return (New-Object PSObject -Property @{ Map = $map; Order = $order })
}

# One-element collections serialize as bare values, which is how a single oversized
# file once ended up in the baseline as "code/src/WorkspacePanel.cs" instead of a list.
# The array cast keeps every entry a list, empty or not.
function UniqueFiles($rows) {
    $names = New-Object 'System.Collections.Generic.SortedSet[string]' ([StringComparer]::Ordinal)
    foreach ($row in $rows) { [void]$names.Add([string]$row['File']) }
    $list = @()
    foreach ($name in $names) { $list += $name }
    return ,$list
}

# The legacy entry is a list of files plus the count each one is allowed to keep.
# A bare list would mean "listed once, unlimited forever", which is how a file with
# one old long line could quietly collect fifty more.
function New-LegacyEntry($rows) {
    $counted = Count-ByFile $rows
    $sorted = New-Object 'System.Collections.Generic.SortedSet[string]' ([StringComparer]::Ordinal)
    foreach ($name in $counted.Map.Keys) { [void]$sorted.Add($name) }
    $names = @()
    $counts = [ordered]@{}
    foreach ($name in $sorted) {
        $names += $name
        $counts[$name] = $counted.Map[$name]
    }
    return [ordered]@{ 'files' = $names; 'counts' = $counts }
}

function Save-Baseline {
    $readme = 'Code guard baseline. Run check-code.ps1 to compare code/src with these numbers. ' +
              'Only shorten the legacy lists and lower the counts. Run with -Accept after ' +
              'fixing code so the file records the improvement.'
    $reason = 'Each number counts existing violations at that date and is a ceiling, not a ' +
              'target. legacy counts[name] is how many that file may keep; no file may exceed it.'
    $payload = [ordered]@{
        '_readme' = $readme
        'version' = 2
        'updated' = (Get-Date).ToString('yyyy-MM-dd')
        'reason' = $reason
        'limits' = [ordered]@{
            'lineLength' = $limitLine
            'nestedBlockDepth' = $limitDepth
            'fileLines' = $limitFile
            'methodBodyLines' = 200
            'duplicateFile' = $limitDup
        }
        'budget' = [ordered]@{
            'longLines' = $longRows.Count
            'deepBlocks' = $depthRows.Count
            'emptyCatch' = $catchRows.Count
            'oversizedFiles' = $fileRows.Count
        }
        'legacy' = [ordered]@{
            'longLines' = (New-LegacyEntry $longRows)
            'deepBlocks' = (New-LegacyEntry $depthRows)
            'emptyCatch' = (New-LegacyEntry $catchRows)
            'oversizedFiles' = (New-LegacyEntry $fileRows)
        }
    }
    $serializer = New-Object System.Web.Script.Serialization.JavaScriptSerializer
    $json = $serializer.Serialize($payload)
    [IO.File]::WriteAllText($budgetPath, $json + "`r`n", (New-Object Text.UTF8Encoding($false)))
    Write-Output ('Baseline updated: longLines={0} deepBlocks={1} emptyCatch={2} oversizedFiles={3}' -f `
        $longRows.Count, $depthRows.Count, $catchRows.Count, $fileRows.Count)
}

Write-Output 'Code guard'
$newLong = Test-Budget 'long lines' $longRows $legacyLong $boundsLong ('over {0} bytes; split the statement or extract a method' -f $limitLine)
$newDepth = Test-Budget 'deep blocks' $depthRows $legacyDepth $boundsDepth ('over {0} nested braces; return early or extract a method' -f $limitDepth)
$newCatch = Test-Budget 'empty catches' $catchRows $legacyCatch $boundsCatch 'swallows an error; log it or narrow the catch'
$newFile = Test-Budget 'oversized files' $fileRows $legacyBig $boundsBig ('over {0} lines; move a concern to its own file' -f $limitFile)
# Discard the return values. A function that returns 0 still writes a bare newline
# into the pipeline, and that stray newline lands in the middle of the next line.
[void]$newLong; [void]$newDepth; [void]$newCatch; [void]$newFile
Write-Output ('  now  longLines={0} deepBlocks={1} emptyCatch={2} oversizedFiles={3}' -f `
    $longRows.Count, $depthRows.Count, $catchRows.Count, $fileRows.Count)
if ($duplicates.Count -gt [int]$limitDup) {
    Add-Problem ('duplicate sources: {0} file(s) outside code/src, allowed {1}' -f $duplicates.Count, $limitDup)
    for ($i = 0; $i -lt $duplicates.Count -and $i -lt 8; $i++) {
        Add-Problem ('    {0} is a copy of {1}' -f $duplicates[$i]['File'], $duplicates[$i]['CopyOf'])
    }
} else {
    Write-Output '  ok   duplicate sources: none'
}

if ($Accept) {
    Save-Baseline
    if ($problems.Count -gt 0) {
        Write-Output 'The code still has new violations; the baseline was written anyway because -Accept was given:'
        foreach ($line in $problems) { Write-Output $line }
        [Environment]::Exit(1)
    }
    [Environment]::Exit(0)
}

if ($problems.Count -gt 0) {
    Write-Output ''
    Write-Output 'Code guard failed. None of this is a style opinion: each rule has a failure story here.'
    foreach ($line in $problems) { Write-Output $line }
    Write-Output ''
    Write-Output 'Fix the code, do not edit the budget to hide it. If a file must be an exception,'
    Write-Output 'say why in a comment next to it and add it to legacy in code-budget.json.'
    [Environment]::Exit(1)
}

function Get-GitCommand {
    # git is not on PATH in every shell that runs the guard (a shell started before
    # git was installed, a scheduled task, a build kicked off from an IDE). Check the
    # two standard Windows install locations before giving up.
    foreach ($name in @('git.exe', 'git')) {
        $found = Get-Command $name -CommandType Application -ErrorAction SilentlyContinue
        if ($null -ne $found) { return $found.Source }
    }
    foreach ($candidate in @(
        (Join-Path $env:ProgramFiles 'Git\cmd\git.exe'),
        (Join-Path ${env:ProgramFiles(x86)} 'Git\cmd\git.exe'),
        (Join-Path $env:LOCALAPPDATA 'Programs\Git\cmd\git.exe')
    )) {
        if ($candidate -and (Test-Path -LiteralPath $candidate)) { return $candidate }
    }
    return ''
}

if (-not $Quiet) {
    Write-Output 'Code guard passed.'
    # A hook that nobody installed protects nothing. Say so on every successful run,
    # because that is the moment the author is least likely to be annoyed by it.
    $hookPath = ''
    $git = Get-GitCommand
    if ($git -ne '') {
        try { $hookPath = & $git -C $repoRoot config --get core.hooksPath 2>$null } catch { $hookPath = '' }
    }
    if ([String]::IsNullOrWhiteSpace($hookPath)) {
        Write-Output 'Note: the pre-commit hook is not active. Run this once per clone:'
        Write-Output '      git config core.hooksPath .githooks'
    }
    # A successful guard that prints nothing after "passed" looks truncated in a build
    # log, where the next step's output is glued to the last line.
    Write-Output ''
}
[Environment]::Exit(0)
