﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

using ObjectServer.Model;

namespace ObjectServer.Exceptions
{
    [Serializable]
    public class SecurityException : ApplicationException
    {
        public SecurityException(string msg)
            : base(msg)
        {
        }

    }
}
