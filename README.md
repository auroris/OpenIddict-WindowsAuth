# OpenIddict-WindowsAuth
An OpenID Connect authorization server for Windows Integrated Authentication using the OpenIddict library.

## Rationale

Essentially a "copy and paste" OpenID Connect authorization server that doesn't need to maintain a database, certificates or any kind of permanent state. All it needs to do is perform Windows Authentication and pass the results to whomever called it.

## NuGet Dependencies

* Microsoft.AspNetCore.Authentication v2.2.0
* OpenIddict.Server.AspNetCore v3.0.0-beta1.20311.67
* OpenIddict.Validation.AspNetCore v3.0.0-beta1.20311.67
* OpenIddict.Validation.ServerIntegration v3.0.0-beta1.20311.67
* System.DirectoryServices.AccountManagement v5.0.0

## Installation

OpenIddict-WindowsAuth needs to be at the root of its IIS web site. The most common way to accomplish this without disturbing any existing web sites is to publish it on a subdomain or on a different port. You will also need to enable both anonymous authentication and windows authentication in IIS.

OpenIddict-WindowsAuth is stateless, so whenever the application is unloaded or recycled any tokens issued will no longer be valid. You can configure the app pool to suspend rather than terminate during periods of inactivity and alter the pool's recycle settings to fire on a set schedule during periods of inactivity.

Windows Integrated Authentication requires IIS run on a domain joined computer or else you will only be able to authenticate with local computer accounts.

## Configuration

Configuration for the application is primarily via appsettings.json, though environment variables and command line options are supported as well. There are two keys you need to be aware of: IdentityServer:Hosts and IdentityServer:Groups

### IdentityServer:ServerUri

The location URI IdentityServer should report in the Issuer and configuration fields of the `/.well-known/openid-configuration` document.

### IdentityServer:Hosts

This key is a list of acceptable hosts attempting to authenticate against this application. We will not return authentication data for hosts not in this list. This list is also used to specify dotNet's cors setting. The expected format is a regular base URL without a trailing slash.

### IdentityServer:Groups

This key is a list of acceptable active directory groups. Items are exact match, case insensitive. If the authenticating user is a member of an active directory group listed here, it will be returned in the roles field.
