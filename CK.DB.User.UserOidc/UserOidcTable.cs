﻿using CK.Setup;
using CK.SqlServer;
using CK.SqlServer.Setup;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CK.Core;
using System.Threading;
using CK.DB.Auth;
using CK.Text;
using System.Reflection;

namespace CK.DB.User.UserOidc
{
    /// <summary>
    /// Oidc authentication provider.
    /// </summary>
    [SqlTable( "tUserOidc", Package = typeof(Package), Schema = "CK" )]
    [Versions("2.0.0")]
    [SqlObjectItem( "transform:sUserDestroy" )]
    public abstract partial class UserOidcTable : SqlTable, IGenericAuthenticationProvider
    {
        IPocoFactory<IUserOidcInfo> _infoFactory;

        /// <summary>
        /// Gets "Oidc" that is the name of the Oidc provider.
        /// </summary>
        public string ProviderName => "Oidc";

        void StObjConstruct( IPocoFactory<IUserOidcInfo> infoFactory )
        {
            _infoFactory = infoFactory;
        }

        /// <summary>
        /// Creates or updates a user entry for this provider. 
        /// This is the "binding account" feature since it binds an external identity to 
        /// an already existing user that may already be registered into other authencation providers.
        /// </summary>
        /// <param name="ctx">The call context to use.</param>
        /// <param name="actorId">The acting actor identifier.</param>
        /// <param name="userId">The user identifier that must be registered.</param>
        /// <param name="info">Provider specific data: the <see cref="IUserOidcInfo"/> poco.</param>
        /// <param name="mode">Optionnaly configures Create, Update only or WithLogin behavior.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>The operation result.</returns>
        public async Task<CreateOrUpdateResult> CreateOrUpdateOidcUserAsync( ISqlCallContext ctx, int actorId, int userId, IUserOidcInfo info, CreateOrUpdateMode mode = CreateOrUpdateMode.CreateOrUpdate, CancellationToken cancellationToken = default(CancellationToken) )
        {
            var r = await RawCreateOrUpdateOidcUserAsync( ctx, actorId, userId, info, mode, cancellationToken ).ConfigureAwait( false );
            return r.Result;
        }

        /// <summary>
        /// Challenges <see cref="IUserOidcInfo"/> data to identify a user.
        /// Note that a successful challenge may have side effects such as updating claims, access tokens or other data
        /// related to the user and this provider.
        /// </summary>
        /// <param name="ctx">The call context to use.</param>
        /// <param name="info">The payload to challenge.</param>
        /// <param name="actualLogin">Set it to false to avoid login side-effect (such as updating the LastLoginTime) on success.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>The positive identifier of the user on success or 0 if the Oidc user does not exist.</returns>
        public async Task<int> LoginUserAsync( ISqlCallContext ctx, IUserOidcInfo info, bool actualLogin = true, CancellationToken cancellationToken = default( CancellationToken ) )
        {
            var mode = actualLogin
                        ? CreateOrUpdateMode.UpdateOnly | CreateOrUpdateMode.WithLogin
                        : CreateOrUpdateMode.UpdateOnly;
            var r = await RawCreateOrUpdateOidcUserAsync( ctx, 1, 0, info, mode, cancellationToken ).ConfigureAwait( false );
            return r.Result == CreateOrUpdateResult.Updated ? r.UserId : 0;
        }

        /// <summary>
        /// Destroys a OidcUser for a user.
        /// </summary>
        /// <param name="ctx">The call context to use.</param>
        /// <param name="actorId">The acting actor identifier.</param>
        /// <param name="userId">The user identifier for which Oidc account information must be destroyed.</param>
        /// <param name="schemeSuffix">
        /// Scheme suffix to delete.
        /// When null, all registrations for this provider regardless of the scheme suffix are deleted.
        /// </param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>The awaitable.</returns>
        [SqlProcedure( "sUserOidcDestroy" )]
        public abstract Task DestroyOidcUserAsync( ISqlCallContext ctx, int actorId, int userId, string schemeSuffix, CancellationToken cancellationToken = default( CancellationToken ) );

