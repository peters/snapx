using System.Diagnostics.CodeAnalysis;
using CommandLine;
using JetBrains.Annotations;

namespace snapx.Options
{
    [SuppressMessage("ReSharper", "UnusedMember.Global")]
    [Verb("demote", HelpText = "Demote latest release")]
    [UsedImplicitly]
    internal class DemoteOptions : BaseSubOptions
    {
        [Option('a', "app", HelpText = "Application id", Required = true)]
        public string AppId { get; [UsedImplicitly] set; }
        [Option('r', "rid", HelpText = "Runtime identifier target name, e.g win-x64", Required = true)]
        public string Rid { get; [UsedImplicitly] set; }
        [Option("lock-retries", HelpText = "The number of retries if a mutex fails to be acquired (default: 3). Specify -1 if you want to retry forever.")]
        public int LockRetries { get; set; } = 3;
        [Option('c', "channel", HelpText = "Channel name. If value is 'all' then current release will demoted from all channels.", Required = true)]
        public string Channel { get; [UsedImplicitly] set; }
        [Option("all", HelpText = "Current release should be demoted from all channels.")]
        public bool All { get; [UsedImplicitly] set; }
    }
}
