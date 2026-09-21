using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Reflection;

using Microsoft.EntityFrameworkCore.Migrations;

namespace Rasa.Services.Preloader
{
    public abstract class PreloaderBase
    {
        protected void Insert(MigrationBuilder migrationBuilder, string tableName, Type entityType)
        {
            Insert(migrationBuilder, tableName, GetColumnNames(entityType));
        }

        /// <summary>
        /// The columns spelled out rather than taken from the entity. A preloader whose table has
        /// grown a column since it was written uses this, so that the migration it runs in still
        /// inserts the columns the table had at that point in the history and the later migration
        /// that adds the column is what fills it.
        /// </summary>
        protected void Insert(MigrationBuilder migrationBuilder, string tableName, string[] columns)
        {
            var values = CreateValues();

            migrationBuilder.InsertData(tableName,
                columns,
                values);
        }
        private string[] GetColumnNames(Type entityType)
        {
            var publicProperties = entityType.GetProperties(BindingFlags.Instance | BindingFlags.Public);
            var columns = publicProperties.SelectMany(p => p.GetCustomAttributes<ColumnAttribute>());
            var columnNames = columns.Select(c => c.Name)
                .Where(name => !string.IsNullOrEmpty(name));
            return columnNames.ToArray();
        }

        private object[,] CreateValues()
        {
            var rows = GetRows()
                .ToList();

            var innerLength = rows.First().Length;

            var result = new object[rows.Count, innerLength];
            for (var i = 0; i < rows.Count; i++)
            {
                for (int j = 0; j < innerLength; j++)
                {
                    result[i, j] = rows[i][j];
                }
            }
            return result;
        }

        protected abstract IEnumerable<object[]> GetRows();
    }
}
