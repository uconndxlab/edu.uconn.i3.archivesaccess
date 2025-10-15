# Archives Access Plugin

A Unity Editor plugin for accessing and displaying metadata from digital archives collections.

## Features

- Fetch metadata from digital archives via API
- Display metadata in a clean two-column table format
- Easy-to-use editor window interface

## Installation

### Option 1: Unity Package Manager (Recommended)

1. In Unity, open **Window > Package Manager**
2. Click the **+** button in the top-left corner
3. Select **Add package from disk...**
4. Navigate to and select the `package.json` file in this folder

### Option 2: Manual Installation

1. Copy the entire package folder to your project's `Packages` directory
2. Unity will automatically import the plugin

### Option 3: Install from Git

If hosting on Git:
```
https://github.com/yourusername/archives-access.git
```

## Usage

1. Open the Archives Access window: **Tools > Archives Access**
2. Enter the URL of an archive asset in the text field
3. Click **Fetch Asset** to retrieve and display metadata
4. The metadata will appear in a table with property names and values

## Requirements

- Unity 2021.3 or later
- Newtonsoft.Json (automatically included as dependency)

## API Configuration

The plugin currently connects to `http://127.0.0.1:8000/api/parse?url=` by default. You can modify the API endpoint in `ArchivesAccess.cs` if needed.

## Dependencies

- **Newtonsoft.Json (3.2.1)** - For JSON parsing

## Package Structure

```
com.yourcompany.archivesaccess/
├── package.json           # Package manifest
├── README.md             # This file
├── CHANGELOG.md          # Version history
├── LICENSE.md            # License information
└── Editor/
    ├── ArchivesAccess.cs     # Main editor window script
    └── Editor.asmdef         # Assembly definition
```

## License

[Your License Here - See LICENSE.md]

## Support

For issues or questions, please contact [your contact information].
