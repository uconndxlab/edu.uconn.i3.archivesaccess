using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.Video;

// Component to hold references to archive assets
public class ArchiveAssetReference : MonoBehaviour
{
    [System.Serializable]
    public class AssetReference
    {
        public string assetPath;
        public string assetType;
        public UnityEngine.Object assetObject;
    }
    
    public List<AssetReference> attachments = new List<AssetReference>();
}

public class AssetSelectionWindow : EditorWindow
{
    private List<AttachmentInfo> _attachments;
    private string _assetName;
    private string _apiUrl;
    private System.Action<string, string, string, string, int> _onAssetSelected;
    private ListView _listView;

    public class AttachmentInfo
    {
        public string Url { get; set; }
        public string Title { get; set; }
        public string MimeType { get; set; }
        public int Index { get; set; }
    }

    public static void ShowWindow(List<AttachmentInfo> attachments, string assetName, string apiUrl, System.Action<string, string, string, string, int> onAssetSelected)
    {
        var window = GetWindow<AssetSelectionWindow>("Select Asset to Import");
        window.minSize = new Vector2(400, 300);
        window._attachments = attachments;
        window._assetName = assetName;
        window._apiUrl = apiUrl;
        window._onAssetSelected = onAssetSelected;
        window.BuildUI(); // Build UI after setting data
        window.ShowModal();
    }

    public void CreateGUI()
    {
        // CreateGUI is called automatically, but we'll build UI in BuildUI() after data is set
    }

    private void BuildUI()
    {
        // Clear any existing content
        rootVisualElement.Clear();

        Debug.Log($"BuildUI called with {_attachments?.Count ?? 0} attachments for asset: {_assetName}");

        var root = rootVisualElement;
        root.style.paddingTop = 10;
        root.style.paddingBottom = 10;
        root.style.paddingLeft = 10;
        root.style.paddingRight = 10;

        var label = new Label($"Select an asset to import for: {_assetName}");
        label.style.fontSize = 14;
        label.style.marginBottom = 10;
        label.style.unityFontStyleAndWeight = FontStyle.Bold;
        root.Add(label);

        var infoLabel = new Label($"Found {_attachments?.Count ?? 0} attachment(s). Click an item to import it.");
        infoLabel.style.marginBottom = 10;
        root.Add(infoLabel);

        // Create list view
        _listView = new ListView();
        _listView.style.flexGrow = 1;
        _listView.selectionType = SelectionType.Single;
        _listView.itemsSource = _attachments;
        _listView.fixedItemHeight = 60;
        
        _listView.makeItem = () =>
        {
            var container = new VisualElement();
            container.style.flexDirection = FlexDirection.Column;
            container.style.paddingTop = 5;
            container.style.paddingBottom = 5;
            container.style.paddingLeft = 10;
            container.style.paddingRight = 10;
            
            var titleLabel = new Label();
            titleLabel.style.fontSize = 12;
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            
            var typeLabel = new Label();
            typeLabel.style.fontSize = 10;
            typeLabel.style.color = new Color(0.6f, 0.6f, 0.6f);
            
            container.Add(titleLabel);
            container.Add(typeLabel);
            
            return container;
        };
        
        _listView.bindItem = (element, index) =>
        {
            if (_attachments == null || index < 0 || index >= _attachments.Count)
            {
                Debug.LogWarning($"Invalid bindItem: index={index}, attachments count={_attachments?.Count ?? 0}");
                return;
            }

            var attachment = _attachments[index];
            var titleLabel = element.Q<Label>();
            var typeLabel = element.ElementAt(1) as Label;
            
            if (titleLabel != null)
                titleLabel.text = attachment.Title;
            if (typeLabel != null)
                typeLabel.text = $"Type: {attachment.MimeType}";
            
            Debug.Log($"Bound item {index}: {attachment.Title}");
        };

        root.Add(_listView);

        // Force rebuild the list view
        _listView.Rebuild();

        // Buttons container
        var buttonContainer = new VisualElement();
        buttonContainer.style.flexDirection = FlexDirection.Row;
        buttonContainer.style.justifyContent = Justify.FlexEnd;
        buttonContainer.style.marginTop = 10;

        var cancelButton = new Button(() => Close()) { text = "Cancel" };
        cancelButton.style.marginRight = 5;
        buttonContainer.Add(cancelButton);

        var importButton = new Button(OnImportClicked) { text = "Import Selected" };
        buttonContainer.Add(importButton);

        root.Add(buttonContainer);
    }

