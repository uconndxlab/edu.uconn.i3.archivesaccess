using UnityEditor.AssetImporters;
using UnityEngine;
using System;
using System.IO;
using System.Linq;

// Currently uses Spire.PDF to render PDF pages to JPEG images via reflection.
// @todo: look into aspose.pdf, maui.pdf or ImageSharp

// PDFtoImage A free library that uses the PDFium rendering engine (the same one used in Google Chrome) and the SkiaSharp cross-platform graphics API to convert PDFs to images. It is compatible with .NET (Core) and .NET Framework.
// Ghostscript.NET	This library is a managed wrapper for the Ghostscript library, a powerful, open-source interpreter for PostScript and PDF files. You can use it to convert PDFs to images by calling the Ghostscript library from your C# code.
// iText7	A powerful library for PDF manipulation that includes conversion capabilities. While it is free, it may have a steeper learning curve and a larger footprint than other options if you only need PDF-to-image conversion, says Microsoft Learn.
// PdfiumSharp	A library that allows you to use the Pdfium library to render PDF pages into images. It can be combined with PDFsharp if you need to read or manipulate PDF files.
// Debenu PDF	Offers a free lite version with a lightweight footprint and good documentation for basic PDF editing and conversion.


[ScriptedImporter(1, "pdf")] // Register this importer for files with the .pdf extension, version 1
public class ImageImporterPDF : ScriptedImporter
{
    public override void OnImportAsset(AssetImportContext ctx)
    {
        // Render each PDF page to a zero-padded JPEG file alongside the PDF using Spire.PDF via reflection.
        // If Spire.PDF is unavailable, fall back to a placeholder TextAsset.
        Debug.Log($"[PDF Importer] Starting import for: {ctx.assetPath}");
        
        try
        {
            if (TryRenderPdfToJpegs(ctx.assetPath, out string outputFolder, out int pageCount, out string failureReason))
            {
                Debug.Log($"[PDF Importer] Successfully rendered {pageCount} page(s) to '{outputFolder}'");
                var info = new TextAsset($"Rendered {pageCount} page(s) to '{outputFolder}'.");
                info.name = Path.GetFileNameWithoutExtension(ctx.assetPath);
                ctx.AddObjectToAsset("info", info);
                ctx.SetMainObject(info);
                return;
            }

            Debug.LogWarning($"[PDF Importer] Failed to render PDF. Reason: {failureReason}");
            var placeholder = new TextAsset($"PDF imported; renderer not available. Reason: {failureReason}");
            placeholder.name = Path.GetFileNameWithoutExtension(ctx.assetPath);
            ctx.AddObjectToAsset("pdf", placeholder);
            ctx.SetMainObject(placeholder);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[PDF Importer] Exception during import: {ex.Message}\n{ex.StackTrace}");
            var errorInfo = new TextAsset("PDF import error: " + ex.Message);
            errorInfo.name = "PDF Import Error";
            ctx.AddObjectToAsset("error", errorInfo);
            ctx.SetMainObject(errorInfo);
        }
    }

    private static bool TryRenderPdfToJpegs(string pdfPath, out string outputFolder, out int totalPages, out string failureReason)
    {
        outputFolder = null;
        totalPages = 0;
        failureReason = null;

        Debug.Log($"[PDF Importer] Looking for Spire.Pdf assembly...");
        var spireAsm = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => string.Equals(a.GetName().Name, "Spire.Pdf", StringComparison.OrdinalIgnoreCase));
        if (spireAsm == null) 
        {
            failureReason = "Spire.Pdf assembly not found. Install Spire.PDF for Unity Editor.";
            Debug.LogWarning($"[PDF Importer] {failureReason}");
            return false;
        }
        Debug.Log($"[PDF Importer] Found Spire.Pdf assembly: {spireAsm.FullName}");

        var docType = spireAsm.GetType("Spire.Pdf.PdfDocument");
        if (docType == null)
        {
            failureReason = "Spire.Pdf.PdfDocument type not found in assembly.";
            Debug.LogWarning($"[PDF Importer] {failureReason}");
            return false;
        }
        Debug.Log($"[PDF Importer] Found PdfDocument type: {docType.FullName}");

