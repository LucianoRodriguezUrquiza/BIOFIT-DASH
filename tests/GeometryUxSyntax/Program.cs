using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

string[] sources = {
    "Assets/BIOFIT/GeomeTry/BiofitGeometryUX.cs",
    "Assets/BIOFIT/GeomeTry/BiofitGeometryHeldMotion.cs",
    "Assets/BIOFIT/Scripts/BiofitBLEMonitor.cs",
    "Assets/BIOFIT/Scripts/BiofitBLEMonitor.Motion.cs",
    "Assets/BIOFIT/GeomeTry/Editor/BiofitContinuousMotionValidation.cs",
    "Assets/BIOFIT/GeomeTry/Editor/BiofitGeomeTryOriginalBuild.cs",
    "tools/geometry_compat/Character.MotionV2.cs",
    "tools/geometry_compat/Cube.MotionV2.cs",
    "tools/geometry_compat/JumpOrb.MotionV2.cs",
    "Assets/BIOFIT/GeomeTry/BiofitUiArtwork.cs",
    "Assets/BIOFIT/GeomeTry/Editor/BiofitGeometryUxPreflight.cs",
    "Assets/BIOFIT/GeomeTry/BiofitGeomeTryLegacyBridge.cs",
};
bool broken = false;
foreach (string define in new[] { "UNITY_EDITOR", "UNITY_ANDROID" })
foreach (string path in sources)
{
    string source = File.ReadAllText(path);
    var tree = CSharpSyntaxTree.ParseText(source,
        new CSharpParseOptions(LanguageVersion.CSharp9, preprocessorSymbols: new[] { define }));
    var bad = tree.GetDiagnostics()
        .Where(x => x.Severity == DiagnosticSeverity.Error).ToArray();
    foreach (var error in bad)
        System.Console.Error.WriteLine(path + ": " + error);
    System.Console.WriteLine(define + " " + path + ": " +
        (bad.Length == 0 ? "PASS C# syntax" : "FAIL " + bad.Length));
    if (bad.Length != 0) broken = true;
}
return broken ? 1 : 0;
