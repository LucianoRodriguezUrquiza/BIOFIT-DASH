"""V2 source preservation + native integration contract (not engine validation)."""
from pathlib import Path
import re
root=Path(__file__).resolve().parents[1]
bridge=(root/'Assets/BIOFIT/GeomeTry/BiofitGeomeTryLegacyBridge.cs').read_text()
builder=(root/'Assets/BIOFIT/GeomeTry/Editor/BiofitGeomeTryOriginalBuild.cs').read_text()
monitor=(root/'Assets/BIOFIT/Scripts/BiofitBLEMonitor.cs').read_text()
partial=(root/'Assets/BIOFIT/Scripts/BiofitBLEMonitor.Motion.cs').read_text()
upstream=root/'ThirdParty/GeomeTryOriginal/Assets/Scripts'
hook='Biofit.GeomeTryOriginal.BiofitGeomeTryLegacyBridge'
# Remove ONLY the enumerated input/observability hooks, then demand complete
# equality with the actual original physics/lifecycle implementation.
character=(root/'tools/geometry_compat/Character.MotionV2.cs').read_text()
character=character.replace('FindFirstObjectByType<GeometryCamera>()','FindFirstObjectByType<Camera>()')
character=character.replace('        '+hook+'.CharacterStarted(this);\n','')
character=character.replace('        '+hook+'.CharacterDied(this);\n','')
character=character.replace('''bool physicalHeld = Input.GetKey(KeyCode.Space) || Input.GetMouseButton(0);
        bool virtualHeld = '''+hook+'''.ReadHeldInput(this, physicalHeld);
        if (physicalHeld || virtualHeld)''','if (Input.GetKey(KeyCode.Space) || Input.GetMouseButton(0))')
character=character.replace('''bool grounded = frontHit.collider != null || backHit.collider != null;
        '''+hook+'''.GroundObserved(this, grounded);
        return grounded;''','return frontHit.collider != null || backHit.collider != null;')
character=character.replace('''    protected virtual void OnDestroy()
    {
        '''+hook+'''.CharacterDestroyed(this);
    }

''','')
assert character == (upstream/'GameMechanics/CharacterType/Character.cs').read_text(), 'Unexpected Character physics/lifecycle alteration'
cube=(root/'tools/geometry_compat/Cube.MotionV2.cs').read_text().replace('            '+hook+'.JumpApplied(this);\n','')
assert cube == (upstream/'GameMechanics/CharacterType/Cube.cs').read_text(), 'Unexpected Cube alteration'
orb=(root/'tools/geometry_compat/JumpOrb.MotionV2.cs').read_text().replace(' ||\n            '+hook+'.TryConsumeOrbPulse(other.GetComponent<Cube>())','').replace('                '+hook+'.OrbApplied(character);\n','')
assert orb == (upstream/'JumpOrb.cs').read_text(), 'Unexpected orb physics alteration'
for forbidden in ('System.Reflection', 'GetField(', 'FindGameObjectWithTag', 'cube.onClick();', 'rb.linearVelocity =', 'Time.timeScale =', 'BiofitJumpBuffer'):
    assert forbidden not in bridge, forbidden
assert 'value.GetInstanceID() == b.actorId' in bridge
assert 'if (id == b.actorId)' in bridge
assert 'MotionLinkRecoveryEnabled = true' in bridge
assert 'Biofit.GeomeTryOriginal' not in monitor+partial
assert 'DefaultExecutionOrder(-200)' in monitor
assert 'if (!tracker.statusGate.ObserveSequence' in monitor
assert 'if (fresh && (eventType == 1 || eventType == 2))' in monitor
assert '0.90' not in bridge # pair callback is diagnostic only
assert 'com.biofitgame.geometrymotionv2' in builder
assert 'com.biofitgame.geometryheldexp' not in builder
assert 'PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, previousIdentifier)' in builder
assert 'apply_geometry_input_v2.ps1' in (root/'tools/apply_geometry_compat.ps1').read_text()
# The Editor-only injector must not exist in the Android preprocessor branch.
assert '#if UNITY_EDITOR\n        private double editorTestUntil' in bridge
print('PASS: original Character/Cube/orb physics byte-equivalent after narrow input/telemetry hooks; V2 isolated APK and receiver boundaries')
