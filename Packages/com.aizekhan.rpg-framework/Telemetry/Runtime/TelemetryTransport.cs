using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RPGFramework.Telemetry
{
    /// <summary>
    /// What the server answered to a batch: an HTTP status (0 = it was never reached) and its body.
    /// </summary>
    public readonly struct TelemetryResponse
    {
        public TelemetryResponse(int status, string body = null)
        {
            Status = status;
            Body = body ?? "";
        }

        public int Status { get; }
        public string Body { get; }

        public bool Succeeded => Status >= 200 && Status < 300;

        /// <summary>
        /// Worth sending again later: the server was not reached, was busy (429) or failed (5xx). Any other refusal
        /// (a bad key, a malformed batch) would fail again, so the batch is dropped.
        /// </summary>
        public bool Retry => Status == 0 || Status == 408 || Status == 429 || Status >= 500;
    }

    /// <summary>
    /// Sends one batch (<see cref="TelemetryJson.Batch"/>) to the game's collector. Called off the game thread.
    /// </summary>
    public interface ITelemetryTransport
    {
        Task<TelemetryResponse> SendAsync(string json, CancellationToken cancellationToken);
    }

    /// <summary>
    /// Posts batches as JSON to an HTTP endpoint with a bearer key (a write-only key of the game's collector).
    /// .NET's HttpClient: it works in the editor and in standalone and mobile players, not in WebGL.
    /// </summary>
    public sealed class HttpTelemetryTransport : ITelemetryTransport, IDisposable
    {
        private readonly HttpClient _http;
        private readonly Uri _endpoint;

        public HttpTelemetryTransport(string endpoint, string key, TimeSpan? timeout = null)
        {
            if (!Uri.TryCreate(endpoint, UriKind.Absolute, out _endpoint) || (_endpoint.Scheme != "https" && _endpoint.Scheme != "http"))
                throw new ArgumentException("An http(s) address", nameof(endpoint));
            _http = new HttpClient { Timeout = timeout ?? TimeSpan.FromSeconds(20) };
            if (!string.IsNullOrEmpty(key))
                _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", key);
        }

        public async Task<TelemetryResponse> SendAsync(string json, CancellationToken cancellationToken)
        {
            try
            {
                using (var content = new StringContent(json, Encoding.UTF8, "application/json"))
                using (var response = await _http.PostAsync(_endpoint, content, cancellationToken).ConfigureAwait(false))
                {
                    string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    return new TelemetryResponse((int)response.StatusCode, body);
                }
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return new TelemetryResponse(0, "timed out");
            }
            catch (HttpRequestException error)
            {
                return new TelemetryResponse(0, error.Message);
            }
        }

        public void Dispose() => _http.Dispose();
    }
}
