// --------------------------------------------------------------------------------------------
// <copyright file="DbContainer.cs" company="Effort Team">
//     Copyright (C) Effort Team
//
//     Permission is hereby granted, free of charge, to any person obtaining a copy
//     of this software and associated documentation files (the "Software"), to deal
//     in the Software without restriction, including without limitation the rights
//     to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
//     copies of the Software, and to permit persons to whom the Software is
//     furnished to do so, subject to the following conditions:
//
//     The above copyright notice and this permission notice shall be included in
//     all copies or substantial portions of the Software.
//
//     THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
//     IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
//     FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
//     AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
//     LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
//     OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
//     THE SOFTWARE.
// </copyright>
// --------------------------------------------------------------------------------------------

namespace Effort.Internal.DbManagement
{
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using Effort.DataLoaders;
    using Effort.Exceptions;
    using Effort.Internal.Caching;
    using Effort.Internal.Common;
    using Effort.Internal.DbCommandTreeTransformation;
    using Effort.Internal.DbManagement.Engine;
    using Effort.Internal.DbManagement.Schema;
    using Effort.Internal.Diagnostics;
    using Effort.Internal.TypeConversion;
    using NMemory;
    using NMemory.Indexes;
    using NMemory.Modularity;
    using NMemory.StoredProcedures;
    using NMemory.Tables;
#if !EFOLD
    using System.Data.Entity.Core.Metadata.Edm;
#else
    using System.Data.Metadata.Edm;
#endif

    internal class DbContainer : ITableProvider
    {
        internal Database database;
        private List<TableName> tableNames;
        private ITypeConverter converter;
        private DbContainerParameters parameters;
        private ILogger logger;
        private ConcurrentDictionary<string, IStoredProcedure> transformCache;
        private bool isCaseSensitive = true;

        public DbContainer(DbContainerParameters parameters)
        {
            this.parameters = parameters;

            this.logger = new Logger();
            this.transformCache = new ConcurrentDictionary<string, IStoredProcedure>();
            this.converter = new DefaultTypeConverter();
            this.tableNames = new List<TableName>();
        }

        public Database Internal
        {
            get 
            {
                var db = this.database;

                if (db == null)
                {
                    throw new EffortException(ExceptionMessages.DatabaseNotInitialized);
                }

                return db; 
            }
        }

        public ILogger Logger
        {
            get { return this.logger; }
        }

        public ConcurrentDictionary<string, IStoredProcedure> TransformCache
        {
            get { return this.transformCache; }
        }

        public bool IsCaseSensitive
        {
            get { return isCaseSensitive; }
            set { isCaseSensitive = value; }
        }

        public ITypeConverter TypeConverter
        {
            get { return this.converter; }
        }

        public IList<TableName> TableNames
        {
            get { return this.tableNames.AsReadOnly(); }
        }

        public object GetTable(TableName name)
        {
            return this.Internal.GetTable(name);
        }
        
        public object GetAllTables()
        {
            return this.Internal.GetAllTables();
        }

        public void SetIdentityFields(bool enabled)
        {
            foreach (IExtendedTable table in this.Internal.Tables.GetAllTables())
            {
                table.IsIdentityFieldEnabled = enabled;
            }
        }

        public bool IsInitialized(StoreItemCollection edmStoreSchema)
        {
            // TODO: Lock
            if (this.database == null)
            {
                return false;
            }

            // Find container
            EntityContainer entityContainer = edmStoreSchema.GetItems<EntityContainer>().FirstOrDefault();

            foreach (EntitySet entitySet in entityContainer.BaseEntitySets.OfType<EntitySet>())
            {
                // TODO: Verify fields
                if (!this.Internal.ContainsTable(entitySet.GetFullTableName()))
                {
                    return false;
                }
            }

            return true;
        }

