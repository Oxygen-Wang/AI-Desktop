$ErrorActionPreference='Stop'
$taskRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$taskHeaders=@{'User-Agent'='QiDesk-Icon-Research'}
function Read-IconSource($repo,$sha,$path){
    $taskUrl='https://raw.githubusercontent.com/'+$repo+'/'+$sha+'/'+$path
    for($taskDepth=0;$taskDepth -lt 8;$taskDepth++){
        $taskResponse=Invoke-WebRequest -Uri $taskUrl -Headers $taskHeaders
        $taskContent=$taskResponse.Content
        if($taskContent -is [byte[]]){$taskContent=[Text.Encoding]::UTF8.GetString($taskContent)}
        if($taskContent.TrimStart().StartsWith('<')){return @{Text=$taskContent;Url=$taskUrl}}
        $taskNext=$taskContent.Trim()
        if($taskNext -notmatch '^[a-zA-Z0-9_./-]+\.svg$'){throw 'Unexpected icon source: '+$taskUrl}
        $taskUrl=[Uri]::new([Uri]$taskUrl,$taskNext).AbsoluteUri
        if(-not $taskUrl.StartsWith('https://raw.githubusercontent.com/'+$repo+'/'+$sha+'/')){throw 'Icon link escaped source revision'}
    }
    throw 'Icon link depth exceeded'
}
$taskSelection=@{
    fluent=@{
        folder='src/scalable/places/default-folder.svg';document='src/scalable/mimetypes/text-x-generic.svg';image='src/scalable/mimetypes/image-x-generic.svg'
        chrome='src/scalable/apps/google-chrome.svg';edge='src/scalable/apps/microsoft-edge.svg';firefox='src/scalable/apps/firefox.svg';qq='src/scalable/apps/qq.svg';wechat='src/scalable/apps/wechat.svg';matlab='src/scalable/apps/matlab.svg';vscode='src/scalable/apps/visualstudiocode.svg';sevenzip='src/scalable/apps/7zip.svg';word='src/scalable/apps/ms-word.svg';excel='src/scalable/apps/ms-excel.svg';powerpoint='src/scalable/apps/ms-powerpoint.svg';cloudmusic='src/scalable/apps/netease-cloud-music.svg'
    }
    papirus=@{
        folder='Papirus/48x48/places/folder-blue.svg';document='Papirus/48x48/mimetypes/text-x-generic.svg';image='Papirus/48x48/mimetypes/image-x-generic.svg'
        chrome='Papirus/48x48/apps/google-chrome.svg';edge='Papirus/48x48/apps/microsoft-edge.svg';firefox='Papirus/48x48/apps/firefox.svg';wechat='Papirus/48x48/apps/wechat.svg';matlab='Papirus/48x48/apps/matlab.svg';vscode='Papirus/48x48/apps/visual-studio-code.svg';sevenzip='Papirus/48x48/apps/7zip.svg';word='Papirus/48x48/apps/ms-word.svg';excel='Papirus/48x48/apps/ms-excel.svg';powerpoint='Papirus/48x48/apps/ms-powerpoint.svg';cloudmusic='Papirus/48x48/apps/netease-cloud-music.svg'
    }
}
foreach($taskKey in @('fluent','papirus','seelen','shortcut-icon-changer')){
    $taskSource=Get-Content -LiteralPath (Join-Path $taskRoot ('references/icon-skins/'+$taskKey+'-source.json')) -Raw | ConvertFrom-Json
    $taskRef=Join-Path $taskRoot ('references/icon-skins/'+$taskKey)
    New-Item -ItemType Directory -Force -Path $taskRef | Out-Null
    $taskLicense=if($taskKey -eq 'fluent'){'COPYING'}else{'LICENSE'}
    foreach($taskDoc in @('README.md',$taskLicense)){
        Invoke-WebRequest -Uri ('https://raw.githubusercontent.com/'+$taskSource.repo+'/'+$taskSource.sha+'/'+$taskDoc) -Headers $taskHeaders -OutFile (Join-Path $taskRef $taskDoc)
    }
    if($taskKey -eq 'seelen'){
        foreach($taskDoc in @('libs/core/src/state/icon_pack.rs','libs/core/src/state/icon_pack.ts','src/background/resources/user_icon_pack.rs')){
            Invoke-WebRequest -Uri ('https://raw.githubusercontent.com/'+$taskSource.repo+'/'+$taskSource.sha+'/'+$taskDoc) -Headers $taskHeaders -OutFile (Join-Path $taskRef ([IO.Path]::GetFileName($taskDoc)))
        }
    }
    if(-not $taskSelection.ContainsKey($taskKey)){continue}
    $taskAssets=Join-Path $taskRoot ('code/assets/icon-skins/'+$taskKey)
    New-Item -ItemType Directory -Force -Path (Join-Path $taskAssets 'svg') | Out-Null
    Copy-Item -LiteralPath (Join-Path $taskRef $taskLicense) -Destination (Join-Path $taskAssets 'LICENSE.txt') -Force
    $taskManifest=@()
    foreach($taskEntry in $taskSelection[$taskKey].GetEnumerator()){
        $taskIcon=Read-IconSource $taskSource.repo $taskSource.sha $taskEntry.Value
        $taskLocal=Join-Path $taskAssets ('svg/'+$taskEntry.Key+'.svg')
        [IO.File]::WriteAllText($taskLocal,$taskIcon.Text,[Text.UTF8Encoding]::new($false))
        $taskManifest+=@{id=$taskEntry.Key;source=$taskEntry.Value;resolved=$taskIcon.Url;sha256=(Get-FileHash -LiteralPath $taskLocal -Algorithm SHA256).Hash.ToLowerInvariant()}
    }
    @{repo=$taskSource.repo;revision=$taskSource.sha;license='GPL-3.0';icons=$taskManifest} | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $taskAssets 'SOURCE.json')
    Write-Output ($taskKey+': '+$taskManifest.Count+' source icons')
}