    private void OnImportClicked()
    {
        if (_listView.selectedIndex >= 0 && _listView.selectedIndex < _attachments.Count)
        {
            var selected = _attachments[_listView.selectedIndex];
            string downloadEndpoint = _apiUrl + "download?url=" + selected.Url;
            _onAssetSelected?.Invoke(_assetName, downloadEndpoint, selected.Title, selected.MimeType, selected.Index);
            Close();
        }
        else
        {
            EditorUtility.DisplayDialog("No Selection", "Please select an asset to import.", "OK");
        }
    }
}

public class ArchivesAccess : EditorWindow
{
    public const string titleContentText = "Archives Access";
    MultiColumnListView _table;
    List<MetadataItem> _metadataItems = new List<MetadataItem>();
    Button _generateButton;
    JsonElement _apiResponse; // Store full API response including attachments

    string apiUrl = "http://127.0.0.1:8000/api/";

    public class MetadataItem
    {
        public string PropertyName { get; set; }
        public string PropertyValue { get; set; }
    }

    [MenuItem("Tools/" + titleContentText)]
    public static void ShowMyEditor()
    {
        // This method is called when the user selects the menu item in the Editor
        EditorWindow wnd = GetWindow<ArchivesAccess>();
        wnd.titleContent = new GUIContent(titleContentText);
    }


