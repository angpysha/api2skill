using System.CommandLine;
using Api2Skill.Cli;

var root = new RootCommand("api2skill — convert an OpenAPI/Swagger document into a Claude Agent Skill")
{
    GenerateCommand.Create(),
    UpdateCommand.Create(),
};

return await root.Parse(args).InvokeAsync();
