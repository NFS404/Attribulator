﻿using YAMLDatabase.API.Plugin;
using YAMLDatabase.ModScript.API;
using YAMLDatabase.Plugins.ModScript.Commands;

namespace YAMLDatabase.Plugins.ModScript
{
    /// <summary>
    ///     Base class for the ModScript plugin.
    /// </summary>
    public class ModScriptPlugin : IPlugin
    {
        private readonly IModScriptService _modScriptService;

        public ModScriptPlugin(IModScriptService modScriptService)
        {
            _modScriptService = modScriptService;
        }

        public string GetName()
        {
            return "ModScript Support";
        }

        public void Init()
        {
            _modScriptService.RegisterCommand<AppendArrayModScriptCommand>("append_array");
            _modScriptService.RegisterCommand<VersionModScriptCommand>("version");
            _modScriptService.RegisterCommand<GameModScriptCommand>("game");
            _modScriptService.RegisterCommand<ResizeFieldModScriptCommand>("resize_field");
            _modScriptService.RegisterCommand<UpdateFieldModScriptCommand>("update_field");
            _modScriptService.RegisterCommand<CopyNodeModScriptCommand>("copy_node");
            _modScriptService.RegisterCommand<AddNodeModScriptCommand>("add_node");
            _modScriptService.RegisterCommand<ChangeVaultModScriptCommand>("change_vault");
            _modScriptService.RegisterCommand<CopyFieldsModScriptCommand>("copy_fields");
            _modScriptService.RegisterCommand<DeleteNodeModScriptCommand>("delete_node");
            _modScriptService.RegisterCommand<AddFieldModScriptCommand>("add_field");
            _modScriptService.RegisterCommand<DeleteFieldModScriptCommand>("delete_field");
            _modScriptService.RegisterCommand<RenameNodeModScriptCommand>("rename_node");
            _modScriptService.RegisterCommand<MoveNodeModScriptCommand>("move_node");
        }
    }
}