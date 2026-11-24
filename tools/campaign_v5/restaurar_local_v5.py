#!/usr/bin/env python3
"""Restaurar recursos privados de una copia validada. Unity cerrado."""
import argparse, hashlib, json, re, zipfile
from pathlib import Path, PurePosixPath
R="Assets/BIOFIT/Resources/"
CHAR="Assets/GeomeTryOriginal/Assets/Scripts/GameMechanics/CharacterType/Character.cs"
LOCAL=[R+"BiofitStereoLocal",R+"BiofitStereoV2",R+"Songs/Musics/BiofitLocalV5",R+"BiofitUi",R+"BiofitThreeModes/Artwork"]
def allowed(n):
    p=PurePosixPath(n)
    return not p.is_absolute() and ".." not in p.parts and "\\" not in n and (n==CHAR or n in [R+"BiofitUi.meta", R+"Songs.meta", R+"Songs/Musics.meta"] or any(n==q+".meta" or n.startswith(q+"/") for q in LOCAL))
def sha(b):return hashlib.sha256(b).hexdigest()
def main():
    p=argparse.ArgumentParser();p.add_argument("--proyecto",required=True,type=Path);p.add_argument("--respaldo",required=True,type=Path);a=p.parse_args();root=a.proyecto.resolve()
    assert (root/"Assets/BIOFIT/GeomeTry/BiofitV5Interaction.cs").is_file(),"Falta codigo V5."
    assert (root/CHAR).is_file(),"Ejecutar primero tools/prepare_geometry_original.ps1 en clon nuevo."
    with zipfile.ZipFile(a.respaldo) as z:
        manifest=json.loads(z.read("MANIFIESTO_SHA256.json"))
        assert set(z.namelist())==set(manifest)|{"MANIFIESTO_SHA256.json"} and len(z.namelist())==len(manifest)+1
        plan={}
        for n,h in manifest.items():
            assert allowed(n),"Ruta no autorizada: "+n
            data=z.read(n);assert sha(data)==h,"Hash invalido: "+n
            dest=root/n;assert dest.resolve().is_relative_to(root),"Ruta fuera del proyecto."
            if dest.exists():
                if dest.read_bytes()==data:continue
                if n==CHAR:
                    original=root/"ThirdParty/GeomeTryOriginal/Assets/Scripts/GameMechanics/CharacterType/Character.cs"
                    source=original.read_text(encoding="utf-8-sig")
                    compat=re.sub(r"(?<![A-Za-z0-9_])Camera(?![A-Za-z0-9_])","GeometryCamera",source)
                    expected=compat.replace("Input.GetMouseButton(0)","(Input.GetMouseButton(0) && !Biofit.GeomeTryOriginal.BiofitStereoPracticeButton.BlocksGameplayPointer())",1)
                    assert dest.read_text(encoding="utf-8-sig")==compat and data.decode("utf-8-sig").replace("\r\n","\n")==expected,"Character no coincide con V3."
                else:raise ValueError("Recurso local diferente; no se sobrescribe: "+n)
            plan[dest]=data
        old={q:q.read_bytes() if q.exists() else None for q in plan}
        try:
            for q,b in plan.items():q.parent.mkdir(parents=True,exist_ok=True);q.write_bytes(b)
            assert all(q.read_bytes()==b for q,b in plan.items())
        except BaseException:
            for q,b in old.items():
                if b is None:q.unlink(missing_ok=True)
                else:q.write_bytes(b)
            raise
    print("OK recursos V5 restaurados:",len(plan),"archivos escritos. Abrir Unity.")
if __name__=="__main__":main()
