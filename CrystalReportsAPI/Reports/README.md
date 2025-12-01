# Reports Folder

This folder is intended to contain Crystal Report (.rpt) files.

Place your .rpt report files in this directory. The API will look for report files here when generating reports.

## Usage

When calling the `/api/reports/generate` endpoint, specify the report name without the `.rpt` extension.

Example: For a file named `Invoice.rpt`, use `"ReportName": "Invoice"` in your request.

## Security Note

Ensure that sensitive report files are properly secured and that access to this folder is restricted appropriately.
