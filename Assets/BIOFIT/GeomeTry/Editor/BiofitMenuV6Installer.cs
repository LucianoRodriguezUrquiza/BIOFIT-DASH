#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using BiofitMenuV6.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace BiofitMenuV6.Editor
{
    public static class BiofitMenuV6Installer
    {
        private const float RefW = 1670f;
        private const float RefH = 942f;
        private const string AssetRoot = "Assets/BIOFIT/Resources/BiofitUiV6";

        private struct Placement
        {
            public string role;
            public string spriteFile;
            public float centerX;
            public float centerY;
            public float width;
            public float height;
            public Placement(string role, string spriteFile, float centerX, float centerY, float width, float height)
            {
                this.role = role; this.spriteFile = spriteFile; this.centerX = centerX; this.centerY = centerY; this.width = width; this.height = height;
            }
        }

        [MenuItem("BIOFIT/Menu V6/Aplicar visual del menÃº principal")]
        public static void ApplyVisuals()
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.isLoaded)
            {
                EditorUtility.DisplayDialog("BIOFIT Menu V6", "No hay una escena cargada.", "OK");
                return;
            }

            var canvas = Object.FindObjectOfType<Canvas>();
            if (canvas == null)
            {
                var go = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                canvas = go.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            }
            ConfigureCanvas(canvas);

            EnsureBackground(canvas.transform);
            EnsureLogo(canvas.transform);
            ApplyButtons(canvas.transform);
            EditorSceneManager.MarkSceneDirty(scene);
            AssetDatabase.SaveAssets();
            EditorUtility.DisplayDialog("BIOFIT Menu V6", "Visual del menÃº V6 aplicado en la escena activa. RevisÃ¡ Main Menu.unity y guardÃ¡ la escena si estÃ¡s conforme.", "OK");
        }

        private static void ConfigureCanvas(Canvas canvas)
        {
            var scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler == null) scaler = canvas.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(RefW, RefH);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            if (canvas.GetComponent<GraphicRaycaster>() == null) canvas.gameObject.AddComponent<GraphicRaycaster>();
        }

        private static void EnsureBackground(Transform parent)
        {
            var go = FindOrCreate(parent, "MenuBackgroundV6");
            go.transform.SetAsFirstSibling();
            var image = EnsureImage(go);
            image.sprite = LoadSprite("Fondo_Menu.png");
            image.preserveAspect = false;
            image.raycastTarget = false;
            StretchFull((RectTransform)go.transform);
        }

        private static void EnsureLogo(Transform parent)
        {
            var go = FindOrCreate(parent, "MenuLogoV6");
            var image = EnsureImage(go);
            image.sprite = LoadSprite("Logo_Biofit_Dash.png");
            image.preserveAspect = true;
            image.raycastTarget = false;
            Position((RectTransform)go.transform, 838.5f, 219.5f, 1129f, 185f);
        }

        private static void ApplyButtons(Transform canvas)
        {
            var placements = new []
            {
                new Placement("Skin", "Boton_Skin.png", 512f, 494.5f, 282f, 287f),
                new Placement("Play", "Boton_Play.png", 835.5f, 496f, 361f, 354f),
                new Placement("Config", "Boton_Configuracion.png", 1160.5f, 495f, 283f, 288f)
            };

            var existing = Object.FindObjectsOfType<Button>(true)
                .Where(b => b.transform.root == canvas.root && b.GetComponentInParent<Canvas>() != null)
                .ToList();

            var used = new HashSet<Button>();
            foreach (var p in placements)
            {
                Button chosen = FindClosestLegacyButton(existing, used, p.centerX, p.centerY);
                if (chosen == null)
                {
                    var go = FindOrCreate(canvas, p.role + "ButtonV6");
                    chosen = go.GetComponent<Button>();
                    if (chosen == null) chosen = go.AddComponent<Button>();
                }
                used.Add(chosen);
                ApplySpriteButton(chosen, p.spriteFile, p.centerX, p.centerY, p.width, p.height);
            }
        }

        private static Button FindClosestLegacyButton(List<Button> buttons, HashSet<Button> used, float centerX, float centerY)
        {
            Vector2 target = CanvasToAnchored(centerX, centerY);
            float best = float.MaxValue;
            Button chosen = null;
            foreach (var b in buttons)
            {
                if (used.Contains(b)) continue;
                var rt = b.transform as RectTransform;
                if (rt == null) continue;
                float d = Vector2.Distance(rt.anchoredPosition, target);
                if (d < best)
                {
                    best = d;
                    chosen = b;
                }
            }
            // Avoid hijacking random pause buttons, etc.
            return best <= 500f ? chosen : null;
        }

        private static void ApplySpriteButton(Button button, string spriteFile, float cx, float cy, float w, float h)
        {
            var img = button.GetComponent<Image>();
            if (img == null) img = button.gameObject.AddComponent<Image>();
            img.sprite = LoadSprite(spriteFile);
            img.preserveAspect = true;
            img.raycastTarget = true;
            button.targetGraphic = img;
            var rt = (RectTransform)button.transform;
            Position(rt, cx, cy, w, h);
            button.transition = Selectable.Transition.None;
            EnsurePressFeedback(button.gameObject);
        }

        private static void EnsurePressFeedback(GameObject go)
        {
            if (go.GetComponent<BiofitMenuPressFeedback>() == null)
                go.AddComponent<BiofitMenuPressFeedback>();
        }

        private static Sprite LoadSprite(string fileName)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{AssetRoot}/{fileName}");
            if (sprite == null)
                throw new System.IO.FileNotFoundException("No se encontrÃ³ el sprite requerido: " + fileName + " en " + AssetRoot);
            return sprite;
        }

        private static GameObject FindOrCreate(Transform parent, string name)
        {
            var child = parent.Find(name);
            if (child != null) return child.gameObject;
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        private static Image EnsureImage(GameObject go)
        {
            var image = go.GetComponent<Image>();
            if (image == null) image = go.AddComponent<Image>();
            return image;
        }

        private static void Position(RectTransform rt, float centerX, float centerY, float width, float height)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(width, height);
            rt.anchoredPosition = CanvasToAnchored(centerX, centerY);
            rt.localScale = Vector3.one;
        }

        private static Vector2 CanvasToAnchored(float centerX, float centerY)
        {
            return new Vector2(centerX - RefW * 0.5f, RefH * 0.5f - centerY);
        }

        private static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.localScale = Vector3.one;
        }
    }
}
#endif

