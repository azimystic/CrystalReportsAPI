# Crystal Reports COM Export Error Troubleshooting

## Error Description
```
"The system cannot find the path specified." during reportDocument.ExportToDisk()
```

This is a COM exception that occurs when Crystal Reports cannot write to the temp file location.

## Fixes Implemented

### 1. **Dual Temp Path Strategy**
The API now tries two locations for temp files:

**Primary:** System temp folder
```
C:\Users\[Username]\AppData\Local\Temp\
```

**Fallback:** Application temp folder
```
C:\...\CrystalReportsAPI\App_Data\Temp\
```

### 2. **Enhanced Error Messages**
Detailed error logging shows:
- Original error message
- Retry error message (if fallback fails)
- Both temp paths that were attempted
- Temp file path length (Windows MAX_PATH = 260 chars)

### 3. **Automatic Directory Creation**
The `App_Data/Temp` directory is automatically created if it doesn't exist.

## Common Causes & Solutions

### Cause 1: Temp Folder Permissions
**Symptom:** COM exception on ExportToDisk
**Solution:** The fallback to `App_Data/Temp` should resolve this automatically

### Cause 2: Antivirus Blocking
**Symptom:** Intermittent failures
**Solution:** Add exclusions for:
- `C:\Users\[Username]\AppData\Local\Temp\`
- `C:\...\CrystalReportsAPI\App_Data\Temp\`

### Cause 3: Disk Space
**Symptom:** Export fails for large reports
**Solution:** Ensure adequate disk space (check both locations)

### Cause 4: Long File Paths
**Symptom:** Path too long exception
**Solution:** The code now checks for paths > 260 characters

### Cause 5: Crystal Reports Runtime Not Properly Installed
**Symptom:** COM exceptions
**Solution:** 
1. Install "SAP Crystal Reports runtime engine for .NET Framework"
2. Match the bitness (32-bit vs 64-bit) with your IIS app pool
3. Restart IIS after installation

## Debug Checklist

When the error occurs, check the Debug Output for:

```
? Report file size: XXXX bytes
? Report loaded: [path]
? Report name: [name]
? Temp directory: [path]
? Temp file path (without extension): [path]
? Exporting to PDF: [full path]

If primary export fails:
? ExportToDisk failed: [error message]
? Retrying with alternative path: [App_Data path]
? Export successful with alternative path
```

## Manual Testing

### Test System Temp Folder
```powershell
# Check if temp folder exists and is writable
$tempPath = [System.IO.Path]::GetTempPath()
Write-Output "Temp path: $tempPath"
Test-Path $tempPath -PathType Container

# Try to create a test file
$testFile = Join-Path $tempPath "test_$(Get-Random).txt"
"test" | Out-File $testFile
Remove-Item $testFile
Write-Output "Temp folder is writable"
```

### Test App Temp Folder
```powershell
# Check App_Data/Temp
$appTemp = "C:\Users\Azeem\source\repos\azimystic\CrystalReportsAPI\CrystalReportsAPI\App_Data\Temp"
Test-Path $appTemp -PathType Container
Get-ChildItem $appTemp
```

## IIS Configuration

If running under IIS, ensure the application pool identity has permissions:

### Option 1: Grant Permissions to App Pool Identity
```powershell
# For ApplicationPoolIdentity
$acl = Get-Acl "C:\...\App_Data\Temp"
$rule = New-Object System.Security.AccessControl.FileSystemAccessRule(
    "IIS AppPool\YourAppPoolName",
    "Modify",
    "ContainerInherit,ObjectInherit",
    "None",
    "Allow"
)
$acl.SetAccessRule($rule)
Set-Acl "C:\...\App_Data\Temp" $acl
```

### Option 2: Use a Custom Account
Change the app pool identity to a specific account with appropriate permissions.

## API Response

### Success
```json
HTTP 200 OK
Content-Type: application/pdf
Content-Disposition: attachment; filename=OutPatient.pdf
Content-Length: [bytes]

[PDF binary data]
```

### Failure (Before Fix)
```json
HTTP 500 Internal Server Error
{
  "Message": "An error occurred: The system cannot find the path specified. Report Path: [path]"
}
```

### Failure (After Fix with Details)
```json
HTTP 500 Internal Server Error
{
  "Message": "Failed to export report to disk. Original error: The system cannot find the path specified. Retry error: Access denied. Temp paths tried: C:\\Users\\...\\Temp and C:\\...\\App_Data\\Temp"
}
```

## Prevention

### Development
- Test exports in debug mode to see full error details
- Check Output window for diagnostic messages
- Verify Crystal Reports runtime is installed

### Production
- Monitor `App_Data/Temp` folder size
- Implement cleanup job for old temp files (API already deletes after export)
- Set up alerts for disk space on server
- Configure antivirus exclusions

## Related Files
- `ReportsController.cs` - Contains the export logic
- `App_Data/Temp/` - Fallback temp directory
- `Web.config` - Connection string and app settings
