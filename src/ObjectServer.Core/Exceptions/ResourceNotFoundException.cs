﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ObjectServer
{
    [Serializable]
    public sealed class ResourceNotFoundException : Exception
    {
        public ResourceNotFoundException(string msg, string resName)
            : base(msg)
        {
            this.ResourceName = resName;
        }

        public string ResourceName { get; private set; }
    }
}