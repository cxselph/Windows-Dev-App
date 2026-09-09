using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

namespace WindowsDevApp.Services;

/// <summary>
/// Validates a GitHub Personal Access Token against the GitHub REST API and
/// tracks the signed-in identity for the current session.
/// </summary>
public class GitHubService
{
    private static readonly HttpClient Http = new();

    public string? Token { get; private set; }
    public string? Login { get; private set; }

    public async Task<(bool Success, string? Login, string? Error)> LoginAsync(string token)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.github.com/user");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Headers.UserAgent.ParseAdd("WindowsDevApp");
            request.Headers.Accept.ParseAdd("application/vnd.github+json");

            using var response = await Http.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                return (false, null, $"{(int)response.StatusCode} {response.ReasonPhrase}");
            }

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var login = doc.RootElement.GetProperty("login").GetString();

            Token = token;
            Login = login;
            return (true, login, null);
        }
        catch (Exception ex)
        {
            return (false, null, ex.Message);
        }
    }

    public void LogOut()
    {
        Token = null;
        Login = null;
    }
}
