using CommandLine;
using CommandLine.Text;
using NetAmermaid;

const string asciiHeading = @"
                         /\                                                        . 
                 /   _  / |                                               .-.     /  
 .  .-.   .-.---/---(  /  |  ..  .-. .-.   .-.  ).--..  .-. .-.  .-.      `-'.-../   
  )/   )./.-'_ /     `/.__|_.' )/   )   )./.-'_/      )/   )   )(  |     /  (   /    
 '/   ( (__.' /  .:' /    |   '/   /   ( (__.'/      '/   /   (  `-'-'_.(__. `-'-..  
       `-       (__.'     `-'           `-'                    `-'                   
"; // from http://www.patorjk.com/software/taag/#p=display&f=Diet%20Cola&t=netAmermaid

try
{
    // set HelpWriter null to enable customizing help text, see below
    ParserResult<GenerateHtmlDiagrammer> parserResult = new Parser(with => with.HelpWriter = null).ParseArguments<GenerateHtmlDiagrammer>(args);

    parserResult.WithParsed(command =>
    {
        command.Run();
        Environment.ExitCode = (int)ExitCodes.Success;
    });

    parserResult.WithNotParsed(errors => Console.WriteLine(HelpText.AutoBuild(parserResult, h =>
    {
        h.Heading = asciiHeading + h.Heading; // enhance heading for branding
        Environment.ExitCode = (int)ExitCodes.Error;

        // see https://learn.microsoft.com/en-us/dotnet/api/system.console.windowwidth?view=net-8.0#remarks
        if (!Console.IsOutputRedirected) h.MaximumDisplayWidth = Console.WindowWidth;

        h.AddPostOptionsLine($"See {GenerateHtmlDiagrammer.RepoUrl} for more info.");
        return h;
    })));
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex.ToString());
    Environment.ExitCode = (int)ExitCodes.Error;
}

public enum ExitCodes
{
    Error = -1,
    Success = 0
}