﻿using System;
using System.Data.SqlClient;
using System.Threading.Tasks;
using CK.Core;
using CK.DB.Actor;
using CK.SqlServer;
using NUnit.Framework;
using System.Linq;
using System.Threading;

namespace CK.DB.User.UserPassword.Tests
{
    [TestFixture]
    public class UserPasswordTests
    {

        [Test]
        public void create_password_and_check_Verify_method()
        {
            var u = TestHelper.StObjMap.Default.Obtain<UserPasswordTable>();
            var user = TestHelper.StObjMap.Default.Obtain<UserTable>();
            using( var ctx = new SqlStandardCallContext() )
            {
                var userName = Guid.NewGuid().ToString();
                int userId = user.CreateUser( ctx, 1, userName );
                var pwd = "pwddetestcrrr";
                var pwd2 = "pwddetestcrdfezfrefzzfrr";

                u.CreatePasswordUser( ctx, 1, userId, pwd );
                Assert.That( u.Verify( ctx, userId, pwd ) == userId );
                Assert.That( u.Verify( ctx, userId, pwd2 ) == 0 );

                u.SetPassword( ctx, 1, userId, pwd2 );
                Assert.That( u.Verify( ctx, userId, pwd2 ) == userId );
                Assert.That( u.Verify( ctx, userId, pwd ) == 0 );

            }
        }

        [Test]
        public void create_a_password_for_an_anonymous_user_is_an_error()
        {
            var u = TestHelper.StObjMap.Default.Obtain<UserPasswordTable>();
            using( var ctx = new SqlStandardCallContext() )
            {
                Assert.Throws<SqlDetailedException>( () => u.CreatePasswordUser( ctx, 1, 0, "x" ) );
                Assert.Throws<SqlDetailedException>( () => u.CreatePasswordUser( ctx, 0, 1, "toto" ) );
            }
        }

        [Test]
        public void destroying_a_user_destroys_its_PasswordUser_facet()
        {
            var u = TestHelper.StObjMap.Default.Obtain<UserPasswordTable>();
            var user = TestHelper.StObjMap.Default.Obtain<UserTable>();
            using( var ctx = new SqlStandardCallContext() )
            {
                int userId = user.CreateUser( ctx, 1, Guid.NewGuid().ToString() );
                u.CreatePasswordUser( ctx, 1, userId, "pwd" );
                user.DestroyUser( ctx, 1, userId );
            }
        }

        [TestCase( "p" )]
        [TestCase( "deefzrfgebhntjuykilompo^ùp$*pù^mlkjhgf250258p" )]
        public void changing_iteration_count_updates_automatically_the_hash( string pwd )
        {
            var u = TestHelper.StObjMap.Default.Obtain<UserPasswordTable>();
            var user = TestHelper.StObjMap.Default.Obtain<UserTable>();
            using( var ctx = new SqlStandardCallContext() )
            {
                UserPasswordTable.HashIterationCount = 1000;
                var userName = Guid.NewGuid().ToString();
                int userId = user.CreateUser( ctx, 1, userName );
                u.CreatePasswordUser( ctx, 1, userId, pwd );
                var hash1 = u.Database.ExecuteScalar<byte[]>( $"select PwdHash from CK.tUserPassword where UserId={userId}" );

                UserPasswordTable.HashIterationCount = 2000;
                Assert.That( u.Verify( ctx, userId, pwd ) == userId );
                var hash2 = u.Database.ExecuteScalar<byte[]>( $"select PwdHash from CK.tUserPassword where UserId={userId}" );

                Assert.That( hash1.SequenceEqual( hash2 ), Is.False, "Hash has been updated." );

                UserPasswordTable.HashIterationCount = UserPasswordTable.DefaultHashIterationCount;
                Assert.That( u.Verify( ctx, userId, pwd ) == userId );
                var hash3 = u.Database.ExecuteScalar<byte[]>( $"select PwdHash from CK.tUserPassword where UserId={userId}" );

                Assert.That( hash1.SequenceEqual( hash3 ), Is.False, "Hash has been updated." );
                Assert.That( hash2.SequenceEqual( hash3 ), Is.False, "Hash has been updated." );

            }
        }

        class MigrationSupport : IUserPasswordMigrator
        {
            readonly int _userIdToMigrate;
            readonly string _pwd;

            public bool MigrationDoneCalled;

            public MigrationSupport( int userIdToMigrate, string pwd )
            {
                _userIdToMigrate = userIdToMigrate;
                _pwd = pwd;
            }

            public void MigrationDone( ISqlCallContext ctx, int userId ) => MigrationDoneCalled = true;

            public bool VerifyPassword( ISqlCallContext ctx, int userId, string password )
            {
                return userId == _userIdToMigrate && _pwd == password;
            }
        }

        [Test]
        public void password_migration_is_supported_by_user_id_and_user_name()
        {
            var p = TestHelper.StObjMap.Default.Obtain<Package>();
            var u = TestHelper.StObjMap.Default.Obtain<UserPasswordTable>();
            var user = TestHelper.StObjMap.Default.Obtain<UserTable>();
            using( var ctx = new SqlStandardCallContext() )
            {
                // By identifier
                {
                    string userName = Guid.NewGuid().ToString();
                    var idU = user.CreateUser( ctx, 1, userName );
                    p.PasswordMigrator = new MigrationSupport( idU, "toto" );
                    Assert.That( u.Verify( ctx, idU, "failed" ) == 0 );
                    p.Database.AssertEmptyReader( $"select 1 from CK.tUserPassword where UserId={idU}" );
                    Assert.That( u.Verify( ctx, idU, "toto" ) == idU );
                    p.Database.AssertScalarEquals( 1, $"select 1 from CK.tUserPassword where UserId={idU}" );
                    Assert.That( u.Verify( ctx, idU, "toto" ) == idU );
                }
                // By user name
                {
                    string userName = Guid.NewGuid().ToString();
                    var idU = user.CreateUser( ctx, 1, userName );
                    p.PasswordMigrator = new MigrationSupport( idU, "toto" );
                    Assert.That( u.Verify( ctx, userName, "failed" ) == 0 );
                    p.Database.AssertEmptyReader( $"select 1 from CK.tUserPassword where UserId={idU}" );
                    Assert.That( u.Verify( ctx, userName, "toto" ) == idU );
                    p.Database.AssertScalarEquals( 1, $"select 1 from CK.tUserPassword where UserId={idU}" );
                    Assert.That( u.Verify( ctx, userName, "toto" ) == idU );
                }
            }
        }