    public void CreateGUI()
    {
        var splitView = new TwoPaneSplitView(0, 75, TwoPaneSplitViewOrientation.Vertical);
        rootVisualElement.Add(splitView);

        var topPane = new VisualElement();
        topPane.style.flexGrow = 1;
        splitView.Add(topPane);

        // Create the table with two columns
        var columns = new Columns();
        columns.Add(new Column
        {
            name = "property-name",
            title = "Property",
            width = 100,
            stretchable = true
        });
        columns.Add(new Column
        {
            name = "property-value",
            title = "Value",
            stretchable = true
        });

    _table = new MultiColumnListView(columns);
    _table.style.flexGrow = 1;
    _table.showAlternatingRowBackgrounds = AlternatingRowBackground.ContentOnly;
    _table.selectionType = SelectionType.Single;
        
        // Set up how items are created and bound
        _table.itemsSource = _metadataItems;
        _table.fixedItemHeight = 45; // Minimum height, labels will wrap if needed
        
        _table.columns["property-name"].makeCell = () => 
        {
            var label = new Label();
            label.style.paddingTop = 10;
            label.style.paddingBottom = 10;
            label.style.paddingLeft = 10;
            label.style.paddingRight = 10;
            label.style.whiteSpace = WhiteSpace.Normal; // Enable text wrapping
            label.style.flexShrink = 1;
            return label;
        };
        _table.columns["property-name"].bindCell = (element, index) =>
        {
            (element as Label).text = _metadataItems[index].PropertyName;
        };
        
        _table.columns["property-value"].makeCell = () => 
        {
            var label = new Label();
            label.style.paddingTop = 10;
            label.style.paddingBottom = 10;
            label.style.paddingLeft = 10;
            label.style.paddingRight = 10;
            label.style.whiteSpace = WhiteSpace.Normal; // Enable text wrapping
            label.style.flexShrink = 1;
            return label;
        };
        _table.columns["property-value"].bindCell = (element, index) =>
        {
            (element as Label).text = _metadataItems[index].PropertyValue;
        };

        // Bottom pane container to host the table and the footer button
        var bottomPane = new VisualElement();
        bottomPane.style.flexGrow = 1;
        bottomPane.style.flexDirection = FlexDirection.Column;

        bottomPane.Add(_table);

        // Footer button: Generate Asset
        _generateButton = new Button(GenerateAsset)
        {
            text = "Generate Asset"
        };
        _generateButton.style.marginTop = 6;
        _generateButton.style.alignSelf = Align.FlexEnd; // place on the right; remove if you prefer full width
        _generateButton.SetEnabled(false);
        bottomPane.Add(_generateButton);

        splitView.Add(bottomPane);

        // Example URLs for different content types
        var exampleUrls = new Dictionary<string, string>
        {
            { "Use PDF Demo URL", "https://collections.ctdigitalarchive.org/node/144961" },
            { "Use Image Demo URL", "https://collections.ctdigitalarchive.org/node/947297" },
            { "Use Video Demo URL", "https://collections.ctdigitalarchive.org/node/745225" },
            { "Use Audio Demo URL", "https://collections.ctdigitalarchive.org/node/2316120" }
        };

        // Create dropdown for asset selection
        var assetDropdown = new DropdownField("Asset URL", 
            exampleUrls.Keys.ToList(),
            "Use Image Demo URL");
        assetDropdown.style.marginBottom = 10;

        // Store the selected URL
        string selectedUrl = exampleUrls["Use Image Demo URL"];
        assetDropdown.RegisterValueChangedCallback(evt =>
        {
            selectedUrl = exampleUrls[evt.newValue];
            Debug.Log($"Selected {evt.newValue}: {selectedUrl}");
        });

        Button downloadButton = new Button(() =>
        {
            Debug.Log("Button pressed!");
            string fetchEndpoint = apiUrl + "parse?url=" + selectedUrl.Trim();
            Debug.Log("Fetching data from API: " + fetchEndpoint);
            var fetchTask = FetchDataFromAPI(fetchEndpoint);
            
            _metadataItems.Clear();
            _metadataItems.Add(new MetadataItem { PropertyName = "Loading...", PropertyValue = "" });
            _table.RefreshItems();
            _generateButton.SetEnabled(false);
            
            fetchTask.ContinueWith(task =>  
            {
                if (task.Result != null)
                {
                    Debug.Log("Data fetched from API: " + task.Result);

                    _metadataItems.Clear();
                    
                    // Parse the JSON response using System.Text.Json
                    try
                    {
                        using (var doc = JsonDocument.Parse(task.Result))
                        {
                            _apiResponse = doc.RootElement.Clone(); // Store for later use in GenerateAsset
                            
                            if (doc.RootElement.TryGetProperty("data", out var data) &&
                                data.TryGetProperty("meta", out var metaData))
                            {
                                foreach (var property in metaData.EnumerateObject())
                                {
                                    var val = "";
                                    if (property.Value.ValueKind == JsonValueKind.Array)
                                    {
                                        foreach (var item in property.Value.EnumerateArray())
                                        {
                                            val += item.ToString() + "\n";
                                        }
                                    }
                                    else
                                    {
                                        val = property.Value.ToString();
                                    }

                                    _metadataItems.Add(new MetadataItem 
                                    { 
                                        PropertyName = property.Name, 
                                        PropertyValue = val 
                                    });
                                }
                            }
                        }
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogError($"Failed to parse JSON response: {ex.Message}");
                    }
                    
                    _table.RefreshItems();
                    _generateButton.SetEnabled(_metadataItems.Count > 0);
                }
                else
                {
                    Debug.LogError("Failed to fetch data from API.");
                    _metadataItems.Clear();
                    _metadataItems.Add(new MetadataItem { PropertyName = "Error", PropertyValue = "Failed to fetch data from API." });
                    _table.RefreshItems();
                    _generateButton.SetEnabled(false);
                }
            }, System.Threading.Tasks.TaskScheduler.FromCurrentSynchronizationContext());
        })
        { text = "Fetch Asset" };

        rootVisualElement.style.paddingBottom = 10;
        rootVisualElement.style.paddingRight = 10;
        rootVisualElement.style.paddingTop = 10;
        rootVisualElement.style.paddingLeft = 10;
        topPane.Add(assetDropdown);
        topPane.Add(downloadButton);
    }

    private async System.Threading.Tasks.Task<string> FetchDataFromAPI(string url)
    {
        using (var client = new System.Net.Http.HttpClient())
        {
            try
            {
                var response = await client.GetAsync(url);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsStringAsync();
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Error fetching data from API: {ex.Message}");
                return null;
            }
        }
    }

