"""Offline source parser regression: no network dependency in CI."""
import base64
import gzip
import importlib.util
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
target = ROOT / "tools/assets/fetch_stereo_reference.py"
spec = importlib.util.spec_from_file_location("fetch_stereo_reference", target)
assert spec and spec.loader
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)

inner = "kS38,1,0;1,1,2,45,3,15;1,9,2,75,3,15,6,180;1,13,2,105,3,105;"
encoded = base64.urlsafe_b64encode(gzip.compress(inner.encode())).decode().rstrip("=")
server = "1:77236587:2:Stereo Madness:4:" + encoded + ":45:3#hash1#hash2"
assert module.parse_server_response(server) == inner
objects = module.parse_objects(inner)
assert len(objects) == 3
assert [o["id"] for o in objects] == [1, 9, 13]
assert objects[1]["rotation_gd"] == 180.0
assert module.summarize(objects)["recognized_object_count"] == 3
try:
    module.parse_server_response("-1")
    assert False, "-1 must fail"
except ValueError:
    pass
assert "1" in module.decode_inner_level(inner)
print("PASS offline GD raw-string parser/gzip/JSON source audit")
