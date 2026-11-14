
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using PaperlessDesktop.Models;
using System.Text;
using System.IO;

namespace PaperlessDesktop.Services
{
    
    public class PaperlessApiClient
    {

        private readonly HttpClient _http;
        private readonly HttpClient _uploadHttp;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        };

        public PaperlessApiClient(AppConfig config)
        {
            var baseAddress = new Uri(config.ServerUrl.TrimEnd('/') + "/");
            _http = CreateClient(baseAddress, config.ApiToken, TimeSpan.FromSeconds(20));
            _uploadHttp = CreateClient(baseAddress, config.ApiToken, TimeSpan.FromMinutes(10));
        }

        private static HttpClient CreateClient(Uri baseAddress, string token, TimeSpan timeout)
        {
            var client = new HttpClient { BaseAddress = baseAddress, Timeout = timeout };
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Token", token);

            client.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "application/json; version=2");
            return client;
        }

        public Task<PagedResult<Document>> GetDocumentsAsync(DocumentQuery query, CancellationToken ct = default)
        => GetAsync<PagedResult<Document>>("api/documents/" + query.ToQueryString(), ct);

        public Task<byte[]> GetThumbnailAsync(int id, CancellationToken ct = default)
    => GetBytesAsync($"api/documents/{id}/thumb/", ct);

        public async Task<(byte[] Data, string? ContentType)> GetPreviewAsync(int id, CancellationToken ct = default)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"api/documents/{id}/preview/");
            using var response = await SendAsync(request, ct);
            byte[] data = await response.Content.ReadAsByteArrayAsync(ct);
            return (data, response.Content.Headers.ContentType?.MediaType);
        }

        public Task<PagedResult<Document>> FindDocumentsByChecksumAsync(string md5, CancellationToken ct = default)
            => GetAsync<PagedResult<Document>>(
                $"api/documents/?checksum__iexact={Uri.EscapeDataString(md5)}&page_size=5&truncate_content=true", ct);

        public async Task<string?> GetOriginalChecksumAsync(int id, CancellationToken ct = default)
        {
            var json = await GetAsync<JsonElement>($"api/documents/{id}/metadata/", ct);
            return json.ValueKind == JsonValueKind.Object
                   && json.TryGetProperty("original_checksum", out var checksum)
                   && checksum.ValueKind == JsonValueKind.String
                ? checksum.GetString()
                : null;
        }

        public async Task<string> UploadDocumentAsync(DocumentUploadRequest upload, IProgress<double>? progress,
                                                      CancellationToken ct = default)
        {
            using var form = new MultipartFormDataContent();

            var fileContent = new ProgressStreamContent(File.OpenRead(upload.FilePath), progress);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue(MimeTypeFor(upload.FilePath));
            form.Add(fileContent, "document", Path.GetFileName(upload.FilePath));

            form.Add(new StringContent(upload.Title), "title");
            if (upload.CorrespondentId is int correspondentId)
                form.Add(new StringContent(correspondentId.ToString()), "correspondent");
            if (upload.DocumentTypeId is int documentTypeId)
                form.Add(new StringContent(documentTypeId.ToString()), "document_type");
            foreach (int tagId in upload.TagIds)
                form.Add(new StringContent(tagId.ToString()), "tags");

            using var request = new HttpRequestMessage(HttpMethod.Post, "api/documents/post_document/") { Content = form };
            using var response = await SendAsync(request, ct, _uploadHttp);

            var json = await ReadJsonAsync<JsonElement>(response, ct);
            if (json.ValueKind == JsonValueKind.String)
                return json.GetString()!;
            if (json.ValueKind == JsonValueKind.Object && json.TryGetProperty("task_id", out var id)
                && id.ValueKind == JsonValueKind.String)
                return id.GetString()!;

            throw new ApiException(ApiErrorKind.InvalidResponse, "The server did not return a task ID.");
        }

        public async Task<PaperlessTask?> GetTaskAsync(string taskId, CancellationToken ct = default)
        {
            var json = await GetAsync<JsonElement>($"api/tasks/?task_id={Uri.EscapeDataString(taskId)}", ct);

            JsonElement list = json.ValueKind == JsonValueKind.Object && json.TryGetProperty("results", out var results)
                ? results
                : json;

            if (list.ValueKind != JsonValueKind.Array || list.GetArrayLength() == 0)
                return null;

            try
            {
                return list[0].Deserialize<PaperlessTask>(JsonOptions);
            }
            catch (JsonException ex)
            {
                throw new ApiException(ApiErrorKind.InvalidResponse,
                    "The server returned data in an unexpected format.", innerException: ex);
            }
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

        private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct,
                                                          HttpClient? client = null)
        {
            HttpResponseMessage response;
            try
            {
                response = await (client ?? _http).SendAsync(request, ct);
            }
            catch (TaskCanceledException) when (!ct.IsCancellationRequested)
            {
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

        public Task<Tag> CreateTagAsync(string name, string color, CancellationToken ct = default)
            => SendJsonAsync<Tag>(HttpMethod.Post, "api/tags/", new TagSaveRequest(name, color), ct);

        public Task<Tag> UpdateTagAsync(int id, string name, string color, CancellationToken ct = default)
            => SendJsonAsync<Tag>(HttpMethod.Patch, $"api/tags/{id}/", new TagSaveRequest(name, color), ct);

        public async Task DeleteTagAsync(int id, CancellationToken ct = default)
        {
            using var request = new HttpRequestMessage(HttpMethod.Delete, $"api/tags/{id}/");
            using var response = await SendAsync(request, ct);
        }

        private record TagSaveRequest(string Name, string Color);

        public Task<Document> UpdateDocumentTagsAsync(int id, IReadOnlyList<int> tagIds, CancellationToken ct = default)
            => SendJsonAsync<Document>(HttpMethod.Patch, $"api/documents/{id}/", new DocumentTagsRequest(tagIds), ct);

        private record DocumentTagsRequest(IReadOnlyList<int> Tags);

        public async Task DeleteDocumentAsync(int id, CancellationToken ct = default)
        {
            using var request = new HttpRequestMessage(HttpMethod.Delete, $"api/documents/{id}/");
            using var response = await SendAsync(request, ct);
        }

        private async Task<T> SendJsonAsync<T>(HttpMethod method, string relativeUrl, object body, CancellationToken ct)
        {
            string json = JsonSerializer.Serialize(body, JsonOptions);
            using var request = new HttpRequestMessage(method, relativeUrl)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            using var response = await SendAsync(request, ct);
            return await ReadJsonAsync<T>(response, ct);
        }

        private static string MimeTypeFor(string path) => Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".pdf" => "application/pdf",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".tif" or ".tiff" => "image/tiff",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".txt" => "text/plain",
            _ => "application/octet-stream"
        };
    }
}
