﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CK.SqlServer.Setup;
using CK.Setup;
using System.Data.SqlClient;
using System.Threading.Tasks;
using CK.SqlServer;

namespace CK.DB.Actor
{
    public abstract partial class GroupTable : SqlTable
    {
        /// <summary>
        /// Creates a new Group.
        /// </summary>
        /// <param name="ctx">The call context.</param>
        /// <returns>The new group identifier.</returns>
        [SqlProcedureNonQuery( "sGroupCreate" )]
        public abstract int CreateGroup( ISqlCallContext ctx, int actorId );

        /// <summary>
        /// Destroys a Group if and only if there is no more users inside.
        /// Idempotent.
        /// </summary>
        /// <param name="ctx">The call context.</param>
        /// <param name="actorId">The actor identifier.</param>
        /// <param name="groupId">
        /// The group identifier to destroy. 
        /// If <paramref name="forceDestroy"/> if false, it must be empty otherwise an exception is thrown.
        /// </param>
        /// <param name="forceDestroy">True to remove all users before destroying the group.</param>
        [SqlProcedureNonQuery( "sGroupDestroy" )]
        public abstract void DestroyGroup( ISqlCallContext ctx, int actorId, int groupId, bool forceDestroy = false );

        /// <summary>
        /// Adds a user into a group.
        /// Idempotent.
        /// </summary>
        /// <param name="ctx">The call context.</param>
        /// <param name="groupId">The group identifier.</param>
        /// <param name="userId">The user identifier to add.</param>
        [SqlProcedureNonQuery( "sGroupUserAdd" )]
        public abstract void AddUser( ISqlCallContext ctx, int actorId, int groupId, int userId );

        /// <summary>
        /// Removes a user from a group.
        /// Idempotent.
        /// </summary>
        /// <param name="ctx">The call context.</param>
        /// <param name="groupId">The group identifier.</param>
        /// <param name="userId">The user identifier to remove.</param>
        [SqlProcedureNonQuery( "sGroupUserRemove" )]
        public abstract void RemoveUser( ISqlCallContext ctx, int actorId, int groupId, int userId );

        /// <summary>
        /// Removes all users from a group.
        /// Idempotent.
        /// </summary>
        /// <param name="ctx">The call context.</param>
        /// <param name="groupId">The group identifier.</param>
        [SqlProcedureNonQuery( "sGroupRemoveAllUsers" )]
        public abstract void RemoveAllUsers( ISqlCallContext ctx, int actorId, int groupId );

    }
}