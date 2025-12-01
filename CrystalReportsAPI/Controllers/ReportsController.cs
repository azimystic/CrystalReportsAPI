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
            string sanitizedReportName = Path.GetFileNameWithoutExtension(request.ReportName);
            if (string.IsNullOrEmpty(sanitizedReportName) || 
                sanitizedReportName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                return Request.CreateErrorResponse(
                    HttpStatusCode.BadRequest,
                    "Invalid report name.");
            }

            // Construct the report file path
            string reportsFolder = HttpContext.Current.Server.MapPath("~/Reports/");
            string reportPath = Path.Combine(reportsFolder, sanitizedReportName + ".rpt");

            // Verify the file exists
            if (!File.Exists(reportPath))
            {
                return Request.CreateErrorResponse(
                    HttpStatusCode.NotFound,
                    $"Report '{sanitizedReportName}' was not found.");
            }

            ReportDocument reportDocument = null;
            try
            {
                reportDocument = new ReportDocument();
                reportDocument.Load(reportPath);

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
                response.Content.Headers.ContentDisposition = new ContentDispositionHeaderValue("inline")
                {
                    FileName = $"{sanitizedReportName}.pdf"
                };

                return response;
            }
            catch (Exception ex)
            {
                // Log the full exception details server-side (implement proper logging as needed)
                System.Diagnostics.Debug.WriteLine($"Report generation error: {ex}");
                
                return Request.CreateErrorResponse(
                    HttpStatusCode.InternalServerError,
                    "An error occurred while generating the report. Please contact support if the issue persists.");
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
