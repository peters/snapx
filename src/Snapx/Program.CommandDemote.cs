using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Snap;
using Snap.AnyOS;
using snapx.Options;
using Snap.Core;
using Snap.Core.IO;
using Snap.Extensions;
using Snap.Logging;
using Snap.NuGet;
using snapx.Core;

namespace snapx
{
    internal partial class Program
    {
        static async Task<int> CommandDemoteAsync([NotNull] DemoteOptions options, [NotNull] ISnapFilesystem filesystem,
            [NotNull] ISnapAppReader snapAppReader, [NotNull] ISnapAppWriter snapAppWriter, [NotNull] INuGetPackageSources nuGetPackageSources,
            [NotNull] INugetService nugetService, [NotNull] IDistributedMutexClient distributedMutexClient,
            [NotNull] ISnapPackageManager snapPackageManager, [NotNull] ISnapPack snapPack, [NotNull] ISnapOsSpecialFolders specialFolders,
            [NotNull] ISnapNetworkTimeProvider snapNetworkTimeProvider, [NotNull] ISnapExtractor snapExtractor, [NotNull] ISnapOs snapOs,
            [NotNull] ISnapxEmbeddedResources snapxEmbeddedResources, [NotNull] ICoreRunLib coreRunLib,
            [NotNull] ILog logger, [NotNull] string workingDirectory, CancellationToken cancellationToken)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            if (filesystem == null) throw new ArgumentNullException(nameof(filesystem));
            if (snapAppReader == null) throw new ArgumentNullException(nameof(snapAppReader));
            if (snapAppWriter == null) throw new ArgumentNullException(nameof(snapAppWriter));
            if (nuGetPackageSources == null) throw new ArgumentNullException(nameof(nuGetPackageSources));
            if (nugetService == null) throw new ArgumentNullException(nameof(nugetService));
            if (distributedMutexClient == null) throw new ArgumentNullException(nameof(distributedMutexClient));
            if (snapPackageManager == null) throw new ArgumentNullException(nameof(snapPackageManager));
            if (snapPack == null) throw new ArgumentNullException(nameof(snapPack));
            if (specialFolders == null) throw new ArgumentNullException(nameof(specialFolders));
            if (snapNetworkTimeProvider == null) throw new ArgumentNullException(nameof(snapNetworkTimeProvider));
            if (snapExtractor == null) throw new ArgumentNullException(nameof(snapExtractor));
            if (snapOs == null) throw new ArgumentNullException(nameof(snapOs));
            if (snapxEmbeddedResources == null) throw new ArgumentNullException(nameof(snapxEmbeddedResources));
            if (coreRunLib == null) throw new ArgumentNullException(nameof(coreRunLib));
            if (logger == null) throw new ArgumentNullException(nameof(logger));
            if (workingDirectory == null) throw new ArgumentNullException(nameof(workingDirectory));
            if (options == null) throw new ArgumentNullException(nameof(options));
            if (nugetService == null) throw new ArgumentNullException(nameof(nugetService));

            var stopWatch = new Stopwatch();
            stopWatch.Restart();

            options.Channel = string.IsNullOrWhiteSpace(options.Channel) ? null : options.Channel;

            if (options.Channel != null && !options.Channel.IsValidChannelName())
            {
                logger.Error($"Invalid channel name: {options.Channel}");
                return 1;
            }

            var (snapApps, snapApp, error, _) = BuildSnapAppFromDirectory(filesystem, snapAppReader,
                nuGetPackageSources, options.AppId, options.Rid, workingDirectory);
            if (snapApp == null)
            {
                if (!error)
                {
                    logger.Error($"Unable to find snap with id: {options.AppId}. Rid: {options.Rid}.");
                }

                return 1;
            }

            var demoteBaseChannel = options.All ? snapApp.Channels.First() : 
                snapApp.Channels.SingleOrDefault(x => string.Equals(x.Name, options.Channel, StringComparison.OrdinalIgnoreCase));
            if (demoteBaseChannel == null)
            {
                logger.Error($"Unable to find channel: {options.Channel}.");
                return 1;
            }

            if (string.IsNullOrWhiteSpace(snapApps.Generic.Token))
            {
                logger.Error("Please specify a token in your snapx.yml file. A random UUID is sufficient.");
                return -1;
            }

            await using var distributedMutex = WithDistributedMutex(distributedMutexClient, logger, snapApps.BuildLockKey(snapApp), cancellationToken);

            logger.Info('-'.Repeat(TerminalBufferWidth));

            var tryAcquireRetries = options.LockRetries == -1 ? int.MaxValue : options.LockRetries;
            if (!await distributedMutex.TryAquireAsync(TimeSpan.FromSeconds(15), tryAcquireRetries))
            {
                logger.Info('-'.Repeat(TerminalBufferWidth));
                return -1;
            }

            var channelsStr = string.Join(", ", snapApp.Channels.Select(x => x.Name));

            logger.Info('-'.Repeat(TerminalBufferWidth));
            logger.Info($"Snap id: {options.AppId}");
            logger.Info($"Rid: {options.Rid}");
            logger.Info($"Source channel: {options.Channel}");
            logger.Info($"Channels: {channelsStr}");
            logger.Info('-'.Repeat(TerminalBufferWidth));

            logger.Info("Downloading releases nupkg.");
            var (snapAppsReleases, _, releasesMemoryStream) = await snapPackageManager.GetSnapsReleasesAsync(snapApp, logger, cancellationToken);

            if (releasesMemoryStream != null)
            {
                await releasesMemoryStream.DisposeAsync();
            }

