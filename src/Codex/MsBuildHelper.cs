using System;
using System.IO;
using System.Linq;
using Codex.Logging;
using Codex.ObjectModel;
using Codex.Utilities;
using Microsoft.Build.Locator;
using Git = LibGit2Sharp;

namespace Codex.Application
{
    internal class MSBuildHelper
    {
        private static Lazy<bool> MSBuildRegistration = new Lazy<bool>(RegisterMSBuildCore);

        private static bool RegisterMSBuildCore()
        {
            try
            {
                MSBuildLocator.RegisterDefaults();
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error registering MSBuild locator: {ex.Message}");
                return false;
            }
        }

        public static void RegisterMSBuild()
        {
            var ignored = MSBuildRegistration.Value;
        }
    }
}