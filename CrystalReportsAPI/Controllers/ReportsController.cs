using System;
using System.Configuration;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Web;
using System.Web.Http;
using CrystalDecisions.Shared;
using CrystalDecisions.CrystalReports.Engine;
using CrystalReportsAPI.Filters;
using CrystalReportsAPI.Models;

namespace CrystalReportsAPI.Controllers
{
    [RoutePrefix("api/reports")]
    public class ReportsController : ApiController
    {
        private const string ConnectionStringName = "ASPGLConnectionString";

        /// <summary>
        /// Diagnostic endpoint to check Crystal Reports setup
        /// </summary>
        [HttpGet]
        [Route("diagnose")]
        public HttpResponseMessage Diagnose()
        {
            var diagnostics = new System.Text.StringBuilder();

            try
            {
                // Check 1: Application bitness
                diagnostics.AppendLine($"Application is 64-bit: {Environment.Is64BitProcess}");
                diagnostics.AppendLine($"OS is 64-bit: {Environment.Is64BitOperatingSystem}");

                // Check 2: Crystal Reports assembly version
                var crystalAssembly = typeof(ReportDocument).Assembly;
                diagnostics.AppendLine($"Crystal Reports Assembly: {crystalAssembly.FullName}");
                diagnostics.AppendLine($"Crystal Reports Location: {crystalAssembly.Location}");

                // Check 3: Reports folder
                string reportsFolder = HttpContext.Current.Server.MapPath("~/Reports");
                diagnostics.AppendLine($"Reports Folder: {reportsFolder}");
                diagnostics.AppendLine($"Reports Folder Exists: {Directory.Exists(reportsFolder)}");

                // Check 4: LabRadiology folder
                string labFolder = Path.Combine(reportsFolder, "LabRadiology");
                diagnostics.AppendLine($"LabRadiology Folder: {labFolder}");
                diagnostics.AppendLine($"LabRadiology Folder Exists: {Directory.Exists(labFolder)}");

                // Check 5: List report files
                if (Directory.Exists(labFolder))
                {
                    var files = Directory.GetFiles(labFolder, "*.rpt");
                    diagnostics.AppendLine($"Report Files Found: {files.Length}");
                    foreach (var file in files)
                    {
                        var fileInfo = new FileInfo(file);
                        diagnostics.AppendLine($"  - {fileInfo.Name} ({fileInfo.Length} bytes)");
                    }
                }

                // Check 6: Specific report file
                string testReport = Path.Combine(labFolder, "rptLabTestUiltraSound_1.rpt");
                diagnostics.AppendLine($"Test Report Path: {testReport}");
                diagnostics.AppendLine($"Test Report Exists: {File.Exists(testReport)}");

                if (File.Exists(testReport))
                {
                    var fileInfo = new FileInfo(testReport);
                    diagnostics.AppendLine($"Test Report Size: {fileInfo.Length} bytes");
                    diagnostics.AppendLine($"Test Report ReadOnly: {fileInfo.IsReadOnly}");
                    diagnostics.AppendLine($"Test Report LastModified: {fileInfo.LastWriteTime}");
                }

                // Check 7: Try to create ReportDocument
                diagnostics.AppendLine("Attempting to create ReportDocument...");
                using (var rd = new ReportDocument())
                {
                    diagnostics.AppendLine("ReportDocument created successfully");

                    // Check 8: Try to load report
                    if (File.Exists(testReport))
                    {
                        diagnostics.AppendLine("Attempting to load report.. .");
                        try
                        {
                            rd.Load(testReport, OpenReportMethod.OpenReportByTempCopy);
                            diagnostics.AppendLine("Report loaded successfully!");
                            diagnostics.AppendLine($"Report Name: {rd.Name}");
                            diagnostics.AppendLine($"Report Has Records: {rd.HasRecords}");
                        }
                        catch (Exception loadEx)
                        {
                            diagnostics.AppendLine($"Report load failed: {loadEx.Message}");
                            if (loadEx.InnerException != null)
                            {
                                diagnostics.AppendLine($"Inner Exception: {loadEx.InnerException.Message}");
                            }
                        }
                    }
                }

                // Check 8: Connection string
                var connStr = ConfigurationManager.ConnectionStrings[ConnectionStringName];
                diagnostics.AppendLine($"Connection String Configured: {connStr != null}");
            }
            catch (Exception ex)
            {
                diagnostics.AppendLine($"Diagnostic Error: {ex.Message}");
                if (ex.InnerException != null)
                {
                    diagnostics.AppendLine($"Inner Exception: {ex.InnerException.Message}");
                }
            }

            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(diagnostics.ToString())
            };
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
            return response;
        }

