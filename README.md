# OpenIddict-WindowsAuth
An OpenID Connect authorization server for Windows Integrated Authentication using the OpenIddict library.

## Rationale

Essentially a "copy and paste" OpenID Connect authorization server that doesn't need to maintain a database, certificates or any kind of permanent state. All it needs to do is perform Windows Authentication and pass the results to whomever called it.

## NuGet Dependencies

* Microsoft.AspNetCore.Authentication v2.2.0
* OpenIddict.Server.AspNetCore v3.0.0-beta1.20311.67
* OpenIddict.Validation.AspNetCore v3.0.0-beta1.20311.67
* OpenIddict.Validation.ServerIntegration v3.0.0-beta1.20311.67
* System.DirectoryServices.AccountManagement v4.7.0

### Installation

When publishing to IIS, you need to enable BOTH anonymous authentication and windows authentication. This application is stateless, so whenever the application is unloaded or recycled any tokens issued will no longer be valid. You can configure IIS to suspend rather than terminate the application during periods of inactivity and set the recycle period settings to mitigate this.

This application relies on Windows Integrated Authentication and must run on a domain joined computer.

## Configuration

Configuration for the application is stored in appsettings.json; and there are two keys you need to be aware of: IdentityServer:Hosts and IdentityServer:Groups

### IdentityServer:Hosts

This key is a list of acceptable hosts attempting to authenticate against this application. We will not return authentication data for hosts not in this list. This list is also used to specify dotNet's cors setting. The expected format is a regular base URL without a trailing slash.

### IdentityServer:Groups

This key is a list of acceptable active directory groups. Items are exact match, case insensitive. If the authenticating user is a member of an active directory group listed here, it will be returned in the roles field.