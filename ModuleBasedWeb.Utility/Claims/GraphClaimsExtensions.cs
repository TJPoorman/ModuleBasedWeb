using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Identity.Web;
using System.Security.Claims;

namespace ModuleBasedWeb.Utility.Claims
{
	public static class GraphClaimTypes
	{
		public const string DisplayName = "graph_name";
		public const string Email = "graph_email";
		public const string Photo = "graph_photo";
		public const string TimeZone = "graph_timezone";
		public const string TimeFormat = "graph_timeformat";
	}

	public static class GraphClaimsExtensions
	{
		public static string? GetUserGraphDisplayName(this ClaimsPrincipal principal) => principal.FindFirstValue(GraphClaimTypes.DisplayName);

		public static string? GetUserGraphEmail(this ClaimsPrincipal principal) => principal.FindFirstValue(GraphClaimTypes.Email);

		public static string? GetUserGraphPhoto(this ClaimsPrincipal principal) => principal.FindFirstValue(GraphClaimTypes.Photo);

		public static string? GetUserGraphTimeZone(this ClaimsPrincipal principal) => principal.FindFirstValue(GraphClaimTypes.TimeZone);

		public static string? GetUserGraphTimeFormat(this ClaimsPrincipal principal) => principal.FindFirstValue(GraphClaimTypes.TimeFormat);

		public static string? GetUserGraphUsername(this ClaimsPrincipal principal)
		{
			if (!principal.Identity.Name.Contains('@')) return principal.Identity.Name;
			return principal.Identity.Name.Split('@')[0];
		}

		public static IEnumerable<string> GetUserGroupNames(this ClaimsPrincipal principal) => principal.FindAll(it => it.Type == "groupNames").Select(it => it.Value);

		public static async Task AddUserCustomSecurityAttributes(this ClaimsPrincipal principal, GraphServiceClient client)
		{
			var identity = principal.Identity as ClaimsIdentity;

			try
			{
				var result = await client.Me.GetAsync((requestConfiguration) =>
				{
					requestConfiguration.QueryParameters.Select = new string[] { "customSecurityAttributes", "userType" };
				});

				if (!string.IsNullOrEmpty(result.UserType)) identity.AddClaim(new Claim($"graph_usertype", result.UserType));

				foreach (var attribute in result.CustomSecurityAttributes.AdditionalData)
				{
					identity.AddClaim(new Claim($"graph_secattribute_{attribute.Key}", attribute.Value.ToString()));
				}
			}
			catch (Exception)
			{
				throw;
			}
		}

		public static async Task AddUserGraphInfo(this ClaimsPrincipal principal, User user)
		{
			await Task.FromResult(0);

			var identity = principal.Identity as ClaimsIdentity;

			identity.AddClaim(new Claim(GraphClaimTypes.DisplayName, user.DisplayName));
			identity.AddClaim(new Claim(GraphClaimTypes.Email, user.Mail ?? user.UserPrincipalName));
			identity.AddClaim(new Claim(GraphClaimTypes.TimeZone, user.MailboxSettings?.TimeZone ?? ""));
			identity.AddClaim(new Claim(GraphClaimTypes.TimeFormat, user.MailboxSettings?.TimeFormat ?? ""));
		}

		public static async Task AddUserGraphPhoto(this ClaimsPrincipal principal, Stream photoStream)
		{
			await Task.FromResult(0);

			var identity = principal.Identity as ClaimsIdentity;

			if (photoStream is null)
			{
				identity.AddClaim(new Claim(GraphClaimTypes.Photo, "/images/NoUser.jpg"));
				return;
			}

			var memoryStream = new MemoryStream();
			photoStream.CopyTo(memoryStream);
			var photoBytes = memoryStream.ToArray();

			var photoUrl = $"data:image/png;base64,{Convert.ToBase64String(photoBytes)}";

			identity.AddClaim(new Claim(GraphClaimTypes.Photo, photoUrl));
		}
	}
}
