#!/usr/bin/env python3
"""Local V2 overlay; no remote Git actions or proprietary files in this package."""
import argparse, collections, hashlib, io, json, plistlib, re, shutil, subprocess, tempfile, zipfile
from datetime import datetime
from pathlib import Path
from PIL import Image, ImageChops, ImageFilter, ImageOps
BASE=Path(__file__).resolve().parent
HEAD='73cd148b9f3747b736eb4972c88ddee5a4fbe517'
SHA='baac8024b41f90e5d08e519f4b51b423e2038853aecb3ce59c6e428d1b49af12'
ART={'pause':'GJ_pauseBtn_001.png','back':'GJ_backBtn_001.png','play':'GJ_playBtn2_001.png','restart':'GJ_replayBtn_001.png','menu':'GJ_menuBtn_001.png','practice':'GJ_practiceBtn_001.png','normal':'GJ_normalBtn_001.png','add_checkpoint':'GJ_checkpointBtn_001.png','remove_checkpoint':'GJ_removeCheckBtn_001.png','coin_empty':'secretCoinUI2_001.png','coin_full':'secretCoinUI_001.png','checkpoint':'checkpoint_01_001.png','checkpoint_color':'checkpoint_01_color_001.png'}
CS=['BiofitStereoLocalRuntime.cs','BiofitStereoSolidContact.cs','BiofitStereoPractice.cs','BiofitV2BitmapText.cs','BiofitV2Hud.cs']
def one(s,a,b):
    if s.count(a)!=1: raise ValueError('No se encontro exactamente una firma V1: '+a[:120])
    return s.replace(a,b,1)
def read(p): return p.read_text(encoding='utf-8-sig').replace('\r\n','\n')
def num(s): return list(map(int,re.findall(r'-?\d+',s)))
def metrics(s):
    def vals(l):return {k:int(v) for k,v in re.findall(r'\b(\w+)=(-?\d+)(?=\s|$)',l)}
    lines=s.splitlines();info=vals(lines[0]);common=vals(lines[1]);glyphs=[vals(l) for l in lines if l.startswith('char ')];kernings=[vals(l) for l in lines if l.startswith('kerning ')];
    assert glyphs and len({x['id'] for x in glyphs})==len(glyphs)
    return dict(size=abs(info['size']),lineHeight=common['lineHeight'],glyphs=glyphs,kernings=kernings)
def assets(stereo,interface,out):
    (out/'Fonts').mkdir(parents=True);(out/'UI').mkdir()
    with zipfile.ZipFile(interface) as z:
        if z.testzip():raise ValueError('ZIP de interfaz corrupto')
        for name in ['bigFont','goldFont','chatFont']:
            d=metrics(z.read(name+'-hd.fnt').decode('utf-8-sig'))
            im=Image.open(io.BytesIO(z.read(name+'-hd.png'))).convert('RGBA')
            for g in d['glyphs']:
                assert g['x']>=0 and g['y']>=0 and g['x']+g['width']<=im.width and g['y']+g['height']<=im.height
            im.save(out/'Fonts'/f'{name}.png');(out/'Fonts'/f'{name}_metrics.json').write_text(json.dumps(d,separators=(',',':')),encoding='utf-8')
        im=Image.open(io.BytesIO(z.read('GJ_progressBar_001-hd.png'))).convert('RGBA');alpha=im.getchannel('A');padded=ImageOps.expand(alpha,border=4,fill=0);inner=padded.filter(ImageFilter.MinFilter(7)).crop((4,4,4+im.width,4+im.height));border=ImageChops.subtract(alpha,inner)
        rim=Image.new('RGBA',im.size,(255,255,255,0));rim.putalpha(border);rim.save(out/'UI/progress_frame.png')
    with zipfile.ZipFile(stereo) as z:
        if z.testzip() or hashlib.sha256(z.read('1.txt').strip()).hexdigest()!=SHA: raise ValueError('ZIP Stereo no corresponde a V1')
        frames={}
        for name in z.namelist():
            if name.startswith('GJ_GameSheet') and name.endswith('.plist'):
                im=Image.open(io.BytesIO(z.read(name.replace('.plist','.png')))).convert('RGBA')
                for k,v in plistlib.loads(z.read(name))['frames'].items(): frames.setdefault(k,(im,v))
        for key,name in ART.items():
            im,v=frames[name]; x,y,w,h=num(v['textureRect']);rot=v['textureRotated'];sw,sh=num(v['spriteSourceSize']);ox,oy=num(v['spriteOffset'])
            crop=im.crop((x,y,x+(h if rot else w),y+(w if rot else h)))
            if rot:crop=crop.transpose(Image.Transpose.ROTATE_90)
            assert crop.size==(w,h)
            full=Image.new('RGBA',(sw,sh));full.paste(crop,(round((sw-w)/2+ox),round((sh-h)/2-oy)))
            full.save(out/'UI'/f'{key}.png')
        # Uncollected secret coin uses the same star, desaturated; the source UI2 is a different coin type.
        coin=Image.open(out/'UI/coin_full.png').convert('RGBA');gray=ImageOps.grayscale(coin).convert('RGBA');gray.putalpha(coin.getchannel('A'));gray.save(out/'UI/coin_empty.png')
        # Use checkpoint base and colored interior together, preserving the source alpha.
        a=Image.open(out/'UI/checkpoint.png').convert('RGBA');b=Image.open(out/'UI/checkpoint_color.png').convert('RGBA');
        if a.size==b.size:
            # Original color layer is white; tint green for practice marker.
            b=ImageChops.multiply(b,Image.new('RGBA',b.size,(80,255,70,255)));a.alpha_composite(b);a.save(out/'UI/checkpoint.png')
    return {'fonts':3,'ui_png':len(list((out/'UI').glob('*.png')))}