            if (snapAppsReleases == null)
            {
                logger.Error($"Unknown error downloading releases nupkg: {snapApp.BuildNugetReleasesFilename()}.");
                return 1;
            }

            var mostRecentRelease = snapAppsReleases.GetMostRecentRelease(snapApp, demoteBaseChannel);
            if (mostRecentRelease == null)
            {
                logger.Error($"Unable to find any releases in channel: {demoteBaseChannel.Name}.");
                return 1;
            }

            snapApp.Version = mostRecentRelease.Version;

            var currentChannelIndex= options.All ? -1 : mostRecentRelease.Channels.FindIndex(channelName => channelName == demoteBaseChannel.Name);
            var demoteableChannels = snapApp.Channels
                .Skip(currentChannelIndex)
                .Select(channel =>
                {
                    var releasesThisChannel = snapAppsReleases.GetReleases(snapApp, channel);
                    return releasesThisChannel.Any(x => mostRecentRelease.IsFull ? x.IsFull : x.IsDelta && x.Version == snapApp.Version) ? null : channel;
                })
                .Where(x => x != null)
                .ToList();

            var demoteChannelsStr = string.Join(", ", demoteableChannels.Select(x => x.Name));
            if (!logger.Prompt("y|yes",
                $"You are about to demote {snapApp.Id} ({snapApp.Version}) for the following " +
                $"channel{(demoteableChannels.Count > 1 ? "s" : string.Empty)}: {demoteChannelsStr}. " +
                "Do you want to continue? [y|n]")
            )
            {
                return 1;
            }

            if (options.All)
            {
                snapAppsReleases.Releases.Remove(mostRecentRelease);
            }
            else
            {
                foreach (var channel in demoteableChannels)
                {
                    mostRecentRelease.Channels.Remove(channel.Name);
                }
            }

            logger.Info("Building releases nupkg.");

            var nowUtc = await SnapUtility.RetryAsync(async () => await snapNetworkTimeProvider.NowUtcAsync(), 3, 1500);
            if (!nowUtc.HasValue)
            {
                logger.Error($"Unknown error while retrieving NTP timestamp from server: {snapNetworkTimeProvider}");
                return 1;
            }

            snapAppsReleases.LastWriteAccessUtc = nowUtc.Value;

            await using var releasesPackageMemoryStream = snapPack.BuildReleasesPackage(snapApp, snapAppsReleases);
            logger.Info("Finished building releases nupkg.");

            var restoreOptions = new RestoreOptions
            {
                AppId = options.AppId,
                Rid = options.Rid,
                BuildInstallers = false
            };

            var restoreSuccess = 0 == await CommandRestoreAsync(
                restoreOptions, filesystem, snapAppReader, snapAppWriter, nuGetPackageSources, nugetService, snapExtractor,
                snapPackageManager, snapOs, snapxEmbeddedResources, coreRunLib, snapPack,
                logger, workingDirectory, cancellationToken
            );

            if (!restoreSuccess)
            {
                return 1;
            }

            const int pushRetries = 3;

            using var tmpDir = new DisposableDirectory(specialFolders.NugetCacheDirectory, filesystem);
            var releasesPackageFilename = snapApp.BuildNugetReleasesFilename();
            var releasesPackageAbsolutePath = filesystem.PathCombine(tmpDir.WorkingDirectory, releasesPackageFilename);
            await filesystem.FileWriteAsync(releasesPackageMemoryStream, releasesPackageAbsolutePath, cancellationToken);

            logger.Info('-'.Repeat(TerminalBufferWidth));

            foreach (var (channel, packageSource) in demoteableChannels.Select(snapChannel =>
            {
                var packageSource = nuGetPackageSources.Items.Single(x => x.Name == snapChannel.PushFeed.Name);
                return (snapChannel, packageSource);
            }).DistinctBy(x => x.packageSource.SourceUri))
            {
                logger.Info($"Uploading releases nupkg to feed: {packageSource.Name}.");

                var success = await SnapUtility.RetryAsync(
                    async () =>
                    {
                        await nugetService.PushAsync(releasesPackageAbsolutePath, nuGetPackageSources, packageSource, cancellationToken: cancellationToken);
                        return true;
                    }, pushRetries);

                if (!success)
                {
                    logger.Error("Unknown error while uploading nupkg.");
                    return 1;
                }

                var retryInterval = TimeSpan.FromSeconds(15);

                await BlockUntilSnapUpdatedReleasesNupkgAsync(logger, snapPackageManager, snapAppsReleases, snapApp, channel, retryInterval,
                    cancellationToken);

                logger.Info($"Successfully uploaded releases nupkg to channel: {channel.Name}.");
                logger.Info('-'.Repeat(TerminalBufferWidth));
            }

            restoreOptions.BuildInstallers = true;
            
            restoreSuccess = 0 == await CommandRestoreAsync(
                restoreOptions, filesystem, snapAppReader, snapAppWriter, nuGetPackageSources, nugetService, snapExtractor,
                snapPackageManager, snapOs, snapxEmbeddedResources, coreRunLib, snapPack,
                logger, workingDirectory, cancellationToken
            );

            if (!restoreSuccess)
            {
                return 1;
            }

            logger.Info($"Demote completed in {stopWatch.Elapsed.TotalSeconds:0.0}s.");

            await CommandListAsync(new ListOptions {Id = snapApp.Id}, filesystem, snapAppReader,
                nuGetPackageSources, nugetService, snapExtractor, logger, workingDirectory, cancellationToken);

            return 0;
        }
    }
}
