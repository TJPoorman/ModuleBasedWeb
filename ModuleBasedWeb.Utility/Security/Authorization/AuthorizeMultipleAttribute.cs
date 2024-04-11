using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Microsoft.Extensions.DependencyInjection;

namespace ModuleBasedWeb.Utility.Security.Authorization
{
	public class AuthorizeMultipleAttribute : AuthorizeAttribute, IAuthorizationFilter
	{
		public AuthorizeMultipleAttribute(params string[] policies)
		{
			// Leave base.Policy and Roles empty, or the custom OnAuthorization won't be called.
			AllowedPolicies = policies ?? new string[0];
		}

		public string[] AllowedPolicies { get; private set; }

		public virtual void OnAuthorization(AuthorizationFilterContext context)
		{
			var authorizationService = context.HttpContext.RequestServices.GetService<IAuthorizationService>();
			var user = context.HttpContext.User;
			var isAuthorized = IsUserAuhorized(authorizationService, user).Result;
			if (!isAuthorized)
			{
				context.Result = new UnauthorizedResult();
			}
		}

		protected virtual async Task<bool> IsUserAuhorized(IAuthorizationService authorizationService, ClaimsPrincipal user)
		{
			if (!AllowedPolicies.Any())
			{
				return true;
			}

			var isAuthorized = AllowedPolicies.Any(policy => authorizationService.AuthorizeAsync(user, policy).Result.Succeeded);

			return await Task.FromResult(isAuthorized);
		}
	}
}
