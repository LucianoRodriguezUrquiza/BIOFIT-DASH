#!/usr/bin/env python3
"""Importación local de nivel clásico: no modifica Unity ni el ZIP fuente.
Requiere Python 3.10+ y Pillow. Uso: python preparar_stereo.py archivo.zip salida
"""
import argparse, base64, gzip, io, json, math, re, zipfile, plistlib, hashlib
from pathlib import Path
from collections import Counter
from PIL import Image, ImageDraw, ImageChops
# ID, nombre de frame (sin sufijo), clase provisional, ancho/alto GD, capa, orden.
# Catálogo GDBuilderAI, mcp/src/data/objects.json; contrastado con los atlas locales.
ROWS = [
(1,'square_01','solid',30,30,5,2),(2,'square_02','solid',30,30,5,2),(3,'square_03','solid',30,30,5,2),(4,'square_04','solid',30,30,5,2),(5,'square_05','decoration',30,30,1,-7),(6,'square_06','solid',30,30,5,2),(7,'square_07','solid',30,30,5,2),(8,'spike_01','hazard',30,30,5,2),(9,'pit_01','hazard',30.25,26,5,2),(12,'portal_03_front','cubePortal',44.5,85.5,5,10),(13,'portal_04_front','shipPortal',44.5,85.5,5,10),(15,'rod_01','decoration',6.5,42,1,-6),(16,'rod_02','decoration',6,26.5,1,-6),(17,'rod_03','decoration',5.5,12.5,1,-6),(18,'d_spikes_01','decoration',128,41.5,3,9),(19,'d_spikes_02','decoration',104,36.5,3,9),(20,'d_spikes_03','decoration',73,29,3,9),(21,'d_spikes_04','decoration',42,13,3,9),(22,'edit_eeNoneBtn','enterEffect',26,26,5,2),(23,'edit_eeFBBtn','enterEffect',16.25,30,5,2),(27,'edit_eeSUBtn','enterEffect',29,12.5,5,2),(29,'edit_eTintCol01Btn','backgroundColor',20.5,28,5,2),(30,'edit_eTintCol01Btn','groundColor',20.5,28,5,2),(39,'spike_02','hazard',30,13.5,5,2),(40,'plank_01','solid',30,13.5,5,2),(41,'chain_01','decoration',19.5,69.5,3,9),(54,'d_ball_05','decoration',30,28.5,3,9),(62,'square_b_01','solid',30,16,5,2),(65,'square_b_04','solid',30,16,5,2),(103,'spike_03','hazard',20,19,5,2),(142,'secretCoin_01','coin',40,40,5,9)]
CAT = {r[0]:r[1:] for r in ROWS}
NUM=lambda s: list(map(int,re.findall(r'-?\d+',s)))
HIDDEN={'enterEffect','backgroundColor','groundColor'}

def pairs(s):
    a=s.split(','); assert len(a)%2==0, 'registro impar'; assert len(set(a[::2]))==len(a)//2, 'propiedad repetida'; return dict(zip(a[::2],a[1::2]))

