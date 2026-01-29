using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text.Json;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UIElements;
using UnityEngine.Video;


[System.Serializable]
public class ArchiveImportedAssetData : ScriptableObject
{
    private string _sourceUrl = "";
    private string _metadataJson = "";
    private string _originalAssetPath = "";
    public List<MetadataEntry> metadataEntries = new List<MetadataEntry>();

    public string OriginalAssetPath
    {
        get { return _originalAssetPath; }
        set { _originalAssetPath = value; }
    }

    [System.Serializable]
    public class MetadataEntry
    {
        public string key;
        public string value;
    }

    public string SourceUrl
    {
        get { return _sourceUrl; }
        set { _sourceUrl = value; }
    }

    public string MetadataJson
    {
        get { return _metadataJson; }
        set { _metadataJson = value; }
    }

    public void ParseMetadata()
    {
        metadataEntries.Clear();
        if (string.IsNullOrEmpty(MetadataJson)) return;

        try
        {
            using (var doc = JsonDocument.Parse(MetadataJson))
            {
                foreach (var property in doc.RootElement.EnumerateObject())
                {
                    string val = "";
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

                    metadataEntries.Add(new MetadataEntry { key = property.Name, value = val });
                }
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"Failed to parse metadata JSON: {ex.Message}");
        }
    }
}

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

#if UNITY_EDITOR
[CustomEditor(typeof(ArchiveAssetReference))]
public class ArchiveAssetReferenceEditor : Editor
{
    private string _hoveredEntryKey = null;

