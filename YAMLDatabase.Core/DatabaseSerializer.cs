﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using VaultLib.Core.Data;
using VaultLib.Core.DB;
using VaultLib.Core.Types;
using VaultLib.Core.Types.Attrib;
using VaultLib.Core.Types.EA.Reflection;
using VaultLib.Core.Utils;
using YamlDotNet.Serialization;

namespace YAMLDatabase.Core
{
    /// <summary>
    /// Serializes a <see cref="VaultLib.Core.DB.Database"/> to YAML files
    /// </summary>
    public class DatabaseSerializer
    {
        private readonly Database _database;
        private readonly string _outputDirectory;

        /// <summary>
        /// Initializes a new instance of the <see cref="DatabaseSerializer"/> class.
        /// </summary>
        /// <param name="database">The database to serialize.</param>
        /// <param name="directory">The directory to output YAML files to.</param>
        public DatabaseSerializer(Database database, string directory)
        {
            _database = database;
            _outputDirectory = directory;
        }

        /// <summary>
        /// Serializes the database.
        /// </summary>
        /// <param name="files">The files that were loaded.</param>
        public void Serialize(IEnumerable<LoadedDatabaseFile> files)
        {
            var loadedDatabase = new LoadedDatabase
            {
                Classes = new List<LoadedDatabaseClass>(),
                Files = new List<LoadedDatabaseFile>(),
                Types = new List<LoadedTypeInfo>()
            };

            loadedDatabase.Files.AddRange(files);

            foreach (var databaseType in _database.Types)
            {
                loadedDatabase.Types.Add(new LoadedTypeInfo
                {
                    Name = databaseType.Name,
                    Size = databaseType.Size
                });
            }

            foreach (var databaseClass in _database.Classes)
            {
                var loadedDatabaseClass = new LoadedDatabaseClass
                {
                    Name = databaseClass.Name,
                    Fields = new List<LoadedDatabaseClassField>()
                };

                loadedDatabaseClass.Fields.AddRange(databaseClass.Fields.Values.Select(field =>
                    new LoadedDatabaseClassField
                    {
                        Name = field.Name,
                        TypeName = field.TypeName,
                        Alignment = field.Alignment,
                        Flags = field.Flags,
                        MaxCount = field.MaxCount,
                        Size = field.Size,
                        Offset = field.Offset,
                        StaticValue = ConvertDataValueToSerializedValue(_outputDirectory, null, field, field.StaticValue)
                    }));

                loadedDatabase.Classes.Add(loadedDatabaseClass);
            }

            var serializerBuilder = new SerializerBuilder();
            var serializer = serializerBuilder.Build();

            using var sw = new StreamWriter(Path.Combine(_outputDirectory, "info.yml"));
            serializer.Serialize(sw, loadedDatabase);

            foreach (var loadedDatabaseFile in loadedDatabase.Files)
            {
                var baseDirectory = Path.Combine(_outputDirectory, loadedDatabaseFile.Group, loadedDatabaseFile.Name);
                Directory.CreateDirectory(baseDirectory);

                foreach (var vault in loadedDatabaseFile.LoadedVaults)
                {
                    var vaultDirectory = Path.Combine(baseDirectory, vault.Name).Trim();
                    Directory.CreateDirectory(vaultDirectory);

                    // Problem: Gameplay data is separated into numerous vaults, so we can't easily construct a proper hierarchy
                    // Solution: Store the name of the parent node instead of having an array of children.

                    foreach (var collectionGroup in _database.RowManager.GetCollectionsInVault(vault)
                        .GroupBy(v => v.Class.Name))
                    {
                        var loadedCollections = new List<LoadedCollection>();
                        AddLoadedCollections(vaultDirectory, loadedCollections, collectionGroup);

                        using var vw = new StreamWriter(Path.Combine(vaultDirectory, collectionGroup.Key + ".yml"));
                        serializer.Serialize(vw, loadedCollections);
                    }
                }
            }
        }

        private void AddLoadedCollections(string directory, ICollection<LoadedCollection> loadedVaultCollections, IEnumerable<VltCollection> vltCollections)
        {
            foreach (var vltCollection in vltCollections)
            {
                var loadedCollection = new LoadedCollection
                {
                    Name = vltCollection.Name,
                    ParentName = vltCollection.Parent?.Name,
                    Data = new Dictionary<string, object>()
                };

                foreach (var (key, value) in vltCollection.GetData())
                {
                    loadedCollection.Data[key] = ConvertDataValueToSerializedValue(directory, vltCollection, vltCollection.Class[key], value);
                }

                loadedVaultCollections.Add(loadedCollection);
            }
        }

        private object ConvertDataValueToSerializedValue(string directory, VltCollection collection, VltClassField field, VLTBaseType dataPairValue)
        {
            switch (dataPairValue)
            {
                case IStringValue stringValue:
                    return stringValue.GetString();
                case PrimitiveTypeBase ptb:
                    return ptb.GetValue();
                case BaseBlob blob:
                    return ProcessBlob(directory, collection, field, blob);
                case VLTArrayType array:
                    {
                        var listType = typeof(List<>);
                        var listGenericType = ResolveType(array.ItemType);
                        var constructedListType = listType.MakeGenericType(listGenericType);
                        var instance = (IList)Activator.CreateInstance(constructedListType);

                        foreach (var arrayItem in array.Items)
                        {
                            instance.Add(listGenericType.IsPrimitive || listGenericType.IsEnum || listGenericType == typeof(string)
                                ? ConvertDataValueToSerializedValue(directory, collection, field, arrayItem)
                                : arrayItem);
                        }

                        return new SerializedArrayWrapper
                        {
                            Capacity = array.Capacity,
                            Data = instance
                        };
                    }
                default:
                    return dataPairValue;
            }
        }

        private object ProcessBlob(string directory, VltCollection collection, VltClassField field, BaseBlob blob)
        {
            if (blob.Data != null && blob.Data.Length > 0)
            {
                var blobDir = Path.Combine(directory, "_blobs");
                Directory.CreateDirectory(blobDir);
                var blobPath = Path.Combine(blobDir,
                    $"{collection.ShortPath.TrimEnd('/', '\\').Replace('/', '_').Replace('\\', '_')}_{field.Name}.bin");

                File.WriteAllBytes(blobPath, blob.Data);

                return blobPath.Substring(directory.Length + 1);
            }

            return "";
        }

        private static Type ResolveType(Type type)
        {
            if (type.IsGenericType)
            {
                if (type.GetGenericTypeDefinition() == typeof(VLTEnumType<>))
                {
                    return type.GetGenericArguments()[0];
                }
            }
            else if (type.BaseType == typeof(PrimitiveTypeBase))
            {
                var info = type.GetCustomAttributes<PrimitiveInfoAttribute>().First();

                return info.PrimitiveType;
            }

            return type;
        }
    }
}