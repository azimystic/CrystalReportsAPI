# Reports Folder

This folder is intended to contain Crystal Report (.rpt) files.

Place your .rpt report files in this directory. The API will look for report files here when generating reports.

## Prerequisites

**Crystal Reports Runtime Installation Required:**

Before using this API, you must install the SAP Crystal Reports Runtime for .NET Framework on the server. Download it from the SAP website:
- SAP Crystal Reports for Visual Studio (SP 34 or later recommended)
- Install the runtime that matches your application's bitness (x86 or x64)

## Usage

When calling the `/api/reports/generate` endpoint, specify the report name without the `.rpt` extension.

Reports can be organized in subdirectories. Use forward slashes to specify the path relative to the Reports folder.

Examples:
- For a file named `Invoice.rpt` in the Reports folder, use `"ReportName": "Invoice"`
- For a file at `Reports/LabRadiology/rptLabTestUiltraSound_1.rpt`, use `"ReportName": "LabRadiology/rptLabTestUiltraSound_1"`

### Example Requests

```json
POST /api/reports/generate
X-API-Key: YOUR-API-KEY

{
    "ReportName": "Invoice",
    "Parameters": [
        { "Name": "InvoiceId", "Value": "12345" }
    ]
}
```

```json
POST /api/reports/generate
X-API-Key: YOUR-API-KEY

{
    "ReportName": "LabRadiology/rptLabTestUiltraSound_1",
    "Parameters": [
        { "Name": "test_wise_ids", "Value": "112" }
    ]
}
```

## Security Note

Ensure that sensitive report files are properly secured and that access to this folder is restricted appropriately.