        /// <summary>
        /// Captures the result of <see cref="RawCreateOrUpdateOidcUser"/> and <see cref="RawCreateOrUpdateOidcUserAsync"/>.
        /// </summary>
        public struct RawResult
        {
            /// <summary>
            /// The user identifier.
            /// </summary>
            public readonly int UserId;

            /// <summary>
            /// The operation result.
            /// </summary>
            public readonly CreateOrUpdateResult Result;

            /// <summary>
            /// Initializes a new <see cref="RawResult"/>.
            /// </summary>
            /// <param name="userId">User identifier.</param>
            /// <param name="result">Operation result.</param>
            public RawResult( int userId, CreateOrUpdateResult result )
            {
                UserId = userId;
                Result = result;
            }
        }

        /// <summary>
        /// Raw call to manage OidcUser. Since this should not be used directly, it is protected.
        /// Actual implementation of the centralized create, update or login procedure.
        /// </summary>
        /// <param name="ctx">The call context to use.</param>
        /// <param name="actorId">The acting actor identifier.</param>
        /// <param name="userId">The user identifier for which a Oidc account must be created or updated.</param>
        /// <param name="info">User information to create or update.</param>
        /// <param name="mode">Configures Create, Update only or WithLogin behavior.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>The user identifier (when <paramref name="userId"/> is 0, this is a login) and the operation result.</returns>
        [SqlProcedure( "sUserOidcCreateOrUpdate" )]
        protected abstract Task<RawResult> RawCreateOrUpdateOidcUserAsync(
            ISqlCallContext ctx,
            int actorId,
            int userId,
            [ParameterSource]IUserOidcInfo info,
            CreateOrUpdateMode mode,
            CancellationToken cancellationToken );

        /// <summary>
        /// Finds a user by its Oidc scheme suffix and sub.
        /// Returns null if no such user exists.
        /// </summary>
        /// <param name="ctx">The call context to use.</param>
        /// <param name="schemeSuffix">The scheme suffix.</param>
        /// <param name="sub">The sub that identifies the user in the <paramref name="schemeSuffix"/>.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>A <see cref="KnownUserOidcInfo"/> object or null if not found.</returns>
        public Task<KnownUserOidcInfo> FindKnownUserInfoAsync( ISqlCallContext ctx, string schemeSuffix, string sub, CancellationToken cancellationToken = default( CancellationToken ) )
        {
            using( var c = CreateReaderCommand( schemeSuffix, sub ) )
            {
                return c.ExecuteRowAsync( ctx[Database], r => r == null ? null : DoCreateUserUnfo( schemeSuffix, sub, r ) );
            }
        }


        /// <summary>
        /// Creates a the reader command parametrized with the Oidc account identifier.
        /// Single-row returned columns are defined by <see cref="AppendUserInfoColumns(StringBuilder)"/>.
        /// </summary>
        /// <param name="schemeSuffix">The scheme suffix.</param>
        /// <param name="sub">The sub that identifies the user in the <paramref name="schemeSuffix"/>.</param>
        /// <returns>A ready to use reader command.</returns>
        SqlCommand CreateReaderCommand( string schemeSuffix, string sub )
        {
            StringBuilder b = new StringBuilder( "select " );
            AppendUserInfoColumns( b ).Append( " from CK.tUserOidc where SchemeSuffix=@S and Sub=@U" );
            var c = new SqlCommand( b.ToString() );
            c.Parameters.Add( new SqlParameter( "@S", schemeSuffix ) );
            c.Parameters.Add( new SqlParameter( "@U", sub ) );
            return c;
        }

