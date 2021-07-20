// --------------------------------------------------------------------------------------------
// <copyright file="DbContainerManagerWrapper.cs" company="Effort Team">
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
    using System.Collections.Generic;
    using System.Linq;
    using Effort.Internal.DbManagement.Engine;
    using Effort.Internal.DbManagement.Schema;
    using Effort.Provider;
    using NMemory.Tables;
    using System;
    using System.Reflection;

    internal class DbContainerManagerWrapper : IDbManager
    {
        private DbContainer container;

        private static readonly string MigrationHistoryTable = "__MigrationHistory";

        public DbContainerManagerWrapper(DbContainer container)
        {
            this.container = container;
        }

        public void SetIdentityFields(bool enabled)
        {
            this.container.SetIdentityFields(enabled);
        } 

        public void SetIdentity<TEntity>(int? seed, int? increment)
        {
            if (container.database == null)
            {
                // BD not create...
                throw new Exception("NEED TEXT!");
            } 

            var tables = ((List<ITable>)container.GetAllTables());

            Dictionary<string, DbTableInfo> listDbTableInfo = new Dictionary<string, DbTableInfo>();

            foreach(var tableToFindDbTableInfo in tables)
            {
                // copy past  public DbTableInfo GetTableInfo(string schema, string name) from EffortConnection
                {
                    var _TableInfo = tableToFindDbTableInfo.GetType().GetProperty("TableInfo",
                          BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.FlattenHierarchy);

                    if (_TableInfo != null)
                    {
                        var TableInfo = (DbTableInfo)_TableInfo.GetValue(tableToFindDbTableInfo, BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.FlattenHierarchy, null, null, null);
                        if (TableInfo != null && TableInfo.EntitySet != null && TableInfo.EntitySet.Name != null)
                        {
                            listDbTableInfo.Add(TableInfo.EntitySet.Name, TableInfo);
                        }
                    } 
                }
            }

            DbTableInfo dbTableInfo = null;
            IExtendedTable table = null;
            if (listDbTableInfo.TryGetValue((typeof(TEntity).Name), out dbTableInfo))
            {   
                table = tables.Where(x => x.EntityType.FullName == dbTableInfo.EntityType.FullName).FirstOrDefault() as IExtendedTable; 
            } 

            if (table != null)
            { 
                table.SetIdentity(seed, increment);
            }
            else
            {
                throw new Exception("Invalid table name");
            }
        } 

        public void ClearMigrationHistory()
        {
            foreach (var tableName in this.container.TableNames)
            {
                if (tableName.Name == MigrationHistoryTable)
                {
                    var table = this.container.GetTable(tableName) as IExtendedTable;
                    table.Clear();
                }
            }
        }

        public void ClearTables()
        {
            var specialTables = new List<string>() { MigrationHistoryTable };

            this.SetRelations(false);

            foreach (TableName name in this.container.TableNames)
            {
                if (specialTables.Contains(name.Name))
                {
                    continue;
                }

                IExtendedTable table = (IExtendedTable)this.container.GetTable(name);
                table.Clear();
            }

            this.SetRelations(true);
        }

        private void SetRelations(bool enabled)
        {
            var db = this.container.Internal;

            foreach (var relation in db.Tables.GetAllRelations())
            {
                relation.IsEnabled = enabled;
            }
        }
    }
}
