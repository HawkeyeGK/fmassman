using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace fmassman.Client.Security;

public class SwaAuthenticationStateProvider : AuthenticationStateProvider
{
    private readonly HttpClient _httpClient;

    public SwaAuthenticationStateProvider(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        try
        {
            var authData = await _httpClient.GetFromJsonAsync<AuthenticationData>("/.auth/me");

            if (authData == null || authData.ClientPrincipal == null)
            {
                return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
            }

            var principal = authData.ClientPrincipal;
            var identity = new ClaimsIdentity(principal.IdentityProvider);

            if (principal.UserDetails != null)
            {
                identity.AddClaim(new Claim(ClaimTypes.Name, principal.UserDetails));
            }

            if (principal.UserRoles != null)
            {
                foreach (var role in principal.UserRoles)
                {
                    identity.AddClaim(new Claim(ClaimTypes.Role, role));
                }
            }

            return new AuthenticationState(new ClaimsPrincipal(identity));
        }
        catch
        {
            // If fetching fails, treat as anonymous
            return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
        }
    }

    public class AuthenticationData
    {
        public ClientPrincipal? ClientPrincipal { get; set; }
    }

    public class ClientPrincipal
    {
        public string? IdentityProvider { get; set; }
        public string? UserId { get; set; }
        public string? UserDetails { get; set; }
        public IEnumerable<string> UserRoles { get; set; } = new List<string>();
    }
}
