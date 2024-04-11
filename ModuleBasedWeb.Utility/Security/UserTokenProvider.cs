using Microsoft.Identity.Web;
using Microsoft.Kiota.Abstractions.Authentication;
using System.Security.Claims;

namespace ModuleBasedWeb.Utility.Security
{
    public class UserTokenProvider : IAccessTokenProvider
    {
        private readonly ITokenAcquisition _tokenAcquisition;
        private readonly ClaimsPrincipal _principal;
        private readonly IEnumerable<string> _scopes;

        public UserTokenProvider(ITokenAcquisition tokenAcquisition, ClaimsPrincipal principal, IEnumerable<string> scopes)
        {
            _tokenAcquisition = tokenAcquisition;
            _principal = principal;
            _scopes = scopes;
        }

        public AllowedHostsValidator AllowedHostsValidator { get; }

        public async Task<string> GetAuthorizationTokenAsync(Uri uri, Dictionary<string, object> additionalAuthenticationContext = null, CancellationToken cancellationToken = default)
        {
            return await _tokenAcquisition.GetAccessTokenForUserAsync(scopes: _scopes, user: _principal);
        }
    }
}
