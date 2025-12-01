using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace CrystalReportsAPI.Models
{
    /// <summary>
    /// Request model for generating a Crystal Report.
    /// </summary>
    public class ReportRequest
    {
        /// <summary>
        /// The name of the report file (without extension). 
        /// The report file should be located in the ~/Reports/ folder.
        /// Example: "Invoice" for Invoice.rpt
        /// </summary>
        [Required]
        public string ReportName { get; set; }

        /// <summary>
        /// Optional list of parameters to apply to the report.
        /// </summary>
        public List<ReportParameter> Parameters { get; set; }

        /// <summary>
        /// Initializes a new instance of ReportRequest with an empty parameters list.
        /// </summary>
        public ReportRequest()
        {
            Parameters = new List<ReportParameter>();
        }
    }
}
