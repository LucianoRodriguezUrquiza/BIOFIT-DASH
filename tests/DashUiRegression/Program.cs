using System;
using System.Reflection;
class Program
{
    static int checks;
    static void Check(bool condition, string reason)
    {
        if (!condition) throw new Exception(reason);
        checks++;
    }

    static void CheckLayout(int w, int h)
    {
        var withPanel = BiofitDashLayout.ForScreen(w,h,true);
        var gameOnly = BiofitDashLayout.ForScreen(w,h,false);
        Check(withPanel.Scale > 0f,"positive GUI scale");
        Check(withPanel.Width >= 1120f,"minimum canvas width");
        Check(withPanel.GameX + withPanel.GameWidth + 16f <= withPanel.PanelX,
            "game and diagnostic panel do not overlap");
        Check(withPanel.PanelX + withPanel.PanelWidth + 18f <= withPanel.Width + .01f,
            "sidebar ends inside device canvas");
        Check(withPanel.GameWidth >= 650f,"game area remains usable in landscape");
        Check(gameOnly.GameWidth > withPanel.GameWidth,
            "hiding technical panel expands gameplay");
        Check(withPanel.Scale*withPanel.Width <= w + .01f &&
              withPanel.Scale*withPanel.Height <= h + .01f,
            "rendered content fits physical screen");
    }

