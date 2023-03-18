# AzureDevOpsNugetRefresher

This is a simple command line tool that can be used to refresh the Nuget cache for a specific package on an Azure DevOps artifact feed.

## Usage
```
AzureDevOpsNugetRefresher.exe -o <devopsOrganization> -pr <devopsProjectName> -f <devopsArtifactFeedName> -p <packageToRefresh> -pv <packageVersionToRefresh>
```

Note that the program will try to do an interactive login, so this does not run in a build pipeline currently.

## TODO
- [ ] Add support for a PAT token
- [ ] Linux version (PAT token only)

[!["Buy Me A Coffee"](https://www.buymeacoffee.com/assets/img/custom_images/orange_img.png)](https://www.buymeacoffee.com/chorn)
