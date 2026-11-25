#!/usr/bin/env python3
"""BIOFIT DIPL — repair V6 menu with explicit runtime navigation."""
import argparse
from pathlib import Path
import os
import struct
import tempfile

REL = Path('Assets/BIOFIT/GeomeTry/BiofitGeometryUX.cs')
ART = Path('Assets/BIOFIT/Resources/BiofitUiV6')
SIZES = {'Fondo_Menu': (1670,942), 'Logo_Biofit_Dash': (1129,185),
         'Boton_Skin': (282,287), 'Boton_Play': (361,354), 'Boton_Configuracion': (283,288)}
MARKER = '// BIOFIT_MENU_V6_RUNTIME_FIX'
OLD = '            if (sceneName == Menu) { ConfigureOriginalMenu(); AttachSceneFonts(); return; }'
NEW = '''            // BIOFIT_MENU_V6_RUNTIME_FIX
            Texture2D[] menuV6 = null;
            if (sceneName == Menu)
            {
                menuV6 = LoadMenuV6Art();
                if (menuV6 == null)
                {
                    ConfigureOriginalMenu(); AttachSceneFonts(); return;
                }
            }'''
DISPATCH = '            if (sceneName == Select) BuildLevelSelection();'
NEW_DISPATCH = '''            if (sceneName == Menu) BuildMenuV6(menuV6);
            else if (sceneName == Select) BuildLevelSelection();'''
ANCHOR = '        private void ConfigureOriginalMenu()'
METHODS = '''        private static Texture2D[] LoadMenuV6Art()
        {
            string[] names = { "Fondo_Menu", "Logo_Biofit_Dash", "Boton_Skin",
                "Boton_Play", "Boton_Configuracion" };
            var result = new Texture2D[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                result[i] = Resources.Load<Texture2D>("BiofitUiV6/" + names[i]);
                if (result[i] == null)
                {
                    Debug.LogWarning("BIOFIT Menu V6: recurso ausente: " + names[i]);
                    return null;
                }
            }
            return result;
        }

        private void BuildMenuV6(Texture2D[] art)
        {
            // Disable ONLY old scene-local canvases and raycasters. Keep their
            // game objects, GameManager, BLE receiver and EventSystem alive.
            var scene = SceneManager.GetActiveScene();
            foreach (Canvas old in FindObjectsByType<Canvas>(FindObjectsInactive.Include,
                FindObjectsSortMode.None))
            {
                if (old == canvas || old.transform.IsChildOf(transform) ||
                    old.gameObject.scene != scene) continue;
                old.enabled = false;
                foreach (GraphicRaycaster ray in old.GetComponents<GraphicRaycaster>())
                    ray.enabled = false;
            }
            // The existing 1600x900 safe-area stage is used for all UI screens.
            // Convert the supplied 1670x942 design without stretching sprites.
            var bg = Rect("BIOFIT V6 Fondo", canvasRoot, new Vector2(.5f,.5f),
                new Vector2(1600f, 900f), Vector2.zero).gameObject.AddComponent<RawImage>();
            bg.texture = art[0];
            bg.raycastTarget = false;
            MenuV6Art("BIOFIT V6 Logo", art[1], 838.5f, 219.5f, 185f, null);
            MenuV6Art("BIOFIT V6 Personaje", art[2], 512f, 494.5f, 287f, () => Navigate(Custom));
            MenuV6Art("BIOFIT V6 Play", art[3], 835.5f, 496f, 354f, () => Navigate(Select));
            MenuV6Art("BIOFIT V6 Configuracion", art[4], 1160.5f, 495f, 288f, () => Navigate(Settings));
            Debug.Log("BIOFIT MENU V6: 3 botones vinculados a Customisation, Level Selection, Settings.");
        }

        private void MenuV6Art(string name, Texture2D texture, float x, float y,
            float h, Action click)
        {
            float height = h * 900f / 942f;
            float width = height * texture.width / texture.height;
            var rect = Rect(name, canvasRoot, new Vector2(x/1670f, 1f-y/942f),
                new Vector2(width,height), Vector2.zero);
            var image = rect.gameObject.AddComponent<RawImage>();
            image.texture = texture;
            image.raycastTarget = click != null;
            if (click == null) return;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.None;
            button.interactable = true;
            button.onClick.AddListener(() => click());
            rect.gameObject.AddComponent<BiofitSettingsButtonFeedback>();
        }

'''

def require(ok, msg):
    if not ok: raise SystemExit('DETENIDO: ' + msg)

def transform(s):
    if MARKER in s:
        require(NEW in s and NEW_DISPATCH in s and METHODS+ANCHOR in s,'Parche parcial o modificado; revisar antes de escribir.')
        return s
    for token in (OLD,DISPATCH,ANCHOR):
        require(s.count(token)==1,'No coincide el codigo V5/V6 esperado: '+token.strip())
    for token in ('private void Navigate(string scene)', 'private static void EnsureUiEventSystem()', 'private RectTransform Rect(', 'private void AttachSceneFonts()', 'private static void AttachSceneFonts()'):
        if 'AttachSceneFonts' in token: continue
        require(token in s,'Falta metodo esperado: '+token)
    return s.replace(OLD,NEW,1).replace(DISPATCH,NEW_DISPATCH,1).replace(ANCHOR,METHODS+ANCHOR,1)

def atomic(p, data):
    fd,tmp=tempfile.mkstemp(prefix=p.name+'.', suffix='.tmp',dir=p.parent)
    try:
        with os.fdopen(fd,'wb') as f: f.write(data)
        os.replace(tmp,p)
    finally:
        if os.path.exists(tmp): os.unlink(tmp)

def main():
    ap=argparse.ArgumentParser(description=__doc__)
    ap.add_argument('--proyecto',required=True,type=Path)
    ap.add_argument('--verificar',action='store_true')
    a=ap.parse_args(); root=a.proyecto.resolve(); path=root/REL
    require((root/'ProjectSettings/ProjectVersion.txt').is_file(),'No parece un proyecto Unity')
    require(path.is_file(),'Falta '+str(REL))
    require((path.parent/'BiofitSettingsButtonFeedback.cs').is_file(),'Falta la animacion validada de BIOFIT')
    for name,size in SIZES.items():
        p=root/ART/(name+'.png')
        require(p.is_file(),'Falta '+str(p))
        with p.open('rb') as f: head=f.read(24)
        require(len(head)==24 and head[:8]==b'\x89PNG\r\n\x1a\n' and struct.unpack('>II',head[16:24])==size,'PNG inesperado: '+name)
    raw=path.read_bytes(); s=raw.decode('utf-8-sig').replace('\r\n','\n'); out=transform(s)
    print('OK 5 PNG y rutas de navegacion reconocidas.')
    if a.verificar:
        print('OK PREVUELO MENU V6.1 - sin escritura'); return
    if out==s:
        print('OK MENU V6.1 ya aplicado'); return
    backup=root/'Builds/BiofitMenuV61/BiofitGeometryUX.cs.before'
    backup.parent.mkdir(parents=True,exist_ok=True)
    require(not backup.exists() or backup.read_bytes()==raw,'Existe otro respaldo de este script; revisar.')
    if not backup.exists(): atomic(backup,raw)
    data=out.replace('\n','\r\n') if b'\r\n' in raw else out
    atomic(path,(b'\xef\xbb\xbf' if raw.startswith(b'\xef\xbb\xbf') else b'')+data.encode('utf-8'))
    print('OK MENU V6.1 - botones conectados directamente a sus escenas.')
    print('No aplicar la herramienta de editor anterior. Abrir Main Menu y Play.')

if __name__=='__main__': main()
