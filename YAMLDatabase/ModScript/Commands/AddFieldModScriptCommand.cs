﻿using System.Collections.Generic;
using System.IO;
using VaultLib.Core;
using VaultLib.Core.Data;
using VaultLib.Core.Types;

namespace YAMLDatabase.ModScript.Commands
{
    // add_field class node field
    public class AddFieldModScriptCommand : BaseModScriptCommand
    {
        public string ClassName { get; set; }
        public string CollectionName { get; set; }
        public string FieldName { get; set; }
        public ushort ArrayCapacity { get; set; }

        public override void Parse(List<string> parts)
        {
            if (parts.Count != 4 && parts.Count != 5)
            {
                throw new ModScriptParserException($"Expected 4 or 5 tokens, got {parts.Count}");
            }

            ClassName = CleanHashString(parts[1]);
            CollectionName = CleanHashString(parts[2]);
            FieldName = CleanHashString(parts[3]);

            if (parts.Count == 5)
            {
                ArrayCapacity = ushort.Parse(parts[4]);
            }
        }

        public override void Execute(ModScriptDatabaseHelper database)
        {
            VltCollection collection = GetCollection(database, ClassName, CollectionName);
            VltClassField field = collection.Class[FieldName];

            if (field.IsInLayout)
            {
                throw new InvalidDataException($"add_field failed because field '{field.Name}' is a base field");
            }

            if (collection.HasEntry(field.Name))
            {
                throw new InvalidDataException($"add_field failed because collection '{collection.ShortPath}' already has field '{field.Name}'");
            }

            var vltBaseType = TypeRegistry.CreateInstance(database.Database.Options.GameId, collection.Class, field, collection);

            if (vltBaseType is VLTArrayType array)
            {
                if (ArrayCapacity > field.MaxCount)
                {
                    throw new ModScriptCommandExecutionException(
                        $"Cannot add field {ClassName}[{FieldName}] with capacity beyond maximum (requested {ArrayCapacity} but limit is {field.MaxCount})");
                }

                array.Capacity = ArrayCapacity;
                array.ItemAlignment = field.Alignment;
                array.FieldSize = field.Size;
                array.Items = new List<VLTBaseType>();

                for (var i = 0; i < ArrayCapacity; i++)
                {
                    array.Items.Add(TypeRegistry.ConstructInstance(array.ItemType, collection.Class, field, collection));
                }
            }

            collection.SetRawValue(field.Name, vltBaseType);
        }
    }
}