﻿using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.InteropServices;
using JetBrains.Annotations;
using NuGet.Versioning;

namespace Snap.Core.Models
{
    public sealed class SnapsChannel
    {
        public string Name { get; set; }
        public string PushFeed { get; set; }
        public string UpdateFeed { get; set; }

        [UsedImplicitly]
        public SnapsChannel()
        {
            
        }

        internal SnapsChannel([NotNull] SnapChannel snapChannel)
        {
            if (snapChannel == null) throw new ArgumentNullException(nameof(snapChannel));
            Name = snapChannel.Name;
            PushFeed = snapChannel.PushFeed.Name;

            switch (snapChannel.UpdateFeed)
            {
                case SnapNugetFeed snapNugetFeed:
                    UpdateFeed = snapNugetFeed.Name;
                    break;
                case SnapHttpFeed snapHttpFeed:
                    UpdateFeed = snapHttpFeed.ToStringSnapUrl();
                    break;
                default:
                    throw new NotSupportedException($"Unknown feed type: {snapChannel.UpdateFeed?.GetType()}.");
            }
        }
    }

    public sealed class SnapsCertificate
    {
        public string Name { get; set; }
        public string Csn { get; set; }
        public string Sha256 { get; set; }

        [UsedImplicitly]
        public SnapsCertificate()
        {

        }

        internal SnapsCertificate([NotNull] SnapCertificate certificate)
        {
            if (certificate == null) throw new ArgumentNullException(nameof(certificate));
            Name = certificate.Name;
            Csn = certificate.Csn;
            Sha256 = certificate.Sha256;
        }
    }

    public sealed class SnapsTarget
    {
        public OSPlatform Os { get; set; }
        public string Name { get; set; }
        public string Framework { get; set; }
        public string Rid { get; set; }
        public string Nuspec { get; set; }

        [UsedImplicitly]
        public SnapsTarget()
        {

        }

        internal SnapsTarget([NotNull] SnapTarget target)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            Os = target.Os;
            Name = target.Name;
            Framework = target.Framework;
            Rid = target.Rid;
            Nuspec = target.Nuspec;
        }
    }

    public sealed class SnapsApp
    {
        public string Id { get; set; }
        public SemanticVersion Version { get; set; }
        public string Certificate { get; set; }
        public List<string> Channels { get; set; }
        public List<SnapsTarget> Targets { get; set; }

        [UsedImplicitly]
        public SnapsApp()
        {
            Channels = new List<string>();
            Targets = new List<SnapsTarget>();
        }

        internal SnapsApp([NotNull] SnapApp snapApp)
        {
            if (snapApp == null) throw new ArgumentNullException(nameof(snapApp));
            Id = snapApp.Id;
            Version = snapApp.Version;
            Certificate = snapApp.Certificate?.Name;
            Channels = snapApp.Channels.Select(x => x.Name).ToList();
            Targets = new List<SnapsTarget> { new SnapsTarget(snapApp.Target) };
        }
    }

    [SuppressMessage("ReSharper", "UnusedMember.Global")]
    public sealed class SnapApps
    {
        public List<SnapsChannel> Channels { get; set; }
        public List<SnapsCertificate> Certificates { get; set; }
        public List<SnapsApp> Apps { get; set; }

        public SnapApps()
        {
            Channels = new List<SnapsChannel>();
            Certificates = new List<SnapsCertificate>();
            Apps = new List<SnapsApp>();
        }

        internal SnapApps([NotNull] SnapApp snapApp)
        {
            if (snapApp == null) throw new ArgumentNullException(nameof(snapApp));
            Channels = snapApp.Channels.Select(x => new SnapsChannel(x)).ToList();
            Apps = new List<SnapsApp> { new SnapsApp(snapApp) };
            Certificates = new List<SnapsCertificate> { new SnapsCertificate(snapApp.Certificate) };
        }

    }
}