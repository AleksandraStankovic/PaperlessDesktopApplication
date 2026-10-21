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

    }
}