    public override void OnInspectorGUI()
    {
        ArchiveAssetReference archiveRef = (ArchiveAssetReference)target;

        EditorGUILayout.LabelField("Archive Asset Attachments", EditorStyles.boldLabel);
        EditorGUILayout.Space(5);

        if (archiveRef.attachments.Count == 0)
        {
            EditorGUILayout.HelpBox("No attachments. Import an asset to add metadata.", MessageType.Info);
            return;
        }

        for (int i = 0; i < archiveRef.attachments.Count; i++)
        {
            var attachment = archiveRef.attachments[i];
            EditorGUILayout.LabelField($"Attachment {i + 1}: {attachment.assetType}", EditorStyles.boldLabel);
            EditorGUILayout.TextField("Path", attachment.assetPath);

            // Try to load metadata SO if it's an asset file
            if (attachment.assetType == ".asset" && attachment.assetObject is ArchiveImportedAssetData metadataSO)
            {
                EditorGUILayout.Space(3);
                EditorGUILayout.LabelField("Metadata", EditorStyles.boldLabel);

                if (metadataSO.metadataEntries.Count > 0)
                {
                    foreach (var entry in metadataSO.metadataEntries)
                    {
                        EditorGUILayout.LabelField(entry.key, EditorStyles.boldLabel);
                        
                        // Create a rect for the text area
                        Rect textAreaRect = EditorGUILayout.GetControlRect(GUILayout.Height(30));
                        
                        // Check if mouse is hovering over this text area
                        bool isHovered = textAreaRect.Contains(Event.current.mousePosition);
                        if (isHovered)
                        {
                            _hoveredEntryKey = entry.key;
                        }
                        
                        // Check if clicked
                        if (Event.current.type == EventType.MouseDown && textAreaRect.Contains(Event.current.mousePosition))
                        {
                            GUIUtility.systemCopyBuffer = entry.value;
                            Debug.Log($"Copied to clipboard: {entry.key}");
                            
                            // Show brief toast notification
                            EditorUtility.DisplayProgressBar("", "✓ Copied to clipboard", 0.5f);
                            System.Threading.Tasks.Task.Delay(750).ContinueWith(_ => EditorUtility.ClearProgressBar(), 
                                System.Threading.Tasks.TaskScheduler.FromCurrentSynchronizationContext());
                        }
                        
                        // Draw text area
                        EditorGUI.TextArea(textAreaRect, entry.value);
                        
                        // Draw copy icon in upper right corner if hovering
                        if (isHovered)
                        {
                            var iconRect = new Rect(textAreaRect.xMax - 25, textAreaRect.yMin + 3, 20, 20);
                            var copyIcon = EditorGUIUtility.IconContent("Clipboard");
                            
                            if (GUI.Button(iconRect, copyIcon, GUIStyle.none))
                            {
                                GUIUtility.systemCopyBuffer = entry.value;
                                Debug.Log($"Copied to clipboard: {entry.key}");
                            }
                        }
                    }
                }
                else
                {
                    EditorGUILayout.HelpBox("No metadata entries parsed. Metadata SO may not have been parsed yet.", MessageType.Info);
                }
            }

            EditorGUILayout.Space(10);
        }
    }
}
#endif

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

        var cancelButton = new UnityEngine.UIElements.Button(() => Close()) { text = "Cancel" };
        cancelButton.style.marginRight = 5;
        buttonContainer.Add(cancelButton);

        var importButton = new UnityEngine.UIElements.Button(OnImportClicked) { text = "Import Selected" };
        buttonContainer.Add(importButton);

        root.Add(buttonContainer);
    }

    private void OnImportClicked()
    {
        if (_listView.selectedIndex >= 0 && _listView.selectedIndex < _attachments.Count)
        {
            var selected = _attachments[_listView.selectedIndex];
            string downloadEndpoint = _apiUrl + "download?url=" + selected.Url;
            
            // Show initial progress bar
            EditorUtility.DisplayProgressBar("Importing Asset", $"Preparing to download: {selected.Title}", 0f);
            
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
    UnityEngine.UIElements.Button _generateButton;
    // Store full API response including attachments
    JsonElement _apiResponse;

    // Automatically detect dev mode by checking if package is in local development
    public static bool DevMode => IsLocalDevelopment();

    private static bool IsLocalDevelopment()
    {
        // Check if .git folder exists in the package directory (indicates local development)
        string packagePath = System.IO.Path.GetFullPath(
            System.IO.Path.Combine(
                UnityEngine.Application.dataPath,
                "..",
                "Packages",
                "edu.uconn.i3.archivesaccess"
            )
        );
        
        string gitPath = System.IO.Path.Combine(packagePath, ".git");
        return System.IO.Directory.Exists(gitPath);
    }

    public string serverUrl = "https://archives-access-server.dxdev.core.uconn.edu/";

    public string apiUrl => GetApiUrl();

    private string GetApiUrl()
    {
        // Remove trailing slash from serverUrl and append /api/
        string url = serverUrl.TrimEnd('/');
        return url + "/api/";
    }

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
        _generateButton = new UnityEngine.UIElements.Button(GenerateAsset)
        {
            text = "Generate Asset"
        };
        _generateButton.style.marginTop = 6;
        _generateButton.style.alignSelf = Align.FlexEnd; // place on the right; remove if you prefer full width
        _generateButton.SetEnabled(false);
        bottomPane.Add(_generateButton);

        splitView.Add(bottomPane);

        rootVisualElement.style.paddingBottom = 10;
        rootVisualElement.style.paddingRight = 10;
        rootVisualElement.style.paddingTop = 10;
        rootVisualElement.style.paddingLeft = 10;
        
        string selectedUrl = "";

        // URL input
        // Show text input field for URL in production mode
        var urlInput = new TextField("Asset URL");
        urlInput.style.marginBottom = 10;
        urlInput.value = "https://collections.ctdigitalarchive.org/node/1686854"; // Example default URL

        // Initialize selectedUrl with the default value
        selectedUrl = urlInput.value;

        urlInput.RegisterValueChangedCallback(evt =>
        {
            selectedUrl = evt.newValue.Trim();
        });

        topPane.Add(urlInput);


        if (DevMode)
        {
            // Show demo URLs dropdown in dev mode
            var exampleUrls = new Dictionary<string, string>
            {
                { "Use Large PDF Demo URL", "https://collections.ctdigitalarchive.org/node/144961" },
                { "Use Small PDF Demo URL", "https://collections.ctdigitalarchive.org/node/323508" },
                { "Use Image Demo URL", "https://collections.ctdigitalarchive.org/node/947297" },
                { "Use Video Demo URL", "https://collections.ctdigitalarchive.org/node/745225" },
                { "Use Audio Demo URL", "https://collections.ctdigitalarchive.org/node/2316120" }
            };

            var assetDropdown = new DropdownField("Asset URL", 
                exampleUrls.Keys.ToList(),
                "Use Image Demo URL");
            assetDropdown.style.marginBottom = 10;

            selectedUrl = exampleUrls["Use Image Demo URL"];
            assetDropdown.RegisterValueChangedCallback(evt =>
            {
                selectedUrl = exampleUrls[evt.newValue];
                Debug.Log($"Selected {evt.newValue}: {selectedUrl}");
            });

            topPane.Add(assetDropdown);
        }

        UnityEngine.UIElements.Button downloadButton = new UnityEngine.UIElements.Button(() =>
        {
            if (string.IsNullOrWhiteSpace(selectedUrl))
            {
                EditorUtility.DisplayDialog("Invalid URL", "Please enter a valid URL.", "OK");
                return;
            }

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
                // If the API returned a set of individual images for this PDF, offer a special
                // "PDF as Images" import option that will download each image separately.
                if (data.TryGetProperty("images", out var imagesProp) && imagesProp.ValueKind == JsonValueKind.Array && imagesProp.GetArrayLength() > 0)
                {
                    // Insert at front so it's visible as a primary option
                    attachmentList.Insert(0, new AssetSelectionWindow.AttachmentInfo
                    {
                        Url = "",
                        Title = "PDF as Images",
                        MimeType = "pdf-images",
                        Index = -1
                    });
                }

                if (attachmentList.Count > 0)
                {

                    // Show selection window
                    AssetSelectionWindow.ShowWindow(attachmentList, name, apiUrl, async (assetName, downloadEndpoint, title, mimeType, index) =>
                    {
                        // This callback is executed when user selects an asset
                        _generateButton.SetEnabled(false);
                        _generateButton.text = "Generating...";

                        try
                        {
                            if (mimeType == "pdf-images")
                            {
                                // Special case: download each image in data.images and attach them
                                EditorUtility.DisplayProgressBar("Importing Asset", $"Downloading PDF images: {title}", 0.3f);
                                var assetPaths = await DownloadPdfImages(assetName);

                                if (assetPaths != null && assetPaths.Count > 0)
                                {
                                    EditorUtility.DisplayProgressBar("Importing Asset", $"Creating GameObject: {assetName}", 0.7f);

                                    var go = new GameObject(assetName);
                                    Undo.RegisterCreatedObjectUndo(go, "Generate Archive Asset");
                                    go.transform.position = Vector3.zero;

                                    Debug.Log($"Generated GameObject for asset: {assetName}");

                                    EditorUtility.DisplayProgressBar("Importing Asset", $"Attaching {assetPaths.Count} images to GameObject", 0.85f);

                                    for (int a = 0; a < assetPaths.Count; a++)
                                    {
                                        var path = assetPaths[a];
                                        AttachAssetToGameObject(go, path, "image/jpeg", a);
                                    }

                                    EditorSceneManager.MarkSceneDirty(go.scene);
                                    Selection.activeGameObject = go;
                                    EditorGUIUtility.PingObject(go);
                                }
                                else
                                {
                                    Debug.LogWarning("No images were downloaded for PDF as Images option.");
                                }
                            }
                            else
                            {
                                EditorUtility.DisplayProgressBar("Importing Asset", $"Downloading: {title}", 0.3f);
                                string assetPath = await DownloadAttachment(assetName, downloadEndpoint, title, mimeType, index);

                                if (!string.IsNullOrEmpty(assetPath))
                                {
                                    EditorUtility.DisplayProgressBar("Importing Asset", $"Creating GameObject: {assetName}", 0.7f);

                                    // Create the GameObject with undo support
                                    var go = new GameObject(assetName);
                                    Undo.RegisterCreatedObjectUndo(go, "Generate Archive Asset");
                                    go.transform.position = Vector3.zero;

                                    Debug.Log($"Generated GameObject for asset: {assetName}");

                                    EditorUtility.DisplayProgressBar("Importing Asset", $"Attaching assets to GameObject", 0.85f);

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
                            }
                        }
                        finally
                        {
                            // Always clear the progress bar
                            EditorUtility.ClearProgressBar();
                            _generateButton.SetEnabled(true);
                            _generateButton.text = "Generate Asset";
                        }
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
                        // Attach API metadata to the imported asset using AssetImporter.userData
                        try
                        {
                            var metaJson = GetMetadataUserData();
                            var importer = AssetImporter.GetAtPath(assetPath);
                            if (importer != null)
                            {
                                importer.userData = metaJson ?? "";
                                UnityEditor.AssetDatabase.WriteImportSettingsIfDirty(assetPath);
                            }
                        }
                        catch (System.Exception exMeta)
                        {
                            Debug.LogWarning($"Failed to write userData for {assetPath}: {exMeta.Message}");
                        }
                                
                                // Also create a ScriptableObject next to the imported asset to hold metadata and source URL
                                try
                                {
                                    CreateAndSaveMetadataSO(assetPath, url);
                                }
                                catch (System.Exception exSo)
                                {
                                    Debug.LogWarning($"Failed to create metadata SO for {assetPath}: {exSo.Message}");
                                }
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

    // Download each image URL listed in `data.images` and save them into the asset folder
    private async System.Threading.Tasks.Task<List<string>> DownloadPdfImages(string assetName)
    {
        var savedPaths = new List<string>();
        try
        {
            if (_apiResponse.ValueKind == JsonValueKind.Undefined) return savedPaths;
            if (!_apiResponse.TryGetProperty("data", out var data)) return savedPaths;
            if (!data.TryGetProperty("images", out var images) || images.ValueKind != JsonValueKind.Array) return savedPaths;

            int count = images.GetArrayLength();
            if (count == 0) return savedPaths;

            int pad = 4; // zero-pad to 4 digits: 0001, 0002, ...
            string safeAssetName = SanitizeFileName(assetName);

            // Prepare folders
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

            using (var client = new System.Net.Http.HttpClient())
            {
                int idx = 0;
                foreach (var img in images.EnumerateArray())
                {
                    idx++;
                    string imageUrl = null;

                    // Primary: if the image entry is a string, use it
                    if (img.ValueKind == JsonValueKind.String)
                    {
                        imageUrl = img.GetString();
                    }
                    else if (img.ValueKind == JsonValueKind.Object)
                    {
                        // Preferred nested path: data.images[].images[0].resource.@id
                        if (img.TryGetProperty("images", out var innerImages) && innerImages.ValueKind == JsonValueKind.Array && innerImages.GetArrayLength() > 0)
                        {
                            var first = innerImages[0];
                            if (first.ValueKind == JsonValueKind.Object)
                            {
                                if (first.TryGetProperty("resource", out var resource) && resource.ValueKind == JsonValueKind.Object)
                                {
                                    if (resource.TryGetProperty("@id", out var idProp) && idProp.ValueKind == JsonValueKind.String)
                                    {
                                        imageUrl = idProp.GetString();
                                    }
                                }

                                // Fallback: first.images[0] may contain a direct "url" field
                                if (string.IsNullOrEmpty(imageUrl) && first.TryGetProperty("url", out var urlProp) && urlProp.ValueKind == JsonValueKind.String)
                                {
                                    imageUrl = urlProp.GetString();
                                }
                            }
                        }

                        // Secondary fallback: entry may have a top-level resource.@id
                        if (string.IsNullOrEmpty(imageUrl) && img.TryGetProperty("resource", out var topResource) && topResource.ValueKind == JsonValueKind.Object)
                        {
                            if (topResource.TryGetProperty("@id", out var topId) && topId.ValueKind == JsonValueKind.String)
                            {
                                imageUrl = topId.GetString();
                            }
                        }
                    }

                    if (string.IsNullOrEmpty(imageUrl))
                    {
                        Debug.LogWarning($"Skipping image entry #{idx} — no usable URL found in data.images entry.");
                        continue;
                    }

                    try
                    {
                        using (var response = await client.GetAsync(imageUrl))
                        {
                            response.EnsureSuccessStatusCode();
                            var bytes = await response.Content.ReadAsByteArrayAsync();
                            string actualMime = response.Content.Headers.ContentType?.MediaType ?? "image/jpeg";
                            string ext = GetExtensionFromMimeType(actualMime);

                            string filename = idx.ToString().PadLeft(pad, '0') + ext;
                            string assetPath = $"{parentFolderPath}/{filename}";
                            string tempPath = assetPath + ".download";

                            SafeWriteAllBytes(tempPath, bytes);
                            TryReplaceWithRetry(tempPath, assetPath, 10, 50);

                            if (System.IO.File.Exists(assetPath))
                            {
                                UnityEditor.AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
                                // Attach API metadata to the imported asset using AssetImporter.userData
                                try
                                {
                                    var metaJson = GetMetadataUserData();
                                    var importer = AssetImporter.GetAtPath(assetPath);
                                    if (importer != null)
                                    {
                                        importer.userData = metaJson ?? "";
                                        UnityEditor.AssetDatabase.WriteImportSettingsIfDirty(assetPath);
                                    }
                                }
                                catch (System.Exception exMeta)
                                {
                                    Debug.LogWarning($"Failed to write userData for {assetPath}: {exMeta.Message}");
                                }

                                savedPaths.Add(assetPath);
                                // Create ScriptableObject metadata next to the imported image
                                try
                                {
                                    CreateAndSaveMetadataSO(assetPath, imageUrl);
                                }
                                catch (System.Exception exSo)
                                {
                                    Debug.LogWarning($"Failed to create metadata SO for {assetPath}: {exSo.Message}");
                                }
                            }
                        }
                    }
                    catch (System.Exception exInner)
                    {
                        Debug.LogWarning($"Failed to download image {imageUrl}: {exInner.Message}");
                        // continue with remaining images
                    }
                }
            }

            return savedPaths;
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Error while downloading PDF images: {ex.Message}");
            return savedPaths;
        }
    }

    // Extract metadata JSON from the stored API response. Prefer `data.meta` when available.
    private string GetMetadataUserData()
    {
        try
        {
            if (_apiResponse.ValueKind != JsonValueKind.Undefined && _apiResponse.TryGetProperty("data", out var data))
            {
                if (data.TryGetProperty("meta", out var meta))
                {
                    return meta.GetRawText();
                }
                // If no meta object, store the whole `data` block
                return data.GetRawText();
            }

            if (_apiResponse.ValueKind != JsonValueKind.Undefined)
            {
                return _apiResponse.GetRawText();
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"Failed to extract metadata for userData: {ex.Message}");
        }

        return "";
    }

    // Build a userData JSON string that includes the original source URL plus the metadata
    private string BuildUserData(string sourceUrl)
    {
        try
        {
            var metaJson = GetMetadataUserData();
            // Serialize sourceUrl safely
            var srcJson = System.Text.Json.JsonSerializer.Serialize(sourceUrl ?? "");

            if (string.IsNullOrEmpty(metaJson))
            {
                return "{\"sourceUrl\":" + srcJson + "}";
            }

            // metaJson is already a JSON object or value; include it under the "meta" key
            return "{\"sourceUrl\":" + srcJson + ",\"meta\":" + metaJson + "}";
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"Failed to build userData JSON: {ex.Message}");
            return "";
        }
    }

    // Create a ScriptableObject asset next to the imported file that stores source URL and metadata
    private void CreateAndSaveMetadataSO(string assetPath, string sourceUrl)
    {
        try
        {
            if (string.IsNullOrEmpty(assetPath))
            {
                Debug.LogWarning("CreateAndSaveMetadataSO: assetPath is empty");
                return;
            }

            var metaJson = GetMetadataUserData();
            Debug.Log($"CreateAndSaveMetadataSO: Creating SO for {assetPath}, metadata length: {metaJson?.Length ?? 0}");

            var so = ScriptableObject.CreateInstance<ArchiveImportedAssetData>();
            so.SourceUrl = sourceUrl ?? "";
            so.MetadataJson = metaJson ?? "";
            so.OriginalAssetPath = assetPath;

            string soPath = System.IO.Path.ChangeExtension(assetPath, ".asset");
            Debug.Log($"CreateAndSaveMetadataSO: SO path will be: {soPath}");

            // If an existing SO exists, overwrite its fields and save
            var existing = UnityEditor.AssetDatabase.LoadAssetAtPath<ArchiveImportedAssetData>(soPath);
            if (existing != null)
            {
                Debug.Log($"CreateAndSaveMetadataSO: Updating existing SO at {soPath}");
                existing.SourceUrl = so.SourceUrl;
                existing.MetadataJson = so.MetadataJson;
                existing.OriginalAssetPath = so.OriginalAssetPath;
                existing.ParseMetadata();
                UnityEditor.EditorUtility.SetDirty(existing);
                UnityEditor.AssetDatabase.SaveAssets();
            }
            else
            {
                Debug.Log($"CreateAndSaveMetadataSO: Creating new SO at {soPath}");
                so.ParseMetadata();
                UnityEditor.AssetDatabase.CreateAsset(so, soPath);
                UnityEditor.AssetDatabase.SaveAssets();
            }

            UnityEditor.AssetDatabase.ImportAsset(soPath, ImportAssetOptions.ForceUpdate);
            Debug.Log($"CreateAndSaveMetadataSO: Successfully saved SO at {soPath}");
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"CreateAndSaveMetadataSO error for {assetPath}: {ex.Message}\n{ex.StackTrace}");
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

        // Always attach metadata SO for all asset types
        try
        {
            string soPath = System.IO.Path.ChangeExtension(assetPath, ".asset");
            if (System.IO.File.Exists(soPath))
            {
                var so = UnityEditor.AssetDatabase.LoadAssetAtPath<ArchiveImportedAssetData>(soPath);
                if (so != null)
                {
                    var assetRef = parent.GetComponent<ArchiveAssetReference>() ?? parent.AddComponent<ArchiveAssetReference>();
                    Undo.RecordObject(assetRef, "Add Asset Reference");
                    assetRef.attachments.Add(new ArchiveAssetReference.AssetReference
                    {
                        assetPath = soPath,
                        assetType = ".asset",
                        assetObject = so
                    });
                    EditorUtility.SetDirty(assetRef);
                    Debug.Log($"Attached metadata SO for {assetPath}");
                }
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"Failed to attach metadata SO for {assetPath}: {ex.Message}");
        }

        // Skip PDF files - they don't create GameObjects
        if (extension == ".pdf")
        {
            Debug.Log($"PDF file imported: {assetPath} (no GameObject created)");
            return;
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
            
            // Focus camera on the created image
            Selection.activeGameObject = imageGO;
            SceneView.FrameLastActiveSceneView();
        }
        else if (asset is VideoClip videoClip)
        {
            // Create Canvas GameObject
            var canvasGO = new GameObject($"Video: {filename}");
            canvasGO.transform.SetParent(parent.transform);
            canvasGO.transform.localPosition = Vector3.zero;
            
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            
            var canvasScaler = canvasGO.AddComponent<CanvasScaler>();
            canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            
            // Create RawImage within the canvas
            var rawImageGO = new GameObject("RawImage");
            rawImageGO.transform.SetParent(canvasGO.transform);
            rawImageGO.transform.localPosition = Vector3.zero;
            
            var rawImage = rawImageGO.AddComponent<RawImage>();
            var rectTransform = rawImageGO.GetComponent<RectTransform>();
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
            
            // Create VideoPlayer on the canvas
            var videoPlayer = canvasGO.AddComponent<UnityEngine.Video.VideoPlayer>();
            videoPlayer.clip = videoClip;
            videoPlayer.playOnAwake = true;
            videoPlayer.targetTexture = new RenderTexture(1920, 1080, 24);
            rawImage.texture = videoPlayer.targetTexture;
            
            Undo.RegisterCreatedObjectUndo(canvasGO, "Add Video Attachment");
            Debug.Log($"Attached video as VideoPlayer with Canvas: {filename}");
            
            // Focus camera on the created video canvas
            Selection.activeGameObject = canvasGO;
            SceneView.FrameLastActiveSceneView();
        }
        else if (asset is AudioClip audioClip)
        {
            var audioSource = parent.AddComponent<AudioSource>();
            audioSource.clip = audioClip;
            audioSource.playOnAwake = false;
            Debug.Log($"Attached audio as AudioSource: {filename} (length: {audioClip.length}s)");
            
            // Focus camera on the parent GameObject (audio has no visual child)
            Selection.activeGameObject = parent;
            SceneView.FrameLastActiveSceneView();
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
            
            // Focus camera on the parent GameObject
            Selection.activeGameObject = parent;
            SceneView.FrameLastActiveSceneView();
        }
    }

    [MenuItem("Assets/Generate Flipbook", false, 20)]
    private static void GenerateFlipbookMenuItem()
    {
        EditorUtility.DisplayDialog("Generate Flipbook", "Flipbook generation started!", "OK");
    }

    [MenuItem("Assets/Generate Flipbook", true)]
    private static bool ValidateGenerateFlipbook()
    {
        // Only show menu item if a PDF file is selected
        if (Selection.activeObject == null) return false;
        
        string path = AssetDatabase.GetAssetPath(Selection.activeObject);
        return !string.IsNullOrEmpty(path) && path.EndsWith(".pdf", System.StringComparison.OrdinalIgnoreCase);
    }
}
