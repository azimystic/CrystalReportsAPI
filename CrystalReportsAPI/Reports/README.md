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

Example: For a file named `Invoice.rpt`, use `"ReportName": "Invoice"` in your request.

### Example Request

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

## Security Note

Ensure that sensitive report files are properly secured and that access to this folder is restricted appropriately.
