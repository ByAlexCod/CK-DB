﻿using CK.Setup;
using CK.SqlServer.Setup;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CK.DB.Auth.AuthScope
{
    /// <summary>
    /// Holds the scope name.
    /// </summary>
    [SqlTable("tAuthScope", Package = typeof(Package))]
    [Versions( "1.0.0,1.0.1")]
    public class AuthScopeTable : SqlTable
    {
    }
}
