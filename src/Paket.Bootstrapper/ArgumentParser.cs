﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Paket.Bootstrapper
{
    public static class ArgumentParser
    {
        public static class CommandArgs
        {
            public const string Help = "--help";
            public const string PreferNuget = "--prefer-nuget";
            public const string ForceNuget = "--force-nuget";
            public const string Prerelease = "prerelease";
            public const string NugetSourceArgPrefix = "--nuget-source=";
            public const string SelfUpdate = "--self";
            public const string Silent = "-s";
            public const string IgnoreCache = "-f";
            public const string MaxFileAge = "--max-file-age=";
            public const string IgnoreSSL = "--no-ssl";
        }
        public static class AppSettingKeys
        {
            public const string PreferNugetAppSettingsKey = "PreferNuget";
            public const string ForceNugetAppSettingsKey = "ForceNuget";
            public const string PaketVersionAppSettingsKey = "PaketVersion";
        }
        public static class EnvArgs
        {
            public const string PaketVersionEnv = "PAKET.VERSION";
        }

        public static BootstrapperOptions ParseArgumentsAndConfigurations(IEnumerable<string> arguments, NameValueCollection appSettings, IDictionary envVariables)
        {
            var options = new BootstrapperOptions();

            var commandArgs = arguments.ToList();

            if (commandArgs.Contains(CommandArgs.PreferNuget) || appSettings.GetKey(AppSettingKeys.PreferNugetAppSettingsKey).ToLowerSafe() == "true")
            {
                options.PreferNuget = true;
                commandArgs.Remove(CommandArgs.PreferNuget);
            }
            if (commandArgs.Contains(CommandArgs.ForceNuget) || appSettings.GetKey(AppSettingKeys.ForceNugetAppSettingsKey).ToLowerSafe() == "true")
            {
                options.ForceNuget = true;
                commandArgs.Remove(CommandArgs.ForceNuget);
            }
            if (commandArgs.Contains(CommandArgs.Silent))
            {
                options.Silent = true;
                commandArgs.Remove(CommandArgs.Silent);
            }
            if (commandArgs.Contains(CommandArgs.Help))
            {
                options.ShowHelp = true;
                commandArgs.Remove(CommandArgs.Help);
            }

            commandArgs = EvaluateDownloadOptions(options.DownloadArguments, commandArgs, appSettings, envVariables).ToList();

            options.UnprocessedCommandArgs = commandArgs;
            return options;
        }

        private static IEnumerable<string> EvaluateDownloadOptions(DownloadArguments downloadArguments, IEnumerable<string> args, NameValueCollection appSettings, IDictionary envVariables)
        {
            var folder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var target = Path.Combine(folder, "paket.exe");
            string nugetSource = null;

            var latestVersion = appSettings.GetKey(AppSettingKeys.PaketVersionAppSettingsKey) ?? envVariables.GetKey(EnvArgs.PaketVersionEnv) ?? String.Empty;
            var ignorePrerelease = true;
            bool doSelfUpdate = false;
            var ignoreCache = false;
            var commandArgs = args.ToList();
            int? maxFileAgeInMinutes = null;
            bool ignoreSSL = false;

            if (commandArgs.Contains(CommandArgs.SelfUpdate))
            {
                commandArgs.Remove(CommandArgs.SelfUpdate);
                doSelfUpdate = true;
            }
            var nugetSourceArg = commandArgs.SingleOrDefault(x => x.StartsWith(CommandArgs.NugetSourceArgPrefix));
            if (nugetSourceArg != null)
            {
                commandArgs = commandArgs.Where(x => !x.StartsWith(CommandArgs.NugetSourceArgPrefix)).ToList();
                nugetSource = nugetSourceArg.Substring(CommandArgs.NugetSourceArgPrefix.Length);
            }
            if (commandArgs.Contains(CommandArgs.IgnoreCache))
            {
                commandArgs.Remove(CommandArgs.IgnoreCache);
                ignoreCache = true;
            }

            var maxFileAgeArg = commandArgs.SingleOrDefault(x => x.StartsWith(CommandArgs.MaxFileAge, StringComparison.Ordinal));
            if (maxFileAgeArg != null)
            {
                var parts = maxFileAgeArg.Split("=".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 2)
                {
                    var maxFileAgeInMinutesCommandArg = parts[1];
                    int parsedMaxFileAgeInMinutesCommandArg;
                    if (int.TryParse(maxFileAgeInMinutesCommandArg, out parsedMaxFileAgeInMinutesCommandArg))
                        maxFileAgeInMinutes = parsedMaxFileAgeInMinutesCommandArg;
                }

                commandArgs.Remove(maxFileAgeArg);
            }

            if (commandArgs.Contains(CommandArgs.IgnoreSSL))
            {
                commandArgs.Remove(CommandArgs.IgnoreSSL);
                ignoreSSL = true;
            }

            if (commandArgs.Count >= 1)
            {
                if (commandArgs[0] == CommandArgs.Prerelease)
                {
                    ignorePrerelease = false;
                    latestVersion = String.Empty;
                    commandArgs.Remove(CommandArgs.Prerelease);
                }
                else
                {
                    latestVersion = commandArgs[0];
                    commandArgs.Remove(commandArgs[0]);
                }
            }

            downloadArguments.LatestVersion = latestVersion;
            downloadArguments.IgnorePrerelease = ignorePrerelease;
            downloadArguments.IgnoreCache = ignoreCache;
            downloadArguments.NugetSource = nugetSource;
            downloadArguments.DoSelfUpdate = doSelfUpdate;
            downloadArguments.Target = target;
            downloadArguments.Folder = folder;
            downloadArguments.MaxFileAgeInMinutes = maxFileAgeInMinutes;
            downloadArguments.IgnoreSSL = ignoreSSL;
            return commandArgs;
        }

        private static string GetKey(this NameValueCollection appSettings, string key)
        {
            if (appSettings != null && appSettings.AllKeys.Any(x => x == key))
                return appSettings.Get(key);
            return null;
        }

        private static string GetKey(this IDictionary dictionary, string key)
        {
            if (dictionary != null && dictionary.Keys.Cast<string>().Any(x => x == key))
                return dictionary[key].ToString();
            return null;
        }

        private static string ToLowerSafe(this string value)
        {
            if (value != null)
                return value.ToLower();
            return null;
        }
    }
}
