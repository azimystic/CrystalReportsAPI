using System;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http.Controllers;
using System.Web.Http.Filters;

namespace CrystalReportsAPI.Filters
{
    /// <summary>
    /// Authorization filter that validates API key from the X-API-Key header.
    /// Apply this attribute to controllers or actions that require API key authentication.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
    public class ApiKeyAuthorizationFilter : AuthorizationFilterAttribute
    {
        private const string ApiKeyHeaderName = "X-API-Key";
        private const string ApiKeyConfigKey = "ReportServiceApiKey";

        /// <summary>
        /// Validates the API key from the request header against the configured value.
        /// </summary>
        /// <param name="actionContext">The action context.</param>
        public override void OnAuthorization(HttpActionContext actionContext)
        {
            // Get the expected API key from configuration
            string expectedApiKey = ConfigurationManager.AppSettings[ApiKeyConfigKey];

            if (string.IsNullOrEmpty(expectedApiKey))
            {
                actionContext.Response = actionContext.Request.CreateErrorResponse(
                    HttpStatusCode.InternalServerError,
                    "API key is not configured on the server.");
                return;
            }

            // Check if the header exists
            if (!actionContext.Request.Headers.Contains(ApiKeyHeaderName))
            {
                actionContext.Response = actionContext.Request.CreateErrorResponse(
                    HttpStatusCode.Unauthorized,
                    "API key is required. Please provide the X-API-Key header.");
                return;
            }

            // Get the provided API key
            string providedApiKey = actionContext.Request.Headers.GetValues(ApiKeyHeaderName).FirstOrDefault();

            // Validate the API key using constant-time comparison to prevent timing attacks
            if (!SecureCompare(expectedApiKey, providedApiKey))
            {
                actionContext.Response = actionContext.Request.CreateErrorResponse(
                    HttpStatusCode.Unauthorized,
                    "Invalid API key.");
                return;
            }

            base.OnAuthorization(actionContext);
        }

        /// <summary>
        /// Performs a constant-time comparison of two strings to prevent timing attacks.
        /// </summary>
        private static bool SecureCompare(string expected, string provided)
        {
            if (provided == null)
                return false;

            if (expected.Length != provided.Length)
                return false;

            int result = 0;
            for (int i = 0; i < expected.Length; i++)
            {
                result |= expected[i] ^ provided[i];
            }

            return result == 0;
        }
    }
}
