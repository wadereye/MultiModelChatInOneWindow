# MultiModelChat Release Notes - Version 1.4 (Windows 10+)

## Overview
This release focuses on fixing critical layout bugs in the expand/collapse functionality for the three AI model views. This version is optimized for Windows 10 and later versions using WebView2.

## Bug Fixes
1. **Fixed Browser Layout Issues**:
   - Fixed issue where Doubao and DeepSeek browsers didn't resize correctly when expanded
   - Added proper Dock property reset before layout changes to prevent layout conflicts
   - Improved layout refresh mechanism with SuspendLayout/ResumeLayout pattern
2. **Enhanced Window Resize Handling**:
   - Browser controls now correctly follow window size changes in expanded state
   - Added PerformLayout() calls to force immediate layout updates
3. **Button State Management**:
   - Improved expand/collapse button text updates
   - All buttons now reset properly when switching between different expanded states

## Improvements
1. **Layout Optimization**:
   - Eliminated layout flickering during expand/collapse transitions
   - Better layout suspension and resumption for smoother UI updates
2. **Code Quality**:
   - Refactored expand/collapse logic for better maintainability
   - Added comprehensive layout reset logic

## Installation

Download the appropriate version for your system from the GitHub Releases page:
- Windows x64 version (for Windows 10+)
- Windows x86 version (for Windows 10+)
- Windows ARM64 version (for Windows 10+)

Extract all files to a folder of your choice and run MultiModelChat.exe to start the application.

## System Requirements
- **Windows 10 or higher** (WebView2 based)
- .NET 6.0 Runtime (will be automatically downloaded if needed)
- Microsoft Edge WebView2 Runtime (automatically installed on Windows 10+)

## Usage
1. After starting the application, the window will automatically display in full screen
2. Enter your query question in the top input box
3. Click the "Query" button or press Enter to send the question to all three models
4. Use the expand/collapse buttons to focus on a specific model's response
5. Click the "Clear" button to clear the input text box
6. Click the "Config" button to customize the URLs for each WebView

## Notes
- This version uses WebView2 and requires Windows 10 or higher
- For Windows 7 support, please use the separate Windows 7 compatible version
- Due to the security policies of various websites, the injection effect may change with website updates
- Do not manually operate the keyboard while the program is running to avoid interfering with the automation process
- Some antivirus software may block keyboard simulation operations, please add the program to the trusted list

## Version History
- **V1.4 (Windows 10+)** - Fixed layout issues in expand/collapse functionality (WebView2)
- **V1.4 (Windows 7+)** - Same fixes for Windows 7 compatible version (CefSharp)
- **V1.2** - Multi-architecture support and custom icons