        [Test]
        public async Task password_migration_is_supported_by_user_id_and_user_name_async()
        {
            var p = TestHelper.StObjMap.Default.Obtain<Package>();
            var u = TestHelper.StObjMap.Default.Obtain<UserPasswordTable>();
            var user = TestHelper.StObjMap.Default.Obtain<UserTable>();
            using( var ctx = new SqlStandardCallContext() )
            {
                // By identifier
                {
                    string userName = Guid.NewGuid().ToString();
                    var idU = await user.CreateUserAsync( ctx, 1, userName );
                    p.PasswordMigrator = new MigrationSupport( idU, "toto" );
                    Assert.That( await u.VerifyAsync( ctx, idU, "failed" ) == 0 );
                    p.Database.AssertEmptyReader( $"select 1 from CK.tUserPassword where UserId={idU}" );
                    Assert.That( await u.VerifyAsync( ctx, idU, "toto" ) == idU );
                    p.Database.AssertScalarEquals( 1, $"select 1 from CK.tUserPassword where UserId={idU}" );
                    Assert.That( await u.VerifyAsync( ctx, idU, "toto" ) == idU );
                }
                // By user name
                {
                    string userName = Guid.NewGuid().ToString();
                    var idU = await user.CreateUserAsync( ctx, 1, userName );
                    p.PasswordMigrator = new MigrationSupport( idU, "toto" );
                    Assert.That( await u.VerifyAsync( ctx, userName, "failed" ) == 0 );
                    p.Database.AssertEmptyReader( $"select 1 from CK.tUserPassword where UserId={idU}" );
                    Assert.That( await u.VerifyAsync( ctx, userName, "toto" ) == idU );
                    p.Database.AssertScalarEquals( 1, $"select 1 from CK.tUserPassword where UserId={idU}" );
                    Assert.That( await u.VerifyAsync( ctx, userName, "toto" ) == idU );
                }
            }
        }

        [Test]
        public void onLogin_extension_point_is_called()
        {
            var u = TestHelper.StObjMap.Default.Obtain<UserPasswordTable>();
            var user = TestHelper.StObjMap.Default.Obtain<UserTable>();
            using( var ctx = new SqlStandardCallContext() )
            {
                // By identifier
                {
                    string userName = Guid.NewGuid().ToString();
                    var idU = user.CreateUser( ctx, 1, userName );
                    var baseTime = u.Database.ExecuteScalar<DateTime>( "select sysutcdatetime();" );
                    u.CreatePasswordUser( ctx, 1, idU, "password" );
                    var firstTime = u.Database.ExecuteScalar<DateTime>( $"select LastLoginTime from CK.tUserPassword where UserId={idU}" );
                    Assert.That( firstTime.Ticks, Is.EqualTo( baseTime.Ticks ).Within( TimeSpan.FromMilliseconds( 1000 ).Ticks ) );
                    Thread.Sleep( 100 );
                    Assert.That( u.Verify( ctx, userName, "failed login", actualLogin: true ) == 0 );
                    var firstTimeNo = u.Database.ExecuteScalar<DateTime>( $"select LastLoginTime from CK.tUserPassword where UserId={idU}" );
                    Assert.That( firstTimeNo, Is.EqualTo( firstTime ) );
                    Assert.That( u.Verify( ctx, userName, "password", actualLogin: true ) == idU );
                    var firstTimeYes = u.Database.ExecuteScalar<DateTime>( $"select LastLoginTime from CK.tUserPassword where UserId={idU}" );
                    Assert.That( firstTimeYes, Is.GreaterThan( firstTimeNo ) );
                }
            }
        }

        [Test]
        public void Basic_AuthProvider_is_registered()
        {
            Auth.Tests.AuthTests.CheckProviderRegistration( "Basic" );
        }

        [Test]
        public void vUserAuthProvider_reflects_the_user_basic_authentication()
        {
            var u = TestHelper.StObjMap.Default.Obtain<UserPasswordTable>();
            var user = TestHelper.StObjMap.Default.Obtain<UserTable>();
            using( var ctx = new SqlStandardCallContext() )
            {
                string userName = "Basic auth - " + Guid.NewGuid().ToString();
                var idU = user.CreateUser( ctx, 1, userName );
                u.Database.AssertEmptyReader( $"select * from CK.vUserAuthProvider where UserId={idU} and ProviderName='Basic'" );
                u.CreatePasswordUser( ctx, 1, idU, "password" );
                u.Database.AssertScalarEquals( 1, $"select count(*) from CK.vUserAuthProvider where UserId={idU} and ProviderName='Basic'" );
                u.DestroyPasswordUser( ctx, 1, idU );
                u.Database.AssertEmptyReader( $"select * from CK.vUserAuthProvider where UserId={idU} and ProviderName='Basic'" );
                // To let the use in the database with a basic authentication.
                u.CreatePasswordUser( ctx, 1, idU, "password" );
            }
        }
    }
}