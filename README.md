# OpenIddict-WindowsAuth
An OpenID Connect authorization server for Windows Integrated Authentication using the OpenIddict library.

## Rationale

Essentially a "copy and paste" OpenID Connect authorization server that doesn't need to maintain a database, certificates or any kind of permanent state. All it needs to do is perform Windows Authentication and pass the results to whomever called it.

## Installation

OpenIddict-WindowsAuth doesn't have a database, so whenever the application is unloaded or recycled any tokens issued will no longer be valid. You can configure the app pool to suspend rather than terminate during periods of inactivity and alter the pool's recycle settings to fire on a set schedule during periods of inactivity.

You must enable both Windows Authentication and Anonymous Authentication in IIS.

## Configuration

Configuration for the application is primarily via appsettings.json, though environment variables and command line options are supported as well. The following configuration keys are of importance.

### IdentityServer:ServerUri

The full URI IdentityServer should report in the Issuer and configuration fields of the `/.well-known/openid-configuration` document. If you install IdentityServer to "http(s)://myserver.com/IdentityServer/" that is what you should report in this field.

### IdentityServer:Hosts

This key is a list of acceptable hosts attempting to authenticate against this application. We will not return authentication data for hosts not in this list. This list is also used to specify dotNet's cors setting. The expected format is a regular base URL without a trailing slash.

### IdentityServer:Groups

This key is a list of acceptable active directory groups. Items are exact match, case insensitive, but also allows wildcards. If the authenticating user is a member of an active directory group listed here, it will be returned as a role claim.

## Testing

If you run the project in Visual Studio, here are the testing Uris:

Viewing the OpenId configuration document:

* http://localhost:5000/.well-known/openid-configuration
* https://localhost:44353/.well-known/openid-configuration

Testing the authorization endpoint with oidcdebugger:

* http://localhost:5000/connect/authorize?client_id=optional&redirect_uri=https://oidcdebugger.com/debug&scope=openid%20profile%20email%20roles&response_type=id_token&response_mode=form_post&nonce=f6pz1s2cfgs
* https://localhost:44353/connect/authorize?client_id=optional&redirect_uri=https://oidcdebugger.com/debug&scope=openid%20profile%20email%20roles&response_type=id_token&response_mode=form_post&nonce=f6pz1s2cfgs
