// using UnityEditor.AssetImporters;
// using UnityEngine;
// using System.IO;

// // Simple PDF importer that imports PDF files as TextAssets.
// // This allows PDFs to be recognized by Unity's asset system.

// [ScriptedImporter(1, "pdf")]
// public class ImageImporterPDF : ScriptedImporter
// {
//     public override void OnImportAsset(AssetImportContext ctx)
//     {
//         // Import PDF as a TextAsset so it's recognized by Unity
//         var pdfData = File.ReadAllBytes(ctx.assetPath);
//         var textAsset = new TextAsset(System.Convert.ToBase64String(pdfData));
//         textAsset.name = Path.GetFileNameWithoutExtension(ctx.assetPath);
        
//         ctx.AddObjectToAsset("pdf", textAsset);
//         ctx.SetMainObject(textAsset);
//     }
// }