def patch_ux(s):
    s=one(s,'backIcon = Resources.Load<Texture2D>("BiofitUi/Back");','backIcon = BiofitV2BitmapText.Art("back") ?? Resources.Load<Texture2D>("BiofitUi/Back");')
    s=one(s,'pauseIcon = Resources.Load<Texture2D>("BiofitUi/Pause");','pauseIcon = BiofitV2BitmapText.Art("pause") ?? Resources.Load<Texture2D>("BiofitUi/Pause");')
    s=one(s,'            return t;\n        }\n\n        private Button Button(','''            BiofitV2BitmapText.Attach(t, label.Length>95 ? "secondary" : "main");
            return t;
        }

        private Button Button(''')
    s=one(s,'if (sceneName == Menu) { ConfigureOriginalMenu(); return; }','''if (sceneName == Menu) { ConfigureOriginalMenu(); AttachSceneFonts(); return; }''')
    s=one(s,'            if (sceneName != Menu) BuildBackButton(sceneName);\n        }','''            if (sceneName != Menu) BuildBackButton(sceneName);
            AttachSceneFonts();
        }

        private static void AttachSceneFonts()
        {
            foreach (var t in FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (t is TextMeshProUGUI) BiofitV2BitmapText.Attach(t);
        }''')
    # Keep the back button aligned with top row only during gameplay.
    s=one(s,'new Vector2(.080f, .902f), new Vector2(88f, 94f),','scene == LevelScene ? new Vector2(.06f, .93f) : new Vector2(.080f, .902f), new Vector2(88f, 88f),')
    start=s.index('        private void BuildLevelHud()');end=s.index('        private void TogglePause()',start)
    s=s[:start]+(BASE/'hud_fragment.txt').read_text()+s[end:]
    start=s.index('        private void BuildPauseScreen()');end=s.index('        private void RestartLevel()',start)
    s=s[:start]+(BASE/'pause_fragment.txt').read_text()+s[end:]
    return s

def patch_modes(s):
    return one(s,'        public static int Collected { get; private set; }','''        public static int Collected { get; private set; }
        public static void RestorePractice(BiofitStereoCoinPickup[] picked)
        {
            if(!BiofitStereoPractice.Enabled) return;
            ResetRun();
            foreach(var coin in picked)
                if(coin!=null && coin.gameObject.activeSelf) {coin.gameObject.SetActive(false);Collected++;}
        }''')

