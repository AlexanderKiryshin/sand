using System.IO;
using UnityEditor;

public static class SandSimulatorReimport
{
    private const string AssemblyPath = "Library/ScriptAssemblies/Assembly-CSharp.dll";
    private const string SymbolsPath = "Library/ScriptAssemblies/Assembly-CSharp.pdb";

    [MenuItem("Sand/Force Recompile FallingSandSimulator")]
    public static void ForceRecompile()
    {
        DeleteIfExists(AssemblyPath);
        DeleteIfExists(SymbolsPath);
        AssetDatabase.ImportAsset("Assets/Scripts/Sand/FallingSandSimulator.cs", ImportAssetOptions.ForceUpdate);
        AssetDatabase.ImportAsset("Assets/Scripts/Conveyor/ConveyorBelt.cs", ImportAssetOptions.ForceUpdate);
        UnityEngine.Debug.Log(
            "Sand collection v4 reimported. Profiler must show SandCollectionPrewarmV4, not IsMatchingCollectionColumn.");
    }

    private static void DeleteIfExists(string relativePath)
    {
        string fullPath = Path.Combine(Directory.GetCurrentDirectory(), relativePath);
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }
    }
}