    private void GenerateAsset()
    {
        // Disable the button to prevent multiple clicks
        _generateButton.SetEnabled(false);
        _generateButton.text = "Opening Selection...";

        try
        {
            // Choose a meaningful name from metadata
            string GetMeta(string key)
            {
                var item = _metadataItems.FirstOrDefault(m => string.Equals(m.PropertyName, key, System.StringComparison.OrdinalIgnoreCase));
                return item?.PropertyValue;
            }

            string name = GetMeta("title") ?? GetMeta("label") ?? GetMeta("name") ?? "Archive Asset";
            if (!string.IsNullOrEmpty(name))
            {
                // If there are multiple lines (e.g., arrays joined), take the first non-empty line
                var lines = name.Split('\n');
                foreach (var l in lines)
                {
                    if (!string.IsNullOrWhiteSpace(l)) { name = l.Trim(); break; }
                }
            }

            // Collect attachment info and show selection window
            if (_apiResponse.ValueKind != JsonValueKind.Undefined &&
                _apiResponse.TryGetProperty("data", out var data) &&
                data.TryGetProperty("attachments", out var attachments) &&
                attachments.ValueKind == JsonValueKind.Array)
            {
                var attachmentList = new List<AssetSelectionWindow.AttachmentInfo>();
                
                int i = 0;
                foreach (var attachment in attachments.EnumerateArray())
                {
                    string url = attachment.TryGetProperty("url", out var urlProp) ? urlProp.GetString() : "";
                    string title = attachment.TryGetProperty("title", out var titleProp) ? titleProp.GetString() : $"Attachment_{i}";
                    
                    string mimeType = "application/octet-stream";
                    if (attachment.TryGetProperty("type", out var typeArray) && typeArray.ValueKind == JsonValueKind.Array)
                    {
                        var parts = new List<string>();
                        foreach (var item in typeArray.EnumerateArray())
                        {
                            parts.Add(item.GetString() ?? "");
                        }
                        mimeType = string.Join("/", parts);
                    }

                    if (!string.IsNullOrEmpty(url))
                    {
                        attachmentList.Add(new AssetSelectionWindow.AttachmentInfo
                        {
                            Url = url,
                            Title = title,
                            MimeType = mimeType,
                            Index = i
                        });
                    }
                    i++;
                }
                
                if (attachmentList.Count > 0)
                {

                    // Show selection window
                    AssetSelectionWindow.ShowWindow(attachmentList, name, apiUrl, async (assetName, downloadEndpoint, title, mimeType, index) =>
                    {
                        // This callback is executed when user selects an asset
                        _generateButton.SetEnabled(false);
                        _generateButton.text = "Generating...";

                        string assetPath = await DownloadAttachment(assetName, downloadEndpoint, title, mimeType, index);
                        
                        if (!string.IsNullOrEmpty(assetPath))
                        {
                            // Create the GameObject with undo support
                            var go = new GameObject(assetName);
                            Undo.RegisterCreatedObjectUndo(go, "Generate Archive Asset");
                            go.transform.position = Vector3.zero;
                            
                            Debug.Log($"Generated GameObject for asset: {assetName}");
                            
                            // Attach the downloaded asset to the GameObject
                            AttachAssetToGameObject(go, assetPath, mimeType, index);
                            
                            // Mark the scene as dirty so the object persists
                            EditorSceneManager.MarkSceneDirty(go.scene);
                            
                            // Select and focus on the new GameObject
                            Selection.activeGameObject = go;
                            EditorGUIUtility.PingObject(go);
                        }
                        else
                        {
                            Debug.LogWarning($"Asset download failed, GameObject not created.");
                        }

                        _generateButton.SetEnabled(true);
                        _generateButton.text = "Generate Asset";
                    });
                }
                else
                {
                    Debug.LogWarning("No attachments found.");
                }
            }
            else
            {
                Debug.LogWarning("No attachments available in API response.");
            }
        }
        finally
        {
            // Re-enable the button when selection window opens
            _generateButton.SetEnabled(true);
            _generateButton.text = "Generate Asset";
        }
    }

