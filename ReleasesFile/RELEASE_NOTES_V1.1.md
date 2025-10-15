# MultiModelChat Release Notes - Version 1.1

## Overview
This release includes several enhancements and new features to improve the user experience of the Multi-Model Q&A System.

## New Features
1. Enhanced UI with expand/collapse buttons for each WebView panel
2. Dynamic button text that changes between "Expand" and "Collapse" based on the current state
3. Improved input box sizing that dynamically adjusts to 80% of the window width
4. Added "Clear" button to easily clear the input text box
5. Better layout management that adapts to window resizing

## Improvements
1. Fixed issue where expand buttons were being obscured by WebView panels
2. Moved expand buttons to the top panel for better visibility and accessibility
3. Enhanced keyboard simulation with additional delays for more reliable operation
4. Improved window resizing behavior for all UI elements

## Bug Fixes
1. Resolved expand/collapse button visibility issues
2. Fixed layout issues when resizing the application window
3. Improved reliability of automatic question submission to all models

## Installation
1. Download the MultiModelChat_V1.1.zip file
2. Extract all files to a folder of your choice
3. Run MultiModelChat.exe to start the application
4. Note: You will need to log in to each service (Tongyi Qianwen, Doubao, and DeepSeek) manually before using the application

## System Requirements
- Windows 10 or higher
- .NET 6.0 Runtime
- Microsoft Edge WebView2 Runtime (usually installed automatically with Windows updates)

## Usage
1. After starting the application, the window will automatically display in full screen
2. Enter your query question in the top input box
3. Click the "Query" button or press Enter to send the question to all three models
4. Use the expand/collapse buttons to focus on a specific model's response
5. Click the "Clear" button to clear the input text box

## Notes
- On first run, WebView2 may need to download runtime components
- Due to the security policies of various websites, the injection effect may change with website updates
- Do not manually operate the keyboard while the program is running to avoid interfering with the automation process
- Some antivirus software may block keyboard simulation operations, please add the program to the trusted list