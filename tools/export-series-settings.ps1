# Dumps the HART series options' current values from this PC's registry to JSON, so the series profile in
# HART. Mode Patch (Core/SeriesSettings.cs) can be compared with the PC the series is recorded on. Read-only.
#
# Run on Windows with Big Ambitions closed (the game writes its PlayerPrefs when it quits):
#   powershell -ExecutionPolicy Bypass -File export-series-settings.ps1 [-Out <file.json>] [-All]
#
# Unity keeps PlayerPrefs under HKCU\Software\Hovgaard Games\Big Ambitions. Each value is named after its key plus
# "_h" and a hash of the key; a mod option's key is "m:<modId>:<optionId>" (the game's ModOptionPrefs), and toggles,
# sliders and dropdowns are stored as DWORD ints. The modId is the Workshop item ID for a Workshop mod and the folder
# path for a local one, so the script matches by option ID and lists every mod ID it finds for it. -All also lists
# every other mod option key in the registry.
param(
    [string]$Out = (Join-Path ([Environment]::GetFolderPath('Desktop')) 'hart-series-settings.json'),
    [switch]$All
)
$ErrorActionPreference = 'Stop'
$RegistryPath = 'HKCU:\Software\Hovgaard Games\Big Ambitions'

# The values HART. Mode Patch's "Apply HART series settings" button writes, in its order. Keep in step with
# Core/SeriesSettings.cs; tools/SettingsCheck compares the two.
$Series = @(
    @{ Owner = 'HARTModPack'; Id = 'scenario_indebted_start'; Value = 1 }
    @{ Owner = 'HARTModPack'; Id = 'rent_rebalance'; Value = 0 }
    @{ Owner = 'HARTModPack'; Id = 'happiness_rebalance'; Value = 1 }
    @{ Owner = 'HARTModPack'; Id = 'owner_job_full_pay'; Value = 1 }
    @{ Owner = 'HARTModPack'; Id = 'factory_diplomas'; Value = 1 }
    @{ Owner = 'HARTModPack'; Id = 'equipment_utilities'; Value = 1 }
    @{ Owner = 'HARTModPack'; Id = 'office_it'; Value = 1 }
    @{ Owner = 'HARTModPack'; Id = 'mute_rlo_night_chirping'; Value = 1 }
    @{ Owner = 'HARTRivalBalance'; Id = 'rival_preset'; Value = 1 }
    @{ Owner = 'DynamicRivals'; Id = 'wholesale_rival_count'; Value = 0 }
    @{ Owner = 'DynamicRivals'; Id = 'import_rival_count'; Value = 1 }
    @{ Owner = 'DynamicRivals'; Id = 'retail_occupancy'; Value = 0 }
    @{ Owner = 'DynamicRivals'; Id = 'office_occupancy'; Value = 0 }
    @{ Owner = 'DynamicRivals'; Id = 'warehouse_occupancy'; Value = 0 }
    @{ Owner = 'DynamicRivals'; Id = 'residential_occupancy'; Value = 0 }
    @{ Owner = 'DynamicRivals'; Id = 'event_frequency'; Value = 2 }
    @{ Owner = 'RealLifeOverhaul'; Id = 'camera_enabled'; Value = 0 }
    @{ Owner = 'RealLifeOverhaul'; Id = 'camera_mouselook'; Value = 0 }
    @{ Owner = 'RealLifeOverhaul'; Id = 'car_camera'; Value = 0 }
    @{ Owner = 'RealLifeOverhaul'; Id = 'social_media'; Value = 0 }
    @{ Owner = 'RealLifeOverhaul'; Id = 'social_texts_max'; Value = 1 }
    @{ Owner = 'RealLifeOverhaul'; Id = 'life_events'; Value = 0 }
    @{ Owner = 'RealLifeOverhaul'; Id = 'accident_frequency'; Value = 1 }
    @{ Owner = 'RealLifeOverhaul'; Id = 'crime_frequency'; Value = 1 }
    @{ Owner = 'RealLifeOverhaul'; Id = 'roadside_checks'; Value = 0 }
    @{ Owner = 'RealLifeOverhaul'; Id = 'player_offences'; Value = 0 }
    @{ Owner = 'RealLifeOverhaul'; Id = 'street_event_frequency'; Value = 1 }
    @{ Owner = 'RealLifeOverhaul'; Id = 'fire_frequency'; Value = 1 }
    @{ Owner = 'RealLifeOverhaul'; Id = 'social_media_notices'; Value = 0 }
    @{ Owner = 'RealLifeOverhaul'; Id = 'accident_marker'; Value = 0 }
    @{ Owner = 'RealLifeOverhaul'; Id = 'accident_notices'; Value = 0 }
    @{ Owner = 'RealLifeOverhaul'; Id = 'ledger_online_notes'; Value = 0 }
    @{ Owner = 'RealLifeOverhaul'; Id = 'markets_dividend_notes'; Value = 0 }
    @{ Owner = 'RealLifeOverhaul'; Id = 'careers_agency_note'; Value = 0 }
    @{ Owner = 'RealLifeOverhaul'; Id = 'business_frequency'; Value = 2 }
    @{ Owner = 'RealLifeOverhaul'; Id = 'social_relationships'; Value = 1 }
    @{ Owner = 'RealLifeOverhaul'; Id = 'clothes_must_be_bought'; Value = 1 }
    @{ Owner = 'RealLifeOverhaul'; Id = 'driving_licence'; Value = 1 }
    @{ Owner = 'RealLifeOverhaul'; Id = 'driving_school'; Value = 1 }
    @{ Owner = 'RealLifeOverhaul'; Id = 'economy'; Value = 1 }
    @{ Owner = 'RealLifeOverhaul'; Id = 'economy_city'; Value = 1 }
    @{ Owner = 'RealLifeOverhaul'; Id = 'economy_company'; Value = 1 }
    @{ Owner = 'RealLifeOverhaul'; Id = 'utilities'; Value = 1 }
    @{ Owner = 'RealLifeOverhaul'; Id = 'economy_company_staff'; Value = 1 }
    @{ Owner = 'RealLifeOverhaul'; Id = 'economy_company_cover'; Value = 1 }
    @{ Owner = 'RealLifeOverhaul'; Id = 'economy_company_marketing'; Value = 1 }
    @{ Owner = 'RealLifeOverhaul'; Id = 'economy_company_tax'; Value = 1 }
    @{ Owner = 'RealLifeOverhaul'; Id = 'economy_company_licences'; Value = 1 }
    @{ Owner = 'RealLifeOverhaul'; Id = 'economy_rota'; Value = 1 }
    @{ Owner = 'RealLifeOverhaul'; Id = 'interiors'; Value = 1 }
    @{ Owner = 'RealLifeOverhaul'; Id = 'business_pressure'; Value = 1 }
    @{ Owner = 'RealLifeOverhaul'; Id = 'family'; Value = 1 }
    @{ Owner = 'RealLifeOverhaul'; Id = 'home_life'; Value = 1 }
    @{ Owner = 'RealLifeOverhaul'; Id = 'social'; Value = 1 }
    @{ Owner = 'RealLifeOverhaul'; Id = 'leisure'; Value = 1 }
    @{ Owner = 'RealLifeOverhaul'; Id = 'civic'; Value = 1 }
    @{ Owner = 'RealLifeOverhaul'; Id = 'civic_elections'; Value = 1 }
    @{ Owner = 'RealLifeOverhaul'; Id = 'civic_office'; Value = 1 }
    @{ Owner = 'RealLifeOverhaul'; Id = 'city_life'; Value = 1 }
    @{ Owner = 'RealLifeOverhaul'; Id = 'ambient_sound'; Value = 1 }
    @{ Owner = 'RealLifeOverhaul'; Id = 'night_life'; Value = 1 }
    @{ Owner = 'RealLifeOverhaul'; Id = 'transit_buses'; Value = 1 }
    @{ Owner = 'RealLifeOverhaul'; Id = 'transit_subway'; Value = 1 }
    @{ Owner = 'RealLifeOverhaul'; Id = 'transit_taxi_call'; Value = 1 }
    @{ Owner = 'RealLifeOverhaul'; Id = 'nav_minimap'; Value = 1 }
    @{ Owner = 'RealLifeOverhaul'; Id = 'nav_gps'; Value = 1 }
    @{ Owner = 'RealLifeOverhaul'; Id = 'planner'; Value = 1 }
    @{ Owner = 'RealLifeOverhaul'; Id = 'jobs'; Value = 1 }
    @{ Owner = 'RealLifeOverhaul'; Id = 'careers'; Value = 1 }
    @{ Owner = 'RealLifeOverhaul'; Id = 'careers_study'; Value = 1 }
    @{ Owner = 'RealLifeOverhaul'; Id = 'careers_jobs'; Value = 1 }
    @{ Owner = 'RealLifeOverhaul'; Id = 'careers_life'; Value = 1 }
    @{ Owner = 'RealLifeOverhaul'; Id = 'careers_degrees'; Value = 1 }
    @{ Owner = 'RealLifeOverhaul'; Id = 'economy_products'; Value = 1 }
    @{ Owner = 'RealLifeOverhaul'; Id = 'economy_consequences'; Value = 1 }
)

