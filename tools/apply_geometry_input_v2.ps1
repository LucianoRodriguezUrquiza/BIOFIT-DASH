# Narrow, idempotent input/diagnostic overlays. Never modifies the pinned source.
# Refuse unexpected local edits; compare normalized text ONLY for CRLF tolerance.
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$source = Join-Path $root 'ThirdParty/GeomeTryOriginal/Assets/Scripts'
$dest = Join-Path $root 'Assets/GeomeTryOriginal/Assets/Scripts'
function Normalize-InputText([string]$s) { return $s.Replace("`r`n", "`n") }
$entries = @(
    @{ Rel='GameMechanics/CharacterType/Character.cs'; Overlay='Character.MotionV2.cs' },
    @{ Rel='GameMechanics/CharacterType/Cube.cs'; Overlay='Cube.MotionV2.cs' },
    @{ Rel='JumpOrb.cs'; Overlay='JumpOrb.MotionV2.cs' }
)
# Preflight ALL files before writing any overlay.
foreach ($e in $entries) {
    $path = Join-Path $dest $e.Rel
    $expected = Normalize-InputText ([IO.File]::ReadAllText((Join-Path $PSScriptRoot ('geometry_compat/' + $e.Overlay))))
    $actual = Normalize-InputText ([IO.File]::ReadAllText($path))
    $original = Normalize-InputText ([IO.File]::ReadAllText((Join-Path $source $e.Rel)))
    if ($e.Overlay -eq 'Character.MotionV2.cs') {
        $original = $original.Replace('FindFirstObjectByType<Camera>()', 'FindFirstObjectByType<GeometryCamera>()')
    }
    if ($e.Overlay -eq 'JumpOrb.MotionV2.cs') {
        $old = 'Input.GetKey(KeyCode.Space) || Input.GetMouseButton(0)'
        $expanded = $old + " ||`n" + '            (other.GetComponent<Cube>() != null &&' + "`n" +
            '             Biofit.GeomeTryOriginal.BiofitGeomeTryLegacyBridge.TryConsumeOrbPulse())'
        $original = $original.Replace($old, $expanded)
    }
    if ($actual -cne $expected -and $actual -cne $original) {
        throw "Cambios locales no reconocidos en $path. Se conservan; no se instala la entrada V2."
    }
    if (!(Test-Path -LiteralPath ($path + '.meta'))) { throw "Falta GUID de $path" }
}
foreach ($e in $entries) {
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot ('geometry_compat/' + $e.Overlay)) -Destination (Join-Path $dest $e.Rel) -Force
}
Write-Host 'OK: entrada mantenida V2 en Character.Update y hooks de diagnostico; fisica y GUID intactos.'
