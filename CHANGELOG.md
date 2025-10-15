# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.0] - 2025-10-15

### Added
- Initial release
- Archives Access editor window accessible via Tools menu
- Metadata fetching from configurable API endpoint
- Two-column table display (Property Name | Value)
- Support for JSON array values with multi-line display
- Text wrapping for long content in table cells
- Table cell padding for better readability
- Alternating row backgrounds
- Loading state indicator
- Error handling and display
- Newtonsoft.Json integration for JSON parsing
- Assembly definition with proper dependencies

### Features
- Fetch metadata from digital archives collections via HTTP API
- Display property names and values in a sortable table
- Loading and error states with user feedback
- Configurable asset URL input field with placeholder
- Async/await API calls with proper threading

### Technical Details
- Unity 2021.3+ compatible
- Uses UIElements/UI Toolkit for modern editor UI
- MultiColumnListView for efficient table rendering
- HttpClient for async HTTP requests
