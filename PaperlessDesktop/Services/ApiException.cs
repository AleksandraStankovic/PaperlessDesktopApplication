using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PaperlessDesktop.Services
{



    public enum ApiErrorKind
    {
        Unreachable,      
        Timeout,
        Unauthorized,     
        Forbidden,        
        NotFound,       
        BadRequest,       
        ServerError,      
        InvalidResponse   
    }


    internal class ApiException : Exception
    {
        public ApiErrorKind Kind { get; }
        public int? StatusCode { get; }
        public string? ResponseBody { get; }

        public ApiException(ApiErrorKind kind, string message, int? statusCode = null,
                    string? responseBody = null, Exception? innerException = null)
    : base(message, innerException)
        {
            Kind = kind;
            StatusCode = statusCode;
            ResponseBody = responseBody;
        }


        /// <summary>
        /// For 400 errors, Paperless explains what's wrong, e.g. {"name": ["Tag with this name already exists."]}.
        /// Returns the first message, or null.
        /// </summary>
        public string? TryGetServerMessage()
        {
            if (string.IsNullOrWhiteSpace(ResponseBody))
                return null;
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(ResponseBody);
                if (doc.RootElement.ValueKind != System.Text.Json.JsonValueKind.Object)
                    return null;

                foreach (var property in doc.RootElement.EnumerateObject())
                {
                    var value = property.Value;
                    if (value.ValueKind == System.Text.Json.JsonValueKind.Array
                        && value.GetArrayLength() > 0
                        && value[0].ValueKind == System.Text.Json.JsonValueKind.String)
                        return value[0].GetString();

                    if (value.ValueKind == System.Text.Json.JsonValueKind.String)
                        return value.GetString();
                }
            }
            catch (System.Text.Json.JsonException)
            {
            }
            return null;
        }

    }
}
