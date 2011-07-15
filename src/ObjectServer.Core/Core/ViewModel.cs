﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

using ObjectServer.Model;

namespace ObjectServer.Core
{

    [Resource]
    public sealed class ViewModel : AbstractTableModel
    {

        public ViewModel()
            : base("core.view")
        {

            Fields.Chars("name").SetLabel("Name").SetSize(64).Required();
            Fields.Chars("model").SetLabel("Model").SetSize(128).Required();
            Fields.Enumeration("kind",
                new Dictionary<string, string>() { 
                    { "form", "Form View" }, 
                    { "list", "List View" },
                    { "tree", "Tree View" },
                })
                .SetLabel("View Kind").Required();
            Fields.Text("layout").SetLabel("Layout");
        }

        [ServiceMethod]
        public static Dictionary<string, object> GetView(
            IModel model, IServiceScope ctx, string modelName, string viewKind = "form", long? viewId = null)
        {
            if (model == null)
            {
                throw new ArgumentNullException("model");
            }

            if (ctx == null)
            {
                throw new ArgumentNullException("ctx");
            }

            /* 获取默认视图的策略如下：
             * 1. 先看是否指定了 viewId，如果有了，就处理一下视图继承然后返回
             * 2. 如果没有视图，就自动生成一个 Quick & Dirty 的
             * 
             * */
            Dictionary<string, object> result = null;
            var destModel = ctx.GetResource(modelName) as IModel;

            if (viewId != null)
            {
                var viewRecords = model.ReadInternal(ctx, new long[] { viewId.Value });
                result = viewRecords[0];
            }
            else
            {
                string layout;
                layout = GenerateViewByType(viewKind, destModel);
                result = new Dictionary<string, object>()
                {
                    { "name", "Auto-generated view" },
                    { "model", modelName },
                    { "kind", viewKind },
                    { "layout", layout },
                };
            }

            return result;
        }

        private static string GenerateViewByType(string viewKind, IModel destModel)
        {
            Debug.Assert(!string.IsNullOrEmpty(viewKind));
            Debug.Assert(destModel != null);

            string layout;
            switch (viewKind)
            {
                case "form":
                    layout = ViewGenerator.GenerateFormView(destModel.Fields);
                    break;

                case "list":
                    layout = ViewGenerator.GenerateListView(destModel.Fields);
                    break;

                default:
                    var msg = string.Format("Not supported view type[{0}]", viewKind);
                    throw new NotSupportedException(msg);
            }

            return layout;
        }

    }
}