using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public class ArchivesAccess : EditorWindow
{
    public const string titleContentText = "Archives Access";
    MultiColumnListView _table;
    List<MetadataItem> _metadataItems = new List<MetadataItem>();

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

        splitView.Add(_table);

        TextField assetURL = new TextField("Asset URL");
        assetURL.textEdition.placeholder = "https://collections.ctdigitalarchive.org/node/1686854";
        assetURL.value = "https://collections.ctdigitalarchive.org/node/1686854";
        assetURL.style.marginBottom = 10;
        assetURL.textEdition.hidePlaceholderOnFocus = true;

        Button downloadButton = new Button(() =>
        {
            Debug.Log("Button pressed!");
            string apiUrl = "http://127.0.0.1:8000/api/parse?url=" + assetURL.value.Trim();
            Debug.Log("Fetching data from API: " + apiUrl);
            var fetchTask = FetchDataFromAPI(apiUrl);
            
            _metadataItems.Clear();
            _metadataItems.Add(new MetadataItem { PropertyName = "Loading...", PropertyValue = "" });
            _table.RefreshItems();
            
            fetchTask.ContinueWith(task =>  
            {
                if (task.Result != null)
                {
                    Debug.Log("Data fetched from API: " + task.Result);

                    _metadataItems.Clear();
                    
                    // Parse the JSON response
                    var jsonResponse = Newtonsoft.Json.JsonConvert.DeserializeObject<dynamic>(task.Result);
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
                }
                else
                {
                    Debug.LogError("Failed to fetch data from API.");
                    _metadataItems.Clear();
                    _metadataItems.Add(new MetadataItem { PropertyName = "Error", PropertyValue = "Failed to fetch data from API." });
                    _table.RefreshItems();
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
}
