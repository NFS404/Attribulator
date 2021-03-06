﻿using System.Collections.Generic;
using System.IO;
using VaultLib.Core.Data;
using VaultLib.Core.DB;
using YAMLDatabase.ModScript.Utils;

namespace YAMLDatabase.ModScript.Commands
{
    // copy_node class sourceNode parentNode nodeName
    public class CopyNodeModScriptCommand : BaseModScriptCommand
    {
        public string ClassName { get; set; }
        public string SourceCollectionName { get; set; }
        public string ParentCollectionName { get; set; }
        public string DestinationCollectionName { get; set; }

        public override void Parse(List<string> parts)
        {
            if (parts.Count != 4 && parts.Count != 5)
            {
                throw new ModScriptParserException($"4 or 5 tokens expected, got {parts.Count}");
            }

            ClassName = parts[1];
            SourceCollectionName = parts[2];
            ParentCollectionName = parts.Count == 5 ? parts[3] : "";
            DestinationCollectionName = parts[^1];
        }

        public override void Execute(ModScriptDatabaseHelper database)
        {
            VltCollection collection = GetCollection(database, ClassName, SourceCollectionName);

            if (collection == null)
            {
                throw new InvalidDataException($"copy_node failed because there is no collection called '{SourceCollectionName}'");
            }

            if (database.FindCollectionByName(ClassName, DestinationCollectionName) != null)
            {
                throw new InvalidDataException($"copy_node failed because there is already a collection called '{DestinationCollectionName}'");
            }

            VltCollection parentCollection = null;

            if (!string.IsNullOrWhiteSpace(ParentCollectionName))
            {
                parentCollection = database.FindCollectionByName(ClassName, ParentCollectionName);

                if (parentCollection == null)
                {
                    throw new InvalidDataException($"copy_node failed because the parent collection called '{ParentCollectionName}' does not exist");
                }
            }

            VltCollection newCollection = new VltCollection(collection.Vault, collection.Class, DestinationCollectionName);
            CopyCollection(database.Database, collection, newCollection);

            if (newCollection.Class.HasField("CollectionName"))
            {
                newCollection.SetDataValue("CollectionName", DestinationCollectionName);
            }

            database.AddCollection(newCollection, parentCollection);
        }

        private void CopyCollection(Database database, VltCollection from, VltCollection to)
        {
            foreach (var dataPair in from.GetData())
            {
                VltClassField field = from.Class[dataPair.Key];
                to.SetRawValue(dataPair.Key, ValueCloningUtils.CloneValue(database, dataPair.Value, to.Class, field, to));
            }
        }
    }
}