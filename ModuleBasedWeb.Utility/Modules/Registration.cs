using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ModuleBasedWeb.Utility.Modules
{
	public abstract class Registration
	{
		public abstract string Name { get; }
		public abstract string Title { get; }
		public abstract string Icon { get; }
		public virtual bool ExcludeTileFromHomePage { get; } = false;
		public virtual string PageHyperLink { get; } = null;
		public virtual List<string> Roles { get; set; }

		public virtual void AddModuleServices(IServiceCollection services, IConfiguration config) { }
	}
}
