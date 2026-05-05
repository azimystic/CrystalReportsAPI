using CrystalDecisions.CrystalReports.Engine;
using CrystalDecisions.Shared;
using CrystalReportsAPI.Filters;
using CrystalReportsAPI.Models;
using CrystalReportsAPI.Helpers;
using System;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Web;
using System.Web.Http;

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
        /// Gets all lab test information for a specific patient
        /// </summary>
        [HttpGet]
        [Route("patient-tests/{patientId}")]
        [ApiKeyAuthorizationFilter]
        public IHttpActionResult GetPatientTests(string patientId)
        {
            if (string.IsNullOrEmpty(patientId))
            {
                return BadRequest("MR no sequence is required.");
            }

            try
            {
                var result = LabTestHelper.GetPatientLabTests(patientId);
                return Ok(result);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting patient tests: {ex}");
                return InternalServerError(ex);
            }
        }

        /// <summary>
        /// Gets MR no sequence for a patient by mobile number
        /// </summary>
        [HttpGet]
        [Route("patient-mrno")]
        [ApiKeyAuthorizationFilter]
        public IHttpActionResult GetMrNoSequenceByMobile([FromUri] string phoneNumber)
        {
            if (string.IsNullOrWhiteSpace(phoneNumber))
            {
                return BadRequest("Phone number is required.");
            }

            try
            {
                var mrNoSequence = LabTestHelper.GetMrNoSequenceByMobile(phoneNumber);
                return Ok(new { mr_no_sequence = mrNoSequence });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting MR no sequence by mobile: {ex}");
                return InternalServerError(ex);
            }
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
                
                System.Diagnostics.Debug.WriteLine($"Loading report from: {reportPath}");
                System.Diagnostics.Debug.WriteLine($"Report file size: {new FileInfo(reportPath).Length} bytes");
                
                reportDocument.Load(reportPath, OpenReportMethod.OpenReportByTempCopy);

                System.Diagnostics.Debug.WriteLine($"Report loaded: {reportPath}");
                System.Diagnostics.Debug.WriteLine($"Report name: {reportDocument.Name}");
                System.Diagnostics.Debug.WriteLine($"Report RecordSelectionFormula: {reportDocument.RecordSelectionFormula}");

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

                
                // Log report sections and objects for resource debugging
                System.Diagnostics.Debug.WriteLine("=== Report Structure Diagnostics ===");
                try
                {
                    System.Diagnostics.Debug.WriteLine($"Report Sections: {reportDocument.ReportDefinition.Sections.Count}");
                    foreach (Section section in reportDocument.ReportDefinition.Sections)
                    {
                        System.Diagnostics.Debug.WriteLine($"  Section: {section.Name}, ReportObjects: {section.ReportObjects.Count}");
                        foreach (ReportObject obj in section.ReportObjects)
                        {
                            if (obj.Kind == ReportObjectKind.PictureObject)
                            {
                                var picObj = (PictureObject)obj;
                                System.Diagnostics.Debug.WriteLine($"    Picture: {picObj.Name}, ObjectFormat.EnableSuppress: {picObj.ObjectFormat.EnableSuppress}");
                            }
                        }
                    }
                }
                catch (Exception structEx)
                {
                    System.Diagnostics.Debug.WriteLine($"Structure diagnostics failed: {structEx.Message}");
                }
                System.Diagnostics.Debug.WriteLine("=== End Report Structure Diagnostics ===");

                
                // For OutPatient report, re-apply CompanyName and CompanyLogo after database connection
                // because ApplyDatabaseConnection can refresh the report and clear some parameters
                if (request.ReportName == "OutPatient")
                {
                    System.Diagnostics.Debug.WriteLine("=== Re-applying Company parameters after DB connection ===");
                    string companyName = ConfigurationManager.AppSettings["CompanyName"] ?? "Hospital Name";
                    string logoPath = GetCompanyLogoPath();
                    
                    try
                    {
                        reportDocument.SetParameterValue("CompanyName", companyName);
                        System.Diagnostics.Debug.WriteLine($"CompanyName re-set to: '{companyName}'");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Failed to re-set CompanyName: {ex.Message}");
                    }
                    
                    try
                    {
                        reportDocument.SetParameterValue("CompanyLogo", logoPath ?? string.Empty);
                        System.Diagnostics.Debug.WriteLine($"CompanyLogo re-set to: '{logoPath}'");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Failed to re-set CompanyLogo: {ex.Message}");
                    }
                    // Log the final record selection formula after all parameters are set
                    System.Diagnostics.Debug.WriteLine($"=== Final Report State ===");
                    System.Diagnostics.Debug.WriteLine($"RecordSelectionFormula: {reportDocument.RecordSelectionFormula}");
                    if (!string.IsNullOrEmpty(request.PatientID))
                    {
                        // Try to log parameter field values
                        try
                        {
                            int outPatientID = Convert.ToInt32(request.PatientID);
                            reportDocument.SetParameterValue("@outPatient", Convert.ToInt32(outPatientID));
                            reportDocument.SetParameterValue("@outPatient", Convert.ToInt32(outPatientID), reportDocument.Subreports[0].Name.ToString());
                            reportDocument.SetParameterValue("@outPatient", Convert.ToInt32(outPatientID), reportDocument.Subreports[1].Name.ToString());
                            reportDocument.SetParameterValue("@outPatient", Convert.ToInt32(outPatientID), reportDocument.Subreports[2].Name.ToString());
                            reportDocument.SetParameterValue("@outPatient", Convert.ToInt32(outPatientID), reportDocument.Subreports[3].Name.ToString());
                        }
                        catch (Exception paramEx)
                        {
                            System.Diagnostics.Debug.WriteLine($"Could not set outPatient parameters: {paramEx.Message}");
                        }
                    }
                }
                
                
                
                // Note: Skipping HasRecords check as it can cause "Missing parameter values" error
                // if any subreport parameters are not fully resolved yet

                // Export to PDF or Excel
                byte[] exportBytes;
                string contentType;
                string fileExtension;
                ExportFormatType exportFormat;

                if (request.ExportToExcel)
                {
                    exportFormat = ExportFormatType.Excel;
                    contentType = "application/vnd.ms-excel";
                    fileExtension = ".xls";
                }
                else
                {
                    exportFormat = ExportFormatType.PortableDocFormat;
                    contentType = "application/pdf";
                    fileExtension = ".pdf";
                }

                System.Diagnostics.Debug.WriteLine($"Exporting to {exportFormat}");

                string appTempDir = HttpContext.Current.Server.MapPath("~/App_Data/Temp");
                if (!Directory.Exists(appTempDir))
                {
                    Directory.CreateDirectory(appTempDir);
                }

                Environment.SetEnvironmentVariable("TEMP", appTempDir, EnvironmentVariableTarget.Process);
                Environment.SetEnvironmentVariable("TMP", appTempDir, EnvironmentVariableTarget.Process);
                System.Diagnostics.Debug.WriteLine($"Process temp directory set to: {appTempDir}");
                System.Diagnostics.Debug.WriteLine($"TEMP env: {Environment.GetEnvironmentVariable("TEMP")}");
                System.Diagnostics.Debug.WriteLine($"TMP env: {Environment.GetEnvironmentVariable("TMP")}");

                try
                {
                    var testFile = Path.Combine(appTempDir, $"cr_{Guid.NewGuid():N}.tmp");
                    File.WriteAllText(testFile, "test");
                    File.Delete(testFile);
                    System.Diagnostics.Debug.WriteLine("Temp directory write test succeeded.");
                }
                catch (Exception tempEx)
                {
                    throw new InvalidOperationException($"Temp directory is not writable: {appTempDir}. {tempEx.Message}", tempEx);
                }
                
                // Log all parameters before export for debugging
                System.Diagnostics.Debug.WriteLine("=== Pre-Export Parameter Diagnostics ===");
                try
                {
                    foreach (ParameterField param in reportDocument.ParameterFields)
                    {
                        System.Diagnostics.Debug.WriteLine($"Parameter: {param.Name}, HasCurrentValue: {param.HasCurrentValue}");
                        if (param.HasCurrentValue)
                        {
                            try
                            {
                                var currentValues = param.CurrentValues;
                                if (currentValues != null && currentValues.Count > 0)
                                {
                                    System.Diagnostics.Debug.WriteLine($"  Value: {currentValues[0]}");
                                }
                            }
                            catch { }
                        }
                    }
                }
                catch (Exception diagEx)
                {
                    System.Diagnostics.Debug.WriteLine($"Parameter diagnostics failed: {diagEx.Message}");
                }
                System.Diagnostics.Debug.WriteLine("=== End Pre-Export Diagnostics ===");

                try
                {
                    using (var exportStream = reportDocument.ExportToStream(exportFormat))
                    {
                        if (exportStream == null)
                        {
                            throw new InvalidOperationException("Export stream was null.");
                        }

                        using (var memoryStream = new MemoryStream())
                        {
                            exportStream.CopyTo(memoryStream);
                            exportBytes = memoryStream.ToArray();
                        }
                    }
                }
                catch (Exception exportEx)
                {
                    throw new InvalidOperationException(
                        $"Failed to export report to stream. Error: {exportEx.Message}",
                        exportEx);
                }
                
                // Close report document to release locks
                if (reportDocument != null)
                {
                    reportDocument.Close();
                    reportDocument.Dispose();
                    reportDocument = null;
                }
                
                if (exportBytes == null || exportBytes.Length == 0)
                {
                    throw new InvalidOperationException("Exported report is empty (0 bytes)");
                }

                System.Diagnostics.Debug.WriteLine($"Bytes read into memory: {exportBytes.Length}");

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
            else if (request.ReportName == "OutPatient")
            {
                return Path.Combine(reportsFolder, "Patients", "rptOutpatientDetails.rpt");
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
            else if (request.ReportName == "OutPatient")
            {
                ApplyOutPatientParameters(reportDocument, request);
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("No generic parameters supplied.");
            }
        }

        private void ApplyTestResultQParameters(ReportDocument reportDocument, ReportRequest request)
        {
            System.Diagnostics.Debug.WriteLine($"=== Setting Report Parameters ===");
            System.Diagnostics.Debug.WriteLine($"ReportColumn: {request.ReportColumn}");
            
            if (!string.IsNullOrEmpty(request.TestIDs))
            {
                // Remove spaces after commas if present - Crystal Reports might be sensitive to this
                string cleanedTestIDs = request.TestIDs.Replace(", ", ",");
                System.Diagnostics.Debug.WriteLine($"Original TestIDs: '{request.TestIDs}'");
                System.Diagnostics.Debug.WriteLine($"Cleaned TestIDs: '{cleanedTestIDs}'");
                System.Diagnostics.Debug.WriteLine($"Setting @Test_IDs: {cleanedTestIDs}");
                reportDocument.SetParameterValue("@Test_IDs", cleanedTestIDs);
            }

            if (!string.IsNullOrEmpty(request.PatientID))
            {
                System.Diagnostics.Debug.WriteLine($"Setting @Patient_ID from PatientID: {request.PatientID}");
                reportDocument.SetParameterValue("@Patient_ID", request.PatientID);
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("WARNING: @Patient_ID not set - no value provided");
            }

            if (!string.IsNullOrEmpty(request.LabTestID))
            {
                System.Diagnostics.Debug.WriteLine($"Setting @Trans_IDs from LabTestID: {request.LabTestID}");
                reportDocument.SetParameterValue("@Trans_IDs", request.LabTestID);
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

        private void ApplyOutPatientParameters(ReportDocument reportDocument, ReportRequest request)
        {
            System.Diagnostics.Debug.WriteLine($"=== Setting OutPatient Report Parameters ===");

            // Get outPatientID from PatientID
            string outPatientID = request.PatientID;

            if (string.IsNullOrEmpty(outPatientID))
            {
                throw new ArgumentException("outPatientID parameter is required for OutPatient report");
            }

            int outPatientIdInt;
            if (!int.TryParse(outPatientID, out outPatientIdInt))
            {
                throw new ArgumentException($"outPatientID must be a valid integer: {outPatientID}");
            }

            // Get company information (matching working code pattern)
            string companyName = ConfigurationManager.AppSettings["CompanyName"] ?? "Hospital Name";
            string logoPath = GetCompanyLogoPath();
            
            System.Diagnostics.Debug.WriteLine($"out_Patient_ID: {outPatientIdInt}");
            System.Diagnostics.Debug.WriteLine($"CompanyName: '{companyName}'");
            System.Diagnostics.Debug.WriteLine($"logoPath: '{logoPath}'");

            // Set parameters EXACTLY like working code - no DB connection changes in between
            System.Diagnostics.Debug.WriteLine("Setting @outPatient for main report");
            reportDocument.SetParameterValue("@outPatient", outPatientIdInt);
            
            System.Diagnostics.Debug.WriteLine("Setting CompanyName");
            reportDocument.SetParameterValue("CompanyName", companyName);
            
            System.Diagnostics.Debug.WriteLine("Setting CompanyLogo");
            reportDocument.SetParameterValue("CompanyLogo", logoPath);
            
            System.Diagnostics.Debug.WriteLine($"Setting @outPatient for subreport[0]: {reportDocument.Subreports[0].Name}");
            reportDocument.SetParameterValue("@outPatient", outPatientIdInt, reportDocument.Subreports[0].Name);
            
            System.Diagnostics.Debug.WriteLine($"Setting @outPatient for subreport[1]: {reportDocument.Subreports[1].Name}");
            reportDocument.SetParameterValue("@outPatient", outPatientIdInt, reportDocument.Subreports[1].Name);
            
            System.Diagnostics.Debug.WriteLine($"Setting @outPatient for subreport[2]: {reportDocument.Subreports[2].Name}");
            reportDocument.SetParameterValue("@outPatient", outPatientIdInt, reportDocument.Subreports[2].Name);
            
            System.Diagnostics.Debug.WriteLine($"Setting @outPatient for subreport[3]: {reportDocument.Subreports[3].Name}");
            reportDocument.SetParameterValue("@outPatient", outPatientIdInt, reportDocument.Subreports[3].Name);

            System.Diagnostics.Debug.WriteLine("=== OutPatient parameter setting complete ===");
        }


        private string GetCompanyLogoPath()
        {
            try
            {
                string logoPath = ConfigurationManager.AppSettings["CompanyLogoPath"];
                if (!string.IsNullOrEmpty(logoPath))
                {
                    // Convert to absolute path if it's a virtual path
                    if (logoPath.StartsWith("~/"))
                    {
                        logoPath = HttpContext.Current.Server.MapPath(logoPath);
                    }
                    
                    // Verify the file exists before returning
                    if (File.Exists(logoPath))
                    {
                        System.Diagnostics.Debug.WriteLine($"Logo file found at: {logoPath}");
                        return logoPath;
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"Logo file not found at: {logoPath}");
                    }
                }

                // Try to get from database if AppSettings doesn't have it
                string connectionString = ConfigurationManager.ConnectionStrings[ConnectionStringName]?.ConnectionString;
                if (!string.IsNullOrEmpty(connectionString))
                {
                    using (var connection = new System.Data.SqlClient.SqlConnection(connectionString))
                    {
                        connection.Open();
                        using (var command = new System.Data.SqlClient.SqlCommand("SELECT dbo.GetCompanyLogo()", connection))
                        {
                            command.CommandTimeout = 30;
                            var result = command.ExecuteScalar();
                            if (result != null && result != DBNull.Value)
                            {
                                string dbLogoPath = result.ToString();
                                System.Diagnostics.Debug.WriteLine($"Logo path from database: {dbLogoPath}");
                                
                                // Check if it's an absolute path that exists
                                if (File.Exists(dbLogoPath))
                                {
                                    System.Diagnostics.Debug.WriteLine($"Database logo file exists: {dbLogoPath}");
                                    return dbLogoPath;
                                }
                                else
                                {
                                    System.Diagnostics.Debug.WriteLine($"Database logo file NOT found: {dbLogoPath}");
                                    
                                    // Try to convert to relative path from app root
                                    try
                                    {
                                        string appRoot = HttpContext.Current.Server.MapPath("~/");
                                        if (dbLogoPath.StartsWith(appRoot, StringComparison.OrdinalIgnoreCase))
                                        {
                                            string relativePath = "~/" + dbLogoPath.Substring(appRoot.Length).Replace("\\", "/");
                                            string mappedPath = HttpContext.Current.Server.MapPath(relativePath);
                                            if (File.Exists(mappedPath))
                                            {
                                                System.Diagnostics.Debug.WriteLine($"Logo found at mapped path: {mappedPath}");
                                                return mappedPath;
                                            }
                                        }
                                    }
                                    catch (Exception mapEx)
                                    {
                                        System.Diagnostics.Debug.WriteLine($"Error mapping logo path: {mapEx.Message}");
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to get company logo path: {ex.Message}");
            }

            System.Diagnostics.Debug.WriteLine("No valid logo path found, returning empty string");
            return string.Empty;
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

            System.Diagnostics.Debug.WriteLine($"=== Applying Database Connection ===");
            System.Diagnostics.Debug.WriteLine($"Server: {connectionInfo.ServerName}");
            System.Diagnostics.Debug.WriteLine($"Database: {connectionInfo.DatabaseName}");
            System.Diagnostics.Debug.WriteLine($"IntegratedSecurity: {connectionInfo.IntegratedSecurity}");
            System.Diagnostics.Debug.WriteLine($"Main report tables: {reportDocument.Database.Tables.Count}");

            ApplyConnectionToTables(reportDocument.Database.Tables, connectionInfo);

            System.Diagnostics.Debug.WriteLine($"Subreports count: {reportDocument.Subreports.Count}");
            foreach (ReportDocument subreport in reportDocument.Subreports)
            {
                System.Diagnostics.Debug.WriteLine($"Applying connection to subreport: {subreport.Name} (Tables: {subreport.Database.Tables.Count})");
                ApplyConnectionToTables(subreport.Database.Tables, connectionInfo);
            }
            
            System.Diagnostics.Debug.WriteLine("=== Database connection applied to all reports and subreports ===");
        }

        private void ApplyConnectionToTables(Tables tables, ConnectionInfo connectionInfo)
        {
            foreach (Table table in tables)
            {
                System.Diagnostics.Debug.WriteLine($"  Applying connection to table: {table.Name} (Location: {table.Location})");
                TableLogOnInfo tableLogOnInfo = table.LogOnInfo;
                tableLogOnInfo.ConnectionInfo = connectionInfo;
                table.ApplyLogOnInfo(tableLogOnInfo);
            }
        }
    }
}
