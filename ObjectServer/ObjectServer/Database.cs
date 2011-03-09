﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

using ObjectServer.Utility;
using ObjectServer.Backend;

namespace ObjectServer
{
    /// <summary>
    /// 数据库
    /// 用于描述一个帐套数据库的上下文环境
    /// 一个数据库包含了该数据库中的所有对象
    /// </summary>
    public class Database : IDatabase
    {
        private IDictionary<string, IResource> resources = new Dictionary<string, IResource>();
        private HashSet<string> loadedResources = new HashSet<string>();

        /// <summary>
        /// 初始化一个数据库环境
        /// </summary>
        /// <param name="dbName"></param>
        public Database(string dbName)
        {
            this.DataContext = DataProvider.CreateDataContext(dbName);

            this.EnsureInitialization();
        }

        public Database(IDataContext dataCtx, IResourceContainer resources)
        {
            this.DataContext = dataCtx;
            this.Resources = resources;

            this.EnsureInitialization();
        }

        private void EnsureInitialization()
        {
            //如果数据库是一个新建的空数据库，那么我们就需要先初始化此数据库为一个 ObjectServer 账套数据库
            if (!this.DataContext.IsInitialized())
            {
                this.DataContext.Initialize();
            }
        }

        ~Database()
        {
            this.Dispose(false);
        }

        public IDataContext DataContext { get; private set; }

        public IResourceContainer Resources { get; private set; }

        #region IDisposable 成员

        public void Dispose()
        {
            this.Dispose(true);
        }

        public void Dispose(bool disposing)
        {
            if (disposing)
            {
                //这里处理托管对象
            }

            this.DataContext.Dispose();
        }

        #endregion

        #region IResourceContainer 成员

        public void RegisterResource(IResource res)
        {
            if (res == null)
            {
                throw new ArgumentNullException("res");
            }

            lock (this)
            {
                IResource extendedRes;
                //先看是否存在               
                if (this.resources.TryGetValue(res.Name, out extendedRes))
                {
                    //处理单表继承（扩展）
                    extendedRes.MergeFrom(res);                 

                }
                else
                {
                    this.resources.Add(res.Name, res);
                }
            }
        }

        public IResource GetResource(string resName)
        {
            IResource res;
            if (this.resources.TryGetValue(resName, out res))
            {
                return res;
            }
            else
            {
                var msg = string.Format("Cannot found resource: '{0}'", resName);
                Logger.Error(() => msg);

                throw new ResourceNotFoundException(msg, resName);
            }
        }

        #endregion

        private static void ResourceDependencySort(IList<IResource> resList)
        {
            Debug.Assert(resList != null);

            var objDepends = new Dictionary<string, string[]>();
            foreach (var res in resList)
            {
                objDepends.Add(res.Name, res.GetReferencedObjects());
            }

            resList.DependencySort(m => m.Name, m => objDepends[m.Name]);
        }

        public string DatabaseName { get; private set; }

        public void InitializeAllResources()
        {
            var allRes = new List<IResource>(this.resources.Values);
            ResourceDependencySort(allRes);

            foreach (var res in allRes)
            {
                if (!this.loadedResources.Contains(res.Name))
                {
                    res.Load(this);
                    this.loadedResources.Add(res.Name);
                }
            }
        }

    }
}
