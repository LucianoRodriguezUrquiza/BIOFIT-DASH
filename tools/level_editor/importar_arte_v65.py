#!/usr/bin/env python3
"""Importa arte GD 2.207 exclusivamente desde una instalación local; no Git add ni push."""
import argparse, json, re, plistlib, subprocess, hashlib, sys
from pathlib import Path
from PIL import Image, ImageOps, ImageChops

def nums(s): return list(map(float,re.findall(r'-?\d+(?:\.\d+)?',s)))
def plist(p): return plistlib.loads(p.read_bytes().lstrip())
def frames(p): return plist(p)['frames']
def crop(img,v):
    x,y,w,h=map(int,nums(v['textureRect']))
    rot=v['textureRotated'];sw,sh=map(int,nums(v['spriteSourceSize']));ox,oy=nums(v['spriteOffset'])
    a=img.crop((x,y,x+(h if rot else w),y+(w if rot else h)))
    if rot:a=a.transpose(Image.Transpose.ROTATE_90)
    if a.size!=(w,h):raise ValueError('Atlas corrupto')
    out=Image.new('RGBA',(sw,sh));out.paste(a,(round((sw-w)/2+ox),round((sh-h)/2-oy)));return out
def tint(a,col):
    result=ImageChops.multiply(a.convert('RGB'),Image.new('RGB',a.size,col)).convert('RGBA')
    result.putalpha(a.getchannel('A'));return result
def save(p,a):
    p.parent.mkdir(parents=True,exist_ok=True)
    if p.exists():
        with Image.open(p) as old:
            if old.convert('RGBA').size==a.size and old.convert('RGBA').tobytes()==a.tobytes():return
        raise ValueError('No se sobrescribe arte local diferente: '+str(p))
    a.save(p)
def jsonsave(p,obj):
    s=json.dumps(obj,ensure_ascii=False,separators=(',',':'))
    p.parent.mkdir(parents=True,exist_ok=True)
    if p.exists() and p.read_text(encoding='utf-8')==s:return
    p.write_text(s,encoding='utf-8')
def ignored(root,p):
    rel=p.relative_to(root).as_posix()
    if subprocess.run(['git','-C',str(root),'check-ignore','-q','--',rel]).returncode!=0:raise ValueError('Ruta no ignorada; no se modifica .gitignore: '+rel)
    if subprocess.run(['git','-C',str(root),'ls-files','--error-unmatch',rel],stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL).returncode==0:raise ValueError('Ruta ya versionada: '+rel)
def center(dst,part,x,y):dst.alpha_composite(part,(round(x-part.width/2),round(y-part.height/2)))
def fit(a,size=96,maxdim=90):
    box=a.getbbox()
    if box is None:raise ValueError('Imagen vacía')
    a=a.crop(box);a.thumbnail((maxdim,maxdim),Image.Resampling.LANCZOS)
    out=Image.new('RGBA',(size,size));center(out,a,size/2,size/2);return out
def classify(name):
    # Only familiar square / plank families get a preliminary solid template.
    # Everything else is visual and can be assigned an explicit collider in the inspector.
    return 'solid' if re.match(r'^(square_\d+|square_b_\d+|block\d+[a-z]?_\d+|plank\d*_\d+)$',name,re.I) else 'decoration'
def import_catalog(gd,work):
    entries={}
    for stem in ('GJ_GameSheet','GJ_GameSheet02','GJ_GameSheetGlow'):
        p=gd/(stem+'-hd.plist');im=Image.open(p.with_suffix('.png')).convert('RGBA')
        for key,v in frames(p).items():
            if not key.endswith('.png'):continue
            name=key[:-4]
            if name.endswith('_001'):name=name[:-4]
            if not re.fullmatch(r'[A-Za-z0-9_-]+',name):continue
            if name.startswith(('edit_','gdh','GJ_','checkpoint','keyframe','secretCoin','bgIcon')):continue
            name='gd_'+name
            if name in entries:continue
            a=crop(im,v)
            if a.width<=0 or a.height<=0 or a.getbbox() is None:continue
            save(work/'Catalog'/f'{name}.png',a)
            entries[name]={'name':name,'widthGD':a.width/2,'heightGD':a.height/2,'kind':classify(name[3:])}
    jsonsave(work/'Catalog.json',{'items':list(entries.values())})
    return len(entries)
def atlas(p):
    data=frames(p);img=Image.open(p.with_suffix('.png')).convert('RGBA')
    return {k:crop(img,v) for k,v in data.items()}
