﻿namespace Dashing.Tests.Engine {
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Data.Common;
    using System.Diagnostics;
    using System.Linq;
    using System.Text;

    using Dashing.Configuration;
    using Dashing.Engine;
    using Dashing.Engine.Dialects;
    using Dashing.Tests.TestDomain;

    using Moq;

    using Xunit;

    public class EngineBaseTests {
        [Fact]
        public void CreateTableGeneratesExpectedSql() {
            var target = this.MakeTarget(new AnsiSqlDialect());
            target.UseMaps(MakeMaps());
            var sql = target.CreateTable<User>();
            Debug.WriteLine(sql);

            target = this.MakeTarget(new SqlServerDialect());
            target.UseMaps(MakeMaps());
            sql = target.CreateTable<User>();
            Debug.WriteLine(sql);

            target = this.MakeTarget(new MySqlDialect());
            target.UseMaps(MakeMaps());
            sql = target.CreateTable<User>();
            Debug.WriteLine(sql);
        }

        private static IDictionary<Type, IMap> MakeMaps() {
            var mapper = new DefaultMapper(new DefaultConvention());
            IDictionary<Type, IMap> maps = new Dictionary<Type, IMap>();
            maps[typeof(User)] = mapper.MapFor<User>();
            return maps;
        }

        private TestEngine MakeTarget(ISqlDialect dialect = null) {
            return new TestEngine(dialect ?? new SqlDialectBase());
        }

        private class TestEngine : EngineBase {
            public override IEnumerable<T> Query<T>(IDbConnection connection, SelectQuery<T> query) {
                throw new NotImplementedException();
            }

            public override int Execute<T>(IDbConnection connection, InsertEntityQuery<T> query) {
                throw new NotImplementedException();
            }

            public override int Execute<T>(IDbConnection connection, UpdateEntityQuery<T> query) {
                throw new NotImplementedException();
            }

            public override int Execute<T>(IDbConnection connection, DeleteEntityQuery<T> query) {
                throw new NotImplementedException();
            }

            public TestEngine(ISqlDialect dialect)
                : base(dialect, new Mock<DbProviderFactory>().Object) {
                this.Dialect = dialect;
            }

            public string CreateTable<T>() {
                var map = this.MapFor<T>();
                var sql = new StringBuilder();

                sql.Append("create table ");
                this.Dialect.AppendQuotedTableName(sql, map);
                sql.Append(" (");

                this.Dialect.AppendColumnSpecification(sql, map.PrimaryKey);

                foreach (var column in map.Columns.Where(c => !c.Value.IsPrimaryKey)) {
                    sql.Append(", ");
                    this.Dialect.AppendColumnSpecification(sql, column.Value);
                }

                sql.Append(" )");

                return sql.ToString();
            }

            public IMap MapFor<T>() {
                return this.MapFor(typeof(T));
            }

            public IMap MapFor(Type type) {
                IMap map;
                if (!this.Maps.TryGetValue(type, out map)) {
                    throw new Exception("Type not found in maps");
                }

                return map;
            }
        }
    }
}