if (Get-Process -Name 'Big Ambitions' -ErrorAction SilentlyContinue) {
    throw 'Close Big Ambitions first: it writes its settings to the registry when it quits.'
}
if (-not (Test-Path -LiteralPath $RegistryPath)) { throw "No Big Ambitions settings at $RegistryPath." }
$key = Get-Item -LiteralPath $RegistryPath

# Unity's name hash: djb2 with xor over the key's bytes, as an unsigned 32-bit number.
function Get-PrefsHash([string]$Name) {
    [uint64]$h = 5381
    foreach ($b in [Text.Encoding]::UTF8.GetBytes($Name)) { $h = ([uint64](($h * 33) % 4294967296)) -bxor [uint64]$b }
    return [uint32]$h
}

$byOption = @{}
$others = @()
foreach ($name in $key.GetValueNames()) {
    if ($name -notmatch '^(m:(.+):([^:]+))_h(\d+)$') { continue }
    $prefKey = $Matches[1]; $modId = $Matches[2]; $optionId = $Matches[3]; $hash = $Matches[4]
    $kind = $key.GetValueKind($name).ToString()
    $raw = $key.GetValue($name)
    $value = if ($kind -eq 'DWord') { [int]$raw } else { "$raw" }
    $entry = [pscustomobject][ordered]@{ modId = $modId; value = $value; registryKind = $kind; hashOk = ((Get-PrefsHash $prefKey).ToString() -eq $hash) }
    if (-not $byOption.ContainsKey($optionId)) { $byOption[$optionId] = @() }
    $byOption[$optionId] = @($byOption[$optionId]) + @($entry)
    if ($All) { $others += [pscustomobject][ordered]@{ key = $prefKey; value = $value; registryKind = $kind } }
}

$rows = foreach ($s in $Series) {
    $found = @()
    if ($byOption.ContainsKey($s.Id)) { $found = @($byOption[$s.Id]) }
    $state = if ($found.Count -eq 0) { 'not stored (the mod default applies)' }
             elseif (@($found | Where-Object { $_.value -eq $s.Value }).Count -eq $found.Count) { 'matches' }
             elseif (@($found | Where-Object { $_.value -eq $s.Value }).Count -gt 0) { 'differs for some mod IDs' }
             else { 'differs' }
    [pscustomobject][ordered]@{ owner = $s.Owner; option = $s.Id; series = $s.Value; state = $state; stored = $found }
}

$report = [ordered]@{
    exported = (Get-Date).ToString('o')
    computer = $env:COMPUTERNAME
    registry = $RegistryPath
    note = 'Option IDs are matched by name; an ID that two mods share lists both mod IDs.'
    options = @($rows)
}
if ($All) { $report['allModOptions'] = $others }
$report | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $Out -Encoding UTF8
$rows | ForEach-Object { '{0,-18} {1,-28} series {2,-3} {3}' -f $_.owner, $_.option, $_.series, $_.state }
"Written to $Out"
