# Unity Scene Button Remover
# Removes ScenarioTestPanel and all its children from SampleScene.unity

$scenePath = "Assets\Scenes\SampleScene.unity"
$content = Get-Content $scenePath -Raw

# Split into YAML blocks (each starts with "--- !u!")
$blocks = $content -split '(?=--- !u!)'

Write-Host "Total blocks: $($blocks.Count)"

# FileIDs to remove - we'll collect these recursively
# Start with known ScenarioTestPanel fileIDs
$removeIds = [System.Collections.Generic.HashSet[string]]::new()

# ScenarioTestPanel and its direct components
@("1773508276","1773508277","1773508278","1773508279") | ForEach-Object { [void]$removeIds.Add($_) }

# ToggleRainBtn and components
@("566339043","566339044","566339045","566339046","566339047") | ForEach-Object { [void]$removeIds.Add($_) }

# Now scan all blocks to find GOs/components for the named children
# Test_Heatwave GO (RT child 724126862)
# Test_Drought GO (RT child 1374786366)
# Test_Disease GO (RT child 1578927225)
# Reset_Healthy GO (RT child 1156927009)
# PanelHeader GO (RT child 135917087)
# ToggleRainBtn text child RT 1162169336

# Parse each block to find all objects that have m_Father or m_GameObject pointing to our remove list
# Strategy: iterate multiple passes until no new IDs are found

function Get-FileIDFromBlock($block) {
    if ($block -match '--- !u!\d+ &(\d+)') { return $Matches[1] }
    return $null
}

function Get-ReferencedIDs($block) {
    $ids = @()
    $matches2 = [regex]::Matches($block, '\{fileID: (\d+)\}')
    foreach ($m in $matches2) {
        $id = $m.Groups[1].Value
        if ($id -ne "0") { $ids += $id }
    }
    return $ids
}

# Build a map: fileID -> block content
$idToBlock = @{}
foreach ($block in $blocks) {
    $id = Get-FileIDFromBlock $block
    if ($id) { $idToBlock[$id] = $block }
}

# Iteratively expand removal set
$changed = $true
while ($changed) {
    $changed = $false
    foreach ($block in $blocks) {
        $id = Get-FileIDFromBlock $block
        if (-not $id) { continue }
        if ($removeIds.Contains($id)) { continue }  # already marked
        
        # Check if this block references any removed ID as m_GameObject or m_Father
        $refs = Get-ReferencedIDs $block
        foreach ($ref in $refs) {
            if ($removeIds.Contains($ref)) {
                # Check it's not just any reference but a parent/owner relationship
                if ($block -match "m_GameObject: \{fileID: $ref\}" -or 
                    $block -match "m_Father: \{fileID: $ref\}") {
                    [void]$removeIds.Add($id)
                    $changed = $true
                    break
                }
            }
        }
    }
}

Write-Host "FileIDs to remove: $($removeIds.Count)"
Write-Host ($removeIds -join ", ")

# Filter out blocks with removed IDs
$keptBlocks = @()
foreach ($block in $blocks) {
    $id = Get-FileIDFromBlock $block
    if ($id -and $removeIds.Contains($id)) {
        Write-Host "Removing block: $id ($(($block -split "`n")[1].Trim()))"
        continue
    }
    $keptBlocks += $block
}

# Also clean up m_Children references in Canvas RectTransform
$result = $keptBlocks -join ""

# Remove child references for removed IDs from any m_Children lists
foreach ($id in $removeIds) {
    $result = $result -replace "  - \{fileID: $id\}`r?`n", ""
}

# Write back
Set-Content $scenePath $result -NoNewline
Write-Host "Done. Kept $($keptBlocks.Count) blocks."
