# IdentityServer
An OpenID Connect authorization server for Windows Authentication using the OpenIddict library.

## Rationale

Essentially a "copy and paste" OpenID Connect authorization server that doesn't need to maintain a database, certificates or any kind of permanent state. All it needs to do is perform Windows Authentication and pass the results to whomever called it.

## NuGet Packages

* Microsoft.AspNetCore.Authentication v2.2.0
* OpenIddict.Server.AspNetCore v3.0.0-beta1.20311.67
* OpenIddict.Validation.AspNetCore v3.0.0-beta1.20311.67
* OpenIddict.Validation.ServerIntegration v3.0.0-beta1.20311.67
* System.DirectoryServices.AccountManagement v4.7.0

## Installation and Configuration

If you're using Visual Studio 2019 you can just open the csproj, though if you're using a different environment you can find the important stuff in Startup.cs. It's a .Net Core 3 Empty Web Application. Add the above NuGet packages and Startup.cs from this repository.

When working with it or publishing to IIS, you need to enable BOTH anonymous authentication and windows authentication. As it is stateless whenever the application is unloaded or recycled any tokens issued will no longer be valid. You can configure IIS to suspend rather than terminate the application during periods of inactivity and set the recycle period settings to mitigate this.
