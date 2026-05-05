using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using CrystalReportsAPI.Models;

namespace CrystalReportsAPI.Helpers
{
    public class LabTestHelper
    {
        private const string ConnectionStringName = "ASPGLConnectionString";

        /// <summary>
        /// Gets all lab test information for a given patient ID
        /// </summary>
        /// <param name="patientID">The patient ID</param>
        /// <returns>List of lab test information</returns>
        public static PatientLabTestsResponse GetPatientLabTests(string mrNoSequence)
        {
            if (string.IsNullOrWhiteSpace(mrNoSequence))
            {
                throw new ArgumentException("MR no sequence is required.");
            }

            var response = new PatientLabTestsResponse();

            string connectionString = ConfigurationManager.ConnectionStrings[ConnectionStringName]?.ConnectionString;
            if (string.IsNullOrEmpty(connectionString))
            {
                throw new InvalidOperationException($"Connection string '{ConnectionStringName}' is not configured.");
            }

            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();

                var patientId = GetPatientIdByMrNoSequence(connection, mrNoSequence);
                if (string.IsNullOrEmpty(patientId))
                {
                    return response;
                }

                response.PatientID = patientId;

                // Get individual tests (non-group tests)
                var individualTests = GetIndividualTests(connection, patientId);
                response.Tests.AddRange(individualTests);

                // Get group tests
                var groupTests = GetGroupTests(connection, patientId);
                response.Tests.AddRange(groupTests);
            }

            return response;
        }

        public static string GetMrNoSequenceByMobile(string mobileNumber)
        {
            if (string.IsNullOrWhiteSpace(mobileNumber))
            {
                throw new ArgumentException("Mobile number is required.");
            }

            string connectionString = ConfigurationManager.ConnectionStrings[ConnectionStringName]?.ConnectionString;
            if (string.IsNullOrEmpty(connectionString))
            {
                throw new InvalidOperationException($"Connection string '{ConnectionStringName}' is not configured.");
            }

            using (var connection = new SqlConnection(connectionString))
            using (var command = new SqlCommand("SELECT mr_no_sequence FROM Reg_patient WHERE mobile1 = @MobileNumber", connection))
            {
                command.Parameters.AddWithValue("@MobileNumber", mobileNumber);
                connection.Open();
                var result = command.ExecuteScalar();
                return result?.ToString() ?? string.Empty;
            }
        }

