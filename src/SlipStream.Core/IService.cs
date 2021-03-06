﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SlipStream
{
    public interface IService
    {
        IResource Resource { get; }
        string Name { get; }
        string Help { get; }

        object Invoke(IResource self, params object[] args);
    }
}
