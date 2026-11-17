"""Counterexample against actual v2.2.7 source; does not modify the firmware checkout.
Usage: python3 tests/PhaseAudit/replay_firmware.py /path/to/BIOFIT-GAME-Firmware
Requires g++, Python, and the pinned firmware checkout (no Arduino/hardware).
"""
from pathlib import Path
import subprocess
import sys
import tempfile

root = Path(sys.argv[1]).resolve()
sha = subprocess.check_output(['git', '-C', str(root), 'rev-parse', 'HEAD'], text=True).strip()
assert sha == '9cb5b7ac06315b0b657a33b8e608aca335eccb9e', sha
assert not subprocess.check_output(['git', '-C', str(root), 'status', '--porcelain'], text=True).strip(), 'Use a clean firmware checkout'
# Reuse the upstream extractor and its hardware mocks, replacing only main().
harness = (root / 'tests/host/replay.cpp').read_text().split('int main(', 1)[0]
harness += r'''
int main() {
 for (float amplitude : {.15f,.25f,.35f}) {
  boot();
  // Defaults and AUTO remain unchanged. Initial position elevated, v=0.
  // Accelerate down 100 ms, coast 400 ms, brake 100 ms: v never positive.
  float physicalV=0, displacement=0; int brakeUps=0;
  for (int i=0;i<120;i++) {
   float a=i<20?-amplitude:i<100?0.f:amplitude;
   physicalV+=a*G_MS2*.005f;
   displacement+=physicalV*.005f;
   int before=ups();sample(0,0,1+a);
   if(ups()>before) {
    assert(i>=100);assert(physicalV < -0.01f);brakeUps++;
    std::cout<<"UP during descent braking: a="<<amplitude
     <<" t="<<(i+1)*5<<"ms true_v="<<physicalV
     <<" shortV="<<shortV<<" event_g="<<events.back().up<<"\n";
   }
  }
  assert(brakeUps==1);assert(fabsf(physicalV)<1e-5f);assert(displacement<0);
 }
 // The same positive acceleration also emits UP on a genuine lift.
 boot();int before=ups();for(int i=0;i<20;i++)sample(0,0,1.35f);
 assert(ups()==before+1);
 std::cout<<"PASS: three braking counterexamples + genuine lift; actual detector, synthetic IMU\n";
}
'''
with tempfile.TemporaryDirectory() as directory:
    tmp = Path(directory)
    (tmp/'Firmware').symlink_to(root/'Firmware', target_is_directory=True)
    host = tmp/'tests/host'
    host.mkdir(parents=True)
    (host/'replay.cpp').write_text(harness)
    (host/'run_replay.py').write_text((root/'tests/host/run_replay.py').read_text())
    subprocess.run([sys.executable, str(host/'run_replay.py')], check=True)
