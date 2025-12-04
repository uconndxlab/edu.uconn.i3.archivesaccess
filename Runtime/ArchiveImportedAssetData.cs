using UnityEngine;

[CreateAssetMenu(menuName = "Archives/Imported Asset Data")]
public class ArchiveImportedAssetData : ScriptableObject
{
    public string sourceUrl;
    [TextArea(1, 10)]
    public string metadataJson;
    public string originalAssetPath;
}
