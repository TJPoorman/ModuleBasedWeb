using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ModuleBasedWeb.Utility.Utilities
{
	/// <summary>
	/// A static utility class containing methods and properties for working with build and environment specifics.
	/// </summary>
	public static class VersionUtility
	{
		/// <summary>
		/// Gets a value indicating whether the current environment is a development environment.
		/// </summary>
		public static bool IsDevEnvironment
		{
			get
			{
				var _accessor = new HttpContextAccessor();
				var _host = _accessor.HttpContext.RequestServices.GetRequiredService<IWebHostEnvironment>();
				return _host.IsDevelopment();
			}
		}

		/// <summary>
		/// Gets the version of the assembly.
		/// </summary>
		public static string Version
		{
			get
			{
				if (IsDevEnvironment)
				{
					return "DEVELOPMENT";
				}
				else
				{
					string path = @"build.txt";

					if (!File.Exists(path)) return "UNKNOWN";

					return File.ReadAllText(path);
				}
			}
		}
	}
}
