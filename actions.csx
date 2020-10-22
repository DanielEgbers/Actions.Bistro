#r "nuget: System.CommandLine, 2.0.0-beta1.20371.2"
#r "nuget: SimpleExec, 6.2.0"
#r "nuget: Flurl.Http, 2.4.2"

#nullable enable

using static SimpleExec.Command;

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

            await GitConfigUserAsync(workingDirectory, "GitHub Actions", "actions@users.noreply.github.com");

            if (!await GitCommitAsync(workingDirectory, "update {files}", Path.GetFileName(WochenplanFilePath)))
                return;

            await GitPushAsync(workingDirectory);
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

private async Task GitConfigUserAsync(string workingDirectory, string name, string email)
{
    await RunAsync("git", $"config user.name \"{name}\"", workingDirectory: workingDirectory);
    await RunAsync("git", $"config user.email \"{email}\"", workingDirectory: workingDirectory);
}

private async Task<bool> GitCommitAsync(string workingDirectory, string message, params string[] files)
{
    var gitStatus = await ReadAsync("git", $"status --short --untracked-files", workingDirectory: workingDirectory);

    var changedFiles = files.Where(f => gitStatus.Contains(f)).ToArray();

    if (changedFiles.Length <= 0)
        return false;

    var changedFilesJoin = $"\"{string.Join("\" \"", changedFiles)}\"";

    await RunAsync("git", $"add {changedFilesJoin}", workingDirectory: workingDirectory);

    var gitCommitMessage = message.Replace($"{{{nameof(files)}}}", changedFilesJoin.Replace("\"", "'"));
    await RunAsync("git", $"commit -m \"{gitCommitMessage}\"", workingDirectory: workingDirectory);

    return true;
}

private async Task GitPushAsync(string workingDirectory)
{
    await RunAsync("git", "push --quiet --progress", workingDirectory: workingDirectory);
}