﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using System.IO;

using Newtonsoft.Json;

using ObjectServer.Model;
using ObjectServer.Runtime;
using ObjectServer.Backend;

namespace ObjectServer.Module
{
    /// <summary>
    /// TODO 线程安全
    /// </summary>
    [ServiceObject]
    public sealed class ModuleModel : ModelBase
    {
        /// <summary>
        /// 单个数据库中加载的模块
        /// </summary>
        private readonly List<Module> loadedModules
            = new List<Module>();

        public ModuleModel()
        {
            this.Name = "core.module";
            this.Versioned = false;

            this.CharsField("name", "Name", 128, true, null, null);
            this.CharsField("state", "State", 16, true, null, null);
            this.TextField("description", "Description", false, null, null);
        }

        public List<Module> LoadedModules
        {
            get { return this.loadedModules; }
        }

    }
}