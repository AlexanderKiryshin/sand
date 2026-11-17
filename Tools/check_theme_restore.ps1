$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$source = Get-Content "$root/Assets/Scripts/UI/ThemeBackdropGraphic.cs" -Raw
# Rendering math needs Unity; this harness exercises the production invalidation lifecycle.
$start = $source.IndexOf('protected override void OnPopulateMesh(')
$brace = $source.IndexOf('{', $start)
$end = $brace + 1
$depth = 1
while ($depth -gt 0) {
    if ($source[$end] -eq '{') { $depth++ }
    if ($source[$end] -eq '}') { $depth-- }
    $end++
}
$source = $source.Substring(0, $brace + 1) + ' }' + $source.Substring($end)
$stubs = @'
namespace UnityEngine {
 public class RequireComponent : System.Attribute { public RequireComponent(System.Type type) {} }
 public class CanvasRenderer {}
 public class Texture {}
}
namespace UnityEngine.UI {
 public class VertexHelper {}
 public class MaskableGraphic {
  public int VerticesDirty, MaterialDirty;
  public virtual UnityEngine.Texture mainTexture => null;
  public void SetVerticesDirty() { VerticesDirty++; }
  public void SetMaterialDirty() { MaterialDirty++; }
  protected virtual void OnPopulateMesh(VertexHelper helper) {}
 }
}
public static class ThemeCatalog { public static int SelectedBackground = 44; }
public static class ThemeBackgroundAssets {
 public static UnityEngine.Texture Get(int theme) => new UnityEngine.Texture();
}
public static class ThemeRestoreChecks {
 static void Tick(ThemeBackdropGraphic background) {
  foreach (string name in new[] { "Update", "LateUpdate" })
   typeof(ThemeBackdropGraphic).GetMethod(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.Invoke(background, null);
 }
 public static void Run() {
  var field = new ThemeBackdropGraphic(); Tick(field);
  field.VerticesDirty = field.MaterialDirty = 0;
  ThemeCatalog.SelectedBackground = 46; Tick(field);
  if (field.VerticesDirty == 0 || field.MaterialDirty == 0)
   throw new System.Exception("FAIL: saved background is 46 but field mesh/texture are still cached for 44");
  field.VerticesDirty = field.MaterialDirty = 0; Tick(field);
  if (field.VerticesDirty != 0 || field.MaterialDirty != 0)
   throw new System.Exception("FAIL: unchanged background rebuilds every frame");
  field.PreviewTheme = 44; Tick(field);
  field.VerticesDirty = field.MaterialDirty = 0;
  ThemeCatalog.SelectedBackground = 47; Tick(field);
  if (field.VerticesDirty != 0 || field.MaterialDirty != 0)
   throw new System.Exception("FAIL: saved selection interferes with an explicit preview");
  System.Console.WriteLine("PASS: delayed background restoration invalidates mesh and texture; unchanged themes and explicit previews stay stable");
 }
}
'@
Add-Type ($source + "`n" + $stubs)
[ThemeRestoreChecks]::Run()
