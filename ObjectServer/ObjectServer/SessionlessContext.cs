﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

namespace ObjectServer
{
    internal class SessionlessContext : IResourceScope
    {
        public SessionlessContext(IDatabaseProfile db)
        {
            Debug.Assert(db != null);
            this.DatabaseProfile = db;
        }

        public IDatabaseProfile DatabaseProfile { get; private set; }

        public Session Session
        {
            get
            {
                throw new NotSupportedException();
            }
        }

        public void Dispose()
        {
        }

        public bool Equals(IResourceScope other)
        {
            throw new NotSupportedException("Invalid Equals invocation");
        }
    }
}
