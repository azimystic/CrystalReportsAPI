using System.Collections.Generic;

namespace CrystalReportsAPI.Models
{
    public class LabTestInfo
    {
        public string LabTestID { get; set; }
        public string LabTestDetailID { get; set; }
        public string BillDetailsID { get; set; }
        public string TestIDs { get; set; }
        public string TestName { get; set; }
        public string ReportColumn { get; set; }
        public bool IsGroupTest { get; set; }
        public string TransactionID { get; set; }
    }

    public class PatientLabTestsResponse
    {
        public string PatientID { get; set; }
        public List<LabTestInfo> Tests { get; set; }

        public PatientLabTestsResponse()
        {
            Tests = new List<LabTestInfo>();
        }
    }
}
