﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.Data;

using NHibernate.SqlCommand;
using Malt;

using ObjectServer.Exceptions;
using ObjectServer.Data;

namespace ObjectServer.Model
{
    using IRecord = IDictionary<string, object>;
    using Record = Dictionary<string, object>;

    /// <summary>
    /// 实体类基类
    /// </summary>
    public abstract class AbstractModel : AbstractResource, IModel
    {
        public const string IdFieldName = "_id";
        public const string VersionFieldName = "_version";
        public const string CreatedTimeFieldName = "_created_time";
        public const string UpdatedTimeFieldName = "_updated_time";
        public const string CreatedUserFieldName = "_created_user";
        public const string UpdatedUserFieldName = "_updated_user";
        public const string ActiveFieldName = "_active";
        public const long FirstVersion = 0;
        public readonly static OrderExpression[] DefaultOrder =
        new OrderExpression[] { new OrderExpression(IdFieldName, SortDirection.Ascend) };

        private readonly IFieldCollection fields;

        public static readonly string QuotedIdColumn = '"' + IdFieldName + '"';

        protected AbstractModel(string name)
            : base(name)
        {
            this.Order = DefaultOrder;
            this.IsVersioned = true;
            this.AutoMigration = true;
            this.fields = new FieldCollection(this);
            this.Inheritances = new InheritanceCollection();

            this.RegisterInternalServiceMethods();
            this.AddInternalFields();
        }

        /// <summary>
        /// 此函数要允许多次调用
        /// </summary>
        /// <param name="tc"></param>
        public override void Initialize(IServiceContext tc, bool update)
        {
            if (tc == null)
            {
                throw new ArgumentNullException("ctx");
            }

            base.Initialize(tc, update);
            this.InitializeInheritances(tc);
            this.VerifyFields();

            if (update)
            {
                this.SyncModel(tc.DataContext);
            }
        }

        private void VerifyFields()
        {
            foreach (var pair in this.Fields)
            {
                pair.Value.VerifyDefinition();
            }
        }


        /// <summary>
        /// 注册内部服务，每个模型都有
        /// </summary>
        private void RegisterInternalServiceMethods()
        {
            base.RegisterAllServiceMethods(typeof(AbstractModel));
        }

        private void AddInternalFields()
        {
            Debug.Assert(!this.Fields.ContainsKey(IdFieldName));

            var idField = new ScalarField(this, IdFieldName, FieldType.Identifier)
                .Required().Readonly();
            this.fields.Add(IdFieldName, idField);
        }

        /// <summary>
        /// 初始化继承设置
        /// </summary>
        /// <param name="db"></param>
        private void InitializeInheritances(IServiceContext tc)
        {
            //验证继承声明
            //这里可以安全地访问 many-to-one 指向的 ResourceContainer 里的对象，因为依赖排序的原因
            //被指向的对象肯定已经更早注册了
            foreach (var ii in this.Inheritances)
            {
                IField inheritField;
                var hasInheritField = this.Fields.TryGetValue(ii.RelatedField, out inheritField);
                if (!hasInheritField
                    || !tc.Resources.ContainsResource(ii.BaseModel)
                    || inheritField.Type != FieldType.ManyToOne
                    || inheritField.OnDeleteAction != OnDeleteAction.Cascade
                    || !inheritField.IsRequired)
                {
                    var ex = new Exceptions.ResourceException(
                        "多表继承必须包含指向父表的 ManyToOne 字段，且必须满足：不能为空，级联删除");
                    LoggerProvider.EnvironmentLogger.ErrorException(ex.Message, ex);
                    throw ex;
                }

                //把“基类”模型的字段引用复制过来
                var baseModel = (IModel)tc.GetResource(ii.BaseModel);
                foreach (var baseField in baseModel.Fields)
                {
                    if (!this.Fields.ContainsKey(baseField.Key))
                    {
                        var imf = new InheritedField(this, baseField.Value);
                        this.Fields.Add(baseField.Key, imf);
                    }
                }

            }
        }

        #region Inheritance staff

        public ICollection<InheritanceDescriptor> Inheritances { get; private set; }

