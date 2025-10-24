using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.Video;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public class ArchivesAccess : EditorWindow
{
    public const string titleContentText = "Archives Access";
    MultiColumnListView _table;
    List<MetadataItem> _metadataItems = new List<MetadataItem>();
    Button _generateButton;
    dynamic _apiResponse; // Store full API response including attachments

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

        TextField assetURL = new TextField("Asset URL");
        assetURL.textEdition.placeholder = "https://collections.ctdigitalarchive.org/node/1686854";
        assetURL.value = "https://collections.ctdigitalarchive.org/node/947297";
        assetURL.style.marginBottom = 10;
        assetURL.textEdition.hidePlaceholderOnFocus = true;

        Button downloadButton = new Button(() =>
        {
            Debug.Log("Button pressed!");
            string fetchEndpoint = apiUrl + "parse?url=" + assetURL.value.Trim();
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
                    
                    // Parse the JSON response
                    var jsonResponse = Newtonsoft.Json.JsonConvert.DeserializeObject<dynamic>(task.Result);
                    _apiResponse = jsonResponse; // Store for later use in GenerateAsset
                    if (jsonResponse?.data?.meta != null)
                    {
                        var metaData = jsonResponse.data.meta;
                        foreach (JProperty property in metaData)
                        {
                            var val = "";
                            if (property.Value is JArray array)
                            {
                                foreach (var item in array)
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
        topPane.Add(assetURL);
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

    private async void GenerateAsset()
    {
        // Disable the button to prevent multiple clicks
        _generateButton.SetEnabled(false);
        _generateButton.text = "Generating...";

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

            // Store downloaded asset info
            var downloadedAssets = new System.Collections.Generic.List<(string path, string mimeType, int index)>();

            // Process attachments if available - download first, create GameObject after
        if (_apiResponse?.data?.attachments != null)
        {
            var attachments = _apiResponse.data.attachments as JArray;
            if (attachments != null && attachments.Count > 0)
            {
                Debug.Log($"Found {attachments.Count} attachment(s), downloading...");
                
                for (int i = 0; i < attachments.Count; i++)
                {
                    var attachment = attachments[i];
                    string url = attachment["url"]?.ToString();
                    string title = attachment["title"]?.ToString() ?? $"Attachment_{i}";
                    var typeArray = attachment["type"] as JArray;
                    string mimeType = typeArray != null && typeArray.Count > 0 
                        ? string.Join("/", typeArray) 
                        : "application/octet-stream";

                    if (!string.IsNullOrEmpty(url))
                    {
                        string downloadEndpoint = apiUrl + "download?url=" + url;
                        string assetPath = await DownloadAttachment(name, downloadEndpoint, title, mimeType, i);
                        
                        if (!string.IsNullOrEmpty(assetPath))
                        {
                            downloadedAssets.Add((assetPath, mimeType, i));
                        }
                    }
                }
            }
        }

        // Only create GameObject if we have downloaded assets
        if (downloadedAssets.Count > 0)
        {
            // Create the GameObject with undo support
            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Generate Archive Asset");
            go.transform.position = Vector3.zero;
            
            Debug.Log($"Generated GameObject for asset: {name}");
            
            // Attach all downloaded assets to the GameObject
            foreach (var (assetPath, mimeType, index) in downloadedAssets)
            {
                AttachAssetToGameObject(go, assetPath, mimeType, index);
            }
            
            // Mark the scene as dirty so the object persists
            EditorSceneManager.MarkSceneDirty(go.scene);
            
            // Select and focus on the new GameObject
            Selection.activeGameObject = go;
            EditorGUIUtility.PingObject(go);
        }
        else
        {
            Debug.LogWarning($"No assets were downloaded, GameObject not created.");
        }
            }
            finally
            {
                // Re-enable the button when done (success or failure)
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

                    // Try to get MIME type from response header if the provided one doesn't work
                    string actualMimeType = mimeType;
                    if (response.Content.Headers.ContentType != null)
                    {
                        string headerMimeType = response.Content.Headers.ContentType.MediaType;
                        Debug.Log($"MIME type from API: {mimeType}, from response header: {headerMimeType}");
                        
                        // Use header MIME type if the provided one would result in .dat
                        string testExt = GetExtensionFromMimeType(mimeType);
                        if (testExt == ".dat" && !string.IsNullOrEmpty(headerMimeType))
                        {
                            actualMimeType = headerMimeType;
                            Debug.Log($"Using MIME type from header: {headerMimeType}");
                        }
                    }

                    // Determine file extension from MIME type
                    string extension = GetExtensionFromMimeType(actualMimeType);
                    Debug.Log($"Final MIME type: {actualMimeType}, extension: {extension}");
                    
                    // Clean title for filename
                    string safeTitle = System.IO.Path.GetInvalidFileNameChars()
                        .Aggregate(title, (current, c) => current.Replace(c, '_'));
                    
                    // Create Assets folder structure
                    string folderPath = "Assets/ArchiveAssets";
                    if (!UnityEditor.AssetDatabase.IsValidFolder(folderPath))
                    {
                        UnityEditor.AssetDatabase.CreateFolder("Assets", "ArchiveAssets");
                    }

                    string parentFolderName = assetName.Replace(" ", "_");
                    string parentFolderPath = $"{folderPath}/{parentFolderName}";
                    if (!UnityEditor.AssetDatabase.IsValidFolder(parentFolderPath))
                    {
                        UnityEditor.AssetDatabase.CreateFolder(folderPath, parentFolderName);
                    }

                    string filename = $"{safeTitle}_{index}{extension}";
                    string assetPath = $"{parentFolderPath}/{filename}";
                    string tempPath = assetPath + ".download"; // temp path to avoid importer races

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
            System.IO.Directory.CreateDirectory(dir);
        }
        
        // Write with retry in case of transient locks
        int attempts = 10;
        for (int i = 0; i < attempts; i++)
        {
            try
            {
                System.IO.File.WriteAllBytes(path, bytes);
                return;
            }
            catch (System.IO.IOException)
            {
                if (i == attempts - 1) throw;
                System.Threading.Thread.Sleep(50);
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
            { "video/mp4", ".mp4" },
            { "video/mpeg", ".mpeg" },
            { "video/quicktime", ".mov" },
            { "video/x-msvideo", ".avi" },
            { "video/webm", ".webm" },
            { "audio/mpeg", ".mp3" },
            { "audio/wav", ".wav" },
            { "audio/ogg", ".ogg" },
            { "text/plain", ".txt" },
            { "text/html", ".html" },
            { "application/json", ".json" }
        };

        return mimeMap.TryGetValue(mimeType.ToLower(), out string ext) ? ext : ".dat";
    }

    private void AttachAssetToGameObject(GameObject parent, string assetPath, string mimeType, int index)
    {
        var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
        
        if (asset == null)
        {
            Debug.LogWarning($"Could not load asset at {assetPath}");
            return;
        }

        // Extract filename without extension for display
        string filename = System.IO.Path.GetFileNameWithoutExtension(assetPath);

        // Handle different asset types
        if (asset is Texture2D texture)
        {
            // Create a child GameObject with a SpriteRenderer or UI Image
            var imageGO = new GameObject($"Image: {filename}");
            imageGO.transform.SetParent(parent.transform);
            imageGO.transform.localPosition = Vector3.zero;
            
            var spriteRenderer = imageGO.AddComponent<SpriteRenderer>();
            var sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
            spriteRenderer.sprite = sprite;
            
            Undo.RegisterCreatedObjectUndo(imageGO, "Add Image Attachment");
            Debug.Log($"Attached image as SpriteRenderer: {filename}");
        }
        else if (asset is VideoClip videoClip)
        {
            // Create a child GameObject with VideoPlayer
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
            // Add AudioSource to parent
            var audioSource = parent.AddComponent<AudioSource>();
            audioSource.clip = audioClip;
            audioSource.playOnAwake = false;
            
            Debug.Log($"Attached audio as AudioSource: {filename}");
        }
        else
        {
            // For other types (PDF, text, etc.), just log the reference
            Debug.Log($"Attachment saved but not automatically attached to scene (type: {asset.GetType().Name}): {assetPath}");
        }
    }
}
