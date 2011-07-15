﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Data;

using ObjectServer.Utility;
using ObjectServer.Model;

namespace ObjectServer.Core
{
    [Resource]
    public class ModelModel : AbstractTableModel
    {
        public const string ModelName = "core.model";

        public ModelModel()
            : base(ModelName)
        {
            this.AutoMigration = false;

            Fields.Chars("name").SetLabel("Name").SetSize(256).Required().Unique();
            Fields.Chars("label").SetLabel("Label").SetSize(256);
            Fields.Text("info").SetLabel("Information");
            Fields.Chars("module").SetLabel("Module").SetSize(128).Required();
            Fields.OneToMany("fields", "core.field", "module").SetLabel("Fields");
        }

        /// <summary>
        /// 提供一个方便读取指定模型所有字段的方法
        /// </summary>
        /// <param name="model"></param>
        /// <param name="ctx"></param>
        /// <param name="modelName"></param>
        /// <returns></returns>
        [ServiceMethod]
        public static Dictionary<string, object>[] GetFields(IModel model, IServiceScope ctx, string modelName)
        {
            if (model == null)
            {
                throw new ArgumentNullException("model");
            }

            if (ctx == null)
            {
                throw new ArgumentNullException("ctx");
            }
            var modelDomain = new object[] { new object[] { "name", "=", modelName } };
            var modelIds = model.SearchInternal(ctx, modelDomain);

            //TODO 检查 IDS 错误

            var fieldModel = (IModel)ctx.GetResource("core.field");
            var fieldDomain = new object[] { new object[] { "model", "=", modelIds[0] } };
            var fieldIds = fieldModel.SearchInternal(ctx, fieldDomain);
            var records = fieldModel.ReadInternal(ctx, fieldIds);

            return records;
        }
    }
}