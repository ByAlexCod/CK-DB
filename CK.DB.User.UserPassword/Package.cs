﻿using CK.Setup;
using CK.SqlServer.Setup;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CK.DB.User.UserPassword
{
    /// <summary>
    /// Package that adds a user password support. 
    /// </summary>
    [SqlPackage( Schema = "CK", ResourcePath = "Res" )]
    [Versions("1.0.0")]
    [SqlObjectItem( "transform:vUserAuthProvider" )]
    public class Package : SqlPackage
    {
        void Construct( Actor.Package actorPackage, Auth.Package auth )
        {
        }

        /// <summary>
        /// Gets or sets an optional <see cref="IUserPasswordMigrator"/>
        /// that will be used to migrate from previous password management 
        /// implementations.
        /// </summary>
        public IUserPasswordMigrator PasswordMigrator { get; set; }
    }
}
