using System.ComponentModel.DataAnnotations;

namespace CrystalReportsAPI.Models
{
    /// <summary>
    /// Represents a single parameter to be applied to a Crystal Report.
    /// </summary>
    public class ReportParameter
    {
        /// <summary>
        /// The name of the parameter as defined in the Crystal Report.
        /// </summary>
        [Required]
        public string Name { get; set; }

        /// <summary>
        /// The value to set for the parameter.
        /// </summary>
        public object Value { get; set; }
    }
}
