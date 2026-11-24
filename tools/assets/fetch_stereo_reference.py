#!/usr/bin/env python3
"""Download/read public GD level *object coordinates* for an exact-layout audit.

Default is READ-ONLY and outputs a private reference JSON in Builds/.
It DOES NOT overwrite Unity levels or import RobTop-owned artwork.
Public level ID 77236587 is a RobsVault copy, not RobTop's internal file.
"""
from __future__ import annotations
import argparse
import base64
from collections import Counter
import gzip
import json
from pathlib import Path
import urllib.error
import urllib.parse
import urllib.request
import zlib

LEVEL_ID = 77236587
ENDPOINT = "https://www.boomlings.com/database/downloadGJLevel22.php"
# Documented GD community protocol constant; NOT a user credential.
GD_PUBLIC_SECRET = "Wmfd2893gb7"


def decode_inner_level(encoded: str) -> str:
    encoded = encoded.strip()
    if encoded.startswith("kS") or encoded.startswith("1,"):
        return encoded
    padded = encoded.replace("-", "+").replace("_", "/")
    padded += "=" * (-len(padded) % 4)
    raw = base64.b64decode(padded, validate=True)
    try:
        return gzip.decompress(raw).decode("utf-8")
    except (OSError, UnicodeDecodeError):
        try:
            return zlib.decompress(raw).decode("utf-8")
        except (zlib.error, UnicodeDecodeError) as exc:
            raise ValueError("Encoded source is not recognized gzip/zlib") from exc


def parse_server_response(raw: str) -> str:
    """The GD server sends a colon-delimited key-value *outer* string."""
    first = raw.strip().split("#", 1)[0]
    if first == "-1" or not first:
        raise ValueError("GD server returned -1/empty (network/rate limit/invalid ID)")
    parts = first.split(":")
    fields = {parts[i]: parts[i+1] for i in range(0, len(parts)-1, 2)}
    encoded = fields.get("4")
    if not encoded:
        raise ValueError("Downloaded level has no key 4 (compressed object string)")
    return decode_inner_level(encoded)


def parse_objects(inner: str) -> list[dict]:
    result = []
    for item in inner.split(";"):
        if not item.strip():
            continue
        parts = item.split(",")
        fields = {parts[i]: parts[i+1] for i in range(0, len(parts)-1, 2)}
        if "1" not in fields:  # the first kSxx metadata segment
            continue
        try:
            obj_id = int(fields["1"])
            x = float(fields["2"])
            y = float(fields["3"])
            angle = float(fields.get("6", "0"))
        except (ValueError, KeyError) as exc:
            raise ValueError("Malformed GD object ID/position: " + item[:90]) from exc
        result.append({"id": obj_id, "x_gd": x, "y_gd": y,
                       "rotation_gd": angle, "props": fields})
    return result


def download_encoded_level() -> str:
    payload = urllib.parse.urlencode({
        "gameVersion": "22", "binaryVersion": "35", "gdw": "0",
        "levelID": str(LEVEL_ID), "inc": "1", "extras": "0",
        "secret": GD_PUBLIC_SECRET
    }).encode("ascii")
    request = urllib.request.Request(
        ENDPOINT, data=payload,
        headers={"User-Agent": "BIOFIT-DASH-LevelLayoutResearch/1.0",
                 "Content-Type": "application/x-www-form-urlencoded"},
        method="POST"
    )
    try:
        with urllib.request.urlopen(request, timeout=25) as res:
            return res.read(8 * 1024 * 1024).decode("utf-8")
    except (urllib.error.URLError, TimeoutError) as exc:
        raise RuntimeError("No se pudo consultar GD; reintentar luego o usar --source") from exc


def summarize(objects: list[dict]) -> dict:
    counts = Counter(str(o["id"]) for o in objects)
    # These are known basic IDs only, NOT a full original-object importer.
    known = {"1": "block", "8": "smallSpike", "9": "spike",
             "12": "cubePortal", "13": "shipPortal"}
    coverage = sum(v for k,v in counts.items() if k in known)
    return {
        "source_level_id": LEVEL_ID,
        "object_count": len(objects),
        "object_ids": dict(counts.most_common()),
        "basic_ids_recognized": known,
        "recognized_object_count": coverage,
        "unknown_object_count": len(objects)-coverage,
        "warning": (
            "Original placement reference only. Importing all objects exactly "
            "requires complete GD ID-to-prefab mapping, GD 30px/unit scaling, "
            "layer/trigger/coin support and pixel art. Never silently drop unknown IDs."
        )
    }


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source", type=Path, help="optional already-downloaded raw server response or inner level string")
    parser.add_argument("--output", type=Path, default=Path("Builds/stereo_reference_objects_77236587.json"))
    args = parser.parse_args()
    raw = args.source.read_text(encoding="utf-8") if args.source else download_encoded_level()
    inner = (parse_server_response(raw) if raw.lstrip().startswith(("1:", "2:", "3:", "4:", "-1"))
             else decode_inner_level(raw))
    objects = parse_objects(inner)
    if len(objects) < 100:
        raise ValueError("Too few objects: this is not verified Stereo Madness data")
    result = {"report": summarize(objects), "objects": objects}
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
    print(f"Reference inspected: {len(objects)} objects, "
          f"{result['report']['unknown_object_count']} IDs lacking our basic prefab mapping")
    print("Saved:", args.output)
    print("No Unity level or proprietary asset modified.")


if __name__ == "__main__":
    main()