        protected AbstractModel Inherit(string modelName, string relatedField)
        {
            var ii = new InheritanceDescriptor(modelName, relatedField);
            if (this.Inheritances.Select(i => i.BaseModel).Contains(modelName))
            {
                var msg = string.Format("Duplicated inheritance: '{0}'", modelName);
                throw new ArgumentException(msg, modelName);
            }

            this.Inheritances.Add(ii);

            return this;
        }

        #endregion

        /// <summary>
        /// 同步代码定义的模型到数据库
        /// </summary>
        /// <param name="db"></param>
        private void SyncModel(IDataContext db)
        {
            Debug.Assert(db != null);

            //检测此模型是否存在于数据库 core_model 表
            var modelId = this.FindExistedModelInDb(db);

            if (modelId == null)
            {
                this.CreateModel(db);
                modelId = this.FindExistedModelInDb(db);
            }

            this.SyncFields(db, modelId.Value);
        }

        /// <summary>
        /// 同步代码定义的字段到数据库
        /// </summary>
        /// <param name="db"></param>
        /// <param name="modelId"></param>
        private void SyncFields(IDataContext db, long modelId)
        {
            Debug.Assert(db != null);

            //同步代码定义的字段与数据库 core_model_field 表里记录的字段信息
            var sqlQuery = SqlString.Parse("select * from core_field where module=? and model=?");

            var dbFields = db.QueryAsDictionary(sqlQuery, this.Module, modelId);
            var dbFieldsNames = (from f in dbFields select (string)f["name"]).ToArray();

            //先插入代码定义了，但数据库不存在的            
            var sql = @"
insert into core_field(module, model, name, required, readonly, relation, label, type, help) 
    values(?,?,?,?,?,?,?,?,?)";
            var sqlInsert = SqlString.Parse(sql);
            var fieldsToAppend = this.Fields.Keys.Except(dbFieldsNames);
            foreach (var fieldName in fieldsToAppend)
            {
                var field = this.Fields[fieldName];
                db.Execute(sqlInsert,
                    this.Module, modelId, fieldName, field.IsRequired, field.IsReadonly,
                    field.Relation, field.Label, field.Type.ToKeyString(), "");
            }

            //删除数据库存在，但代码未定义的
            var fieldsToDelete = dbFieldsNames.Except(this.Fields.Keys);
            sql = @"delete from core_field where name=? and module=? and model=?";
            var sqlDelete = SqlString.Parse(sql);
            foreach (var f in fieldsToDelete)
            {
                db.Execute(sqlDelete, f, this.Module, modelId);
            }

            //更新现存的（交集）
            var fieldsToUpdate = dbFieldsNames.Intersect(this.Fields.Keys);
            foreach (var dbField in dbFields)
            {
                var fieldName = (string)dbField["name"];

                if (fieldsToUpdate.Contains(fieldName))
                {
                    SyncSingleField(db, dbField, fieldName);
                }
            }
        }

        /// <summary>
        /// 同步单个字段
        /// </summary>
        /// <param name="db"></param>
        /// <param name="sql"></param>
        /// <param name="dbField"></param>
        /// <param name="fieldName"></param>
        /// <returns></returns>
        private void SyncSingleField(IDataContext db, IRecord dbField, string fieldName)
        {
            Debug.Assert(db != null);
            Debug.Assert(dbField != null);
            Debug.Assert(!string.IsNullOrEmpty(fieldName));

            var fieldLabel = dbField["label"].IsNull() ? null : (string)dbField["label"];
            var fieldRelation = dbField["relation"].IsNull() ? null : (string)dbField["relation"];
            var fieldHelp = dbField["help"].IsNull() ? null : (string)dbField["help"];
            var fieldType = (string)dbField["type"];
            var fieldId = (long)dbField[IdFieldName];

            var metaField = this.Fields[fieldName];
            var metaFieldType = metaField.Type.ToKeyString();
            if (fieldLabel != metaField.Label ||
                fieldRelation != metaField.Relation ||
                fieldType != metaFieldType ||
                fieldHelp != metaField.Help)
            {
                /*
                 * 
                UPDATE ""core_field"" SET ""type""=@0, ""relation""=@1, ""label""=@2, 
                ""help""=@3  WHERE ""_id""=@4";
                */
                var sql =
                    new SqlString(
                        "update core_field set ",
                        "type=", Parameter.Placeholder, ",",
                        "required=", Parameter.Placeholder, ",",
                        "readonly=", Parameter.Placeholder, ",",
                        "relation=", Parameter.Placeholder, ",",
                        "label=", Parameter.Placeholder, ",",
                        "help=", Parameter.Placeholder,
                        " where _id=", Parameter.Placeholder);
                db.Execute(
                    sql, metaFieldType, metaField.IsRequired, metaField.IsReadonly,
                    metaField.Relation, metaField.Label, metaField.Help, fieldId);

            }
        }

