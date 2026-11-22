"""Offline contract for the isolated WaveVibe spike test (no Unity required)."""
import json
import pathlib

root = pathlib.Path(__file__).resolve().parents[1]
path = root / "tools" / "geometry_training" / "WaveVibe.json"
level = json.loads(path.read_text(encoding="utf-8-sig"))

assert set(level) == {"id", "name", "difficulty", "map", "music"}
assert (level["id"], level["name"], level["difficulty"], level["music"]) == (
    4, "Wave Vibe", 1, "CantLetGo"
)
spikes = [item for item in level["map"] if item["type"] == "spike"]
markers = [item for item in level["map"] if item["type"] == "ground"]
assert len(level["map"]) == 8
assert len(spikes) == 7 and len(markers) == 1
assert [item["x"] for item in spikes] == [25, 65, 105, 145, 185, 225, 265]
assert all(item["y"] == 0 and set(item) == {"type", "x", "y"} for item in spikes)
assert markers == [{"type": "ground", "x": 285, "y": -2}]
assert len({(item["x"], item["y"]) for item in level["map"]}) == len(level["map"])
# Original GeomeTry Character: 6.5 units/Unity second; timeScale 1.5.
# Original Game places player near x=-10, finish near max(map.x)+1.
duration_estimate = (285 + 1 - (-10)) / (6.5 * 1.5)
assert 29 <= duration_estimate <= 32, duration_estimate
print(f"WaveVibe training PASS: 7 spikes, 40-unit gaps; nominal {duration_estimate:.2f} s")