def main():
    ap=argparse.ArgumentParser();ap.add_argument('--proyecto',required=True,type=Path);ap.add_argument('--recursos',required=True,type=Path);ap.add_argument('--interfaz',required=True,type=Path);ap.add_argument('--musica-practica',type=Path);a=ap.parse_args()
    root=a.proyecto.resolve(); geome=root/'Assets/BIOFIT/GeomeTry';local=root/'Assets/BIOFIT/Resources/BiofitStereoLocal';out=root/'Assets/BIOFIT/Resources/BiofitStereoV2';
    def git(*args):return subprocess.check_output(['git','-C',str(root),*args],text=True).strip()
    if git('rev-parse','HEAD')!=HEAD:raise SystemExit('Se requiere el commit base de V1 '+HEAD)
    if out.exists():raise SystemExit('V2 ya instalada, no se sobreescribe.')
    d=json.loads((local/'Objects.json').read_text(encoding='utf-8'));assert d['sourceSha256']==SHA and len(d['objects'])==2291 and len({o['id'] for o in d['objects']})==31
    runtime=geome/'BiofitStereoLocalRuntime.cs'; ux=geome/'BiofitGeometryUX.cs'; modes=geome/'BiofitThreeModes.cs';post=geome/'Editor/BiofitStereoLocalTextureImport.cs'
    if hashlib.sha256(read(runtime).encode()).hexdigest()!='ef660f5a78eac01a22c41ec13ad869c2f3612543733109208b63b70304f128f3':raise SystemExit('Runtime V1 distinto; no se aplica automaticamente.')
    if 'BiofitStereoLocalRuntime.SelectMap(i, map)' not in read(ux):raise SystemExit('V1 no instalada en UX.')
    new={runtime:read(BASE/runtime.name),ux:patch_ux(read(ux)),modes:patch_modes(read(modes)),post:one(read(post),'if(!assetPath.StartsWith("Assets/BIOFIT/Resources/BiofitStereoLocal/Sprites/",System.StringComparison.Ordinal)) return;','''if(!assetPath.StartsWith("Assets/BIOFIT/Resources/BiofitStereoLocal/Sprites/",System.StringComparison.Ordinal) &&
                !assetPath.StartsWith("Assets/BIOFIT/Resources/BiofitStereoV2/",System.StringComparison.Ordinal)) return;''')}
    for name in CS[1:]:
        p=geome/name
        if p.exists():raise SystemExit('Ya existe '+str(p))
        new[p]=read(BASE/name)
    exclude=Path(git('rev-parse','--absolute-git-dir'))/'info/exclude'
    backup=root/'Builds'/('StereoV2_backup_'+datetime.now().strftime('%Y%m%d_%H%M%S'))
    with tempfile.TemporaryDirectory() as td:
        stage=Path(td)/'assets';report=assets(a.recursos,a.interfaz,stage)
        if a.musica_practica and a.musica_practica.is_file():shutil.copy2(a.musica_practica,stage/'PracticeMusic.mp3');report['practiceMusic']=True
        else: report['practiceMusic']=False
        backups={p:p.read_bytes() if p.exists() else None for p in new};oldexclude=exclude.read_bytes() if exclude.exists() else None
        backup.mkdir(parents=True,exist_ok=False)
        for p,b in backups.items():
            if b is not None:
                q=backup/p.relative_to(root);q.parent.mkdir(parents=True,exist_ok=True);q.write_bytes(b)
        try:
            exclude.parent.mkdir(parents=True,exist_ok=True)
            with exclude.open('ab') as f:f.write(b'\n# Local original UI assets; do not stage\n/Assets/BIOFIT/Resources/BiofitStereoV2/\n/Assets/BIOFIT/Resources/BiofitStereoV2.meta\n')
            shutil.copytree(stage,out)
            for p,s in new.items():p.write_text(s,encoding='utf-8')
        except BaseException:
            for p,b in backups.items():
                if b is None:p.unlink(missing_ok=True)
                else:p.write_bytes(b)
            if out.exists():shutil.rmtree(out)
            if oldexclude is None:exclude.unlink(missing_ok=True)
            else:exclude.write_bytes(oldexclude)
            raise
    print(json.dumps(report,indent=2));print('OK V2 LOCAL - 2291 registros intactos, 3 fuentes, HUD y practica.');print('Backup: '+str(backup));print('No hacer git add -f ni push de recursos locales.')
if __name__=='__main__':main()
