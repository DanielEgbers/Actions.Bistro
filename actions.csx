#r "nuget: System.CommandLine, 2.0.0-beta1.20371.2"
#r "nuget: Flurl.Http, 2.4.2"

#load "../Actions.Shared/git.csx"

#nullable enable

using System.CommandLine;
using System.CommandLine.Invocation;
using Flurl.Http;

return await InvokeCommandAsync(Args.ToArray());

private async Task<int> InvokeCommandAsync(string[] args)
{
    const string WochenplanFilePath = "data/wochenplan.png";
    const string WochenplanUrlEnvironmentVariable = "WOCHENPLAN_URL";

    var scrape = new Command("scrape")
    {
        Handler = CommandHandler.Create(async () =>
        {
            var url = Environment.GetEnvironmentVariable(WochenplanUrlEnvironmentVariable);

            if (string.IsNullOrWhiteSpace(url))
                throw new ArgumentException($"environment variable '{WochenplanUrlEnvironmentVariable}' is missing");

            var imageBytes = await url.GetBytesAsync();

            File.WriteAllBytes(WochenplanFilePath, imageBytes);
        })
    };

    var push = new Command("push")
    {
        Handler = CommandHandler.Create(async () =>
        {
            if (Debugger.IsAttached)
                return;

            var workingDirectory = Path.GetDirectoryName(WochenplanFilePath)!;

            await Git.ConfigUserAsync(workingDirectory, "GitHub Actions", "actions@users.noreply.github.com");

            if (!await Git.CommitAsync(workingDirectory, "update {files}", Path.GetFileName(WochenplanFilePath)))
                return;

            await Git.PushAsync(workingDirectory);
        })
    };

    var root = new RootCommand()
    {
        scrape,
        push,
    };

    root.Handler = CommandHandler.Create(async () =>
    {
        await scrape.InvokeAsync(string.Empty);
        await push.InvokeAsync(string.Empty);
    });

    return await root.InvokeAsync(args);
}