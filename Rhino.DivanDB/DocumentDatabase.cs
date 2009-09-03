using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security;
using System.Threading;
using Newtonsoft.Json.Linq;
using Rhino.DivanDB.DataStructures;
using Rhino.DivanDB.Json;
using Rhino.DivanDB.Linq;
using Rhino.DivanDB.Storage;

namespace Rhino.DivanDB
{
    public class DocumentDatabase
    {
        public DocumentStorage Storage { get; private set; }

        private readonly Hashtable<string, ViewFunc> viewsCache = new Hashtable<string, ViewFunc>(); 

        public DocumentDatabase(string path)
        {
            Storage = new DocumentStorage(path);
            Storage.Initialize();
        }

        public string AddDocument(JObject document)
        {
            string key = GetKeyFromDocumentOrGenerateNewOne(document);
            Storage.Batch(actions =>
            {
                actions.AddDocument(key, document.ToString());
                actions.Commit();
            });
            return key;
        }

        private static string GetKeyFromDocumentOrGenerateNewOne(IDictionary<string, JToken> document)
        {
            string id = GetKeyFromDocumentOrNull(document);
            if (id != null)
                return id;
            Guid value;
            UuidCreateSequential(out value);
            return  value.ToString();
        }

        private static string GetKeyFromDocumentOrNull(IDictionary<string, JToken> document)
        {
            JToken idToken;
            if (document.TryGetValue("_id", out idToken))
            {
                return (string)((JValue) idToken).Value;
            }
            return null;
        }

        [SuppressUnmanagedCodeSecurity]
        [DllImport("rpcrt4.dll", SetLastError = true)]
        static extern int UuidCreateSequential(out Guid value);

        public void Dispose()
        {
            Storage.Dispose();
        }

        public JObject DocumentByKey(string key)
        {
            string document = null;
            Storage.Batch(actions =>
            {
                document = actions.DocumentByKey(key);
                actions.Commit();
            });

            if(document == null)
                return null;

            return JObject.Parse(document);
        }

        public void EditDocument(JObject document)
        {
            string key = GetKeyFromDocument(document);

            Storage.Batch(actions =>
            {
                actions.DeleteDocument(key);
                actions.AddDocument(key, document.ToString());
                actions.Commit();
            });
        }

        private string GetKeyFromDocument(JObject document)
        {
            var key = GetKeyFromDocumentOrNull(document);
            if(key == null)
                throw new InvalidOperationException("'_id' is a mandatory property for editing documents");
            return key;
        }

        public void DeleteDocument(JObject document)
        {
            string key = GetKeyFromDocument(document);
            Storage.Batch(actions =>
            {
                actions.DeleteDocument(key);
                actions.Commit();
            });
        }

        public void AddView(string viewDefinition)
        {
            var transformer = new LinqTransformer(viewDefinition, "docs", typeof(JsonDynamicObject));
            Type type = transformer.Compile();
            var generator = (AbstractViewGenerator)Activator.CreateInstance(type);

            byte[] compiled = File.ReadAllBytes(transformer.PathToAssembly);
            Storage.Batch(actions =>
            {
                actions.AddView(transformer.Name, viewDefinition, compiled);
                actions.CreateViewTable(transformer.Name, generator.GeneratedType);
                actions.Commit();
            });
        }

        public string[] ListView()
        {
            string[] views = null;
            Storage.Batch(actions =>
            {
                views = actions.ListViews();
                actions.Commit();
            });
            return views;
        }

        public string ViewDefinitionByName(string name)
        {
            string def = null;
            Storage.Batch(actions =>
            {
                def = actions.ViewDefinitionByName(name);
                actions.Commit();
            });
            return def;
        }

        public ViewFunc ViewInstanceByName(string name)
        {
            ViewFunc viewFunc = null;
            Storage.Batch(actions =>
            {
                string hash = actions.ViewHashByName(name);
                if (hash == null)
                    throw new InvalidOperationException("Cannot find a view named: '" + name + "'");

                viewsCache.Read(reader => reader.TryGetValue(hash, out viewFunc));

                if(viewFunc != null)
                {
                    actions.Commit();
                    return;
                }

                viewsCache.Write(writer =>
                {
                    if(writer.TryGetValue(hash, out viewFunc))
                        return;
                    var assemblyDef = actions.ViewCompiledAssemblyByName(name);
                    var assembly = Assembly.Load(assemblyDef.CompiledAssembly);

                    var type = assembly.GetType(assemblyDef.Name);
                    var generator = (AbstractViewGenerator)Activator.CreateInstance(type);

                    writer.Add(hash, generator.CompiledDefinition);
                    viewFunc = generator.CompiledDefinition;
                });

                actions.Commit();
            });
            return viewFunc;
        }
    }
}