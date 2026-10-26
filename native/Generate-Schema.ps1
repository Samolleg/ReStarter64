$ErrorActionPreference = 'Stop'
# Build-time migration of the existing option contract; no CLR is used by the EXE.
$source = Get-Content -LiteralPath (Join-Path $PSScriptRoot '../EIStarter/REG_INI_IO/OwnedRendererSchema.cs') -Raw -Encoding UTF8
$pattern = 'new OwnedRendererOption\("([^"]+)", (\d+), OwnedValueType\.(\w+), ([\d.]+)m, ([\d.]+)m, ([\d.]+)m, ([\d.]+)m, (null|new decimal\[\] \{[^}]+\}), (null|new string\[\] \{[^}]+\}), "([^"]+)", "([^"]+)"\)'
$types = @{ Boolean=0; Integer=1; Float=2 }
$rows = foreach ($m in [regex]::Matches($source,$pattern)) {
    $g=$m.Groups
    $presets=if($g[8].Value -eq 'null'){'{}'}else{$g[8].Value -replace 'new decimal\[\] ',' ' -replace 'm',''}
    $choices=if($g[9].Value -eq 'null'){'{}'}else{($g[9].Value -replace 'new string\[\] ',' ' -replace '"([^"]*)"','L"$1"')}
    '        {L"'+$g[1].Value+'", '+$g[2].Value+', '+$types[$g[3].Value]+', '+$g[4].Value+', '+$g[5].Value+', '+$g[6].Value+', '+$g[7].Value+', '+$presets+', '+$choices+', L"'+$g[10].Value+'", L"'+$g[11].Value+'"},'
}
if ($rows.Count -ne 50) { throw 'Renderer schema changed; review the native contract before building.' }
$body = '#include "core.hpp"' + "`r`nnamespace ei {`r`nconst std::vector<Option>& schema() {`r`n    static const std::vector<Option> data = {`r`n" + ($rows -join "`r`n") + "`r`n    };`r`n    return data;`r`n}`r`n}`r`n"
[IO.File]::WriteAllText((Join-Path $PSScriptRoot 'obj/schema.cpp'), $body, [Text.UTF8Encoding]::new($true))
