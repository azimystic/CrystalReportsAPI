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
    /// <summary>
    /// Controller for generating Crystal Reports and returning them as PDF documents.
    /// </summary>
    [RoutePrefix("api/reports")]
    public class ReportsController : ApiController
    {
        private const string ConnectionStringName = "ASPGLConnectionString";

        /// <summary>
        /// Generates a Crystal Report and returns it as a PDF.
        /// </summary>
        /// <param name="request">The report request containing report name and parameters.</param>
        /// <returns>PDF file as a byte stream with application/pdf Content-Type.</returns>
        /// <response code="200">Returns the generated PDF report.</response>
        /// <response code="400">Bad request - invalid report name or parameters.</response>
        /// <response code="401">Unauthorized - missing or invalid API key.</response>
        /// <response code="404">Report file not found.</response>
        /// <response code="500">Internal server error during report generation.</response>
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

            // Sanitize the report name to prevent path traversal
            // Support subdirectories (e.g., "LabRadiology/rptLabTestUiltraSound_1")
            
            // Validate that the path doesn't contain suspicious patterns (before normalization)
            if (request.ReportName.Contains("..") || 
                request.ReportName.Contains(":") ||
                request.ReportName.StartsWith("/") || 
                request.ReportName.StartsWith("\\"))
            {
                return Request.CreateErrorResponse(
                    HttpStatusCode.BadRequest,
                    "Invalid report name: path traversal not allowed.");
            }
            
            // Normalize path separators after validation
            string reportName = request.ReportName.Replace('\\', '/');

            // Split into directory and file parts
            string[] pathParts = reportName.Split('/');
            foreach (string part in pathParts)
            {
                if (string.IsNullOrWhiteSpace(part) || 
                    part.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                {
                    return Request.CreateErrorResponse(
                        HttpStatusCode.BadRequest,
                        "Invalid report name: contains invalid characters.");
                }
            }

            // Construct the report file path
            string reportsFolder = HttpContext.Current.Server.MapPath("~/Reports/");
            string reportPath = Path.Combine(reportsFolder, reportName.TrimEnd('/'));
            
            // Add .rpt extension if not already present
            if (!reportPath.EndsWith(".rpt", StringComparison.OrdinalIgnoreCase))
            {
                reportPath += ".rpt";
            }

            // Ensure the resolved path is still within the Reports folder (security check)
            string fullReportPath = Path.GetFullPath(reportPath);
            string fullReportsFolder = Path.GetFullPath(reportsFolder);
            if (!fullReportPath.StartsWith(fullReportsFolder, StringComparison.OrdinalIgnoreCase))
            {
                return Request.CreateErrorResponse(
                    HttpStatusCode.BadRequest,
                    "Invalid report name: path must be within Reports folder.");
            }

            // Verify the file exists
            if (!File.Exists(fullReportPath))
            {
                return Request.CreateErrorResponse(
                    HttpStatusCode.NotFound,
                    $"Report '{reportName}' was not found.");
            }

            ReportDocument reportDocument = null;
            try
            {
                reportDocument = new ReportDocument();
                reportDocument.Load(fullReportPath);
                
                // Verify database to ensure report is properly initialized
                // This helps prevent "The document has not been opened" errors
                reportDocument.VerifyDatabase();

                // Apply database connection
                ApplyDatabaseConnection(reportDocument);

                // Apply parameters if provided
                if (request.Parameters != null && request.Parameters.Count > 0)
                {
                    ApplyParameters(reportDocument, request);
                }

                // Export to PDF
                byte[] pdfBytes;
                using (var stream = reportDocument.ExportToStream(ExportFormatType.PortableDocFormat))
                {
                    using (var memoryStream = new MemoryStream())
                    {
                        stream.CopyTo(memoryStream);
                        pdfBytes = memoryStream.ToArray();
                    }
                }

                // Create the response with PDF content
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(pdfBytes)
                };
                response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
                
                // Use the last part of the report name (file name without path) for the PDF filename
                string pdfFileName = Path.GetFileNameWithoutExtension(Path.GetFileName(reportName)) + ".pdf";
                response.Content.Headers.ContentDisposition = new ContentDispositionHeaderValue("inline")
                {
                    FileName = pdfFileName
                };

                return response;
            }
            catch (Exception ex)
            {
                // Log the full exception details server-side (implement proper logging as needed)
                System.Diagnostics.Debug.WriteLine($"Report generation error: {ex}");
                System.Diagnostics.Debug.WriteLine($"Report Path: {fullReportPath}");
                System.Diagnostics.Debug.WriteLine($"Is64BitProcess: {Environment.Is64BitProcess}");
                
                // Build error message for client (don't expose full file system paths)
                string errorMessage = $"Crystal Reports error: {ex.Message}";
                if (ex.InnerException != null)
                {
                    errorMessage += $" Inner: {ex.InnerException.Message}";
                }
                errorMessage += $" Report: {reportName}";
                errorMessage += $" Is64BitProcess: {Environment.Is64BitProcess}";
                
                return Request.CreateErrorResponse(
                    HttpStatusCode.InternalServerError,
                    errorMessage);
            }
            finally
            {
                if (reportDocument != null)
                {
                    reportDocument.Close();
                    reportDocument.Dispose();
                }
            }
        }

        /// <summary>
        /// Applies the database connection string to all tables in the report.
        /// </summary>
        private void ApplyDatabaseConnection(ReportDocument reportDocument)
        {
            string connectionString = ConfigurationManager.ConnectionStrings[ConnectionStringName]?.ConnectionString;

            if (string.IsNullOrEmpty(connectionString))
            {
                throw new InvalidOperationException($"Connection string '{ConnectionStringName}' is not configured.");
            }

            // Parse the connection string
            var builder = new System.Data.SqlClient.SqlConnectionStringBuilder(connectionString);

            ConnectionInfo connectionInfo = new ConnectionInfo
            {
                ServerName = builder.DataSource,
                DatabaseName = builder.InitialCatalog,
                IntegratedSecurity = builder.IntegratedSecurity
            };

            // Only set UserID and Password when not using integrated security
            if (!builder.IntegratedSecurity)
            {
                connectionInfo.UserID = builder.UserID;
                connectionInfo.Password = builder.Password;
            }

            // Apply connection info to all tables in the main report
            ApplyConnectionToTables(reportDocument.Database.Tables, connectionInfo);

            // Apply connection info to subreports
            foreach (ReportDocument subreport in reportDocument.Subreports)
            {
                ApplyConnectionToTables(subreport.Database.Tables, connectionInfo);
            }
        }

        /// <summary>
        /// Applies connection info to a collection of tables.
        /// </summary>
        private void ApplyConnectionToTables(Tables tables, ConnectionInfo connectionInfo)
        {
            foreach (Table table in tables)
            {
                TableLogOnInfo tableLogOnInfo = table.LogOnInfo;
                tableLogOnInfo.ConnectionInfo = connectionInfo;
                table.ApplyLogOnInfo(tableLogOnInfo);
            }
        }

        /// <summary>
        /// Applies parameters to the report.
        /// </summary>
        private void ApplyParameters(ReportDocument reportDocument, ReportRequest request)
        {
            foreach (var param in request.Parameters)
            {
                if (!string.IsNullOrEmpty(param.Name))
                {
                    try
                    {
                        reportDocument.SetParameterValue(param.Name, param.Value);
                    }
                    catch (Exception ex)
                    {
                        throw new ArgumentException($"Failed to set parameter '{param.Name}': {ex.Message}", ex);
                    }
                }
            }
        }
    }
}
