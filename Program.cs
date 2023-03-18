using System.CommandLine;
using System.CommandLine.Invocation;
using System.Net;
using Microsoft.VisualStudio.Services.Client;
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

		Uri devopsUrl = new Uri($"https://dev.azure.com/{organizationName}");
		VssConnection connection = new VssConnection(devopsUrl, credentials);
		
		Console.WriteLine("Connecting to azure devops...");
		await connection.ConnectAsync();

		Console.WriteLine("Connected.");
		
		credentials.TryGetTokenProvider(devopsUrl, out var provider);
		CookieCollection cookies = ((VssFederatedToken)provider.CurrentToken).CookieCollection;

		Uri pkgRefresh =
			new Uri(
				$"https://pkgs.dev.azure.com/{organizationName}/{projectName}/_apis/packaging/feeds/{feedName}/nuget/packages/{packageName}/versions/{packageVersion}/content?api-version=5.0-preview.1");

		CookieContainer cookieContainer = new();
		foreach (Cookie cookie in cookies)
		{
			cookieContainer.Add(new Uri("https://pkgs.dev.azure.com/"), cookie);
		}

		using var handler = new HttpClientHandler() { CookieContainer = cookieContainer };
		using var client = new HttpClient(handler) { BaseAddress = pkgRefresh };
		HttpRequestMessage headReq = new HttpRequestMessage(HttpMethod.Head, "");
		Console.WriteLine($"Refreshing package {packageName}:{packageVersion}");
		HttpResponseMessage headResponse = await client.SendAsync(headReq);
		headResponse.EnsureSuccessStatusCode();
		Console.WriteLine("Refreshed.");
		HttpResponseMessage getResponse = await client.GetAsync(pkgRefresh);
		getResponse.EnsureSuccessStatusCode();
		
		Console.WriteLine($"Server returned {getResponse.Content.Headers.ContentDisposition.FileName}.");
	}
}