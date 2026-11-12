param([switch]$CheckOnly)
$ErrorActionPreference='Stop'
$repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
foreach($language in @('EN','KO')) {
    foreach($entry in @(@{Source='USER_GUIDE';Target='guide'},@{Source='FAQ';Target='faq'})) {
        $source=Join-Path $repo ('docs\'+$entry.Source+'_'+$language+'.md')
        $target=Join-Path $repo ('DexManager\Resources\Help\'+$language.ToLowerInvariant()+'\'+$entry.Target+'.txt')
        $text=[IO.File]::ReadAllText($source).Replace("`r`n","`n")
        $text=[regex]::Replace($text,'(?ms)^[ \t]*<p\b[^>]*>.*?</p>[ \t]*\n?','')
        $text=[regex]::Replace($text,'!\[([^\]]*)\]\([^\)]+\)','$1')
        $text=[regex]::Replace($text,'\[([^\]]+)\]\([^\)]+\)','$1')
        $text=[regex]::Replace($text,'(?m)^#{1,6}\s+','')
        $text=[regex]::Replace($text,'(?m)^>\s?','')
        $text=$text.Replace('**','').Replace('`','')
        if($CheckOnly) {
            if([IO.File]::ReadAllText($target).Replace("`r`n","`n") -ne $text){throw ('Embedded documentation is stale: '+$target)}
        } else {[IO.File]::WriteAllText($target,$text,[Text.UTF8Encoding]::new($false))}
    }
}
Write-Host 'English/Korean embedded guide and FAQ match their current Markdown sources.'
