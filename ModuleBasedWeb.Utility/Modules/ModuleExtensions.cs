using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Graph;
using ModuleBasedWeb.Utility.Security.Authorization;
using System.Reflection;

namespace ModuleBasedWeb.Utility.Modules
{
	public static class ModuleExtensions
	{
		public static IServiceCollection AddModules(this IServiceCollection services, IConfiguration configuration)
		{
			ModuleRegistration.ClearModules();

			foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies().Where(a => a.FullName.StartsWith("ModuleBasedWeb")))
			{
				string moduleName = null;
				foreach (Type type in assembly.GetTypes().Where(myType => myType.IsClass && !myType.IsAbstract && myType.IsSubclassOf(typeof(Registration))))
				{
					Registration module = (Registration)Activator.CreateInstance(type);
					module.AddModuleServices(services, configuration);
					ModuleRegistration.AddModule(module);
					moduleName = module.Name;
				}

				if (!string.IsNullOrEmpty(moduleName))
				{
					foreach (Type type in assembly.GetTypes())
					{
						var roles = GetRolesFromType(type);
						roles.ForEach(role =>
						{
							ModuleRegistration.AddModuleRole(moduleName, role);
						});
					}
				}
			}

			return services;
		}

		public static IServiceCollection AddModuleAuthorizations(this IServiceCollection services)
		{
			var serviceProvider = services.BuildServiceProvider();
			var client = serviceProvider.GetRequiredService<GraphServiceClient>();
			var configuration = serviceProvider.GetService<IConfiguration>();

			var groupPrefix = configuration.GetSection("AzureAd").GetValue<string>("AppGroupPrefix");

			var groups = client.Groups.GetAsync(c =>
			{
				c.QueryParameters.Filter = $"startswith(displayName, '{groupPrefix}')";
				c.QueryParameters.Select = new string[] { "id", "displayName" };
			}).Result;
			var admin = groups.Value.FirstOrDefault(a => a.DisplayName.Equals($"{groupPrefix}Administrator", StringComparison.InvariantCultureIgnoreCase));
			services.AddAuthorization(options =>
			{
				options.AddPolicy(admin.DisplayName, policyBuilder => policyBuilder.RequireClaim("groups", admin.Id, admin.DisplayName));
			});

			foreach (var group in groups.Value)
			{
				services.AddAuthorization(options =>
				{
					options.AddPolicy(group.DisplayName, policyBuilder => policyBuilder.RequireClaim("groups", group.Id, group.DisplayName, admin.Id, admin.DisplayName));
				});
			}

			foreach (var module in ModuleRegistration.Modules)
			{
				var modRoles = module.Roles;
				if (modRoles is null || !modRoles.Any()) modRoles = new List<string>();

				modRoles.AddRange(groups.Value.Where(a => (module?.Roles?.Any(b => b == a.DisplayName)) ?? false).Select(a => a.Id));
				if (modRoles.Any())
				{
					modRoles.Add(admin.Id);
					modRoles.Add(admin.DisplayName);

					services.AddAuthorization(options =>
					{
						options.AddPolicy($"{groupPrefix}module-{module.Name}", policyBuilder => policyBuilder.RequireClaim("groups", modRoles));
					});
				}
				else
				{
					services.AddAuthorization(options =>
					{
						options.AddPolicy($"{groupPrefix}module-{module.Name}", policyBuilder => policyBuilder.RequireClaim("groups"));
					});
				}
			}

			return services;
		}

		private static List<string> GetRolesFromType(Type type)
		{
			var typeAttributes = type.GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>();
			var methodAttributes = type.GetMethods().SelectMany(method => method.GetCustomAttributes(typeof(AuthorizeAttribute), true)).Cast<AuthorizeAttribute>();
			var authorizeAttributes = typeAttributes.Concat(methodAttributes).ToList();

			var roles = new List<string>();
			authorizeAttributes.ForEach(authorizeAttribute =>
			{
				if (!string.IsNullOrEmpty(authorizeAttribute.Policy))
				{
					roles.Add(authorizeAttribute.Policy);
				}

				if (authorizeAttribute is AuthorizeMultipleAttribute roleAuthorizeAttribute)
				{
					roles.AddRange(roleAuthorizeAttribute.AllowedPolicies);
				}
			});

			return roles.Distinct().ToList();
		}
	}
}