        public void Initialize(StoreItemCollection edmStoreSchema)
        {
            if (this.IsInitialized(edmStoreSchema))
            {
                return;
            }

            DbSchema schema = 
                DbSchemaStore.GetDbSchema(
                    edmStoreSchema, 
                    sic => DbSchemaFactory.CreateDbSchema(sic));

            this.Initialize(schema);
        }

        public void Initialize(DbSchema schema)
        {
            // TODO: locking
            Stopwatch fullTime = Stopwatch.StartNew();
            Stopwatch partialTime = Stopwatch.StartNew();

            this.Logger.Write("Database creation started...");

            this.EnsureInitializedDatabase();

            // Temporary dictionary
            Dictionary<TableName, ITable> tables = new Dictionary<TableName, ITable>();

            this.Logger.Write("Creating tables...");
            partialTime.Restart();

            foreach (DbTableInfo tableInfo in schema.Tables)
            {
                ITable table = DatabaseReflectionHelper.CreateTable(
                    this.Internal,
                    tableInfo.EntityType,
                    (IKeyInfo)tableInfo.PrimaryKeyInfo,
                    tableInfo.IdentityField,
                    tableInfo.ConstraintFactories,
                    tableInfo);

                tables.Add(tableInfo.TableName, table);
                tableNames.Add(tableInfo.TableName);
            }

            this.Logger.Write(
                "Tables created in {0:0.0} ms", 
                partialTime.Elapsed.TotalMilliseconds);

            this.Logger.Write("Adding initial data...");
            partialTime.Restart();
            
            // Add initial data to the tables
            using (ITableDataLoaderFactory loaderFactory = this.CreateDataLoaderFactory())
            {
                foreach (DbTableInfo tableInfo in schema.Tables)
                { 
                    // Get the table reference from the temporary dictionary
                    ITable table = tables[tableInfo.TableName];

                    // Return initial entity data and materialize them
                    IEnumerable<object> data = ObjectLoader.Load(loaderFactory, tableInfo);

                    DatabaseReflectionHelper.InitializeTableData(table, data);
                }
            }

            this.Logger.Write(
                "Initial data added in {0:0.0} ms",
                partialTime.Elapsed.TotalMilliseconds);

            this.Logger.Write("Building additional indexes...");
            partialTime.Restart();

            foreach (DbTableInfo tableInfo in schema.Tables)
            {
                ITable table = tables[tableInfo.TableName];

                foreach (IKeyInfo key in tableInfo.UniqueKeys)
                {
                    DatabaseReflectionHelper.CreateIndex(table, key, true);
                }

                foreach (IKeyInfo key in tableInfo.ForeignKeys)
                {
                    DatabaseReflectionHelper.CreateIndex(table, key, false);
                }
            }

            this.Logger.Write(
                "Additional indexes built in {0:0.0} ms",
                partialTime.Elapsed.TotalMilliseconds);

            this.Logger.Write("Creating and verifying associations...");
            partialTime.Restart();

            foreach (DbRelationInfo relation in schema.Relations)
            {
                DatabaseReflectionHelper.CreateAssociation(this.Internal, relation);
            }

            this.Logger.Write(
               "Associations created and verfied in {0:0.0} ms",
               partialTime.Elapsed.TotalMilliseconds);

            this.Logger.Write(
                "Database creation finished in {0:0.0} ms", 
                fullTime.Elapsed.TotalMilliseconds);
        }

        private void EnsureInitializedDatabase()
        {
            if (this.database == null)
            {
                IDatabaseComponentFactory componentFactory = 
                    new DatabaseComponentFactory(this.parameters.IsTransient);

                this.database = 
                    new Database(componentFactory);
            }
        }

        private ITableDataLoaderFactory CreateDataLoaderFactory()
        {
            if (this.parameters.DataLoader == null)
            {
                return new EmptyTableDataLoaderFactory();
            }

            return this.parameters.DataLoader.CreateTableDataLoaderFactory();
        }
    }
}