        private long? FindExistedModelInDb(IDataContext db)
        {
            Debug.Assert(db != null);

            //var sql = "SELECT MAX(\"_id\") FROM core_model WHERE name=@0";
            var sql = new SqlString(
                @"select max(""_id"") from ""core_model"" where ""name""=", Parameter.Placeholder);
            var o = db.QueryValue(sql, this.Name);
            if (o.IsNull())
            {
                return null;
            }
            else
            {
                return (long)o;
            }
        }

        private void CreateModel(IDataContext db)
        {
            Debug.Assert(db != null);

            var sql = new SqlString(
                "insert into core_model(name, module, label) values(",
                Parameter.Placeholder, ",",
                Parameter.Placeholder, ",",
                Parameter.Placeholder, ")");
            var rowCount = db.Execute(sql, this.Name, this.Module, this.Label);

            if (rowCount != 1)
            {
                throw new ObjectServer.Exceptions.DataException("Failed to insert record of table core_model");
            }

            sql = new SqlString(
                @"select max(""_id"") from ""core_model""  where ""name"" =", Parameter.Placeholder,
                @" and ""module""=", Parameter.Placeholder);

            var modelId = (long)db.QueryValue(sql, this.Name, this.Module);

            //插入一笔到 core_model_data 方便以后导入时引用
            var key = "model_" + this.Name.Replace('.', '_');
            Core.ModelDataModel.Create(db, this.Module, Core.ModelModel.ModelName, key, modelId);
        }

        /// <summary>
        /// 获取此对象以来的所有其他对象名称
        /// 这里处理的很简单，就是直接检测 many-to-one 的对象
        /// </summary>
        /// <returns></returns>
        public override string[] GetReferencedObjects()
        {
            var inheritedObjs = from i in this.Inheritances select i.BaseModel;
            var fieldsObjs = from f in this.Fields.Values
                             where f.Type == FieldType.ManyToOne
                             select f.Relation;

            var refObjs = new List<string>();
            foreach (var f in this.fields.Values.Where(f => f.Type == FieldType.Reference))
            {
                refObjs.AddRange(f.Options.Keys);
            }

            var query = inheritedObjs.Union(fieldsObjs).Union(refObjs);
            //自己不能依赖自己
            query = from m in query
                    where m != this.Name
                    select m;

            return query.Distinct().ToArray();
        }

        public IFieldCollection Fields { get { return this.fields; } }

        public bool ContainsField(string fieldName)
        {
            if (string.IsNullOrEmpty(fieldName))
            {
                throw new ArgumentNullException("fieldName");
            }

            return this.Fields.ContainsKey(fieldName);
        }

        public IField[] GetAllStorableFields()
        {
            return this.Fields.Values.Where(f => f.IsColumn && f.Name != IdFieldName).ToArray();
        }

        public override void MergeFrom(IResource res)
        {
            if (res == null)
            {
                throw new ArgumentNullException("res");
            }

            base.MergeFrom(res);

            var model = res as IModel;
            if (model != null)
            {
                //这里的字段合并策略也是添加，如果存在就直接替换
                foreach (var field in model.Fields)
                {
                    this.Fields[field.Key] = field.Value;
                }
            }
        }


        #region Service Methods