    private async System.Threading.Tasks.Task<string> DownloadAttachment(string assetName, string url, string title, string mimeType, int index)
    {
        try
        {
            using (var client = new System.Net.Http.HttpClient())
            {
                Debug.Log($"Downloading attachment from: {url}");
                using (var response = await client.GetAsync(url))
                {
                    response.EnsureSuccessStatusCode();
                    var bytes = await response.Content.ReadAsByteArrayAsync();

                    // Always prefer MIME type from HTTP response header over API metadata
                    // The actual response is the authoritative source for what the file really is
                    string actualMimeType = mimeType;
                    if (response.Content.Headers.ContentType != null && 
                        !string.IsNullOrEmpty(response.Content.Headers.ContentType.MediaType))
                    {
                        string headerMimeType = response.Content.Headers.ContentType.MediaType;
                        Debug.Log($"MIME type from API metadata: {mimeType}, from HTTP response: {headerMimeType}");
                        
                        // Always use the HTTP response MIME type as it's authoritative
                        actualMimeType = headerMimeType;
                        Debug.Log($"Using MIME type from HTTP response: {headerMimeType}");
                    }
                    else
                    {
                        Debug.Log($"No Content-Type in HTTP response, using API metadata: {mimeType}");
                    }

                    // Determine file extension from MIME type
                    string extension = GetExtensionFromMimeType(actualMimeType);
                    Debug.Log($"Final MIME type: {actualMimeType}, extension: {extension}");
                    
                    // Clean title for filename - remove all invalid characters
                    string safeTitle = SanitizeFileName(title);
                    string safeAssetName = SanitizeFileName(assetName);
                    
                    // Create Assets folder structure
                    string folderPath = "Assets/ArchiveAssets";
                    if (!UnityEditor.AssetDatabase.IsValidFolder(folderPath))
                    {
                        UnityEditor.AssetDatabase.CreateFolder("Assets", "ArchiveAssets");
                    }

                    string parentFolderPath = $"{folderPath}/{safeAssetName}";
                    if (!UnityEditor.AssetDatabase.IsValidFolder(parentFolderPath))
                    {
                        UnityEditor.AssetDatabase.CreateFolder(folderPath, safeAssetName);
                    }

                    string filename = $"{safeTitle}_{index}{extension}";
                    string assetPath = $"{parentFolderPath}/{filename}";
                    string tempPath = assetPath + ".download"; // temp path to avoid importer races

                    Debug.Log($"Downloading to: {assetPath}");

                    // Write to temp file first to avoid sharing violations
                    SafeWriteAllBytes(tempPath, bytes);
                    
                    // Move temp to final with retry (overwrite if needed)
                    TryReplaceWithRetry(tempPath, assetPath, 10, 50);
                    
                    // Import only the written asset (faster and reduces races)
                    if (System.IO.File.Exists(assetPath))
                    {
                        UnityEditor.AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
                    }

                    Debug.Log($"Downloaded and saved: {assetPath} ({bytes.Length} bytes, {mimeType})");

                    return assetPath;
                }
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Failed to download attachment from {url}: {ex.Message}");
            return null;
        }
    }

    private void SafeWriteAllBytes(string path, byte[] bytes)
    {
        // Ensure any previous temp file is gone
        TryDeleteWithRetry(path, 5, 20);
        
        // Create directory if missing
        var dir = System.IO.Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir) && !System.IO.Directory.Exists(dir))
        {
            try
            {
                System.IO.Directory.CreateDirectory(dir);
                Debug.Log($"Created directory: {dir}");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Failed to create directory '{dir}': {ex.Message}");
                throw;
            }
        }
        
