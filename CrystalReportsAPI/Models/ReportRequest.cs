using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace CrystalReportsAPI.Models
{
    public class ReportRequest
    {
        [Required]
        public string ReportName { get; set; }

        public string ReportTitle { get; set; }

        public int ReportColumn { get; set; } = 4;

        public string TestIDs { get; set; }

        public string PatientID { get; set; }

        public string TransID { get; set; }

        public string BillDetailID { get; set; }

        public string LabTestDetailID { get; set; }

        public bool IncludeLetterhead { get; set; } = true;

        public bool ExportToExcel { get; set; } = false;

        public List<ReportParameter> Parameters { get; set; }

        public ReportRequest()
        {
            Parameters = new List<ReportParameter>();
        }
    }
}