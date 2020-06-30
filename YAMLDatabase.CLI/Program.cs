﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommandLine;
using McMaster.NETCore.Plugins;
using Microsoft.Extensions.DependencyInjection;
using YAMLDatabase.API;
using YAMLDatabase.API.Plugin;
using YAMLDatabase.API.Services;
using YAMLDatabase.CLI.Services;

namespace YAMLDatabase.CLI
{
    internal static class Program
    {
        public static async Task<int> Main(string[] args)
        {
            // Setup
            var services = new ServiceCollection();
            var loaders = GetPluginLoaders();

            // Register services
            services.AddSingleton<ICommandService, CommandServiceImpl>();
            services.AddSingleton<IProfileService, ProfileServiceImpl>();
            services.AddSingleton<IStorageFormatService, StorageFormatServiceImpl>();
            ConfigureServices(services, loaders);

            await using var serviceProvider = services.BuildServiceProvider();

            // Load commands and profiles from DI container
            LoadCommands(services, serviceProvider);
            LoadProfiles(services, serviceProvider);

            // Off to the races!
            return await RunApplication(serviceProvider, args);
        }

        private static async Task<int> RunApplication(IServiceProvider serviceProvider, IEnumerable<string> args)
        {
            var commandService = serviceProvider.GetRequiredService<ICommandService>();
            var commandTypes = commandService.GetCommandTypes().ToArray();
            return await Parser.Default.ParseArguments(args, commandTypes)
                .MapResult((BaseCommand cmd) =>
                {
                    cmd.SetServiceProvider(serviceProvider);
                    return cmd.Execute();
                }, errs => Task.FromResult(1));
        }

        private static void LoadCommands(ServiceCollection services, IServiceProvider serviceProvider)
        {
            var commandTypes = (from service in services
                where typeof(BaseCommand).IsAssignableFrom(service.ImplementationType)
                select service.ImplementationType).ToList();
            var commandService = serviceProvider.GetRequiredService<ICommandService>();
            foreach (var commandType in commandTypes) commandService.RegisterCommand(commandType);
        }

        private static void LoadProfiles(ServiceCollection services, IServiceProvider serviceProvider)
        {
            var profileTypes = (from service in services
                where typeof(IProfile).IsAssignableFrom(service.ImplementationType)
                select service.ImplementationType).ToList();
            var profileService = serviceProvider.GetRequiredService<IProfileService>();
            foreach (var profileType in profileTypes) profileService.RegisterProfile(profileType);
        }

        private static IEnumerable<PluginLoader> GetPluginLoaders()
        {
            // create plugin loaders
            var pluginsDir = Path.Combine(AppContext.BaseDirectory, "plugins");

            return (from dir in Directory.GetDirectories(pluginsDir)
                let dirName = Path.GetFileName(dir)
                select Path.Combine(dir, dirName + ".dll")
                into pluginDll
                where File.Exists(pluginDll)
                select PluginLoader.CreateFromAssemblyFile(pluginDll, new[]
                {
                    // Basic stuff
                    typeof(IPluginFactory), typeof(IServiceCollection),

                    // Application stuff
                    typeof(BaseCommand), typeof(IProfile),

                    // CommandLineParser
                    typeof(VerbAttribute)
                }, conf => conf.PreferSharedTypes = true)).ToList();
        }

        private static void ConfigureServices(IServiceCollection services, IEnumerable<PluginLoader> loaders)
        {
            // Create an instance of plugin types
            foreach (var loader in loaders)
            foreach (var pluginType in loader
                .LoadDefaultAssembly()
                .GetTypes()
                .Where(t => typeof(IPluginFactory).IsAssignableFrom(t) && !t.IsAbstract))
            {
                // This assumes the implementation of IPluginFactory has a parameterless constructor
                var plugin = Activator.CreateInstance(pluginType) as IPluginFactory;
                plugin?.Configure(services);
            }
        }
    }
}