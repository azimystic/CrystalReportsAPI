using System.ComponentModel.DataAnnotations;

namespace CrystalReportsAPI.Models
{
    /// <summary>
    /// Represents a single parameter to be applied to a Crystal Report.
    /// </summary>
     public class ReportParameter
    {
        /// <summary>
        /// Parameter name as defined in the Crystal Report.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Parameter value. 
        /// </summary>
        public object Value { get; set; }
    }
}
