using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Graph;
using ModuleBasedWeb.Utility.Models;
using System.Diagnostics;

namespace ModuleBasedWeb.Controllers
{
	public class HomeController : Controller
	{
		private readonly ILogger<HomeController> _logger;
		private readonly IConfiguration _config;
		private readonly GraphServiceClient _client;

		public HomeController(ILogger<HomeController> logger, IConfiguration config, GraphServiceClient client)
		{
			_logger = logger;
			_config = config;
			_client = client;
		}

		public IActionResult Index() => View();

		public async Task<IActionResult> Claims()
		{
			var ad = _config.GetSection("AzureAd");

			try
			{
				var groups = await _client.Groups.GetAsync((requestConfiguration) =>
				{
					requestConfiguration.QueryParameters.Filter = $"startswith(displayName, '{ad.GetValue<string>("AppGroupPrefix")}')";
					requestConfiguration.QueryParameters.Select = new[] { "id", "displayName" };
				});

				return View(groups);
			}
			catch (Exception)
			{
				throw;
			}
		}

		[AllowAnonymous]
		[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
		public IActionResult Error() => View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
	}
}