def import_ball(gd,art):
    names=[];fail=[]
    for p in sorted((gd/'icons').glob('player_ball_*-hd.plist')):
        m=re.fullmatch(r'player_ball_(\d+)-hd',p.stem)
        if not m:continue
        stem='player_ball_'+m[1];name='V65Ball_'+m[1]
        try:
            f=atlas(p);a=f[stem+'_001.png'];out=Image.new('RGBA',(256,256))
            center(out,tint(a,(255,130,60)),128,128)
            if stem+'_2_001.png' in f:center(out,tint(f[stem+'_2_001.png'],(255,245,210)),128,128)
            if stem+'_extra_001.png' in f:center(out,f[stem+'_extra_001.png'],128,128)
            save(art/'V65Ball'/f'{name}.png',fit(out));names.append(name)
        except (KeyError,ValueError,OSError) as e:fail.append([p.name,str(e)])
    return names,fail
def spider_frames(gd,art):
    anim=plist(gd/'Spider_AnimDesc.plist')['animationContainer']
    runs=[anim[f'Spider_run_{n:03d}.png'] for n in range(1,8)]
    names=[];fail=[]
    for p in sorted((gd/'icons').glob('spider_*-hd.plist')):
        m=re.fullmatch(r'spider_(\d+)-hd',p.stem)
        if not m:continue
        stem='spider_'+m[1];name='V65Spider_'+m[1]
        try:
            f=atlas(p);images=[]
            for pose in runs:
                out=Image.new('RGBA',(256,256))
                for piece in sorted(pose.values(),key=lambda a:int(a['zValue'])):
                    k=piece['texture'].replace('spider_01',stem,1)
                    a=f[k];part=tint(a,(190,90,255))
                    secondary=k.replace('_001.png','_2_001.png')
                    if secondary in f:center(part,tint(f[secondary],(240,255,255)),part.width/2,part.height/2)
                    extra=k.replace('_001.png','_extra_001.png')
                    if extra in f:center(part,f[extra],part.width/2,part.height/2)
                    sx,sy=nums(piece['scale']);fx,fy=nums(piece['flipped'])
                    part=part.resize((max(1,round(part.width*abs(sx))),max(1,round(part.height*abs(sy)))),Image.Resampling.BICUBIC)
                    if bool(fx)^(sx<0):part=ImageOps.mirror(part)
                    if bool(fy)^(sy<0):part=ImageOps.flip(part)
                    part=part.rotate(-float(piece['rotation']),Image.Resampling.BICUBIC,expand=True)
                    x,y=nums(piece['position']);center(out,part,128+2*x,128-2*y)
                images.append(out)
            boxes=[a.getbbox() for a in images];box=(min(a[0] for a in boxes),min(a[1] for a in boxes),max(a[2] for a in boxes),max(a[3] for a in boxes))
            # Common crop and scale across the entire cycle; no frame-by-frame jitter.
            scale=90/max(box[2]-box[0],box[3]-box[1])
            size=(max(1,round((box[2]-box[0])*scale)),max(1,round((box[3]-box[1])*scale)))
            for n,a in enumerate(images):
                out=Image.new('RGBA',(96,96));part=a.crop(box).resize(size,Image.Resampling.LANCZOS)
                out.alpha_composite(part,((96-part.width)//2,96-part.height-3))
                save(art/'V65Spider'/f'{name}_{n}.png',out)
            names.append(name)
        except (KeyError,ValueError,OSError) as e:fail.append([p.name,str(e)])
    return names,fail
def main():
    p=argparse.ArgumentParser();p.add_argument('--proyecto',required=True,type=Path);p.add_argument('--recursos',required=True,type=Path);a=p.parse_args();root=a.proyecto.resolve();gd=a.recursos.resolve()
    if not (root/'.git').exists() or not (gd/'GJ_GameSheet-hd.plist').is_file():raise ValueError('Proyecto o Resources GD incorrecto')
    if (root/'Temp/UnityLockfile').exists():raise ValueError('Cerrá Unity')
    work=root/'Builds/BiofitV65Art';art=root/'Assets/BIOFIT/Resources/BiofitThreeModes/Artwork'
    for dest in (work/'Catalog/test.png',art/'V65Ball/test.png',art/'V65Spider/test.png',art/'V65Skins.json',root/'Assets/BIOFIT/Resources/BiofitStereoLocal/Sprites/gd_portal_09_front.png'):
        ignored(root,dest)
    count=import_catalog(gd,work);balls,bf=import_ball(gd,art);spiders,sf=spider_frames(gd,art)
    if not balls or not spiders:raise ValueError('No se reconstruyeron modelos completos; ver recursos originales.')
    jsonsave(art/'V65Skins.json',{'balls':balls,'spiders':spiders})
    jsonsave(work/'Informe.json',{'catalog':count,'balls':len(balls),'spiders':len(spiders),'skipped':bf+sf,'source':'GD 2.207 HD; composicion local a partir de PLIST; color personalizado'})
    print('OK ARTE LOCAL V6.5:',count,'sprites de catálogo,',len(balls),'balls,',len(spiders),'spiders,',len(bf+sf),'variantes omitidas; no se subió arte.')
if __name__=='__main__':
    try:main()
    except Exception as e:print('DETENIDO:',e,file=sys.stderr);sys.exit(1)
