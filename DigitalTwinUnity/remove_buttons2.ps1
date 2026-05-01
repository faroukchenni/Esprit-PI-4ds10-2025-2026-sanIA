$scenePath = "Assets\Scenes\SampleScene.unity"
$content = Get-Content $scenePath -Raw
$blocks = $content -split '(?=--- !u!)'

$removeIds = [System.Collections.Generic.HashSet[string]]::new()
# Remaining button GOs and their components
@(
  # Test_Heatwave GO + remaining components
  "724126861","724126863","724126864","724126865",
  # Reset_Healthy GO + remaining components
  "1156927008","1156927010","1156927011","1156927012",
  # Test_Drought GO + remaining components
  "1374786365","1374786367","1374786368","1374786369",
  # Test_Disease GO + remaining components
  "1578927224","1578927226","1578927227","1578927228"
) | ForEach-Object { [void]$removeIds.Add($_) }

function Get-FileIDFromBlock($block) {
    if ($block -match '--- !u!\d+ &(\d+)') { return $Matches[1] }
    return $null
}

$keptBlocks = @()
foreach ($block in $blocks) {
    $id = Get-FileIDFromBlock $block
    if ($id -and $removeIds.Contains($id)) {
        Write-Host "Removing: $id"
        continue
    }
    $keptBlocks += $block
}

$result = $keptBlocks -join ""
Set-Content $scenePath $result -NoNewline
Write-Host "Done. Kept $($keptBlocks.Count) blocks."
