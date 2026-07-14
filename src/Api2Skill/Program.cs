using System.CommandLine;
using Api2Skill.Cli;
using Api2Skill.OAuth;

// OS protocol handler launches: `api2skill <callback-uri>` — deliver to waiting oauth-capture.
if (args is [{ } maybeUri]
    && Uri.TryCreate(maybeUri, UriKind.Absolute, out var protocolUri)
    && !protocolUri.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase)
    && !protocolUri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase)
    && maybeUri.Contains("://", StringComparison.Ordinal))
{
    return await CustomSchemeCapture.DeliverHandoffAsync(maybeUri).ConfigureAwait(false);
}

var root = new RootCommand("api2skill — convert an OpenAPI/Swagger document into a Claude Agent Skill")
{
    GenerateCommand.Create(),
    UpdateCommand.Create(),
    InstallCreatorCommand.Create(),
    OAuthCaptureCommand.Create(),
    RegisterProtocolCommand.CreateRegister(),
    RegisterProtocolCommand.CreateUnregister(),
};

return await root.Parse(args).InvokeAsync();