def main():
    ap=argparse.ArgumentParser();ap.add_argument('zip');ap.add_argument('salida');ap.add_argument('--velocidad-gd',type=float,default=311.58,help='GD unidades/s; hipótesis para interpolación visual, no modifica físicas');args=ap.parse_args();out=Path(args.salida);out.mkdir(parents=True,exist_ok=True)
    with zipfile.ZipFile(args.zip) as z:
        assert z.testzip() is None, 'ZIP corrupto'
        raw=z.read('1.txt').strip(); s=gzip.decompress(base64.urlsafe_b64decode(raw)).decode('utf-8'); parts=s.split(';'); header=pairs(parts[0]); raw_objects=[pairs(p) for p in parts[1:] if p]; frames={}
        for name in z.namelist():
            if name.startswith('GJ_GameSheet') and name.endswith('.plist'):
                img=Image.open(io.BytesIO(z.read(name.replace('.plist','.png')))).convert('RGBA');pl=plistlib.loads(z.read(name))
                for k,v in pl['frames'].items():
                    if k in frames: continue
                    frames[k]=(img,v,name)
        assert set(int(o['1']) for o in raw_objects)==set(CAT), 'IDs diferentes: revisar catálogo, no omitir objetos'
        objs=[]; sprites={}; meta={}
        def extract(name):
            if name in sprites:return sprites[name]
            key=name+'_001.png'; im,v,atlas=frames[key];x,y,w,h=NUM(v['textureRect']);r=v['textureRotated']; sw,sh=NUM(v['spriteSourceSize']);ox,oy=NUM(v['spriteOffset']); assert x>=0 and y>=0 and x+(h if r else w)<=im.width and y+(w if r else h)<=im.height
            crop=im.crop((x,y,x+(h if r else w),y+(w if r else h)));crop=crop.transpose(Image.Transpose.ROTATE_90) if r else crop
            assert crop.size==(w,h); full=Image.new('RGBA',(sw,sh));full.paste(crop,(round((sw-w)/2+ox),round((sh-h)/2-oy)))
            sprites[name]=full;meta[name]={'atlas':atlas,'frame':key,'sourcePx':[sw,sh],'offsetPx':[ox,oy],'rotatedInAtlas':r};return full
        for i,p in enumerate(raw_objects):
            ident=int(p['1']); frame,kind,w,h,layer,order=CAT[ident]; layers=[frame] if kind not in HIDDEN else []
            if ident in (12,13):layers=[frame.replace('_front','_back'),frame]
            for f in layers:extract(f)
            o={'index':i,'id':ident,'kind':kind,'xGD':float(p['2']),'yGD':float(p['3']),'xBlocks':float(p['2'])/30,'yBlocks':float(p['3'])/30,'rotationGD':float(p.get('6',0)),'flipX':p.get('4','0')=='1','flipY':p.get('5','0')=='1','widthGD':w,'heightGD':h,'layer':layer,'order':order,'frames':layers,'raw':p};objs.append(o)
        bg=Image.open(io.BytesIO(z.read('game_bg_01_001-hd.png'))).convert('RGBA');ground=Image.open(io.BytesIO(z.read('groundSquare_01_001-hd.png'))).convert('RGBA')
        data={'sourceSha256':hashlib.sha256(raw).hexdigest(),'unitsPerBlock':30,'header':header,'objects':objs,'spriteMetadata':meta,'limitations':['Collider exacto no incluido','Capas y tintes provisionales según catálogo y video','No es formato de nivel del juego BIOFIT actual']}
        (out/'stereo_datos.json').write_text(json.dumps(data,ensure_ascii=False,indent=2),encoding='utf-8');
        # Los recursos se extraen localmente; no se empaquetan en el código.
        sd=out/'sprites_locales';sd.mkdir(exist_ok=True)
        for n,im in sprites.items():im.save(sd/(n+'.png'))
        bg.save(sd/'fondo.png');ground.save(sd/'suelo.png')
        def rgb_header(key):
            a=header[key].split('_');d=dict(zip(a[::2],a[1::2]));return tuple(int(d[str(i)]) for i in (1,2,3))
        def make_color(id,key):
            current=rgb_header(key);trans=[]
            def evaluate(x):
                result=current
                for start,old,new,dur in trans:
                    if x<start:break
                    q=min(1,max(0,(x-start)/max(dur,1e-9)));result=tuple(round(a+(b-a)*q) for a,b in zip(old,new))
                return result
            for o in sorted((a for a in objs if a['id']==id),key=lambda a:a['xGD']):
                p=o['raw'];x=o['xGD'];trans.append((x,evaluate(x),tuple(int(p[str(i)]) for i in (7,8,9)),float(p['10'])*args.velocidad_gd))
            return evaluate
        bcol=make_color(29,'kS29');gcol=make_color(30,'kS30')
        # Vista de editor: todos los objetos, sin cámara/partículas, segmentos contiguos.
        width=3000;height=520;top=27;base=465;N=math.ceil(max(o['xGD'] for o in objs)/width); full=Image.new('RGB',(width,N*(height+top)),(22,22,24)); draw=ImageDraw.Draw(full)
        bg=bg.resize((max(1,bg.width//2),max(1,bg.height//2)));ground=ground.resize((max(1,ground.width//2),max(1,ground.height//2)))
        def tint(im,color):
            p=ImageChops.multiply(im.convert('RGB'),Image.new('RGB',im.size,color));p.putalpha(im.getchannel('A'));return p
        for n in range(N):
            start=n*width;can=Image.new('RGB',(width,height)); d=ImageDraw.Draw(can)
            for x in range(0,width,4):
                d.rectangle((x,0,x+3,base-1),fill=bcol(start+x));d.rectangle((x,base,x+3,height),fill=gcol(start+x))
            # Texturas repetidas sobre un gradiente RGB continuo, sin saltos de tinte entre baldosas.
            btex=Image.new('RGBA',(width,base));gtex=Image.new('RGBA',(width,height-base))
            for x in range(-(start%bg.width),width,bg.width):
                for y in range(0,base,bg.height):btex.paste(bg,(x,y))
            for x in range(-(start%ground.width),width,ground.width):gtex.paste(ground,(x,0))
            for texture,region,yy in ((btex,can.crop((0,0,width,base)),0),(gtex,can.crop((0,base,width,height)),base)):
                p=ImageChops.multiply(texture.convert('RGB'),region);p.putalpha(texture.getchannel('A'));can.paste(p,(0,yy),p)
            d=ImageDraw.Draw(can);d.line((0,base,width,base),fill=(210,210,210),width=1)
            for o in sorted(objs,key=lambda a:(a['layer'],a['order'],a['index'])):
                if not o['frames'] or not (start-160<=o['xGD']<=start+width+160):continue
                for name in o['frames']:
                    p=sprites[name].copy();p=p.resize((max(1,round(p.width/2)),max(1,round(p.height/2))),Image.Resampling.LANCZOS)
                    if o['id']==9:p=tint(p,(0,0,0)) # hipótesis visual, hierba de pinchos negra
                    if o['id'] in (18,19,20,21):p=tint(p,(255,200,75)) # decoración, color aproximado
                    if o['flipX']:p=p.transpose(Image.Transpose.FLIP_LEFT_RIGHT)
                    if o['flipY']:p=p.transpose(Image.Transpose.FLIP_TOP_BOTTOM)
                    p=p.rotate(-o['rotationGD'],expand=True,resample=Image.Resampling.BICUBIC);cx=o['xGD']-start;cy=base-o['yGD'];can.paste(p,(round(cx-p.width/2),round(cy-p.height/2)),p)
            # Marcadores editor para triggers, no son arte de gameplay.
            for o in objs:
                if start<=o['xGD']<start+width and o['kind'] in ('cubePortal','shipPortal','coin'):
                    x=o['xGD']-start;d.line((x,0,x,14),fill='white',width=2);d.text((x+3,2),f"{o['kind']} {o['xGD']:g}",fill='white',stroke_width=1,stroke_fill='black')
            y=n*(height+top);draw.text((12,y+7),f'TRAMO {n+1:02d} | X GD {start} - {start+width} | bloques {start/30:g} - {(start+width)/30:g}',fill='white');full.paste(can,(0,y+top))
        full.save(out/'stereo_mapa_revision.png')
        c=Counter(o['kind'] for o in objs);assert len(objs)==len(raw_objects)==2291;assert len(set(o['index'] for o in objs))==2291;assert all(o['xBlocks']*30==o['xGD'] or math.isclose(o['xBlocks']*30,o['xGD']) for o in objs)
        report={'objects':len(objs),'ids':len(CAT),'counts':dict(c),'spritesExtracted':len(sprites),'zipIntegrity':'OK','coordinateBoundsGD':[min(o['xGD'] for o in objs),min(o['yGD'] for o in objs),max(o['xGD'] for o in objs),max(o['yGD'] for o in objs)],'catalogSource':'https://github.com/TalhaMudo/GDBuilderAI/blob/main/mcp/src/data/objects.json'}
        (out/'validacion.json').write_text(json.dumps(report,indent=2),encoding='utf-8');print(json.dumps(report,indent=2))
if __name__=='__main__':main()
