using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Isam.Esent.Interop;
using Rhino.DivanDB.Linq;
using Xunit;

namespace Rhino.DivanDB.Tests.Storage
{
    public class CreateAndDeleteViews : AbstractDocumentStorageTest, IDisposable
    {
        private readonly DocumentDatabase db;

        public CreateAndDeleteViews()
        {
            db = new DocumentDatabase("divan.db.test.esent");
        }

        [Fact]
        public void Can_add_view_to_document()
        {
            db.AddView(
                @"var pagesByTitle = 
    from doc in docs
    where doc.type == ""page""
    select new { Key = doc.title, Value = doc.content, Size = (int)doc.size };
");
            var views = db.ListView();
            Assert.Equal(1, views.Length);
            Assert.Equal("pagesByTitle", views[0]);
        }

        [Fact]
        public void Can_list_view_definition()
        {
            const string definition = @"var pagesByTitle = 
    from doc in docs
    where doc.type == ""page""
    select new { Key = doc.title, Value = doc.content, Size = (int)doc.size };
";
            db.AddView(
                definition);
            var actualDefinition = db.ViewDefinitionByName("pagesByTitle");
            Assert.Equal(definition, actualDefinition);
        }

        [Fact]
        public void Can_get_view_instance_by_name()
        {
            const string definition = @"var pagesByTitle = 
    from doc in docs
    where doc.type == ""page""
    select new { Key = doc.title, Value = doc.content, Size = (int)doc.size };
";
            db.AddView(
                definition);
            var actualDefinition = db.ViewInstanceByName("pagesByTitle");
            Assert.IsAssignableFrom<ViewFunc>(actualDefinition);
        }

        [Fact]
        public void Creating_view_will_generate_table_for_the_values()
        {
            const string definition = @"var pagesByTitle = 
    from doc in docs
    where doc.type == ""page""
    select new { Key = doc.title, Value = doc.content, Size = (int)doc.size };
";
            db.AddView(definition);
            using(var session = new Session(db.Storage.Instance))
            {
                JET_DBID dbid;
                Api.JetOpenDatabase(session, db.Storage.Database, null, out dbid, OpenDatabaseGrbit.None);
                try
                {
                    ColumnInfo[] columnInfos =
                        Api.GetTableColumns(session, dbid, "views_pagesByTitle").ToArray();

                    Assert.Equal("Key", columnInfos[0].Name);
                    Assert.Equal("Size", columnInfos[1].Name);
                    Assert.Equal("Value", columnInfos[2].Name);

                    Assert.Equal(JET_coltyp.LongText, columnInfos[0].Coltyp);
                    Assert.Equal(JET_coltyp.Long, columnInfos[1].Coltyp);
                    Assert.Equal(JET_coltyp.LongText, columnInfos[2].Coltyp);
                }
                finally
                {
                    Api.JetCloseDatabase(session, dbid, CloseDatabaseGrbit.None);
                }
            }
        }

        [Fact]
        public void View_instances_will_be_cached()
        {
            const string definition = @"var pagesByTitle = 
    from doc in docs
    where doc.type == ""page""
    select new { Key = doc.title, Value = doc.content, Size = (int)doc.size };
";
            db.AddView(
                definition);
            var actualDefinition1 = db.ViewInstanceByName("pagesByTitle");
            var actualDefinition2 = db.ViewInstanceByName("pagesByTitle");
            Assert.Same(actualDefinition1, actualDefinition2);
        }

        public void Dispose()
        {
            db.Dispose();
        }
    }
}