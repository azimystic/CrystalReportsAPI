# OutPatient Report Documentation

## Overview
The OutPatient report generates patient details using the `rptOutpatientDetails.rpt` report file located in the `Reports/Patients/` directory.

## Request Format

### Endpoint
```
POST /api/reports/generate
```

### Headers
```
Content-Type: application/json
X-API-Key: your-api-key
```

### Request Body
```json
{
  "ReportName": "OutPatient",
  "ReportTitle": "Out Patient Report",
  "ReportColumn": 0,
  "TestIDs": "",
  "PatientID": "",
  "TransID": "",
  "BillDetailID": "",
  "LabTestDetailID": "",
  "IncludeLetterhead": false,
  "ExportToExcel": false,
  "Parameters": [
    {
      "Name": "outPatientID",
      "Value": "332313"
    }
  ]
}
```

## Required Parameters

| Parameter | Location | Type | Required | Description |
|-----------|----------|------|----------|-------------|
| `outPatientID` | Parameters array | string/int | Yes | The patient ID to generate report for |

## Report Features

### Main Report Parameters
- `@outPatient` - Patient ID (set on main report)
- `CompanyName` - Company/Hospital name (from AppSettings or default)
- `CompanyLogo` - Company logo path (from database function `GetCompanyLogo()` or AppSettings)

### Subreport Parameters
The report automatically sets the `@outPatient` parameter for all subreports (up to 4 subreports).

## Configuration

### Web.config Settings
```xml
<appSettings>
  <add key="CompanyName" value="Your Hospital Name" />
  <add key="CompanyLogoPath" value="~/Images/logo.png" />
</appSettings>
```

### Database Function (Optional)
If you have a database function to retrieve the company logo:
```sql
CREATE FUNCTION dbo.GetCompanyLogo()
RETURNS VARCHAR(255)
AS
BEGIN
    RETURN (SELECT LogoPath FROM Sys_Companys WHERE Company_ID = @CompanyID)
END
```

## Example cURL Request
```bash
curl -X POST \
  --header 'Content-Type: application/json' \
  --header 'X-API-Key: 12345' \
  -d '{
    "ReportName": "OutPatient",
    "ReportTitle": "Out Patient Report",
    "Parameters": [
      {
        "Name": "outPatientID",
        "Value": "332313"
      }
    ]
  }' \
  --output outpatient_report.pdf \
  'http://your-server:port/api/reports/generate'
```

## Error Handling

### Missing outPatientID
```json
{
  "Message": "outPatientID parameter is required for OutPatient report"
}
```

### Invalid outPatientID Format
```json
{
  "Message": "outPatientID must be a valid integer: abc"
}
```

### Report File Not Found
```json
{
  "Message": "Report file was not found at: C:\\...\\Reports\\Patients\\rptOutpatientDetails.rpt"
}
```

## Debug Logging

Enable debug output to see parameter processing:
```
=== Setting OutPatient Report Parameters ===
Parameters count: 1
Found outPatientID: 332313
Setting @outPatient: 332313
Setting CompanyName: Hospital Name
Setting CompanyLogo: ~/Images/logo.png
Total subreports: 4
Setting @outPatient for subreport[0]: Subreport1
Setting @outPatient for subreport[1]: Subreport2
Setting @outPatient for subreport[2]: Subreport3
Setting @outPatient for subreport[3]: Subreport4
=== OutPatient parameter setting complete ===
```

## File Structure
```
Reports/
??? Patients/
    ??? rptOutpatientDetails.rpt (main report with 4 subreports)
```