        private static List<LabTestInfo> GetIndividualTests(SqlConnection connection, string patientID)
        {
            var tests = new List<LabTestInfo>();

            string query = @"
                SELECT DISTINCT
                    lt.Lab_Test_ID,
                    ltd.Lab_Test_Detail_ID,
                    ltd.Bill_Details_ID,
                    ltd.Test_ID,
                    tdef.Report_Colum,
                    lt.Parent_Transaction_Type_ID,
                    bd.Is_Group_Category,
                    tdef.Test_Name
                FROM Laboratory_Test lt
                JOIN Lab_Test_Detail ltd ON lt.Lab_Test_ID = ltd.Lab_Test_ID
                JOIN Lab_Tests tdef ON ltd.Test_ID = tdef.Test_ID
                JOIN Reg_patient pa ON lt.Patient_ID = pa.pat_id
                JOIN Ar_Billing_Details bd ON ltd.Bill_Details_ID = bd.Bill_Details_ID
                WHERE pa.pat_id = @PatientID
                    AND ISNULL(bd.Is_Group_Category, 0) = 0
                    AND ISNULL(ltd.Is_Result, 0) = 1
                ORDER BY lt.Lab_Test_ID, ltd.Lab_Test_Detail_ID";

            using (var command = new SqlCommand(query, connection))
            {
                command.Parameters.AddWithValue("@PatientID", patientID);

                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        tests.Add(new LabTestInfo
                        {
                            LabTestID = reader["Lab_Test_ID"]?.ToString() ?? "",
                            LabTestDetailID = reader["Lab_Test_Detail_ID"]?.ToString() ?? "",
                            BillDetailsID = reader["Bill_Details_ID"]?.ToString() ?? "",
                            TestIDs = reader["Test_ID"]?.ToString() ?? "",
                            TestName = reader["Test_Name"]?.ToString() ?? "",
                            ReportColumn = reader["Report_Colum"]?.ToString() ?? "4",
                            IsGroupTest = false,
                            TransactionID = reader["Parent_Transaction_Type_ID"]?.ToString() ?? ""
                        });
                    }
                }
            }

            return tests;
        }

        private static List<LabTestInfo> GetGroupTests(SqlConnection connection, string patientID)
        {
            var tests = new List<LabTestInfo>();

            // First get the patient number
            string patientNo = GetPatientNumber(connection, patientID);
            if (string.IsNullOrEmpty(patientNo))
            {
                return tests;
            }

            string query = @"
                SELECT DISTINCT
                    ltm.Lab_Test_ID,
                    MIN(bd.Bill_Details_ID) AS Bill_Details_ID,
                    lt.Group_Category,
                    dv.Segment2 AS Report_Colum,
                    ltm.Parent_Transaction_Type_ID,
                    dbo.GetDataValueName('Test Groups', lt.Group_Category) AS Group_Test_Name
                FROM Lab_Test_Detail ld
                JOIN Lab_Tests lt ON lt.Test_ID = ld.Test_ID
                JOIN Laboratory_Test ltm ON ltm.Lab_Test_ID = ld.Lab_Test_ID
                JOIN AR_Billing_Details bd ON bd.Bill_Details_ID = ld.Bill_Details_ID
                JOIN Reg_patient pat ON ld.Patient_ID = pat.pat_id
                LEFT JOIN Datavalues dv ON dv.Datavalue_Name = lt.Group_Category AND dv.ValueSet_ID = 70
                WHERE ISNULL(bd.Is_Group_Category, 0) = 1
                    AND pat.Patient_No = @PatientNo
                    AND ISNULL(ld.Is_Result, 0) = 1
                GROUP BY ltm.Lab_Test_ID, lt.Group_Category, dv.Segment2, ltm.Parent_Transaction_Type_ID
                ORDER BY ltm.Lab_Test_ID";

            using (var command = new SqlCommand(query, connection))
            {
                command.Parameters.AddWithValue("@PatientNo", patientNo);

                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        string groupCategory = reader["Group_Category"]?.ToString() ?? "";

                        // Get sub-test IDs for this group
                        string subTestIDs = "";
                        if (!string.IsNullOrEmpty(groupCategory))
                        {
                            // We'll get this after closing the current reader
                            subTestIDs = groupCategory; // Placeholder, will be replaced
                        }

                        tests.Add(new LabTestInfo
                        {
                            LabTestID = reader["Lab_Test_ID"]?.ToString() ?? "",
                            LabTestDetailID = null, // Null for group tests
                            BillDetailsID = reader["Bill_Details_ID"]?.ToString() ?? "",
                            TestIDs = subTestIDs,
                            TestName = reader["Group_Test_Name"]?.ToString() ?? "",
                            ReportColumn = reader["Report_Colum"]?.ToString() ?? "4",
                            IsGroupTest = true,
                            TransactionID = reader["Parent_Transaction_Type_ID"]?.ToString() ?? ""
                        });
                    }
                }
            }

            // Now get sub-test IDs for each group test
            foreach (var test in tests)
            {
                if (test.IsGroupTest && !string.IsNullOrEmpty(test.TestIDs))
                {
                    test.TestIDs = GetGroupSubTests(connection, test.TestIDs);
                }
            }

            return tests;
        }

        private static string GetPatientIdByMrNoSequence(SqlConnection connection, string mrNoSequence)
        {
            string query = "SELECT pat_id FROM Reg_patient WHERE mr_no_sequence = @MrNoSequence";

            using (var command = new SqlCommand(query, connection))
            {
                command.Parameters.AddWithValue("@MrNoSequence", mrNoSequence);
                var result = command.ExecuteScalar();
                return result?.ToString() ?? "";
            }
        }

        private static string GetPatientNumber(SqlConnection connection, string patientID)
        {
            string query = "SELECT Patient_No FROM Reg_patient WHERE pat_id = @PatientID";
            
            using (var command = new SqlCommand(query, connection))
            {
                command.Parameters.AddWithValue("@PatientID", patientID);
                var result = command.ExecuteScalar();
                return result?.ToString() ?? "";
            }
        }

        private static string GetGroupSubTests(SqlConnection connection, string groupCategory)
        {
            string query = "SELECT dbo.GetGroupSubTests(@GroupCategory)";
            
            using (var command = new SqlCommand(query, connection))
            {
                command.Parameters.AddWithValue("@GroupCategory", groupCategory);
                var result = command.ExecuteScalar();
                return result?.ToString() ?? "";
            }
        }
    }
}