        [ServiceMethod("GetFieldDefaultValues")]
        public static Dictionary<string, object> GetFieldDefaultValues(
            IModel model, IServiceContext ctx, object[] clientFields)
        {
            if (model == null)
            {
                throw new ArgumentNullException("model");
            }

            if (ctx == null)
            {
                throw new ArgumentNullException("ctx");
            }

            if (!model.CanRead)
            {
                throw new NotSupportedException();
            }

            if (clientFields == null)
            {
                throw new ArgumentNullException("clientFields");
            }

            //这里不检查权限，也许是一个安全漏洞？
            var strFields = clientFields.Cast<string>().ToArray();

            return model.GetFieldDefaultValuesInternal(ctx, strFields);
        }


        [ServiceMethod("Count")]
        public static long Count(
            IModel model, IServiceContext ctx, object[] constraint)
        {
            if (model == null)
            {
                throw new ArgumentNullException("model");
            }

            if (ctx == null)
            {
                throw new ArgumentNullException("ctx");
            }

            if (!model.CanRead)
            {
                throw new NotSupportedException();
            }

            if (!ctx.CanReadModel(model.Name))
            {
                throw new SecurityException("Access denied");
            }

            return model.CountInternal(ctx, constraint);
        }

        [ServiceMethod("Search")]
        public static long[] Search(
            IModel model, IServiceContext ctx, object[] constraint, object[] order, long offset, long limit)
        {
            if (model == null)
            {
                throw new ArgumentNullException("model");
            }

            if (ctx == null)
            {
                throw new ArgumentNullException("ctx");
            }

            if (!model.CanRead)
            {
                throw new NotSupportedException();
            }

            if (!ctx.CanReadModel(model.Name))
            {
                throw new SecurityException("Access denied");
            }

            OrderExpression[] orderInfos = null;
            if (order != null)
            {
                orderInfos = new OrderExpression[order.Length];
                for (int i = 0; i < orderInfos.Length; i++)
                {
                    var orderTuple = (object[])order[i];
                    var so = SortDirectionParser.Parser((string)orderTuple[1]);
                    orderInfos[i] = new OrderExpression((string)orderTuple[0], so);
                }
            }
            else
            {
                orderInfos = model.Order.ToArray();
            }

            return model.SearchInternal(ctx, constraint, orderInfos, offset, limit);
        }

        [ServiceMethod("Read")]
        public static Record[] Read(
            IModel model, IServiceContext ctx, dynamic clientIds, dynamic clientFields)
        {
            if (model == null)
            {
                throw new ArgumentNullException("model");
            }

            if (ctx == null)
            {
                throw new ArgumentNullException("ctx");
            }

            if (clientIds == null)
            {
                throw new ArgumentNullException("clientIds");
            }

            if (!model.CanRead)
            {
                throw new NotSupportedException();
            }

            if (!ctx.CanReadModel(model.Name))
            {
                throw new SecurityException("Access denied");
            }

            string[] strFields = null;
            if (clientFields != null)
            {
                strFields = new string[clientFields.Length];
                for (int i = 0; i < strFields.Length; i++)
                {
                    strFields[i] = clientFields[i];
                }
            }
            else
            {
                strFields = model.Fields.Select(p => p.Value.Name).ToArray();
            }

            VerifyFieldAccess(model, ctx, "read", strFields);

            var ids = new long[clientIds.Length];
            for (int i = 0; i < ids.Length; i++)
            {
                ids[i] = clientIds[i];
            }
            return model.ReadInternal(ctx, ids, strFields);
        }

        [ServiceMethod("Create")]
        public static long Create(
            IModel model, IServiceContext ctx, IRecord propertyBag)
        {
            if (model == null)
            {
                throw new ArgumentNullException("model");
            }

            if (ctx == null)
            {
                throw new ArgumentNullException("ctx");
            }

            if (!model.CanCreate)
            {
                throw new NotSupportedException();
            }

            if (!ctx.CanCreateModel(model.Name))
            {
                throw new SecurityException("Access denied");
            }

            VerifyFieldAccess(model, ctx, "write", propertyBag.Keys);

            return model.CreateInternal(ctx, propertyBag);
        }

