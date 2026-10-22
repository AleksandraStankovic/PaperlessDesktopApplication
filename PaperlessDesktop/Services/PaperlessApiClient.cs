
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using PaperlessDesktop.Models;


namespace PaperlessDesktop.Services
{
    
    public class PaperlessApiClient
    {

        private readonly HttpClient _http;


        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        };


        public PaperlessApiClient(AppConfig config)
        {
            _http = new HttpClient
            {
                BaseAddress = new Uri(config.ServerUrl.TrimEnd('/') + "/"),
                Timeout = TimeSpan.FromSeconds(20)
            };
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Token", config.ApiToken);

                
            _http.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "application/json; version=2");
        }


        public Task<PagedResult<Document>> GetDocumentsAsync(DocumentQuery query, CancellationToken ct = default)
        => GetAsync<PagedResult<Document>>("api/documents/" + query.ToQueryString(), ct);


        public Task<byte[]> GetThumbnailAsync(int id, CancellationToken ct = default)
    => GetBytesAsync($"api/documents/{id}/thumb/", ct);

        /// <summary>Downloads the document file for preview (usually a PDF). Also returns its type.</summary>
        public async Task<(byte[] Data, string? ContentType)> GetPreviewAsync(int id, CancellationToken ct = default)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"api/documents/{id}/preview/");
            using var response = await SendAsync(request, ct);
            byte[] data = await response.Content.ReadAsByteArrayAsync(ct);
            return (data, response.Content.Headers.ContentType?.MediaType);
        }


        public Task<Document> GetDocumentAsync(int id, CancellationToken ct = default)
            => GetAsync<Document>($"api/documents/{id}/", ct);

        public Task<List<Tag>> GetAllTagsAsync(CancellationToken ct = default)
       => GetAllPagesAsync<Tag>("api/tags/", ct);

        public Task<List<Correspondent>> GetAllCorrespondentsAsync(CancellationToken ct = default)
            => GetAllPagesAsync<Correspondent>("api/correspondents/", ct);

        public Task<List<DocumentType>> GetAllDocumentTypesAsync(CancellationToken ct = default)
            => GetAllPagesAsync<DocumentType>("api/document_types/", ct);



        private async Task<List<T>> GetAllPagesAsync<T>(string path, CancellationToken ct)
        {
            var all = new List<T>();
            int page = 1;

            while (true)
            {
                var result = await GetAsync<PagedResult<T>>($"{path}?page={page}&page_size=100", ct);
                all.AddRange(result.Results);

                if (result.Next is null || result.Results.Count == 0)
                    break;
                page++;
            }
            return all;
        }

        private async Task<T> GetAsync<T>(string relativeUrl, CancellationToken ct)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, relativeUrl);
            using var response = await SendAsync(request, ct);
            return await ReadJsonAsync<T>(response, ct);
        }

        private async Task<byte[]> GetBytesAsync(string relativeUrl, CancellationToken ct)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, relativeUrl);
            using var response = await SendAsync(request, ct);
            return await response.Content.ReadAsByteArrayAsync(ct);
        }

        private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            HttpResponseMessage response;
            try
            {
                response = await _http.SendAsync(request, ct);
            }
            catch (TaskCanceledException) when (!ct.IsCancellationRequested)
            {
                // Cancelled by the HttpClient timeout, not by us
                throw new ApiException(ApiErrorKind.Timeout, "The server did not respond in time.");
            }
            catch (HttpRequestException ex)
            {
                throw new ApiException(ApiErrorKind.Unreachable,
                    "Cannot connect to the server. Check your internet connection.", innerException: ex);
            }

            if (response.IsSuccessStatusCode)
                return response;

            int status = (int)response.StatusCode;
            string body = await response.Content.ReadAsStringAsync(ct);
            response.Dispose();

            var (kind, message) = response.StatusCode switch
            {
                HttpStatusCode.Unauthorized => (ApiErrorKind.Unauthorized, "Invalid API token. Access denied."),
                HttpStatusCode.Forbidden => (ApiErrorKind.Forbidden, "You don't have permission for this action."),
                HttpStatusCode.NotFound => (ApiErrorKind.NotFound, "The requested item was not found."),
                HttpStatusCode.BadRequest => (ApiErrorKind.BadRequest, "The server rejected the request."),
                _ when status >= 500 => (ApiErrorKind.ServerError, "The server encountered an error. Try again later."),
                _ => (ApiErrorKind.ServerError, $"Unexpected server response ({status}).")
            };

            throw new ApiException(kind, message, status, body);
        }



        private static async Task<T> ReadJsonAsync<T>(HttpResponseMessage response, CancellationToken ct)
        {
            try
            {
                await using var stream = await response.Content.ReadAsStreamAsync(ct);
                return await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, ct)
                       ?? throw new ApiException(ApiErrorKind.InvalidResponse, "The server returned an empty response.");
            }
            catch (JsonException ex)
            {
                throw new ApiException(ApiErrorKind.InvalidResponse,
                    "The server returned data in an unexpected format.", innerException: ex);
            }
        }


    }
}
