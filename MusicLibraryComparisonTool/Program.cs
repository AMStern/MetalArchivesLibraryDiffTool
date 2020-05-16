﻿using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading;

namespace MediaLibraryCompareTool
{
    /// <summary>
    /// The purpose of this application is to compare the content of a given library of metal music against a database of known metal releases.
    /// Primarily to help me find good things I might be missing in my collection. :)
    /// 
    /// TODO: provide search results from youtube for missing albums from my list?
    /// TODO: or bandcamp
    /// </summary>
    [ExcludeFromCodeCoverage]
    class Program
    {
        private static MetalArchivesServiceClient _metalArchivesServiceClient;
        private static MetalArchivesServiceProvider _metalArchivesServiceProvider;
        private static MetalArchivesResponseParser _metalArchivesResponseParser;

        private static DirectoryInfo LibraryLocation { get; set; }

        private static DirectoryInfo LibraryDiffOutputLocation { get; set; }

        private static MetalArchivesServiceProvider MetalArchivesServiceProvider
        {
            get { return _metalArchivesServiceProvider ?? (_metalArchivesServiceProvider = new MetalArchivesServiceProvider()); }
        }

        private static MetalArchivesResponseParser MetalArchivesResponseParser
        {
            get { return _metalArchivesResponseParser ?? (_metalArchivesResponseParser = new MetalArchivesResponseParser()); }
        }

        private static MetalArchivesServiceClient MetalArchivesServiceClient
        {
            get { return _metalArchivesServiceClient ?? (_metalArchivesServiceClient = new MetalArchivesServiceClient(MetalArchivesServiceProvider, MetalArchivesResponseParser)); }
        }

        /// <summary>
        /// Accepts two parameters:
        ///     "in={PathToYourMusicCollection}", 
        ///     "out={PathToLibraryComparisonResult}"
        /// then writes a text file to the location specified by the out param.
        /// 
        /// The music collection is expected to be organized in the form of "{PathToYourMusicCollection}\ArtistName\AlbumName"
        /// </summary>
        /// <param name="args"></param>
        static void Main(string[] args)
        {
            MusicLibrary differences = null;

            try
            {
                ParseArgs(args);

                var localMusicLibrary = GetLocalMusicLibrary();
                var remoteMusicLibrary = GetRemoteMusicLibrary(localMusicLibrary);
                differences = CompareLibraries(localMusicLibrary, remoteMusicLibrary);
            }
            catch (Exception exc)
            {
                Console.WriteLine($"Exception: {exc.Message}");
            }
            finally
            {
                WriteResults(differences);
            }
        }

        private static void ParseArgs(string[] args)
        {
            foreach (string arg in args)
            {
                var argKey = arg.Split('=')[0];
                var argValue = arg.Split('=')[1];

                switch (argKey.ToUpperInvariant())
                {
                    case "IN":
                    case "/IN":
                        LibraryLocation = new DirectoryInfo(argValue);
                        break;
                    case "OUT":
                    case "/OUT":
                        LibraryDiffOutputLocation = new DirectoryInfo(argValue);
                        break;
                    default:
                        break;
                }
            }
        }

        private static MusicLibrary CompareLibraries(MusicLibrary local, MusicLibrary remote)
        {
            return (new MusicLibraryComparer()).Compare(local, remote).RightOutersection;
        }

        private static MusicLibrary GetLocalMusicLibrary()
        {
            var localMusicLibrary = new MusicLibrary(LibraryLocation);
            Console.WriteLine($"Discovered {localMusicLibrary.Collection.Count} items on disk");
            return localMusicLibrary;
        }

        private static MusicLibrary GetRemoteMusicLibrary(MusicLibrary musicLibrary)
        {
            var remoteMusicLibrary = new MusicLibrary(new List<MusicLibraryItem>());

            foreach (string artistName in musicLibrary.Collection.Select(mli => mli.ArtistData).Select(ad => ad.ArtistName).Distinct())
            {
                remoteMusicLibrary.AddToCollection(MetalArchivesServiceClient.FindByArtist(artistName));
                Console.WriteLine($"Added {artistName} to library");
                Thread.Sleep(3000);
            }

            return remoteMusicLibrary;
        }

        private static void WriteResults(MusicLibrary differences)
        { 
            if (!Directory.Exists(LibraryDiffOutputLocation.Parent.FullName))
            {
                Directory.CreateDirectory(LibraryDiffOutputLocation.Parent.FullName);
            }

            // TODO: cleanup
            string timestampedFileName = 
                LibraryDiffOutputLocation.FullName.Replace(LibraryDiffOutputLocation.Extension, "") + 
                "_" + DateTime.Now.ToLongTimeString().Replace(":", "_").Replace(" ", "_") +
                LibraryDiffOutputLocation.Extension;

            string text = String.Join(Environment.NewLine, differences?.Collection);
            File.WriteAllText(timestampedFileName, text);
        }
    }
}