# Distribution Guide

## Package Distribution Methods

### Method 1: Unity Package Manager (Local)

Users can install directly from their local copy:

1. In Unity, open **Window > Package Manager**
2. Click **+** > **Add package from disk...**
3. Navigate to and select `package.json` in this folder
4. Click **Open**

### Method 2: Git Repository

1. Upload this package to a Git repository (GitHub, GitLab, etc.)
2. Users can install via **Package Manager**:
   - Click **+** > **Add package from git URL...**
   - Enter: `https://github.com/yourusername/archives-access.git`

### Method 3: Traditional .unitypackage

To create a `.unitypackage` file for distribution:

1. In Unity, right-click the package folder in the Project window
2. Select **Export Package...**
3. Ensure all files are selected
4. Click **Export** and save the `.unitypackage` file
5. Share this file with users

Users install by:
1. Double-clicking the `.unitypackage` file, or
2. In Unity: **Assets > Import Package > Custom Package...**

### Method 4: Unity Asset Store

For wider distribution:

1. Create an Asset Store Publisher account
2. Follow Unity's Asset Store submission guidelines
3. Upload your package with screenshots and documentation

## What to Customize Before Distribution

1. **package.json**: Update author name, email, URL, and package name
2. **LICENSE.md**: Update copyright holder name
3. **README.md**: Add your contact information and support details
4. **API Configuration**: Document how to change the API endpoint

## Package Name Convention

Current: `com.yourcompany.archivesaccess`

Format: `com.[company].[package-name]`

Examples:
- `com.johndoe.archivesaccess`
- `com.archivetools.metadataviewer`
- `com.mycompany.digitalarchives`

Update this in `package.json` and the folder name for consistency.
