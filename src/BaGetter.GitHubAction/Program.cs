using CommandLine;
using CommandLine.Text;

using var cancellationTokenSource = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cancellationTokenSource.Cancel();
};

try
{
    var result = new Parser(settings =>
    {
        settings.CaseInsensitiveEnumValues = true;
    }).ParseArguments<ActionInputs>(args);

    await result
        .WithNotParsed(errors =>
        {
            var help = HelpText.AutoBuild(result);
            help.Heading = new HeadingInfo("BaGetter Package Upload");
            Console.Error.WriteLine(help);
            Environment.ExitCode = 2;
        })
        .WithParsedAsync(inputs => GitHubPackageUploader.UploadAsync(inputs, cancellationTokenSource.Token));
}
catch (OperationCanceledException)
{
}
catch (Exception e)
{
    Console.Error.WriteLine(e.Message);
    Console.Error.WriteLine(e);
    Environment.ExitCode = 2;
}