        [ServiceMethod("Write")]
        public static void Write(
           IModel model, IServiceContext ctx, object id, IRecord userRecord)
        {
            //TODO 检查 id 类型

            if (model == null)
            {
                throw new ArgumentNullException("model");
            }

            if (ctx == null)
            {
                throw new ArgumentNullException("ctx");
            }

            if (!model.CanWrite)
            {
                throw new NotSupportedException();
            }

            if (!ctx.CanWriteModel(model.Name))
            {
                throw new SecurityException("Access denied");
            }

            VerifyFieldAccess(model, ctx, "write", userRecord.Keys);

            model.WriteInternal(ctx, (long)id, userRecord);
        }

        [ServiceMethod("Delete")]
        public static void Delete(
            IModel model, IServiceContext ctx, dynamic clientIDs)
        {
            if (model == null)
            {
                throw new ArgumentNullException("model");
            }

            if (ctx == null)
            {
                throw new ArgumentNullException("ctx");
            }

            if (!model.CanDelete)
            {
                throw new NotSupportedException();
            }

            if (!ctx.CanDeleteModel(model.Name))
            {
                throw new SecurityException("Access denied");
            }

            long[] ids;

            if (clientIDs is long)
            {
                ids = new long[] { clientIDs };
            }
            else
            {
                ids = new long[clientIDs.Length];
                for (int i = 0; i < ids.Length; i++)
                {
                    ids[i] = clientIDs[i];
                }
            }

            model.DeleteInternal(ctx, ids);
        }

        [ServiceMethod("GetFields")]
        public static Dictionary<string, object>[] GetFields(
            IModel model, IServiceContext ctx, string[] fields)
        {
            if (ctx == null)
            {
                throw new ArgumentNullException("ctx");
            }

            if (model == null)
            {
                throw new ArgumentNullException("model");
            }

            return model.GetFieldsInternal(ctx, fields);
        }

        #endregion


        #region IModel Members

        public abstract string TableName { get; protected set; }
        public virtual bool Hierarchy { get; protected set; }
        public virtual bool CanCreate { get; protected set; }
        public virtual bool CanRead { get; protected set; }
        public virtual bool CanWrite { get; protected set; }
        public virtual bool CanDelete { get; protected set; }
        public virtual bool IsVersioned { get; protected set; }

        public bool AutoMigration { get; protected set; }

        public virtual bool LogCreation { get; protected set; }
        public virtual bool LogWriting { get; protected set; }
        public int DepencyWeight { get; private set; }


        public virtual NameGetter NameGetter { get; protected set; }

        public abstract long CountInternal(IServiceContext ctx, object[] constraints);
        public abstract long[] SearchInternal(
            IServiceContext ctx, object[] constraints, OrderExpression[] orders, long offset, long limit);
        public abstract long CreateInternal(
            IServiceContext ctx, IRecord record);
        public abstract void WriteInternal(
            IServiceContext ctx, long id, IRecord record);
        public abstract Record[] ReadInternal(
            IServiceContext ctx, long[] ids, string[] requiredFields);
        public abstract void DeleteInternal(IServiceContext scope, long[] ids);


        public Dictionary<string, object> GetFieldDefaultValuesInternal(
            IServiceContext ctx, string[] fieldNames)
        {
            if (ctx == null)
            {
                throw new ArgumentNullException("ctx");
            }

            if (fieldNames == null)
            {
                throw new ArgumentNullException("fieldNames");
            }

            var result = new Dictionary<string, object>(fieldNames.Length);
            foreach (var f in fieldNames)
            {
                if (f == null || !this.Fields.ContainsKey(f))
                {
                    throw new ArgumentOutOfRangeException("fieldNames");
                }

                var fi = this.Fields[f];

                if (fi.DefaultProc != null)
                {
                    var dv = fi.DefaultProc(ctx);
                    result.Add(f, dv);
                }
            }
            return result;
        }

        public dynamic Browse(IServiceContext ctx, long id)
        {
            return new BrowsableRecord(ctx, this, id);
        }

