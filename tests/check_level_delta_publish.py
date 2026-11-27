"""Regression for exact publishing allowlist and the blocked BMD branch."""
import importlib.util, tempfile, pathlib, sys, json
from unittest.mock import patch
ROOT=pathlib.Path(__file__).resolve().parents[1]
spec=importlib.util.spec_from_file_location("level_delta",ROOT/"tools/level_editor/level_delta.py")
m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m)
assert "feat/bmd-v231" in m.PUBLISH_BRANCHES
assert "feat/unapproved" not in m.PUBLISH_BRANCHES
with tempfile.TemporaryDirectory() as tmp:
    root=pathlib.Path(tmp)
    active=m.map_path(root,3);active.parent.mkdir(parents=True)
    base='{"objects":[{"index":1,"id":1}],"editorRevision":1,"endGD":100}'
    active.write_text(base,encoding="utf-8")
    delta=root/"tools/level_editor/changes/BIOFIT_level_3_20261127_155800.json"
    delta.parent.mkdir(parents=True)
    delta.write_text(json.dumps(dict(schema=1,slot=3,editorRevision=1,baseSha256=m.sha(base),targetSha256=m.sha(base),endGD=100,removed=[],added=[],edits=[],order=[1]),separators=(",",":")),encoding="utf-8")
    calls=[]
    branch="feat/bmd-v231"
    def git(r,*args):
        calls.append(args)
        if args[0]=="branch":return branch
        if args[0]=="remote":return "https://github.com/"+m.REPO+".git"
        if args[0]=="ls-remote":return "abc refs/heads/"+branch
        if args[0]=="rev-parse":return "abc"
        return ""
    argv=["level_delta.py","--proyecto",str(root),"--delta",str(delta),"--publicar"]
    with patch.object(m,"git",git),patch.object(sys,"argv",argv):
        branch="feat/unapproved"
        try:m.main()
        except ValueError as e:assert "rama BIOFIT autorizada" in str(e)
        else:raise AssertionError("unauthorized branch allowed")
        assert not any(a[0] in ("add","commit","push") for a in calls)
        calls.clear();branch="feat/bmd-v231";m.main()
        assert ("push","origin","HEAD:refs/heads/feat/bmd-v231") in calls
        assert ("add","--","tools/level_editor/changes/"+delta.name,"tools/level_editor/changes/INDEX.md") in calls
        assert active.read_text(encoding="utf-8")==base
print("PASS delta publication BMD allowlist, unauthorized guard and map preservation")