        // Write with retry in case of transient locks
        int attempts = 10;
        for (int i = 0; i < attempts; i++)
        {
            try
            {
                System.IO.File.WriteAllBytes(path, bytes);
                Debug.Log($"Successfully wrote {bytes.Length} bytes to: {path}");
                return;
            }
            catch (System.IO.IOException ioEx)
            {
                if (i == attempts - 1)
                {
                    Debug.LogError($"Failed to write file after {attempts} attempts: {ioEx.Message}");
                    throw;
                }
                System.Threading.Thread.Sleep(50);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Unexpected error writing file '{path}': {ex.GetType().Name} - {ex.Message}");
                throw;
            }
        }
    }

    private bool TryDeleteWithRetry(string path, int attempts, int sleepMs)
    {
        for (int i = 0; i < attempts; i++)
        {
            try
            {
                if (System.IO.File.Exists(path))
                {
                    System.IO.File.Delete(path);
                }
                return true;
            }
            catch (System.IO.IOException)
            {
                System.Threading.Thread.Sleep(sleepMs);
            }
        }
        return false;
    }

    private void TryReplaceWithRetry(string tempPath, string finalPath, int attempts, int sleepMs)
    {
        for (int i = 0; i < attempts; i++)
        {
            try
            {
                if (System.IO.File.Exists(finalPath))
                {
                    System.IO.File.Delete(finalPath);
                }
                System.IO.File.Move(tempPath, finalPath);
                return;
            }
            catch (System.IO.IOException)
            {
                if (i == attempts - 1) throw;
                System.Threading.Thread.Sleep(sleepMs);
            }
        }
    }

    private string SanitizeFileName(string filename)
    {
        if (string.IsNullOrEmpty(filename))
            return "Untitled";

        // Get invalid characters for both path and filename
        var invalidChars = System.IO.Path.GetInvalidFileNameChars()
            .Concat(System.IO.Path.GetInvalidPathChars())
            .Distinct()
            .ToArray();

        // Replace invalid characters with underscore
        string safe = invalidChars.Aggregate(filename, (current, c) => current.Replace(c, '_'));

        // Also replace some additional problematic characters
        safe = safe.Replace(":", "_")
                   .Replace("/", "_")
                   .Replace("\\", "_")
                   .Replace("|", "_")
                   .Replace("?", "_")
                   .Replace("*", "_")
                   .Replace("\"", "_")
                   .Replace("<", "_")
                   .Replace(">", "_")
                   .Replace("\n", "_")
                   .Replace("\r", "_")
                   .Replace("\t", "_");

        // Remove leading/trailing spaces and dots (Windows doesn't like these)
        safe = safe.Trim(' ', '.');

        // Ensure it's not empty after sanitization
        if (string.IsNullOrEmpty(safe))
            return "Untitled";

        // Limit length to avoid path too long errors (Windows MAX_PATH is 260)
        if (safe.Length > 100)
            safe = safe.Substring(0, 100);

        return safe;
    }

    private string GetExtensionFromMimeType(string mimeType)
    {
        // Common MIME type to extension mapping
        var mimeMap = new Dictionary<string, string>
        {
            { "application/pdf", ".pdf" },
            { "image/jpeg", ".jpg" },
            { "image/jpg", ".jpg" },
            { "image/png", ".png" },
            { "image/gif", ".gif" },
            { "image/bmp", ".bmp" },
            { "image/jp2", ".jp2" },
            { "image/jpx", ".jp2" },
            { "image/jpm", ".jpm" },
            { "video/mp4", ".mp4" },
            { "video/mpeg", ".mpeg" },
            { "video/quicktime", ".mov" },
            { "video/x-msvideo", ".avi" },
            { "video/webm", ".webm" },
            { "audio/mpeg", ".mp3" },
            { "audio/wav", ".wav" },
            { "audio/x-wav", ".wav" },
            { "audio/ogg", ".ogg" },
            { "text/plain", ".txt" },
            { "text/html", ".html" },
            { "application/json", ".json" }
        };

        return mimeMap.TryGetValue(mimeType.ToLower(), out string ext) ? ext : ".dat";
    }

    private void AttachAssetToGameObject(GameObject parent, string assetPath, string mimeType, int index)
    {
        // Extract filename without extension for display
        string filename = Path.GetFileNameWithoutExtension(assetPath);
        string extension = Path.GetExtension(assetPath).ToLowerInvariant();

        // Special handling: backend now returns ZIP of JPEG pages instead of a PDF.
        if (extension == ".pdf")
        {
            try
            {
                byte[] pdfBytes = File.ReadAllBytes(assetPath);
                bool looksZip = pdfBytes.Length > 4 && pdfBytes[0] == 'P' && pdfBytes[1] == 'K';
                if (looksZip)
                {
                    Debug.Log($"PDF asset at '{assetPath}' identified as ZIP of pages. Extracting...");
                    string parentFolder = Path.GetDirectoryName(assetPath);
                    string pagesFolder = Path.Combine(parentFolder, filename + "_pages");
                    if (!Directory.Exists(pagesFolder)) Directory.CreateDirectory(pagesFolder);

                    using (var ms = new MemoryStream(pdfBytes))
                    using (var archive = new ZipArchive(ms, ZipArchiveMode.Read))
                    {
                        int pageIndex = 0;
                        foreach (var entry in archive.Entries)
                        {
                            if (entry.Length == 0) continue; // skip directories
                            string entryExt = Path.GetExtension(entry.Name).ToLowerInvariant();
                            if (entryExt != ".jpg" && entryExt != ".jpeg" && entryExt != ".png") continue;

                            string safeEntryName = SanitizeFileName(Path.GetFileNameWithoutExtension(entry.Name)) + entryExt;
                            string outPath = Path.Combine(pagesFolder, safeEntryName);

                            // Avoid overwrite: if exists, append index
                            if (File.Exists(outPath))
                            {
                                outPath = Path.Combine(pagesFolder, safeEntryName.Replace(entryExt, "_" + pageIndex + entryExt));
                            }

                            using (var es = entry.Open())
                            using (var fs = new FileStream(outPath, FileMode.Create, FileAccess.Write))
                            {
                                es.CopyTo(fs);
                            }

                            // Import and create sprite GameObject
                            string unityPath = outPath.Replace("\\", "/").Replace(Application.dataPath, "Assets");
                            // If path is under Assets folder already, ensure unityPath relative
                            if (!unityPath.StartsWith("Assets"))
                            {
                                // Convert absolute to relative if extraction ended up absolute
                                int assetsIndex = outPath.IndexOf("Assets");
                                if (assetsIndex >= 0) unityPath = outPath.Substring(assetsIndex).Replace("\\", "/");
                            }
                            AssetDatabase.ImportAsset(unityPath, ImportAssetOptions.ForceUpdate);
                            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(unityPath);
                            if (tex != null)
                            {
                                var pageGO = new GameObject($"Page {pageIndex + 1}: {filename}");
                                pageGO.transform.SetParent(parent.transform);
                                pageGO.transform.localPosition = new Vector3(pageIndex * 0.5f, 0, 0); // slight offset
                                var sr = pageGO.AddComponent<SpriteRenderer>();
                                var sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                                sr.sprite = sprite;
                                Undo.RegisterCreatedObjectUndo(pageGO, "Add PDF Page Sprite");
                                Debug.Log($"Imported PDF page {pageIndex + 1}: {unityPath}");

                                // Track reference
                                var assetRef = parent.GetComponent<ArchiveAssetReference>() ?? parent.AddComponent<ArchiveAssetReference>();
                                assetRef.attachments.Add(new ArchiveAssetReference.AssetReference
                                {
                                    assetPath = unityPath,
                                    assetType = ".jpg",
                                    assetObject = tex
                                });
                            }
                            else
                            {
                                Debug.LogWarning($"Failed to load extracted page as Texture2D: {unityPath}");
                            }
                            pageIndex++;
                        }
                        if (pageIndex == 0)
                        {
                            Debug.LogWarning("ZIP (from PDF) contained no image pages to import.");
                        }
                    }
                    return; // Done handling PDF-zip
                }
                else
                {
                    Debug.Log("PDF does not appear to be a ZIP. Storing reference only.");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Failed extracting PDF pages from '{assetPath}': {ex.Message}");
            }
        }

        // Load asset normally for non-PDF types
        var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
        if (asset == null)
        {
            Debug.LogWarning($"Could not load asset at {assetPath}");
            return;
        }

        if (asset is Texture2D texture)
        {
            var imageGO = new GameObject($"Image: {filename}");
            imageGO.transform.SetParent(parent.transform);
            imageGO.transform.localPosition = Vector3.zero;
            var spriteRenderer = imageGO.AddComponent<SpriteRenderer>();
            var sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
            spriteRenderer.sprite = sprite;
            Undo.RegisterCreatedObjectUndo(imageGO, "Add Image Attachment");
            Debug.Log($"Attached image as SpriteRenderer: {filename} ({texture.width}x{texture.height})");
        }
        else if (asset is VideoClip videoClip)
        {
            var videoGO = new GameObject($"Video: {filename}");
            videoGO.transform.SetParent(parent.transform);
            videoGO.transform.localPosition = Vector3.zero;
            var videoPlayer = videoGO.AddComponent<UnityEngine.Video.VideoPlayer>();
            videoPlayer.clip = videoClip;
            videoPlayer.playOnAwake = false;
            Undo.RegisterCreatedObjectUndo(videoGO, "Add Video Attachment");
            Debug.Log($"Attached video as VideoPlayer: {filename}");
        }
        else if (asset is AudioClip audioClip)
        {
            var audioSource = parent.AddComponent<AudioSource>();
            audioSource.clip = audioClip;
            audioSource.playOnAwake = false;
            Debug.Log($"Attached audio as AudioSource: {filename} (length: {audioClip.length}s)");
        }
        else
        {
            // Fallback: store reference for unsupported but imported assets (e.g., text/json)
            var assetRef = parent.GetComponent<ArchiveAssetReference>() ?? parent.AddComponent<ArchiveAssetReference>();
            assetRef.attachments.Add(new ArchiveAssetReference.AssetReference
            {
                assetPath = assetPath,
                assetType = extension,
                assetObject = asset
            });
            EditorUtility.SetDirty(parent);
            Debug.Log($"Stored reference for asset type {extension}: {filename}");
        }
    }
}
