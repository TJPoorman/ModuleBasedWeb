using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace ModuleBasedWeb.Utility.Security.Authorization
{
	public static class AuthorizationExtensions
	{
		/// <summary>
		/// Used for policy checking.
		/// </summary>
		/// <param name="authorizationService">AuthorizationService.</param>
		/// <param name="user">The user.</param>
		/// <param name="policyName">Policy name to check.</param>
		/// <param name="additionPolicyNames">Additional policy names to check.</param>
		/// <returns>true is user is authorized.</returns>
		/// <exception cref="ArgumentNullException"></exception>
		public static async Task<bool> IsAuthorizedAsync(this IAuthorizationService authorizationService, ClaimsPrincipal user, string policyName, params string[] additionPolicyNames)
		{
			if (policyName == null)
			{
				throw new ArgumentNullException(nameof(policyName));
			}

			var policies = new List<string> { policyName };
			if (additionPolicyNames != null && additionPolicyNames.Any())
			{
				policies.AddRange(additionPolicyNames);
			}

			var isAuthorized = policies.Any(policy => authorizationService.AuthorizeAsync(user, policy).Result.Succeeded);

			return await Task.FromResult(isAuthorized);
		}
	}
}
