using System.Web.Http;
using Swashbuckle.Application;

namespace CrystalReportsAPI
{
    /// <summary>
    /// Swagger configuration for the Crystal Reports API.
    /// </summary>
    public class SwaggerConfig
    {
        /// <summary>
        /// Registers Swagger configuration for the Web API.
        /// </summary>
        public static void Register(HttpConfiguration config)
        {
            config
                .EnableSwagger(c =>
                {
                    c.SingleApiVersion("v1", "Crystal Reports API")
                        .Description("API for generating Crystal Reports as PDF documents.")
                        .Contact(cc => cc
                            .Name("Crystal Reports API Support"));

                    // Include XML comments if available
                    var xmlCommentsPath = GetXmlCommentsPath();
                    if (System.IO.File.Exists(xmlCommentsPath))
                    {
                        c.IncludeXmlComments(xmlCommentsPath);
                    }

                    // Configure the API Key security definition
                    c.ApiKey("X-API-Key")
                        .Description("API Key Authentication. Enter your API key in the value field.")
                        .Name("X-API-Key")
                        .In("header");
                })
                .EnableSwaggerUi(c =>
                {
                    // Enable API key support in the UI
                    c.EnableApiKeySupport("X-API-Key", "header");
                });
        }

        /// <summary>
        /// Gets the path to the XML documentation file.
        /// </summary>
        private static string GetXmlCommentsPath()
        {
            return System.String.Format(@"{0}\bin\CrystalReportsAPI.xml",
                System.AppDomain.CurrentDomain.BaseDirectory);
        }
    }
}
