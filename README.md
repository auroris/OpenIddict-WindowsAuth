# IdentityServer
An OpenID Connect authorization server for Windows Authentication using the OpenIddict library.

# Rationale

Essentially a "copy and paste" OpenID Connect authorization server that doesn't need to maintain a database, certificates or any kind of permanent state. All it needs to do is perform Windows Authentication and pass the results to whomever called it.

# NuGet Packages

* Microsoft.AspNetCore.Authentication v2.2.0
* OpenIddict.Server.AspNetCore v3.0.0-beta1.20311.67
* OpenIddict.Validation.AspNetCore v3.0.0-beta1.20311.67
* OpenIddict.Validation.ServerIntegration v3.0.0-beta1.20311.67
* System.DirectoryServices.AccountManagement v4.7.0

# Installation and Configuration

When working with it in Visual Studio, or publishing to IIS, you need to enable BOTH anonymous authentication and windows authentication. Because it's stateless whenever the application is unloaded or recycled any tokens issued will no longer be valid. If this matters to you, configure IIS to suspend rather than terminate for inactivity and alter app recycle periods to some value that makes sense to you.
