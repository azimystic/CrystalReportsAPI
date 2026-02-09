using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Http;

namespace CrystalReportsAPI
{
    public static class WebApiConfig
    {
        public static void Register(HttpConfiguration config)
        {
            // Web API configuration and services
            
            // Remove XML formatter to avoid conflicts
            config.Formatters.Remove(config.Formatters.XmlFormatter);
            
            // Ensure JSON formatter doesn't interfere with ByteArrayContent
            var jsonFormatter = config.Formatters.JsonFormatter;
            jsonFormatter.SupportedMediaTypes.Add(new System.Net.Http.Headers.MediaTypeHeaderValue("text/html"));

            // Web API routes
            config.MapHttpAttributeRoutes();

            config.Routes.MapHttpRoute(
                name: "DefaultApi",
                routeTemplate: "api/{controller}/{id}",
                defaults: new { id = RouteParameter.Optional }
            );
        }
    }
}