        /// <summary>
        /// Test endpoint to verify binary file download works
        /// </summary>
        [HttpGet]
        [Route("test-pdf")]
        public HttpResponseMessage TestPdf()
        {
            // Create a simple PDF byte array (PDF header)
            byte[] pdfBytes = new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x34 }; // %PDF-1.4
            
            System.Diagnostics.Debug.WriteLine($"Test PDF - Bytes: {pdfBytes.Length}");
            
            var response = new HttpResponseMessage(HttpStatusCode.OK);
            var content = new ByteArrayContent(pdfBytes);
            
            content.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
            content.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment")
            {
                FileName = "test.pdf"
            };
            content.Headers.ContentLength = pdfBytes.Length;
            
            response.Content = content;
            return response;
        }

        /// <summary>
        /// Generates a Crystal Report and returns it as a PDF. 
        /// </summary>
        [HttpPost]
        [Route("generate")]
        [ApiKeyAuthorizationFilter]
        public HttpResponseMessage Generate([FromBody] ReportRequest request)
        {
            if (request == null || string.IsNullOrEmpty(request.ReportName))
            {
                return Request.CreateErrorResponse(
                    HttpStatusCode.BadRequest,
                    "Report name is required.");
            }

            ReportDocument reportDocument = null;
            string reportPath = string.Empty;
            string tempFilePath = string.Empty;

            try
            {
                // Force garbage collection before loading report
                GC.Collect();
                GC.WaitForPendingFinalizers();

                // Get report path
                reportPath = GetReportPath(request);

                // Verify the file exists
                if (!File.Exists(reportPath))
                {
                    return Request.CreateErrorResponse(
                        HttpStatusCode.NotFound,
                        $"Report file was not found at: {reportPath}");
                }

                // Create and load the report document
                reportDocument = new ReportDocument();
                reportDocument.Load(reportPath, OpenReportMethod.OpenReportByTempCopy);

                System.Diagnostics.Debug.WriteLine($"Report loaded: {reportPath}");
                System.Diagnostics.Debug.WriteLine($"Report name: {reportDocument.Name}");

                // Set report title if provided
                if (!string.IsNullOrEmpty(request.ReportTitle))
                {
                    reportDocument.SummaryInfo.ReportTitle = request.ReportTitle;
                }

                // Apply parameters BEFORE database connection (like in working code)
                ApplyReportParameters(reportDocument, request);
                
                // Apply database connection AFTER setting parameters
                ApplyDatabaseConnection(reportDocument);
                System.Diagnostics.Debug.WriteLine("Database connection applied");
                
                // Check if report has records
                try
                {
                    bool hasRecords = reportDocument.HasRecords;
                    System.Diagnostics.Debug.WriteLine($"Report HasRecords: {hasRecords}");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Could not check HasRecords: {ex.Message}");
                }

                // Export to PDF or Excel
                byte[] exportBytes;
                string contentType;
                string fileExtension;
                tempFilePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

                if (request.ExportToExcel)
                {
                    tempFilePath += ".xls";
                    reportDocument.ExportToDisk(ExportFormatType.Excel, tempFilePath);
                    contentType = "application/vnd.ms-excel";
                    fileExtension = ".xls";
                }
                else
                {
                    tempFilePath += ".pdf";
                    reportDocument.ExportToDisk(ExportFormatType.PortableDocFormat, tempFilePath);
                    contentType = "application/pdf";
                    fileExtension = ".pdf";
                }
                
                System.Diagnostics.Debug.WriteLine($"File exported to: {tempFilePath}");
                System.Diagnostics.Debug.WriteLine($"File exists: {File.Exists(tempFilePath)}");
                
                // Close report document to release locks
                if (reportDocument != null)
                {
                    reportDocument.Close();
                    reportDocument.Dispose();
                    reportDocument = null;
                }
                
                // Read the file - ensure it exists and has content
                if (!File.Exists(tempFilePath))
                {
                    throw new FileNotFoundException($"Exported file not found: {tempFilePath}");
                }
                
                var fileInfo = new FileInfo(tempFilePath);
                System.Diagnostics.Debug.WriteLine($"File size: {fileInfo.Length} bytes");
                
                if (fileInfo.Length == 0)
                {
                    throw new InvalidOperationException("Exported file is empty (0 bytes)");
                }
                
                exportBytes = File.ReadAllBytes(tempFilePath);
                System.Diagnostics.Debug.WriteLine($"Bytes read into memory: {exportBytes.Length}");
                
                // Delete temp file NOW, before creating response
                try
                {
                    File.Delete(tempFilePath);
                    System.Diagnostics.Debug.WriteLine($"Temp file deleted: {tempFilePath}");
                    tempFilePath = string.Empty; // Clear so finally block doesn't try again
                }
                catch (Exception delEx)
                {
                    System.Diagnostics.Debug.WriteLine($"Warning: Could not delete temp file: {delEx.Message}");
                }

                // Create the response with bytes already in memory
                System.Diagnostics.Debug.WriteLine($"Creating response with {exportBytes.Length} bytes");
                System.Diagnostics.Debug.WriteLine($"Content-Type: {contentType}");
                
                // Log first few bytes for debugging
                if (exportBytes.Length >= 10)
                {
                    var firstBytes = new byte[10];
                    Array.Copy(exportBytes, firstBytes, 10);
                    System.Diagnostics.Debug.WriteLine($"First 10 bytes: {string.Join(",", firstBytes)}");
                }
                
                var response = new HttpResponseMessage(HttpStatusCode.OK);
                var content = new ByteArrayContent(exportBytes);
                
                content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
                content.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment")
                {
                    FileName = $"{request.ReportName}{fileExtension}"
                };
                content.Headers.ContentLength = exportBytes.Length;
                
                response.Content = content;

                System.Diagnostics.Debug.WriteLine($"Response created successfully with Content-Length: {exportBytes.Length}");
                System.Diagnostics.Debug.WriteLine($"Content-Disposition: {content.Headers.ContentDisposition}");
                
                return response;
            }
            catch (CrystalDecisions.Shared.CrystalReportsException crEx)
            {
                System.Diagnostics.Debug.WriteLine($"Crystal Reports error: {crEx}");

                string errorMessage = $"Crystal Reports error: {crEx.Message}";
                if (crEx.InnerException != null)
                {
                    errorMessage += $" Inner: {crEx.InnerException.Message}";
                }
                errorMessage += $" Report Path: {reportPath}";
                errorMessage += $" Is64BitProcess: {Environment.Is64BitProcess}";

                return Request.CreateErrorResponse(
                    HttpStatusCode.InternalServerError,
                    errorMessage);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Report generation error: {ex}");

                return Request.CreateErrorResponse(
                    HttpStatusCode.InternalServerError,
                    $"An error occurred: {ex.Message}.  Report Path: {reportPath}");
            }
            finally
            {
                // Clean up report document if not already disposed
                if (reportDocument != null)
                {
                    try
                    {
                        reportDocument.Close();
                        reportDocument.Dispose();
                    }
                    catch { }
                }
                
                // Clean up temp file if it still exists
                if (!string.IsNullOrEmpty(tempFilePath) && File.Exists(tempFilePath))
                {
                    try
                    {
                        File.Delete(tempFilePath);
                        System.Diagnostics.Debug.WriteLine($"Temp file deleted in finally: {tempFilePath}");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Failed to delete temp file in finally: {ex.Message}");
                    }
                }
                
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
        }

        private string GetReportPath(ReportRequest request)
        {

            string reportsFolder = HttpContext.Current.Server.MapPath("~/Reports/");
            string reportFileName;

            if (request.ReportName == "TestResultQ")
            {
                int reportColumns = request.ReportColumn;

                switch (reportColumns)
                {
                    case 2:
                        reportFileName = "rptLabTestResultQ_2.rpt";
                        break;
                    case 3:
                        reportFileName = "rptLabTestResultQ_3.rpt";
                        break;
                    case 4:
                        reportFileName = "rptLabTestResultQ.rpt";
                        break;
                    case 5:
                        reportFileName = "rptLabTestResultQ_6.rpt";
                        break;
                    case 6:
                        reportFileName = "rptLabTestResultQ_5.rpt";
                        break;
                    case 7:
                        reportFileName = "rptLabResult_Organism.rpt";
                        break;
                    case 8:
                        reportFileName = "rptLabTestUiltraSound_1.rpt";
                        break;
                    case 9:
                        reportFileName = "rptLabTestResultQ_8.rpt";
                        break;
                    case 10:
                        reportFileName = "rptLabTestBloodBankResultQ.rpt";
                        break;
                    case 11:
                        reportFileName = "rptLabTestResultQ_Fixed_Patient_Value.rpt";
                        break;
                    case 12:
                        reportFileName = "rptLabTestHistopathology.rpt";
                        break;
                    default:
                        reportFileName = "rptLabTestResultQ.rpt";
                        break;
                }

                return Path.Combine(reportsFolder, "LabRadiology", reportFileName);
            }
            else
            {
                string sanitizedReportName = Path.GetFileNameWithoutExtension(request.ReportName);
                if (string.IsNullOrEmpty(sanitizedReportName) ||
                    sanitizedReportName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                {
                    throw new ArgumentException("Invalid report name.");
                }
                return Path.Combine(reportsFolder, sanitizedReportName + ".rpt");
            }
        }

        private void ApplyReportParameters(ReportDocument reportDocument, ReportRequest request)
        {
            if (request.ReportName == "TestResultQ")
            {
                ApplyTestResultQParameters(reportDocument, request);
            }
            else
            {
                if (request.Parameters != null && request.Parameters.Count > 0)
                {
                    foreach (var param in request.Parameters)
                    {
                        if (!string.IsNullOrEmpty(param.Name))
                        {
                            try
                            {
                                reportDocument.SetParameterValue(param.Name, param.Value ?? "");
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"Failed to set parameter '{param.Name}': {ex.Message}");
                            }
                        }
                    }
                }
            }
        }

        private void ApplyTestResultQParameters(ReportDocument reportDocument, ReportRequest request)
        {
            System.Diagnostics.Debug.WriteLine($"=== Setting Report Parameters ===");
            System.Diagnostics.Debug.WriteLine($"ReportColumn: {request.ReportColumn}");
            
            if (!string.IsNullOrEmpty(request.TestIDs))
            {
                System.Diagnostics.Debug.WriteLine($"Setting @Test_IDs: {request.TestIDs}");
                reportDocument.SetParameterValue("@Test_IDs", request.TestIDs);
            }

            string testWiseIds = string.Empty;
            if (request.Parameters != null)
            {
                System.Diagnostics.Debug.WriteLine($"Parameters count: {request.Parameters.Count}");
                var testWiseIdsParam = request.Parameters.Find(p => p.Name == "test_wise_ids");
                if (testWiseIdsParam != null && testWiseIdsParam.Value != null)
                {
                    testWiseIds = testWiseIdsParam.Value.ToString();
                    System.Diagnostics.Debug.WriteLine($"Found test_wise_ids: {testWiseIds}");
                }
            }
            
            if (!string.IsNullOrEmpty(testWiseIds))
            {
                System.Diagnostics.Debug.WriteLine($"Setting @Patient_ID from test_wise_ids: {testWiseIds}");
                reportDocument.SetParameterValue("@Patient_ID", testWiseIds);
            }
            else if (!string.IsNullOrEmpty(request.PatientID))
            {
                System.Diagnostics.Debug.WriteLine($"Setting @Patient_ID from PatientID: {request.PatientID}");
                reportDocument.SetParameterValue("@Patient_ID", request.PatientID);
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("WARNING: @Patient_ID not set - no value provided");
            }

            if (!string.IsNullOrEmpty(request.TransID))
            {
                System.Diagnostics.Debug.WriteLine($"Setting @Trans_IDs: {request.TransID}");
                reportDocument.SetParameterValue("@Trans_IDs", request.TransID);
            }

            if (!string.IsNullOrEmpty(request.BillDetailID))
            {
                System.Diagnostics.Debug.WriteLine($"Setting @Bill_Details_IDs: {request.BillDetailID}");
                reportDocument.SetParameterValue("@Bill_Details_IDs", request.BillDetailID);
            }

            string companyName = ConfigurationManager.AppSettings["CompanyName"] ?? "Hospital Name";
            string companyAddress = ConfigurationManager.AppSettings["CompanyAddress"] ?? "";
            string companyPhone = ConfigurationManager.AppSettings["CompanyPhone"] ?? "";

            System.Diagnostics.Debug.WriteLine($"Setting CompanyName: {companyName}");
            reportDocument.SetParameterValue("CompanyName", companyName);
            reportDocument.SetParameterValue("Address", companyAddress);
            reportDocument.SetParameterValue("Phone", companyPhone);

            if (request.ReportColumn == 7 && !string.IsNullOrEmpty(request.LabTestDetailID))
            {
                string labTestDetailId = request.LabTestDetailID;
                if (labTestDetailId == "null" || string.IsNullOrEmpty(labTestDetailId))
                {
                    labTestDetailId = "1";
                }
                System.Diagnostics.Debug.WriteLine($"Setting @labTDetailsID: {labTestDetailId}");
                reportDocument.SetParameterValue("@labTDetailsID", labTestDetailId);
            }

            if (request.IncludeLetterhead)
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine("Setting ChkLetterHead: true");
                    reportDocument.SetParameterValue("ChkLetterHead", true);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to set ChkLetterHead: {ex.Message}");
                }
            }
            
            System.Diagnostics.Debug.WriteLine("=== Parameter setting complete ===");
        }

        private void ApplyDatabaseConnection(ReportDocument reportDocument)
        {
            string connectionString = ConfigurationManager.ConnectionStrings[ConnectionStringName]?.ConnectionString;

            if (string.IsNullOrEmpty(connectionString))
            {
                throw new InvalidOperationException($"Connection string '{ConnectionStringName}' is not configured.");
            }

            var builder = new System.Data.SqlClient.SqlConnectionStringBuilder(connectionString);

            ConnectionInfo connectionInfo = new ConnectionInfo
            {
                ServerName = builder.DataSource,
                DatabaseName = builder.InitialCatalog,
                IntegratedSecurity = builder.IntegratedSecurity
            };

            if (!builder.IntegratedSecurity)
            {
                connectionInfo.UserID = builder.UserID;
                connectionInfo.Password = builder.Password;
            }

            ApplyConnectionToTables(reportDocument.Database.Tables, connectionInfo);

            foreach (ReportDocument subreport in reportDocument.Subreports)
            {
                ApplyConnectionToTables(subreport.Database.Tables, connectionInfo);
            }
        }

        private void ApplyConnectionToTables(Tables tables, ConnectionInfo connectionInfo)
        {
            foreach (Table table in tables)
            {
                TableLogOnInfo tableLogOnInfo = table.LogOnInfo;
                tableLogOnInfo.ConnectionInfo = connectionInfo;
                table.ApplyLogOnInfo(tableLogOnInfo);
            }
        }
    }
}