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

                // Set report title if provided
                if (!string.IsNullOrEmpty(request.ReportTitle))
                {
                    reportDocument.SummaryInfo.ReportTitle = request.ReportTitle;
                }

                // Apply database connection BEFORE setting parameters
                ApplyDatabaseConnection(reportDocument);

                // Apply parameters based on report type
                ApplyReportParameters(reportDocument, request);

                // Export to PDF or Excel
                byte[] exportBytes;
                string contentType;
                string fileExtension;

                if (request.ExportToExcel)
                {
                    using (var stream = reportDocument.ExportToStream(ExportFormatType.Excel))
                    {
                        using (var memoryStream = new MemoryStream())
                        {
                            stream.CopyTo(memoryStream);
                            exportBytes = memoryStream.ToArray();
                        }
                    }
                    contentType = "application/vnd.ms-excel";
                    fileExtension = ".xls";
                }
                else
                {
                    using (var stream = reportDocument.ExportToStream(ExportFormatType.PortableDocFormat))
                    {
                        using (var memoryStream = new MemoryStream())
                        {
                            stream.CopyTo(memoryStream);
                            exportBytes = memoryStream.ToArray();
                        }
                    }
                    contentType = "application/pdf";
                    fileExtension = ".pdf";
                }

                // Create the response
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(exportBytes)
                };
                response.Content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
                response.Content.Headers.ContentDisposition = new ContentDispositionHeaderValue("inline")
                {
                    FileName = $"{request.ReportName}{fileExtension}"
                };

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
                if (reportDocument != null)
                {
                    reportDocument.Close();
                    reportDocument.Dispose();
                }

                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
        }

        private string GetReportPath(ReportRequest request)
        {

            string reportsFolder = "C:\\Users\\cmaag\\source\\repos\\NomiButtVp\\Standard_Hospital\\GL\\Reports";
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
                        reportFileName = "rptLabTestResultQ. rpt";
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
                        reportFileName = "rptLabTestBloodBankResultQ. rpt";
                        break;
                    case 11:
                        reportFileName = "rptLabTestResultQ_Fixed_Patient_Value. rpt";
                        break;
                    case 12:
                        reportFileName = "rptLabTestHistopathology. rpt";
                        break;
                    default:
                        reportFileName = "rptLabTestResultQ. rpt";
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
            if (!string.IsNullOrEmpty(request.TestIDs))
            {
                reportDocument.SetParameterValue("@Test_IDs", request.TestIDs);
            }

            if (!string.IsNullOrEmpty(request.PatientID))
            {
                reportDocument.SetParameterValue("@Patient_ID", request.PatientID);
            }

            if (!string.IsNullOrEmpty(request.TransID))
            {
                reportDocument.SetParameterValue("@Trans_IDs", request.TransID);
            }

            if (!string.IsNullOrEmpty(request.BillDetailID))
            {
                reportDocument.SetParameterValue("@Bill_Details_IDs", request.BillDetailID);
            }

            string companyName = ConfigurationManager.AppSettings["CompanyName"] ?? "Hospital Name";
            string companyAddress = ConfigurationManager.AppSettings["CompanyAddress"] ?? "";
            string companyPhone = ConfigurationManager.AppSettings["CompanyPhone"] ?? "";

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
                reportDocument.SetParameterValue("@labTDetailsID", labTestDetailId);
            }

            if (request.IncludeLetterhead)
            {
                try
                {
                    reportDocument.SetParameterValue("ChkLetterHead", true);
                }
                catch { }
            }
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