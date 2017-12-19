using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace OSIResearch.Qillion.Functions
{
    class QiClientSlim
    {
        public QiClientSlim()
        {
            BaseUrl = ConfigurationManager.AppSettings["QiUrl"];
            TenantId = ConfigurationManager.AppSettings["QiAccountId"];
            ClientId = ConfigurationManager.AppSettings["QiClientId"];
            ClientSecret = ConfigurationManager.AppSettings["QiClientSecret"];
            Resource = ConfigurationManager.AppSettings["QiResource"];

            _httpClientHandler = new HttpClientHandler
            {
                AllowAutoRedirect = false
            };

            _httpClient = new HttpClient(_httpClientHandler);
            
            if (BaseUrl.Substring(BaseUrl.Length - 1).CompareTo(@"/") != 0)
            {
                BaseUrl = BaseUrl + "/";
            }

            _httpClient.BaseAddress = new Uri(BaseUrl);
            _httpClient.Timeout = new TimeSpan(0, 5, 0);
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            _authenticationContext = new AuthenticationContext($"https://login.windows.net/{TenantId}");

            AuthenticationResult = AuthenticateAsync().ConfigureAwait(false).GetAwaiter().GetResult(); //eww
        }

        public string BaseUrl { get; private set; }
        public string TenantId { get; private set; }
        public string ClientId { get; private set; }
        public string ClientSecret { get; private set; }
        public string Resource { get; private set; }

        protected AuthenticationResult AuthenticationResult { get; private set; }

        private AuthenticationContext _authenticationContext;
        private HttpClient _httpClient;
        private HttpClientHandler _httpClientHandler;

        public async Task<string> GetLatestValueJsonAsync(string namespaceId, string streamId)
        {
            string path = $"/api/Tenants/{TenantId}/Namespaces/{namespaceId}/Streams/{streamId}/Data/GetLastValue";
            return await GetQiObjectJsonAsync(path);
        }

        public async Task<List<string>> GetQiStreamsAsync(string namespaceId, string filter)
        {
            string path = $"/api/Tenants/{TenantId}/Namespaces/{namespaceId}/Streams";
            Dictionary<string, string> query = new Dictionary<string, string>() { { "query", filter }, { "count", int.MaxValue.ToString() } };
            string qiStreamsJson = await GetQiObjectJsonAsync(path, query);
            var stringPrototype = new[] { new { Id = "" } };
            var streams = JsonConvert.DeserializeAnonymousType(qiStreamsJson, stringPrototype);
            return streams.Select(s => s.Id).ToList();
        }

        protected async Task<string> GetQiObjectJsonAsync(string path, IEnumerable<KeyValuePair<string, string>> query = null)
        {
            if (query?.Any() == true)
            {
                using (var content = new FormUrlEncodedContent(query))
                {
                    string querystring = content.ReadAsStringAsync().Result;
                    path += $"?{querystring}";
                }
            }

            
            HttpRequestMessage request = new HttpRequestMessage
            {
                RequestUri = new Uri(path, UriKind.Relative),
                Method = HttpMethod.Get
            };

            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", AuthenticationResult.AccessToken);

            HttpResponseMessage response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode) return null;
            
            return await response.Content.ReadAsStringAsync();
        }

        protected async Task<AuthenticationResult> AuthenticateAsync()
        {
            // tokens expire after a certain period of time
            // You can check this with the ExpiresOn property of AuthenticationResult, but it is not necessary.
            // ADAL maintains an in-memory cache of tokens and transparently refreshes them as needed
            ClientCredential clientCredential = new ClientCredential(ClientId, ClientSecret);
            return await _authenticationContext.AcquireTokenAsync(Resource, clientCredential);
        }
    }
}
