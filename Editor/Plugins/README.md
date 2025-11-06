# Spire.PDF Setup Instructions

## Installation

1. **Move the DLL**: 
   - Move `Spire.Pdf.dll` from the package root to `Editor/Plugins/Spire.Pdf.dll`
   - Also move `Spire.Pdf.xml` (documentation file) if present

2. **Unity will auto-import**: 
   - Unity should automatically detect and import the DLL
   - Check the Console for any import errors

3. **Verify Import Settings**:
   - Select `Spire.Pdf.dll` in the Project window
   - In the Inspector, ensure:
     - "Editor" platform is checked
     - Other platforms are unchecked (this is Editor-only)

4. **Test the conversion**:
   - Go to menu: `Tools > Archives Access > Convert Demo PDF to Images`
   - This will convert the demo PDF (`20002_20117917.pdf`) to JPEG images
   - Images will be saved to `Assets/ArchiveAssets/DemoPDF_Pages/`

## Alternative: Manual Meta File

If Unity doesn't auto-import properly, create `Spire.Pdf.dll.meta` with this content:

```yaml
fileFormatVersion: 2
guid: a1b2c3d4e5f6789012345678901234ab
PluginImporter:
  externalObjects: {}
  serializedVersion: 2
  iconMap: {}
  executionOrder: {}
  defineConstraints: []
  isPreloaded: 0
  isOverridable: 1
  isExplicitlyReferenced: 0
  validateReferences: 1
  platformData:
  - first:
      Any: 
    second:
      enabled: 0
      settings: {}
  - first:
      Editor: Editor
    second:
      enabled: 1
      settings:
        DefaultValueInitialized: true
  userData: 
  assetBundleName: 
  assetBundleVariant: 
```

## License Note

Spire.PDF Free Edition adds a watermark. For production use, you'll need a commercial license from e-iceblue.
