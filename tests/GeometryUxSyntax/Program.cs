using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

string[] sources = {
    "Assets/BIOFIT/GeomeTry/BiofitGeometryUX.cs",
    "Assets/BIOFIT/GeomeTry/BiofitSettingsButtonFeedback.cs",
    "Assets/BIOFIT/GeomeTry/BiofitUiArtwork.cs",
    "Assets/BIOFIT/GeomeTry/Editor/BiofitGeometryUxPreflight.cs",
    "Assets/BIOFIT/GeomeTry/BiofitGeomeTryLegacyBridge.cs",
    "Assets/BIOFIT/GeomeTry/BiofitThreeModes.cs",
    "Assets/BIOFIT/GeomeTry/Editor/BiofitThreeModesBuild.cs",
};
bool broken = false;
foreach (string path in sources)
{
    string source = File.ReadAllText(path);
    var tree = CSharpSyntaxTree.ParseText(source,
        new CSharpParseOptions(LanguageVersion.Latest));
    var bad = tree.GetDiagnostics()
        .Where(x => x.Severity == DiagnosticSeverity.Error).ToArray();
    foreach (var error in bad)
        System.Console.Error.WriteLine(path + ": " + error);
    System.Console.WriteLine(path + ": " +
        (bad.Length == 0 ? "PASS C# syntax" : "FAIL " + bad.Length));
    if (bad.Length != 0) broken = true;
}
return broken ? 1 : 0;