        KnownUserOidcInfo DoCreateUserUnfo( string schemeSuffix, string sub, SqlDataReader r )
        {
            var info = _infoFactory.Create();
            info.SchemeSuffix = schemeSuffix;
            info.Sub = sub;
            FillUserOidcInfo( info, r, 1 );
            KnownUserOidcInfo result = new KnownUserOidcInfo()
            {
                UserId = r.GetInt32( 0 ),
                Info = info
            };
            return result;
        }


        /// <summary>
        /// Adds the columns name to read.
        /// </summary>
        /// <param name="b">The string builder.</param>
        /// <returns>The string builder.</returns>
        protected virtual StringBuilder AppendUserInfoColumns( StringBuilder b )
        {
            var props = _infoFactory.PocoClassType.GetProperties().Where( p => p.Name != nameof( IUserOidcInfo.SchemeSuffix ) && p.Name != nameof( IUserOidcInfo.Sub ) );
            return props.Any() ? b.Append("UserId, ").AppendStrings(props.Select(p => p.Name)) : b.Append( "UserId " );
        }

        /// <summary>
        /// Creates a <see cref="IUserOidcInfo"/> poco.
        /// </summary>
        /// <returns>A new instance.</returns>
        public T CreateUserInfo<T>() where T : IUserOidcInfo => (T)_infoFactory.Create();

        /// <summary>
        /// Fill UserInfo properties from reader.
        /// </summary>
        /// <param name="info">The info to fill.</param>
        /// <param name="r">The data reader.</param>
        /// <param name="idx">The index of the first column.</param>
        /// <returns>The updated index.</returns>
        protected virtual int FillUserOidcInfo( IUserOidcInfo info, SqlDataReader r, int idx )
        {
            var props = _infoFactory.PocoClassType.GetProperties().Where( p => p.Name != nameof( IUserOidcInfo.SchemeSuffix ) && p.Name != nameof( IUserOidcInfo.Sub ) );
            foreach( var p in props )
            {
                p.SetValue( info, r.GetValue( idx++ ) );
            }
            return idx;
        }

        #region IGenericAuthenticationProvider explicit implementation.

        CreateOrUpdateResult IGenericAuthenticationProvider.CreateOrUpdateUser( ISqlCallContext ctx, int actorId, int userId, object payload, CreateOrUpdateMode mode )
        {
            IUserOidcInfo info = _infoFactory.ExtractPayload(payload);
            return CreateOrUpdateOidcUser( ctx, actorId, userId, info, mode );
        }

        int IGenericAuthenticationProvider.LoginUser( ISqlCallContext ctx, object payload, bool actualLogin )
        {
            IUserOidcInfo info = _infoFactory.ExtractPayload(payload);
            return LoginUser( ctx, info, actualLogin );
        }

        Task<CreateOrUpdateResult> IGenericAuthenticationProvider.CreateOrUpdateUserAsync( ISqlCallContext ctx, int actorId, int userId, object payload, CreateOrUpdateMode mode, CancellationToken cancellationToken )
        {
            IUserOidcInfo info = _infoFactory.ExtractPayload(payload);
            return CreateOrUpdateOidcUserAsync( ctx, actorId, userId, info, mode, cancellationToken );
        }

        Task<int> IGenericAuthenticationProvider.LoginUserAsync( ISqlCallContext ctx, object payload, bool actualLogin, CancellationToken cancellationToken )
        {
            IUserOidcInfo info = _infoFactory.ExtractPayload(payload);
            return LoginUserAsync( ctx, info, actualLogin, cancellationToken );
        }

        void IGenericAuthenticationProvider.DestroyUser( ISqlCallContext ctx, int actorId, int userId, string schemeSuffix )
        {
            DestroyOidcUser( ctx, actorId, userId, schemeSuffix );
        }

        Task IGenericAuthenticationProvider.DestroyUserAsync( ISqlCallContext ctx, int actorId, int userId, string schemeSuffix, CancellationToken cancellationToken )
        {
            return DestroyOidcUserAsync( ctx, actorId, userId, schemeSuffix, cancellationToken );
        }

        #endregion
    }
}