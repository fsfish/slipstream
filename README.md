# Slipstream 快速数据库应用开发平台

[![Build status](https://ci.appveyor.com/api/projects/status/rrwcg81bs32xhekp/branch/master?svg=true)](https://ci.appveyor.com/project/oldrev/slipstream/branch/master)


SlipStream 是一个用 C# 实现的数据库应用快速开发平台原型。

代码实现参考了 OpenERP、Orchard 等开源项目。

## 项目特性

* 内置 CRUD 操作，只需定义列表及表单界面的布局，系统自动处理数据提交、保存、并发冲突等
* 易于创建业务方法及 RPC 方法
* 内置基于版本的数据库字段乐观并发冲突处理
* 高性能异步 HTTP 服务器及基于 ZeroMQ 的工作线程池技术，高吞吐量
* 各种 one-to-many, many-to-many 等复杂字段支持
* 根据实体类的定义自动调整数据库架构，自动迁移
* 权限支持访问控制列表、字段过滤等，且基于可配置而非硬编码代码

## 快速开始

### 需求

* 64 位操作系统环境（若是 32 位系统请修改项目依赖的 ClrZMQ 库版本）
* Microsoft Visual Studio 2017
* Microsoft SQL Server Express 2005+ 数据库或 PostgreSQL 9.0+ 数据库

调试环境系统默认使用 SQL Server Express，需要建立用户名及密码均为“slipstream”的数据库角色。

### 如何运行 Demo

#. 启动 SlipStream.DevServer 项目
#. 启动 SlipStream.Client.Agos 项目
#. 默认服务器用户名及密码均为“root”


## 如何编写业务模块

请参考 src/SlipStream.HostApp/Modules/SlipStream.Demo

### module.xml

此文件所有模块都必须包含，示例如下：
```xml
<?xml version="1.0" encoding="utf-8" ?>
<module-metadata>
  <name>demo</name>
  <label>演示模块</label>
  <info>
    <![CDATA[
      此模块演示一个最小化的 SlipStream 业务模块应该怎么编写
   ]]>
  </info>
  <demo>true</demo>
  <author>Wei Li [liwei@sandwych.com]</author>
  <version>0.01</version>
  <auto-load>true</auto-load>
  <project-file>SlipStream.DemoModule.csproj</project-file>
  <init-files>
    <file>ui.xml</file>
    <file>demo-data.xml</file>
  </init-files>

  <requires>
    <module>core</module>
  </requires>

</module-metadata>
```
### 实体类

实体类使用类似 ActiveRecord 模型，定义数据库表结构及 RPC 形式的业务方法：

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using SlipStream.Model;

namespace SlipStream.DemoModule
{
    [Resource]
    public sealed class EmployeeModel : AbstractSqlModel
    {
        public EmployeeModel()
            : base("demo.employee")
        {
            Fields.Chars("name").SetLabel("姓名").Required();
            Fields.Chars("address").SetLabel("地址");
            Fields.Double("salary").SetLabel("月薪");
            Fields.Date("birthdate").SetLabel("出生日期");
        }
    }
}
```

### 定义界面相关数据

界面及初始化数据同样使用 XML 文档定义，这里我们省掉了 employee 类的表单定义，因为简单的表单布局可由系统自动生成：
```xml
<?xml version="1.0" encoding="utf-8" ?>
<data noupdate="true">

  <!-- 创建员工的列表视图 -->
  <record model="core.view" key="employee_tree_view">
    <field name="name">员工管理</field>
    <field name="model">demo.employee</field>
    <field name="kind">tree</field>
    <field name="layout">
      <![CDATA[<?xml version="1.0" encoding="utf-8" ?>
      <tree label="员工管理">
        <field name="name" where="basic" />
        <field name="address" where="basic" />
        <field name="salary" where="basic" />
        <field name="birthdate" where="basic" />
      </tree>
      ]]>
    </field>
  </record>

  <!-- 创建点击菜单的动作 -->
  <record  model="core.action_window" key="employee_menu_action">
    <field name="name">员工管理</field>
    <field name="type">core.action_window</field>
    <field name="view" ref-key="employee_tree_view" />
    <field name="model">demo.employee</field>
  </record>

  <!-- 创建一个菜单 -->
  <record model="core.menu" key="menu_employees">
    <field name="name">员工管理</field>
    <field name="ordinal">0</field>
    <field name="action" ref-model="core.action_window" ref-key="employee_menu_action" />
  </record>


</data>
```

到这里就完成了整个模块的编写，将模块放入服务器的 Modules 目录并重启服务器程序，系统就会自动发现并编译、安装此模块。

随后登录系统即可看到新增加的操作菜单。

### 运行截图

#### 登录
![登录](https://github.com/Sandwych/slipstream/raw/master/doc/static/images/demo/login.png)

#### 列表视图
![列表视图](https://github.com/Sandwych/slipstream/raw/master/doc/static/images/demo/list-view.png)

#### 表单视图
![表单视图](https://github.com/Sandwych/slipstream/raw/master/doc/static/images/demo/formview.png)

## 版权

本项目的授权方式为 AGPL3。

第三方对此项目贡献的代码视为将代码版权转移给昆明维智众源企业管理咨询有限公司，
昆明维智众源企业管理咨询有限公司遵循 AGPL3 协议将本项目开源，但保留以本项目为基础开发商业软件的权利。

版权所有 (C) 2010 至今 昆明维智众源企业管理咨询有限公司。
保留所有权利。

* 昆明维智众源企业管理咨询有限公司  
* http://www.sandwych.com  
* contact@sandwych.com  
