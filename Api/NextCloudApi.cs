using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using KSol.NextCloudMailToFolder.Data;
using Microsoft.EntityFrameworkCore;

namespace KSol.NextCloudMailToFolder.Api
{
    public class NextCloudApi : IDisposable
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;

        public NextCloudApi(ApplicationDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        public async Task<bool> UploadFileAsync(string userId, Stream contentStream, string contentType, string destinationPath)
        {
            var httpClient = await GetHttpClientAsync(userId);

            using var content = new StreamContent(contentStream);
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
            

            var request = new HttpRequestMessage(HttpMethod.Put, $"{_configuration["NextCloud:FileEndpoint"]?.TrimEnd('/')}/{destinationPath}")
            {
                Content = content
            };

            var response = await httpClient.SendAsync(request);
            return response.IsSuccessStatusCode;
        }

        private async Task<HttpClient> GetHttpClientAsync(string userId)
        {
            await EnsureTokenIsValidAsync(userId);
            var user = await _context.NextCloudUsers.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null)
            {
                throw new Exception("User not found.");
            }
            HttpClient httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", user.Token);

            return httpClient;
        }

        private async Task EnsureTokenIsValidAsync(string userId)
        {
            if (!await IsTokenValidAsync(userId))
            {
                var response = await RefreshTokenAsync(userId);
                var user = await _context.NextCloudUsers.FirstOrDefaultAsync(u => u.Id == userId);
                if (user == null)
                {
                    throw new Exception("User not found.");
                }
                user.Token = response.AccessToken;
                user.RefreshToken = response.RefreshToken;
                user.TokenExpiration = DateTime.UtcNow.AddSeconds(response.ExpiresIn);
                _context.NextCloudUsers.Update(user);
                await _context.SaveChangesAsync();
            }
        }

        private async Task<bool> IsTokenValidAsync(string userId)
        {
            var user = await _context.NextCloudUsers.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null)
            {
                throw new Exception("User not found.");
            }
            if (user.TokenExpiration < DateTime.UtcNow)
            {
                return false;
            }
            return true;
        }

        private async Task<TokenResponse> RefreshTokenAsync(string userId)
        {
            var user = await _context.NextCloudUsers.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null)
            {
                throw new Exception("User not found.");
            }
            var httpClient = new HttpClient();
            var tokenEndpoint = _configuration["NextCloud:TokenEndpoint"];

            var refreshQuery = new Dictionary<string, string>
            {
                { "grant_type", "refresh_token" },
                { "refresh_token", user.RefreshToken },
                { "client_id", _configuration["NextCloud:ClientId"] },
                { "client_secret", _configuration["NextCloud:ClientSecret"] }
            };

            var response = await httpClient.PostAsync(tokenEndpoint, new FormUrlEncodedContent(refreshQuery));

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception("Failed to refresh token.");
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            var tokenResponse = JsonSerializer.Deserialize<TokenResponse>(responseContent);

            if (tokenResponse == null || string.IsNullOrEmpty(tokenResponse.AccessToken))
            {
                throw new Exception("Invalid token response.");
            }

            return tokenResponse;
        }

        public void Dispose()
        {
            _context?.Dispose();
        }

        private class TokenResponse
        {
            [JsonPropertyName("access_token")]
            public string AccessToken { get; set; }

            [JsonPropertyName("token_type")]
            public string TokenType { get; set; }

            [JsonPropertyName("expires_in")]
            public int ExpiresIn { get; set; }

            [JsonPropertyName("refresh_token")]
            public string RefreshToken { get; set; }

            [JsonPropertyName("scope")]
            public string Scope { get; set; }
        }
    }
}