        object doc = null;
        try
        {
            Debug.Log($"[PDF Importer] Creating PdfDocument instance...");
            doc = Activator.CreateInstance(docType);

            var loadFromFile = docType.GetMethod("LoadFromFile", new[] { typeof(string) });
            if (loadFromFile == null)
            {
                failureReason = "LoadFromFile method not found on PdfDocument.";
                Debug.LogWarning($"[PDF Importer] {failureReason}");
                return false;
            }
            
            Debug.Log($"[PDF Importer] Loading PDF from: {pdfPath}");
            loadFromFile.Invoke(doc, new object[] { pdfPath });

            var pagesProp = docType.GetProperty("Pages");
            if (pagesProp == null)
            {
                failureReason = "Pages property not found on PdfDocument.";
                Debug.LogWarning($"[PDF Importer] {failureReason}");
                return false;
            }
            
            var pagesObj = pagesProp.GetValue(doc);
            var pagesType = pagesObj?.GetType();
            var countProp = pagesType?.GetProperty("Count");
            if (countProp == null)
            {
                failureReason = "Count property not found on Pages collection.";
                Debug.LogWarning($"[PDF Importer] {failureReason}");
                return false;
            }
            
            totalPages = (int)Convert.ChangeType(countProp.GetValue(pagesObj), typeof(int));
            Debug.Log($"[PDF Importer] PDF has {totalPages} page(s)");
            
            if (totalPages <= 0)
            {
                failureReason = "PDF has no pages.";
                Debug.LogWarning($"[PDF Importer] {failureReason}");
                return true;
            }

            var saveAsImage = docType.GetMethod("SaveAsImage", new[] { typeof(int) });
            if (saveAsImage == null)
            {
                failureReason = "SaveAsImage method not found on PdfDocument.";
                Debug.LogWarning($"[PDF Importer] {failureReason}");
                return false;
            }

            var dir = Path.GetDirectoryName(pdfPath);
            var baseName = Path.GetFileNameWithoutExtension(pdfPath);
            outputFolder = Path.Combine(dir ?? string.Empty, baseName + "_pages");
            
            Debug.Log($"[PDF Importer] Creating output folder: {outputFolder}");
            if (!Directory.Exists(outputFolder)) Directory.CreateDirectory(outputFolder);

            Debug.Log($"[PDF Importer] Looking for System.Drawing assembly...");
            var drawingAsm = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => string.Equals(a.GetName().Name, "System.Drawing", StringComparison.OrdinalIgnoreCase));
            if (drawingAsm == null)
            {
                failureReason = "System.Drawing assembly not found.";
                Debug.LogWarning($"[PDF Importer] {failureReason}");
                return false;
            }
            Debug.Log($"[PDF Importer] Found System.Drawing assembly: {drawingAsm.FullName}");
            
            var imageFormatType = drawingAsm.GetType("System.Drawing.Imaging.ImageFormat");
            var jpegProp = imageFormatType?.GetProperty("Jpeg");
            var jpegFormat = jpegProp?.GetValue(null);
            if (jpegFormat == null)
            {
                failureReason = "ImageFormat.Jpeg not found.";
                Debug.LogWarning($"[PDF Importer] {failureReason}");
                return false;
            }

            Debug.Log($"[PDF Importer] Starting page rendering loop...");
            int successfulPages = 0;
            for (int i = 0; i < totalPages; i++)
            {
                object image = null;
                try
                {
                    Debug.Log($"[PDF Importer] Rendering page {i + 1}/{totalPages}...");
                    image = saveAsImage.Invoke(doc, new object[] { i });
                    
                    if (image == null)
                    {
                        Debug.LogWarning($"[PDF Importer] Page {i + 1} returned null image - possible Spire.PDF Free Edition 10-page limit");
                        continue;
                    }
                    
                    var imageType = image.GetType();
                    var saveMethod = imageType.GetMethod("Save", new[] { typeof(string), jpegFormat.GetType() });
                    if (saveMethod == null)
                    {
                        saveMethod = imageType.GetMethod("Save", new[] { typeof(string) });
                    }

                    var fileName = (i + 1).ToString("D4") + ".jpeg";
                    var outPath = Path.Combine(outputFolder, fileName);

                    Debug.Log($"[PDF Importer] Saving page to: {outPath}");
                    if (saveMethod.GetParameters().Length == 2)
                        saveMethod.Invoke(image, new object[] { outPath, jpegFormat });
                    else
                        saveMethod.Invoke(image, new object[] { outPath });
                    
                    // Check if file was actually created and has content
                    if (File.Exists(outPath))
                    {
                        var fileInfo = new FileInfo(outPath);
                        if (fileInfo.Length > 0)
                        {
                            successfulPages++;
                            Debug.Log($"[PDF Importer] Page {i + 1} saved successfully ({fileInfo.Length} bytes)");
                        }
                        else
                        {
                            Debug.LogWarning($"[PDF Importer] Page {i + 1} saved but file is empty (0 bytes) - likely Spire.PDF Free Edition limitation");
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"[PDF Importer] Page {i + 1} file was not created");
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[PDF Importer] Error rendering page {i + 1}: {ex.Message}");
                    throw;
                }
                finally
                {
                    if (image != null)
                    {
                        var disp = image.GetType().GetMethod("Dispose", Type.EmptyTypes);
                        disp?.Invoke(image, null);
                    }
                }
            }

            Debug.Log($"[PDF Importer] Closing PDF document...");
            var close = docType.GetMethod("Close", Type.EmptyTypes);
            close?.Invoke(doc, null);
            var dispose = docType.GetMethod("Dispose", Type.EmptyTypes);
            dispose?.Invoke(doc, null);

            if (successfulPages < totalPages)
            {
                Debug.LogWarning($"[PDF Importer] Only {successfulPages} of {totalPages} pages rendered successfully. " +
                                $"Spire.PDF Free Edition has a 10-page limit. Consider upgrading to a licensed version for full PDF support.");
            }
            else
            {
                Debug.Log($"[PDF Importer] Successfully rendered all {totalPages} pages");
            }
            
            return true;
        }
        catch (Exception ex)
        {
            failureReason = $"Exception during rendering: {ex.Message}";
            Debug.LogError($"[PDF Importer] {failureReason}\n{ex.StackTrace}");
            
            try
            {
                if (doc != null)
                {
                    var dispose = doc.GetType().GetMethod("Dispose", Type.EmptyTypes);
                    dispose?.Invoke(doc, null);
                }
            }
            catch { }
            return false;
        }
    }
}