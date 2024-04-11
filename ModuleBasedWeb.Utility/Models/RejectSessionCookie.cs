using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Identity.Client;
using Microsoft.Identity.Web;

namespace ModuleBasedWeb.Utility.Models
{
	public class RejectSessionCookieWhenAccountNotInCacheEvents : CookieAuthenticationEvents
	{
		public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
		{
			try
			{
				var tokenAcquisition = context.HttpContext.RequestServices.GetRequiredService<ITokenAcquisition>();
				string token = await tokenAcquisition.GetAccessTokenForUserAsync(scopes: new[] { "profile" }, user: context.Principal);
			}
			catch (MicrosoftIdentityWebChallengeUserException ex)
				when (AccountDoesNotExitInTokenCache(ex))
			{
				context.RejectPrincipal();
			}
		}

		private static bool AccountDoesNotExitInTokenCache(MicrosoftIdentityWebChallengeUserException ex) =>
			ex.InnerException is MsalUiRequiredException && (ex.InnerException as MsalUiRequiredException).ErrorCode == "user_null";
	}
}