    static void Main()
    {
        CheckLayout(1536,691); // user's actual screenshot ratio
        CheckLayout(1920,1080);
        CheckLayout(1280,720);
        CheckLayout(800,600); // conservative viewport

        var d = new BiofitSingleFootDiagnostic();
        Check(Math.Abs(d.LeftThresholdG-.25f)<.001f &&
              Math.Abs(d.RightThresholdG-.25f)<.001f,
              "both feet start at experimentally supported 0.25g");
        Check(d.Observe(1,.30f,0.0,d.Threshold(1),.32)==
              BiofitSingleFootDiagnostic.Decision.Accepted,
              "left real decision accepts 0.30g at default");
        Check(d.Observe(2,.30f,0.0,d.Threshold(2),.32)==
              BiofitSingleFootDiagnostic.Decision.Accepted,
              "right real decision accepts 0.30g at default");
        d.Reset();
        Check(d.Observe(1,.20f,1.0,.35f,.32) ==
            BiofitSingleFootDiagnostic.Decision.BelowThreshold, "below-threshold candidate classified");
        Check(d.Left.Received==1 && d.Left.BelowThreshold==1 && d.Left.Accepted==0,
            "below-threshold counted, no accepted jump");
        Check(d.Observe(1,.50f,2.0,.35f,.32) ==
            BiofitSingleFootDiagnostic.Decision.Accepted, "good left candidate accepted");
        Check(d.Observe(1,.55f,2.15,.35f,.32) ==
            BiofitSingleFootDiagnostic.Decision.Refractory, "repeated left movement identified");
        Check(d.Observe(2,.50f,2.15,.35f,.32) ==
            BiofitSingleFootDiagnostic.Decision.Accepted,
            "right foot cooldown independent from left");
        d.RecordGameResult(1,false);
        d.RecordGameResult(2,true);
        Check(d.Left.GameUnavailable==1 && d.Left.Jumped==0 &&
            d.Right.Jumped==1, "game busy distinguished from sensor rejection");
        Check(d.Observe(1,.40f,3.0,.35f,.32) ==
            BiofitSingleFootDiagnostic.Decision.Accepted,"new gesture after refractory");
        Check(d.Observe(1,float.NaN,4.0,.35f,.32) ==
            BiofitSingleFootDiagnostic.Decision.Invalid,"invalid sample counted");
        Check(d.Observe(3,.90f,5.0,.35f,.32) ==
            BiofitSingleFootDiagnostic.Decision.Invalid,"invalid foot ignored");
        Check(d.Left.Received==5 && d.Left.Accepted==2 &&
              d.Left.Refractory==1 && d.Left.BelowThreshold==1 &&
              d.Left.Invalid==1, "per-foot counters consistent");
        d.AdjustThreshold(1,.10f);
        Check(Math.Abs(d.Threshold(1)-.35f)<.001f && d.Threshold(2)==.25f,"thresholds independent");
        d.AdjustThreshold(2,float.NaN);d.AdjustThreshold(2,float.PositiveInfinity);
        Check(d.Threshold(2)==.25f,"invalid threshold changes rejected");
        d.Reset();
        Check(Math.Abs(d.Threshold(1)-.35f)<.001f,"capture reset preserves thresholds");
        Check(d.Left.Received==0 && d.Right.Received==0,
            "reset clears both diagnostics");
        Check(d.Observe(1,.50f,1.0,.35f,.32) ==
            BiofitSingleFootDiagnostic.Decision.Accepted,
            "reset clears refractory state");
        // A/B mode is opt-in; default and OFF must preserve 0.25 g.
        var ab = new BiofitSingleFootDiagnostic();
        ab.AdjustThreshold(2,-.10f);
        Check(Math.Abs(ab.RightThresholdG-.25f)<.001f && !ab.ExperimentalMode,
            "normal mode refuses lowering right threshold under 0.25g");
        ab.SetExperimentalMode(true);
        ab.AdjustThreshold(2,-.10f);
        Check(Math.Abs(ab.RightThresholdG-.15f)<.001f &&
              ab.Observe(2,.165f,1.0,ab.Threshold(2),.32) ==
              BiofitSingleFootDiagnostic.Decision.Accepted,
            "opt-in A/B accepts early 0.165g candidate at 0.15g");
        ab.AdjustThreshold(1,-.20f);
        Check(ab.LeftThresholdG==.15f && ab.RightThresholdG==.15f,
            "A/B clamps at 0.15g in both feet without touching normal defaults");
        ab.SetExperimentalMode(false);
        Check(!ab.ExperimentalMode && ab.LeftThresholdG==.25f &&
              ab.RightThresholdG==.25f,
            "leaving A/B restores at least 0.25g baseline");


        // Compile and execute the real MonoBehaviour's GUI entry point
        // against shims. This is NOT a visual Unity/Android validation.
        UnityEngine.Screen.width=1536;
        UnityEngine.Screen.height=691;
        var game = new BiofitDashGame();
        typeof(BiofitDashGame).GetMethod("Awake",
            BindingFlags.Instance|BindingFlags.NonPublic).Invoke(game,null);
        typeof(BiofitDashGame).GetMethod("OnGUI",
            BindingFlags.Instance|BindingFlags.NonPublic).Invoke(game,null);
        Check(true,"real Dash UI code executed without host exception");
        var gui=typeof(BiofitDashGame).GetMethod("OnGUI",BindingFlags.Instance|BindingFlags.NonPublic);
        var monitor=(BiofitBLEMonitor)typeof(BiofitDashGame).GetField("monitor",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(game);
        var internalDiag=(BiofitSingleFootDiagnostic)typeof(BiofitDashGame).GetField("soloDiagnostic",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(game);
        Check(internalDiag.LeftThresholdG==.25f && internalDiag.RightThresholdG==.25f,
            "actual Dash game uses both 0.25g defaults");
        UnityEngine.GUI.ClickNext="IZQ: 0.25 g";gui.Invoke(game,null);
        Check((byte)typeof(BiofitDashGame).GetField("thresholdFoot",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(game)==2,
            "actual UI selector switches to right foot");
        UnityEngine.GUI.ClickNext="DER: 0.25 g";gui.Invoke(game,null);
        Check((byte)typeof(BiofitDashGame).GetField("thresholdFoot",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(game)==1,
            "actual UI selector switches back to left foot");
        UnityEngine.GUI.ClickNext="EXP: NO";gui.Invoke(game,null);
        Check(internalDiag.ExperimentalMode, "EXPERIMENT A/B is explicit opt-in");
        internalDiag.AdjustThreshold(2,-.10f);
        Check(internalDiag.RightThresholdG==.15f, "A/B permits 0.15g with one threshold at a time");
        UnityEngine.GUI.ClickNext="EXP: SI";gui.Invoke(game,null);
        Check(!internalDiag.ExperimentalMode && internalDiag.RightThresholdG==.25f,
            "UI EXP:SI toggle restores normal right threshold");

        UnityEngine.GUI.ClickNext="1. INICIAR";gui.Invoke(game,null);
        Check(monitor.GameCaptureActive,"actual start button starts capture");
        UnityEngine.GUI.ClickNext="EXP: NO";gui.Invoke(game,null);
        UnityEngine.GUI.ClickNext="EXP: SI";gui.Invoke(game,null);
        UnityEngine.GUI.ClickNext="SYNC VIDEO";gui.Invoke(game,null);
        Check(monitor.SyncLabel=="SYNC 001","visible numbered video marker");
        UnityEngine.GUI.ClickNext="SALTAR";gui.Invoke(game,null);
        typeof(BiofitDashGame).GetMethod("Update",
            BindingFlags.Instance|BindingFlags.NonPublic).Invoke(game,null);
        Check(internalDiag.LeftThresholdG==.25f && internalDiag.RightThresholdG==.25f,
            "capture start preserves both preferred threshold values");
        UnityEngine.GUI.ClickNext="2. FINALIZAR";gui.Invoke(game,null);
        monitor.CaptureCompletion.GetAwaiter().GetResult();
        Check(!monitor.GameCaptureActive && System.IO.File.Exists(monitor.SavedCapturePath),"actual finish button saves archive");
        string savedCsv=System.IO.File.ReadAllText(monitor.SavedCapturePath);
        Check(savedCsv.Contains("MODO_EXP_UMBRAL_ON") && savedCsv.Contains("MODO_EXP_UMBRAL_OFF") && savedCsv.Contains("SYNC 001"),"actual GUI EXP and video marks survive archive");
        Check(System.IO.File.ReadAllText(monitor.SavedCapturePath).Contains("PRIMER_ESTADO_SALTO"),
            "first animated simulation frame has a distinct timestamp marker");
        Check(BiofitAndroidExport.IsFinalStatus("GUARDADO: CSV verificado (123 bytes)") &&
              BiofitAndroidExport.IsFinalStatus("CANCELADO: original conservado") &&
              BiofitAndroidExport.IsFinalStatus("ERROR: falta activity"),
            "native export terminal states recognized");
        Check(!BiofitAndroidExport.IsFinalStatus("ABRIENDO_SELECTOR") &&
              !BiofitAndroidExport.IsFinalStatus("SELECCIONAR_DESTINO"),
            "picker request is not misreported as a completed save");
        UnityEngine.GUI.ClickNext="3. ARCHIVO";gui.Invoke(game,null);
        Check(UnityEngine.GUI.ClickNext==null,"actual archive button invokes export");
        string path=System.IO.Path.Combine(UnityEngine.Application.persistentDataPath,
            "BiofitEditorExports",System.IO.Path.GetFileName(monitor.SavedCapturePath));
        Check(System.IO.File.Exists(path) &&
              BiofitCaptureArchive.Verify(System.IO.File.ReadAllText(path)) &&
              BiofitAndroidExport.LastStatus.StartsWith("EDITOR:"),
              "GUI ARCHIVO saves complete CSV without chooser or clipboard");
        UnityEngine.GUI.ClickNext="COPIAR";gui.Invoke(game,null);
        Check(UnityEngine.GUIUtility.systemCopyBuffer.Contains("filas_total="),"clipboard fallback retained");
        Console.WriteLine("PASS: " + checks +
            " UI/unilateral host checks; no actual Unity or Android render.");
    }
}
