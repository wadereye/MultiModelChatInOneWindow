# MultiModelChat Release Notes - Version 1.2

## Overview
This release includes several enhancements and new features to improve the user experience of the Multi-Model Q&A System, including support for multiple architectures and custom icons.

## New Features
1. **Multi-architecture Support**:
   - Windows x64 version
   - Windows x86 version
   - Windows ARM64 version
2. **Custom Application Icon**:
   - Added favicon.ico as the application icon
   - All forms now use the custom icon
3. **Enhanced Configuration**:
   - Improved configuration form with better validation
   - Better error handling for URL validation

## Improvements
1. **Self-contained Executables**:
   - Standalone executables that don't require .NET installation
   - Optimized single-file bundles for easier distribution
2. **UI Enhancements**:
   - Consistent icon usage across all forms
   - Better form initialization with icon loading

## Bug Fixes
1. Fixed icon loading issues
2. Improved error handling in configuration form
3. Enhanced URL validation in configuration

## Installation
Choose the appropriate version for your system:

### Windows x64
1. Download MultiModelChat_V1.2_win-x64.zip
2. Extract all files to a folder of your choice
3. Run MultiModelChat.exe to start the application

### Windows x86
1. Download MultiModelChat_V1.2_win-x86.zip
2. Extract all files to a folder of your choice
3. Run MultiModelChat.exe to start the application

### Windows ARM64
1. Download MultiModelChat_V1.2_win-arm64.zip
2. Extract all files to a folder of your choice
3. Run MultiModelChat.exe to start the application

## System Requirements
- Windows 10 or higher
- No additional runtime requirements (self-contained package)

## Usage
1. After starting the application, the window will automatically display in full screen
2. Enter your query question in the top input box
3. Click the "Query" button or press Enter to send the question to all three models
4. Use the expand/collapse buttons to focus on a specific model's response
5. Click the "Clear" button to clear the input text box
6. Click the "Config" button to customize the URLs for each WebView

## Notes
- On first run, WebView2 may need to download runtime components
- Due to the security policies of various websites, the injection effect may change with website updates
- Do not manually operate the keyboard while the program is running to avoid interfering with the automation process
- Some antivirus software may block keyboard simulation operations, please add the program to the trusted list