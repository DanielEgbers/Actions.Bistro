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
            var dataPath = Path.GetDirectoryName(WochenplanFilePath)!;

            if (!Git.IsRootDirectory(workingDirectory: dataPath))
                return;

            if (!(await Git.GetChangesAsync(workingDirectory: dataPath)).Any())
                return;

            await Git.ConfigUserAsync(name: "GitHub Actions", email: "actions@users.noreply.github.com", workingDirectory: dataPath);

            await Git.StageAllAsync(workingDirectory: dataPath);

            await Git.CommitAsync("update", workingDirectory: dataPath);

            await Git.PushAsync(workingDirectory: dataPath);
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