﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.IO;

using NHibernate.SqlCommand;

using Npgsql;

namespace ObjectServer.Data.Postgresql
{
    internal sealed class PgDBConnection : AbstractDBConnection, IDBConnection
    {
        private const string INITDB = "ObjectServer.Data.Postgresql.initdb.sql";
        private readonly static SqlString SqlToListDBs = SqlString.Parse(@"
                select datname from pg_database  
                    where datdba = (select distinct usesysid from pg_user where usename=?) 
                        and datname not in ('template0', 'template1', 'postgres')  
	                order by datname asc;");


        public PgDBConnection(string dbName)
        {
            if (string.IsNullOrEmpty(dbName))
            {
                throw new ArgumentNullException("dbName");
            }

            var cfg = Platform.Configuration;
            string connectionString = string.Format(
              "Server={0};" +
              "Database={3};" +
              "Encoding=UNICODE;" +
              "User ID={1};" +
              "Password={2};",
              cfg.DBHost, cfg.DBUser, cfg.DBPassword, dbName);
            this.conn = new NpgsqlConnection(connectionString);
            this.DatabaseName = dbName;
        }

        public PgDBConnection()
            : this("template1")
        {
        }

        #region IDatabase 成员

        public override string[] List()
        {
            EnsureConnectionOpened();

            var dbUser = Platform.Configuration.DBUser;

            return this.QueryAsArray<string>(SqlToListDBs, dbUser);
        }


        public override void Create(string dbName)
        {
            if (string.IsNullOrEmpty(dbName))
            {
                throw new ArgumentNullException("dbName");
            }

            EnsureConnectionOpened();

            var sqlBuilder = new SqlStringBuilder();
            sqlBuilder.Add("create database ");
            sqlBuilder.Add(DataProvider.Dialect.QuoteForSchemaName(dbName));
            sqlBuilder.Add(" template template0 encoding 'unicode' ");

            var sql = sqlBuilder.ToSqlString();

            this.Execute(sql);
        }

        public override void Initialize()
        {
            //执行初始化数据库脚本
            var assembly = Assembly.GetExecutingAssembly();
            using (var resStream = assembly.GetManifestResourceStream(INITDB))
            using (var sr = new StreamReader(resStream, Encoding.UTF8))
            {
                var text = sr.ReadToEnd();
                var lines = text.Split(';');
                foreach (var l in lines)
                {
                    if (!string.IsNullOrEmpty(l) && l.Trim().Length > 0)
                    {
                        this.Execute(SqlString.Parse(l));
                    }
                }
            }

            //创建数据库只执行到这里，安装核心模块是外部的事情
        }

        public override bool IsInitialized()
        {
            var sql = SqlString.Parse(@"
select distinct count(table_name) 
    from information_schema.tables 
    where table_name in ('core_module', 'core_model', 'core_field')
");
            var rowCount = (long)this.QueryValue(sql);
            return rowCount == 3;
        }

        #endregion

        public override ITableContext CreateTableContext(string tableName)
        {
            if (string.IsNullOrEmpty(tableName))
            {
                throw new ArgumentNullException("tableName");
            }

            return new PgTableContext(this, tableName);
        }

        public override void LockTable(string tableName)
        {
            if (string.IsNullOrEmpty(tableName))
            {
                throw new ArgumentNullException("tableName");
            }

            var sql = new SqlString("lock ", DataProvider.Dialect.QuoteForTableName(tableName));
            this.Execute(sql);
        }

    }
}
