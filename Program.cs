using System.CommandLine;
using System.CommandLine.Invocation;
using System.Net;
using System.Net.Http;
using Microsoft.VisualStudio.Services.Client;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.Profile;
using Microsoft.VisualStudio.Services.Profile.Client;
using Microsoft.VisualStudio.Services.WebApi;

namespace Chorn.AzureDevOpsNugetRefresher;

internal class Program
{
	private static readonly Option<string> orgOption;

	private static readonly Option<string> projOption;

	private static readonly Option<string> feedOption;

	private static readonly Option<string> packageOption;

	private static readonly Option<string> versionOption;

	static Program()
	{
		versionOption = new Option<string>(
			name: "--package-version",
			description: "The version of the package to refresh.");
		versionOption.IsRequired = true;
		versionOption.AddAlias("-pv");

		orgOption = new Option<string>(
			name: "--organization",
			description: "The organization on azure devops.");
		orgOption.IsRequired = true;
		orgOption.AddAlias("-o");

		projOption = new Option<string>(
			name: "--project",
			description: "The project the nuget artifact feed is in on azure devops.");
		projOption.IsRequired = true;
		projOption.AddAlias("-pr");

		feedOption = new Option<string>(
			name: "--feed",
			description: "The feed name of the artifact nuget feed.");
		feedOption.IsRequired = true;
		feedOption.AddAlias("-f");

		packageOption = new Option<string>(
			name: "--package",
			description: "The package to refresh.");
		packageOption.IsRequired = true;
		packageOption.AddAlias("-p");
	}


	private static async Task Main(string[] args)
	{
		RootCommand rootCommand = new RootCommand("Manual package refresher for an azure artifact nuget feed.");

		rootCommand.AddOption(orgOption);
		rootCommand.AddOption(projOption);
		rootCommand.AddOption(feedOption);
		rootCommand.AddOption(packageOption);
		rootCommand.AddOption(versionOption);

		rootCommand.SetHandler(RefreshPackage);
		await rootCommand.InvokeAsync(args);
	}

	private static async Task RefreshPackage(InvocationContext arg)
	{
		string organizationName = arg.ParseResult.GetValueForOption(orgOption)!;
		string projectName = arg.ParseResult.GetValueForOption(projOption)!;
		string feedName = arg.ParseResult.GetValueForOption(feedOption)!;
		string packageName = arg.ParseResult.GetValueForOption(packageOption)!;
		string packageVersion = arg.ParseResult.GetValueForOption(versionOption)!;

		VssClientCredentials credentials = new();
		credentials.Storage =
			new VssClientCredentialStorage();

		Uri devopsUrl = new Uri($"https://dev.azure.com/{organizationName}");
		VssConnection connection = new VssConnection(devopsUrl, credentials);

		Console.WriteLine("Connecting to azure devops...");
		await connection.ConnectAsync();

		Console.WriteLine("Connected.");

		// Get user profile
		ProfileHttpClient profileClient = connection.GetClient<ProfileHttpClient>();
		Profile profile = await profileClient.GetProfileAsync(new ProfileQueryContext(AttributesScope.Core));
		Console.WriteLine($"Authenticated as: {profile.DisplayName}");
		
		credentials.TryGetTokenProvider(devopsUrl, out IssuedTokenProvider? provider);
		CookieCollection cookies = ((VssFederatedToken)provider.CurrentToken).CookieCollection;

		Uri pkgRefresh =
			new Uri(
				$"https://pkgs.dev.azure.com/{organizationName}/{projectName}/_apis/packaging/feeds/{feedName}/nuget/packages/{packageName}/versions/{packageVersion}/content?api-version=5.0-preview.1");

		CookieContainer cookieContainer = new();
		foreach (Cookie cookie in cookies)
		{
			cookieContainer.Add(new Uri("https://pkgs.dev.azure.com/"), cookie);
		}

		using HttpClientHandler handler = new() { CookieContainer = cookieContainer };
		using HttpClient client = new(handler) { BaseAddress = pkgRefresh };
		HttpRequestMessage headReq = new HttpRequestMessage(HttpMethod.Head, "");
		Console.WriteLine($"Refreshing package {packageName}:{packageVersion}");
		HttpResponseMessage headResponse = await client.SendAsync(headReq);
		headResponse.EnsureSuccessStatusCode();
		Console.WriteLine("Refreshed.");
		
		HttpResponseMessage pkgRefreshResponse = await client.GetAsync(pkgRefresh);
		pkgRefreshResponse.EnsureSuccessStatusCode();
		
		HttpRequestMessage getRequest = new HttpRequestMessage(HttpMethod.Get, "");
		Console.WriteLine("Downloading package to confirm refresh...");
		HttpResponseMessage getResponse = await client.SendAsync(getRequest);
		getResponse.EnsureSuccessStatusCode();


		Console.WriteLine($"Server returned {pkgRefreshResponse.Content.Headers.ContentDisposition.FileName}.");
	}
}