﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using ObjectServer.Model;

namespace ObjectServer.Core
{
    //[ServiceObject] 
    public class Field : ModelBase
    {

        public Field()
            : base()
        {
            this.Automatic = false;
            this.Name = "Core.Field";
            this.TableName = "core_field";
        }


    }
}