        public dynamic BrowseMany(IServiceContext ctx, long[] ids)
        {
            var records = this.ReadInternal(ctx, ids, null);
            dynamic browObjs = new dynamic[records.Length];
            for (int i = 0; i < browObjs.Length; i++)
            {
                browObjs[i] = new BrowsableRecord(ctx, this, records[i]);
            }
            return browObjs;
        }

        public IEnumerable<OrderExpression> Order { get; protected set; }

        public IModelDescriptor OrderBy(IEnumerable<OrderExpression> order)
        {
            if (order == null)
            {
                throw new ArgumentNullException("order");
            }

            this.Order = order;

            return this;
        }

        public virtual Dictionary<string, object>[] GetFieldsInternal(
            IServiceContext ctx, string[] fieldNames)
        {
            if (ctx == null)
            {
                throw new ArgumentNullException("ctx");
            }

            var modelModel = (IModel)ctx.GetResource("core.model");

            var destModel = (AbstractModel)ctx.GetResource(this.Name);

            var modelDomain = new object[] { new object[] { "name", "=", this.Name } };
            var modelIds = modelModel.SearchInternal(ctx, modelDomain, null, 0, 0);

            //TODO 检查 IDS 错误，好好想一下要用数据库的字段信息还是用内存的字段信息

            var fieldModel = (IModel)ctx.GetResource("core.field");
            var fieldDomain = new object[] { new object[] { "model", "=", modelIds[0] } };
            var fieldIds = fieldModel.SearchInternal(ctx, fieldDomain, null, 0, 0);
            var records = fieldModel.ReadInternal(ctx, fieldIds, fieldNames);

            foreach (var r in records)
            {
                var fieldName = (string)r["name"];
                var field = destModel.Fields[fieldName];

                if (field.Type == FieldType.Enumeration || field.Type == FieldType.Reference)
                {
                    r["options"] = field.Options;
                }
                else if (field.Type == FieldType.ManyToMany)
                {
                    r["related_field"] = field.RelatedField;
                    r["origin_field"] = field.OriginField;
                }
            }

            return records;
        }

        #endregion


        protected static void VerifyFieldAccess(
            IModel model, IServiceContext tc, string action, IEnumerable<string> fields)
        {
            var availableFields = fields.Where(k => model.Fields.ContainsKey(k));
            var fam = (Core.FieldAccessModel)tc.GetResource("core.field_access");
            var result = fam.GetFieldAccess(tc, model.Name, availableFields, action);
            foreach (var p in result)
            {
                if (!p.Value)
                {
                    throw new Exceptions.SecurityException("Access Denied");
                }
            }
        }

        public virtual void ImportRecord(
              IServiceContext ctx, bool noUpdate, IDictionary<string, object> record, string key)
        {
            if (ctx == null)
            {
                throw new ArgumentNullException("ctx");
            }

            if (record == null)
            {
                throw new ArgumentNullException("record");
            }

            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentNullException("key");
            }

            //查找 key 指定的记录看是否存在
            long? existedId = null;
            if (!string.IsNullOrEmpty(key))
            {
                existedId = Core.ModelDataModel.TryLookupResourceId(
                    ctx.DataContext, this.Name, key);
            }

            if (existedId == null) // Create
            {
                existedId = (long)this.CreateInternal(ctx, record);
                if (!string.IsNullOrEmpty(key))
                {
                    Core.ModelDataModel.Create(
                        ctx.DataContext, this.Module, this.Name, key, existedId.Value);
                }
            }
            else if (existedId != null && !noUpdate) //Update 
            {
                if (this.Fields.ContainsKey(AbstractModel.VersionFieldName)) //处理版本
                {
                    var fields = new string[] { AbstractModel.VersionFieldName };
                    var read = this.ReadInternal(ctx, new long[] { existedId.Value }, fields)[0];
                    record[AbstractModel.VersionFieldName] = read[AbstractModel.VersionFieldName];
                }

                this.WriteInternal(ctx, existedId.Value, record);
                Core.ModelDataModel.UpdateResourceId(
                    ctx.DataContext, this.Name, key, existedId.Value);
            }
            else
            {
                //忽略此记录
            }
        }

    }
}
