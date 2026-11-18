#requires -Version 7.6.10
#requires -PSEdition Core
$ErrorActionPreference = 'Stop'
# Build-time migration of the existing option contract; no CLR is used by the EXE.
$source = Get-Content -LiteralPath (Join-Path $PSScriptRoot '../EIStarter/REG_INI_IO/OwnedRendererSchema64.cs') -Raw -Encoding UTF8
$pattern = 'new OwnedRendererOption\("([^"]+)", (\d+), OwnedValueType\.(\w+), (-?[\d.]+)m, (-?[\d.]+)m, (-?[\d.]+)m, (-?[\d.]+)m, (null|new decimal\[\] \{[^}]+\}), (null|new\[\] \{[^}]+\}),\s*"([^"]+)", "([^"]+)", (null|new\[\] \{[^}]+\}), (null|"[^"]*")\)'
$types = @{ Boolean=0; Integer=1; Float=2; Text=3 }
$rows = foreach ($m in [regex]::Matches($source,$pattern)) {
    $g=$m.Groups
    $presets=if($g[8].Value -eq 'null'){'{}'}else{$g[8].Value -replace 'new decimal\[\] ',' ' -replace 'm',''}
    $choices=if($g[9].Value -eq 'null'){'{}'}else{($g[9].Value -replace 'new\[\] ',' ' -replace '"([^"]*)"','L"$1"')}
    $russian=if($g[12].Value -eq 'null'){'{}'}else{($g[12].Value -replace 'new\[\] ',' ' -replace '"([^"]*)"','L"$1"')}
    $text=if($g[13].Value -eq 'null'){'L""'}else{'L'+$g[13].Value}
    '        {L"'+$g[1].Value+'", '+$g[2].Value+', '+$types[$g[3].Value]+', '+$g[4].Value+', '+$g[5].Value+', '+$g[6].Value+', '+$g[7].Value+', '+$presets+', '+$choices+', L"'+$g[10].Value+'", L"'+$g[11].Value+'", '+$russian+', '+$text+'},'
}
$expected = [regex]::Matches($source,'new OwnedRendererOption\(').Count
if (!$expected -or $rows.Count -ne $expected) { throw 'Incomplete x64 renderer schema migration.' }
$body = '#include "core.hpp"' + "`r`nnamespace ei {`r`nconst std::vector<Option>& schema() {`r`n    static const std::vector<Option> data = {`r`n" + ($rows -join "`r`n") + "`r`n    };`r`n    return data;`r`n}`r`n}`r`n"
$output=Join-Path $PSScriptRoot 'obj/schema.cpp'
$null=[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($output))
if(![IO.File]::Exists($output) -or [IO.File]::ReadAllText($output) -cne $body){
    [IO.File]::WriteAllText($output, $body, [Text.UTF8Encoding]::new($true))
}
