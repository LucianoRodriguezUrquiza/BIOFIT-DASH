using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Biofit.Geometry;
using UnityEngine;
class Program
{
    static int checks;
    static void Check(bool v,string message){checks++;if(!v)throw new Exception(message);}
    static void Set(object o,string field,object value)=>o.GetType().GetField(field,BindingFlags.NonPublic|BindingFlags.Instance).SetValue(o,value);
    static void Main()
    {
        var game=new BiofitGeometryGame();var monitor=new BiofitBLEMonitor();monitor.ShowDiagnostics=false;
        var levels=GeometryLevel.ResourceNames.Select(n=>JsonSerializer.Deserialize<GeometryLevel>(File.ReadAllText("Assets/BIOFIT/Geometry/Resources/BiofitGeometry/Levels/"+n+".json"),new JsonSerializerOptions{IncludeFields=true})).ToArray();
        Set(game,"monitor",monitor);Set(game,"levels",levels);
        Screen.safeArea=new Rect(0,0,1280,720);
        var draw=typeof(BiofitGeometryGame).GetMethod("OnGUI",BindingFlags.Instance|BindingFlags.NonPublic);
        Action render=()=>{GUI.ButtonTexts.Clear();draw.Invoke(game,null);};
        render();Check(GUI.ButtonTexts.Count(x=>x=="JUGAR")==4,"four visible level choices");
        GUI.ClickNext="REGISTRO";render();render();
        Check(!GUI.ButtonTexts.Contains("JUGAR") && GUI.ButtonTexts.Contains("1. INICIAR"),"session modal blocks underlying level buttons");
        GUI.ClickNext="1. INICIAR";render();Check(monitor.GameCaptureActive,"real existing capture starts from new UI");
        GUI.ClickNext="2. FINALIZAR";render();monitor.CaptureCompletion.GetAwaiter().GetResult();
        Check(BiofitCaptureArchive.Verify(File.ReadAllText(monitor.SavedCapturePath)),"new UI keeps private file integrity");
        Check(File.ReadAllText(monitor.SavedCapturePath).Contains("GAME_VERSION=GEOMETRY_1 CONTROL=LEGACY_ONLY"),"capture identifies gameplay and algorithm");
        GUI.ClickNext="SESIONES ANTERIORES";render();render();
        Check(!GUI.ButtonTexts.Contains("1. INICIAR") && GUI.ButtonTexts.Contains("VOLVER"),"archive modal blocks capture controls");
        GUI.ClickNext="VOLVER";render();GUI.ClickNext="CERRAR";render();
        GUI.ClickNext="CRÉDITOS";render();render();
        Check(!GUI.ButtonTexts.Contains("JUGAR") && GUI.ButtonTexts.Contains("CERRAR CRÉDITOS"),"credits modal blocks level buttons");
        Console.WriteLine("PASS "+checks+" production UI/capture checks with API shims; all Geometry runtime C# compiled. Not Unity rendering, physics, APK or BLE.");
    }
}
