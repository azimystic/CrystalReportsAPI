# Patient Lab Tests Endpoint

## Overview
Get all lab test information for a specific patient, including individual tests and group tests.

## Endpoint
```
GET /api/reports/patient-tests/{patientId}
```

## Authentication
Requires `X-API-Key` header.

## Parameters
- `patientId` (path parameter) - The patient ID

## Response Format
```json
{
  "PatientID": "3203",
  "Tests": [
    {
      "LabTestID": "123",
      "LabTestDetailID": "456",
      "BillDetailsID": "789",
      "TestIDs": "152",
      "TestName": "Complete Blood Count",
      "ReportColumn": "8",
      "IsGroupTest": false,
      "TransactionID": "3"
    },
    {
      "LabTestID": "124",
      "LabTestDetailID": null,
      "BillDetailsID": "790",
      "TestIDs": "1949,1936,128,126,125",
      "TestName": "Liver Function Tests",
      "ReportColumn": "5",
      "IsGroupTest": true,
      "TransactionID": "5"
    }
  ]
}
```

## Response Fields

### LabTestInfo Object
- `LabTestID` - The laboratory test ID
- `LabTestDetailID` - The lab test detail ID (null for group tests)
- `BillDetailsID` - The billing details ID
- `TestIDs` - Comma-separated test IDs (single for individual tests, multiple for group tests)
- `TestName` - The test name (individual test name or group test name)
- `ReportColumn` - The report column number (determines which report template to use)
- `IsGroupTest` - Boolean indicating if this is a group test
- `TransactionID` - The transaction/parent transaction type ID

## Important Notes
- **Group Tests**: When `IsGroupTest` is `true`:
  - `LabTestDetailID` will be `null`
  - `TestIDs` contains comma-separated list of sub-test IDs
  - `TestName` is the group test name
  - Only one entry is returned per group test
  
- **Individual Tests**: When `IsGroupTest` is `false`:
  - `LabTestDetailID` contains the specific detail ID
  - `TestIDs` contains a single test ID
  - `TestName` is the individual test name

## Example Usage

### Using cURL
```bash
curl -X GET "http://localhost:port/api/reports/patient-tests/3203" \
  -H "X-API-Key: your-api-key-here"
```

### Using JavaScript/Fetch
```javascript
fetch('http://localhost:port/api/reports/patient-tests/3203', {
  method: 'GET',
  headers: {
    'X-API-Key': 'your-api-key-here'
  }
})
.then(response => response.json())
.then(data => {
  console.log('Patient Tests:', data);
  
  // Generate reports for each test
  data.Tests.forEach(test => {
    generateReport(test);
  });
});

function generateReport(test) {
  const reportRequest = {
    "ReportName": "TestResultQ",
    "ReportTitle": "Department Of Pathology",
    "ReportColumn": parseInt(test.ReportColumn),
    "TestIDs": test.TestIDs,
    "PatientID": data.PatientID,
    "TransID": test.TransactionID,
    "BillDetailID": test.BillDetailsID,
    "LabTestDetailID": test.LabTestDetailID,
    "IncludeLetterhead": true,
    "ExportToExcel": false,
    "Parameters": [
      {
        "Name": "test_wise_ids",
        "Value": data.PatientID
      }
    ]
  };
  
  // Call the generate endpoint
  fetch('http://localhost:port/api/reports/generate', {
    method: 'POST',
    headers: {
      'X-API-Key': 'your-api-key-here',
      'Content-Type': 'application/json'
    },
    body: JSON.stringify(reportRequest)
  })
  .then(response => response.blob())
  .then(blob => {
    // Download the PDF
    const url = window.URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `Test_${test.LabTestID}.pdf`;
    a.click();
  });
}
```

## Use Case
This endpoint is useful when you need to:
1. Get all pending lab tests for a patient
2. Batch generate reports for all tests
3. Display a list of available tests before generating reports
4. Automatically populate report generation parameters

## Error Responses
- `400 Bad Request` - Patient ID not provided
- `401 Unauthorized` - Invalid or missing API key
- `500 Internal Server Error` - Database or server error

## Notes
- Individual tests have a single `TestID`
- Group tests have multiple `TestIDs` separated by commas
- The `ReportColumn` determines which Crystal Report template to use (see TestResultQ documentation)
- The response includes both completed and pending tests
