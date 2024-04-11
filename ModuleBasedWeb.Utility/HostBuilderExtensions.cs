using Azure.Core;
using Azure.Identity;
using ElmahCore;
using ElmahCore.Mvc;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Graph;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.UI;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using System.Net;
using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography.X509Certificates;
using ModuleBasedWeb.Utility.Claims;
using ModuleBasedWeb.Utility.Modules;
using Microsoft.Graph.Models.ExternalConnectors;
using Microsoft.Kiota.Abstractions.Authentication;
using ModuleBasedWeb.Utility.Security;

namespace ModuleBasedWeb.Utility
{
    public static class HostBuilderExtensions
	{
		public static void ConfigureModuleHost(this WebApplicationBuilder builder)
        {
			var env = builder.Environment;

            builder.Configuration.SetBasePath(env.ContentRootPath);
			builder.Configuration.AddJsonFile("appsettings.json", false, true);
			builder.Configuration.AddJsonFile("privatesettings.json", true, true);

			EagerLoadModuleAssemblies();

			MicrosoftIdentityOptions identityOptions = new();
			builder.Configuration.Bind("AzureAd", identityOptions);
            string[] initialScopes = builder.Configuration.GetValue<string>("DownstreamApi:Scopes")?.Split(' ');

            // Configure global graph client
            TokenCredential authCredential = GetTokenCredentialFromIdentityOptions(identityOptions);
			var client = new GraphServiceClient(authCredential, new[] { "https://graph.microsoft.com/.default" });
			builder.Services.AddSingleton(client);

			// Add authentication
			builder.Services.AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
				.AddMicrosoftIdentityWebApp(o =>
				{
                    builder.Configuration.Bind("AzureAd", o);

                    o.Events.OnTokenValidated = async context =>
					{
                        var tokenAcquisition = context.HttpContext.RequestServices.GetRequiredService<ITokenAcquisition>();

                        var authenticationProvider = new BaseBearerTokenAuthenticationProvider(new UserTokenProvider(tokenAcquisition, context.Principal, initialScopes));
                        var graphClient = new GraphServiceClient(authenticationProvider);

                        var user = await graphClient.Me.GetAsync();
                        user.Extensions = (await graphClient.Me.Extensions.GetAsync()).Value;

                        await context.Principal.AddUserGraphInfo(user);

						try
						{
							var photo = await graphClient.Me.Photos["48x48"].Content.GetAsync();
							await context.Principal.AddUserGraphPhoto(photo);
						}
						catch (Exception)
						{
							await context.Principal.AddUserGraphPhoto(null);
						}
					};

					o.Events.OnAuthenticationFailed = context =>
					{
						var error = WebUtility.UrlEncode(context.Exception.Message);
						context.Response.Redirect($"/Home/ErrorWithMessage?message=Authentication+error&debug={error}");

						return Task.FromResult(0);
					};

					o.Events.OnRemoteFailure = context =>
					{
						if (context.Failure is OpenIdConnectProtocolException)
						{
							var error = WebUtility.UrlEncode(context.Failure.Message);
							context.Response.Redirect($"/Home/ErrorWithMessage?message=Sign+in+error&debug={error}");
							context.HandleResponse();
						}

						return Task.FromResult(0);
					};
				})
				.EnableTokenAcquisitionToCallDownstreamApi(initialScopes)
				//.AddMicrosoftGraph(builder.Configuration.GetSection("DownstreamApi"))
				.AddInMemoryTokenCaches();

			// Add various services to the container.
			builder.Services.AddControllersWithViews(options =>
			{
				var policy = new AuthorizationPolicyBuilder()
					.RequireAuthenticatedUser()
					.Build();
				options.Filters.Add(new AuthorizeFilter(policy));
			});

			builder.Services.AddRazorPages().AddMicrosoftIdentityUI().AddMvcOptions(options => { });
			builder.Services.AddAuthorization(options => { options.FallbackPolicy = options.DefaultPolicy; });

			builder.Services.AddElmah<XmlFileErrorLog>(options =>
			{
				options.OnPermissionCheck = context => context?.User?.Identity?.IsAuthenticated ?? false;
				options.LogPath = "~/log";
			});

			builder.Services.AddModules(builder.Configuration).AddModuleAuthorizations();

			var mvc = builder.Services.AddMvc();
			foreach (var module in ModuleRegistration.Modules)
			{
				mvc.AddApplicationPart(module.GetType().Assembly);
			}

			// Build the WebApp
			var app = builder.Build();

			// Configure WebApp
			if (app.Environment.IsDevelopment())
			{
				app.UseDeveloperExceptionPage();
			}
			else
			{
				app.UseExceptionHandler("/Home/Error");
				app.UseHsts();
			}
			app.UseHttpsRedirection();

			//Add static files for root host and modules
			app.UseStaticFiles();
			foreach (var module in ModuleRegistration.Modules)
			{
				string path = Path.Combine(Path.GetDirectoryName(module.GetType().Assembly.Location), "WebApp");
				if (!Directory.Exists(path)) continue;

				app.UseStaticFiles(new StaticFileOptions
				{
					FileProvider = new PhysicalFileProvider(path)
				});
			}

			app.UseRouting();

			app.UseAuthentication();
			app.UseAuthorization();

			app.UseElmah();

			app.MapControllerRoute(
				name: "areas",
				pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");
			app.MapControllerRoute(
				name: "default",
				pattern: "{controller=Home}/{action=Index}/{id?}");
			app.MapRazorPages();
			app.MapControllers();

			app.Run();
		}

		private static void EagerLoadModuleAssemblies()
		{
			string? assemblyPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
			if (assemblyPath is null) return;
			string modulePath = Path.Combine(assemblyPath, "Modules");
			if (!Directory.Exists(modulePath)) return;

			foreach (var directory in Directory.GetDirectories(modulePath))
			{
				var moduleAssemblyPaths = Directory.GetFiles(directory, "*.dll", SearchOption.TopDirectoryOnly).ToList();
				moduleAssemblyPaths.ForEach(assemblyPath =>
				{
					try
					{
						var a = Assembly.LoadFrom(assemblyPath);
						AssemblyLoadContext.Default.LoadFromAssemblyPath(assemblyPath);
					}
					catch { }
				});
			}
		}

		private static TokenCredential GetTokenCredentialFromIdentityOptions(MicrosoftIdentityOptions options)
		{
			TokenCredential authCredential;
			if (options?.ClientCertificates?.FirstOrDefault()?.CertificateThumbprint is not null)
			{
				try
				{
					Enum.TryParse(options.ClientCertificates.FirstOrDefault().CertificateStorePath.Split("/")[1], out StoreName storeName);
					Enum.TryParse(options.ClientCertificates.FirstOrDefault().CertificateStorePath.Split("/")[0], out StoreLocation storeLocation);

					X509Store certStore = new X509Store(storeName, storeLocation);
					certStore.Open(OpenFlags.ReadOnly);
					X509Certificate2Collection certs = certStore.Certificates.Find(X509FindType.FindByThumbprint, options.ClientCertificates.FirstOrDefault().CertificateThumbprint, false);
					certStore.Close();
					if (certs.Count <= 0) throw new Exception("Could not locate certificate based on thumbprint");

					authCredential = new ClientCertificateCredential(options.TenantId, options.ClientId, certs[0], new TokenCredentialOptions { AuthorityHost = AzureAuthorityHosts.AzurePublicCloud });
				}
				catch
				{
					throw new Exception("Unable to create token from IdentityOptions");
				}
			}
			else if (options?.ClientSecret is not null)
			{
				authCredential = new ClientSecretCredential(options.TenantId, options.ClientId, options.ClientSecret, new TokenCredentialOptions { AuthorityHost = AzureAuthorityHosts.AzurePublicCloud });
			}
			else
			{
				throw new Exception("Cannot start application without certificate or secret");
			}

			return authCredential;
		}
	}
}
