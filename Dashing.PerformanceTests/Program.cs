﻿namespace PerformanceTest {
    using System;
    using System.Collections.Generic;
    using System.Data.Common;
#if NET46
    using System.Data.Entity;
#endif
    using System.Data.SqlClient;
    using System.Diagnostics;
    using System.Linq;

    using Dapper;

    using Dashing;
    using Dashing.Configuration;
    using Dashing.Engine.DDL;
    using Dashing.Engine.Dialects;
    using Dashing.PerformanceTests;
    using Dashing.PerformanceTests.Domain;
    using Dashing.PerformanceTests.Tests.Dashing;
    using Dashing.PerformanceTests.Tests.EntityFramework;

    //using LightSpeed.Domain;

    //using Mindscape.LightSpeed;

    //using NHibernate.Linq;

    //using PerformanceTest.Tests.EF;
    //using PerformanceTest.Tests.NHibernate;

    //using ServiceStack.OrmLite;

    //using Database = Simple.Data.Database;

    internal static class Program {
        internal static readonly string ConnectionString = "Data Source=.;Initial Catalog=tempdb;Integrated Security=True";

        private static void Main(string[] args) {
            SetupDatabase();
            var tests = SetupTests();
            foreach (var testGroup in tests.GroupBy(t => t.TestName)) {
                Console.WriteLine("Running " + testGroup.Key);
                Console.WriteLine("------------------------");

                // do the stuff
                var results = RunTestGroup(testGroup);

                foreach (var result in results.OrderBy(s => s.Item2)) {
                    Console.WriteLine("{0,7:N0} {1}", result.Item2, result.Item1);
                }

                Console.WriteLine();
            }
        }

        private static IEnumerable<Tuple<string, long>> RunTestGroup(IEnumerable<Test> tests) {
            var random = new Random();

            foreach (var test in tests.OrderBy(t => random.Next())
                                      .Where(t => t.Provider != Providers.NHibernate)) {
                // warm up
                test.TestFunc(1);

                // iterate 
                var watch = new Stopwatch();
                watch.Start();
                for (var i = 1; i <= 500; ++i) {
                    test.TestFunc(random.Next(1, 500));
                }

                watch.Stop();

                yield return Tuple.Create(test.FriendlyName, watch.ElapsedMilliseconds);
            }
        }

        private static readonly IConfiguration dashingConfig = new DashingConfiguration();

        private static SqlDatabase dashingSqlDatabase;

        //private static readonly OrmLiteConnectionFactory connectionFactory = new OrmLiteConnectionFactory(
        //    ConnectionString.ConnectionString,
        //    SqlServerDialect.Provider);

        //private static readonly LightSpeedContext<TestUnitOfWork> lsContext = new LightSpeedContext<TestUnitOfWork> {
        //                                                                                                                PluralizeTableNames = true,
        //                                                                                                                ConnectionString =
        //                                                                                                                    ConnectionString
        //                                                                                                                    .ConnectionString,
        //                                                                                                                DataProvider =
        //                                                                                                                    DataProvider.SqlServer2012
        //                                                                                                            };

        private static List<Test> SetupTests() {
            var tests = new List<Test>();
            SetupSelectSingleTest(tests);
            SetupFetchTest(tests);
            SetupFetchChangeTests(tests);
            SetupFetchCollectionTests(tests);
            SetupFetchMultiCollectionTests(tests);
            SetupFetchMultipleMultiCollection(tests);
            SetupFetchMultipleChain(tests);
            return tests;
        }

        private static void SetupFetchMultipleChain(List<Test> tests) {
            const string TestName = "Fetch Multiple Chained Collections";

            // add dashing
            tests.Add(
                new Test(
                    Providers.Dashing,
                    TestName,
                    i => {
                        using (var dashingSession = dashingSqlDatabase.BeginSession()) {
                            return dashingSession.Query<Blog>()
                                                 .FetchMany(b => b.Posts)
                                                 .ThenFetch(p => p.Tags)
                                                 .SingleOrDefault(b => b.BlogId == i / 5);
                        }
                    }));

            // add dashing without transaction
            tests.Add(
                new Test(
                    Providers.Dashing,
                    TestName,
                    i => {
                        using (var dashingSession = dashingSqlDatabase.BeginTransactionLessSession()) {
                            return dashingSession.Query<Blog>()
                                                 .FetchMany(b => b.Posts)
                                                 .ThenFetch(p => p.Tags)
                                                 .SingleOrDefault(b => b.BlogId == i / 5);
                        }
                    },
                    "without Transaction"));

            // add EF
#if NET46
            tests.Add(
                new Test(
                    Providers.EntityFramework,
                    TestName,
                    i =>
                    {
                        using (var EfDb = new EfContext())
                        {
                            return EfDb.Blogs.Include(b => b.Posts.Select(p => p.Tags)).SingleOrDefault(b => b.BlogId == i / 5);
                        }
                    }));
#endif
        }

        private static void SetupFetchMultipleMultiCollection(List<Test> tests) {
            const string TestName = "Fetch Multiple Multiple Collections";

            // add dashing
            tests.Add(
                new Test(
                    Providers.Dashing,
                    TestName,
                    i => {
                        using (var dashingSession = dashingSqlDatabase.BeginSession()) {
                            return dashingSession.Query<Post>()
                                                 .Fetch(p => p.Comments)
                                                 .Fetch(p => p.Tags)
                                                 .Where(p => p.PostId > i && p.PostId < i + 3)
                                                 .ToList();
                        }
                    }));

            // add dashing
            tests.Add(
                new Test(
                    Providers.Dashing,
                    TestName,
                    i => {
                        using (var dashingSession = dashingSqlDatabase.BeginTransactionLessSession()) {
                            var j = i + 3;
                            return dashingSession.Query<Post>()
                                                 .Fetch(p => p.Comments)
                                                 .Fetch(p => p.Tags)
                                                 .Where(p => p.PostId > i && p.PostId < j)
                                                 .ToList();
                        }
                    },
                    "without Transaction"));

            // add EF
#if NET46
            tests.Add(
                new Test(
                    Providers.EntityFramework,
                    TestName,
                    i =>
                    {
                        using (var EfDb = new EfContext())
                        {
                            var j = i + 3;
                            return EfDb.Posts.Include(p => p.Comments).Include(p => p.Tags).Where(p => p.PostId > i && p.PostId < j).ToList();
                        }
                    }));
#endif

            // add nh stateful
            //tests.Add(
            //    new Test(
            //        Providers.NHibernate,
            //        TestName,
            //        i => {
            //            using (var nhSession = Nh.SessionFactory.OpenSession()) {
            //                // First(p => p.PostId == i) doesn't work?
            //                // ok, nHIbernate linq broken (now I remember the pain!)
            //                var j = i + 3;
            //                var posts = nhSession.QueryOver<Post>().Where(p => p.PostId > i && p.PostId < j).Future<Post>();
            //                var comments =
            //                    nhSession.QueryOver<Post>().Fetch(p => p.Comments).Eager.Where(p => p.PostId > i && p.PostId < j).Future<Post>();
            //                var tags = nhSession.QueryOver<Post>().Fetch(p => p.Tags).Eager.Where(p => p.PostId > i && p.PostId < j).Future<Post>();
            //                return posts.ToList();
            //            }
            //        },
            //        "Stateful"));
        }

        private static void SetupFetchMultiCollectionTests(List<Test> tests) {
            const string TestName = "Fetch Multiple Collections";

            // add dashing
            tests.Add(
                new Test(
                    Providers.Dashing,
                    TestName,
                    i => {
                        using (var dashingSession = dashingSqlDatabase.BeginSession()) {
                            return dashingSession.Query<Post>()
                                                 .Fetch(p => p.Comments)
                                                 .Fetch(p => p.Tags)
                                                 .Single(p => p.PostId == i);
                        }
                    }));

            // add EF
#if NET46
            tests.Add(
                new Test(
                    Providers.EntityFramework,
                    TestName,
                    i =>
                    {
                        using (var EfDb = new EfContext())
                        {
                            return EfDb.Posts.Include(p => p.Tags).Include(p => p.Comments).First(p => p.PostId == i);
                        }
                    }));
#endif

            // add nh stateful
            //tests.Add(
            //    new Test(
            //        Providers.NHibernate,
            //        TestName,
            //        i => {
            //            using (var nhSession = Nh.SessionFactory.OpenSession()) {
            //                // First(p => p.PostId == i) doesn't work?
            //                // ok, nHIbernate linq broken (now I remember the pain!)
            //                var posts = nhSession.QueryOver<Post>().Where(p => p.PostId == i).Future<Post>();
            //                var comments = nhSession.QueryOver<Post>().Fetch(p => p.Comments).Eager.Where(p => p.PostId == i).Future<Post>();
            //                var tags = nhSession.QueryOver<Post>().Fetch(p => p.Tags).Eager.Where(p => p.PostId == i).Future<Post>();
            //                return posts.First();
            //            }
            //        },
            //        "Stateful"));

            // add nh stateless
            // No can do, get NotSupportedException on first line here.
            //tests.Add(
            //    new Test(
            //        Providers.NHibernate,
            //        TestName,
            //        i => {
            //            // First(p => p.PostId == i) doesn't work?
            //            // ok, nHIbernate linq broken (now I remember the pain!)
            //            var posts = nhStatelessSession.QueryOver<Post>().Future<Post>();
            //            var comments =
            //                nhStatelessSession.QueryOver<Post>().Fetch(p => p.Comments).Eager.Future<Post>();
            //            var tags =
            //                nhStatelessSession.QueryOver<Post>().Fetch(p => p.Tags).Eager.Future<Post>();
            //            var post = posts.Where(p => p.PostId == i).First();
            //        },
            //        "Stateless"));
        }

        private static void SetupFetchCollectionTests(List<Test> tests) {
            const string TestName = "Fetch Collection";

            // add dapper
            tests.Add(
                new Test(
                    Providers.Dapper,
                    TestName,
                    i => {
                        using (var dapperConn = Open()) {
                            var post = dapperConn.Query<Post>(
                                                     "select [PostId], [Title], [Content], [Rating], [AuthorId], [BlogId], [DoNotMap] from [Posts] where ([PostId] = @l_1)",
                                                     new {
                                                             l_1 = i
                                                         })
                                                 .First();
                            var comments = dapperConn.Query<Comment>(
                                                         "select * from [Comments] where [PostId] = @postId",
                                                         new {
                                                                 postId = post.PostId
                                                             })
                                                     .ToList();
                            post.Comments = comments;
                            return post;
                        }
                    },
                    "Naive"));

            tests.Add(
                new Test(
                    Providers.Dapper,
                    TestName,
                    i => {
                        using (var dapperConn = Open()) {
                            var sql = @"
select * from Posts where PostId = @id
select * from Comments where PostId = @id";

                            var multi = dapperConn.QueryMultiple(
                                sql,
                                new {
                                        id = i
                                    });
                            var post = multi.Read<Post>()
                                            .Single();
                            post.Comments = multi.Read<Comment>()
                                                 .ToList();
                            multi.Dispose();
                            return post;
                        }
                    },
                    "Multiple Result Method"));

            // add Dashing
            tests.Add(
                new Test(
                    Providers.Dashing,
                    TestName,
                    i => {
                        using (var dashingSession = dashingSqlDatabase.BeginSession()) {
                            return dashingSession.Query<Post>()
                                                 .Fetch(p => p.Comments)
                                                 .First(p => p.PostId == i);
                        }
                    }));

            // add Dashing without transaction
            tests.Add(
                new Test(
                    Providers.Dashing,
                    TestName,
                    i => {
                        using (var dashingSession = dashingSqlDatabase.BeginTransactionLessSession()) {
                            return dashingSession.Query<Post>()
                                                 .Fetch(p => p.Comments)
                                                 .First(p => p.PostId == i);
                        }
                    },
                    "without transaction"));

            // add EF
#if NET46
            tests.Add(
                new Test(
                    Providers.EntityFramework,
                    TestName,
                    i =>
                    {
                        using (var EfDb = new EfContext())
                        {
                            return EfDb.Posts.Include(p => p.Comments).First(p => p.PostId == i);
                        }
                    }));
#endif

            // add nh stateful
            //tests.Add(
            //    new Test(
            //        Providers.NHibernate,
            //        TestName,
            //        i => {
            //            using (var nhSession = Nh.SessionFactory.OpenSession()) {
            //                return nhSession.Query<Post>().Fetch(p => p.Comments).First(p => p.PostId == i);
            //            }
            //        },
            //        "Stateful"));

            // add nh stateless
            //tests.Add(
            //    new Test(
            //        Providers.NHibernate,
            //        TestName,
            //        i => {
            //            using (var nhStatelessSession = Nh.SessionFactory.OpenStatelessSession()) {
            //                return nhStatelessSession.Query<Post>().Fetch(p => p.Comments).First(p => p.PostId == i);
            //            }
            //        },
            //        "Stateless"));
        }

        private static void SetupFetchChangeTests(List<Test> tests) {
            const string TestName = "Get And Change";
            var r = new Random();

            // dapper
            tests.Add(
                new Test(
                    Providers.Dapper,
                    TestName,
                    i => {
                        using (var dapperConn = Open()) {
                            var post = dapperConn.Query<Post>(
                                                     "select [PostId], [Title], [Content], [Rating], [AuthorId], [BlogId], [DoNotMap] from [Posts] where ([PostId] = @l_1)",
                                                     new {
                                                             l_1 = i
                                                         })
                                                 .First();
                            post.Title = Providers.Dapper + "_" + i + r.Next(100000);
                            dapperConn.Execute(
                                "Update [Posts] set [Title] = @Title where [PostId] = @PostId",
                                new {
                                        post.Title,
                                        post.PostId
                                    });
                            var thatPost = dapperConn.Query<Post>(
                                                         "select [PostId], [Title], [Content], [Rating], [AuthorId], [BlogId], [DoNotMap] from [Posts] where ([PostId] = @l_1)",
                                                         new {
                                                                 l_1 = i
                                                             })
                                                     .First();
                            if (thatPost.Title != post.Title) {
                                Console.WriteLine(TestName + " failed for " + Providers.Dapper + " as the update did not work");
                            }

                            return post;
                        }
                    }));

            // add Dashing
            tests.Add(
                new Test(
                    Providers.Dashing,
                    TestName,
                    i => {
                        using (var dashingSession = dashingSqlDatabase.BeginSession()) {
                            var post = dashingSession.Query<Post>()
                                                     .First(p => p.PostId == i);
                            post.Title = Providers.Dashing + "_" + i + r.Next(100000);
                            dashingSession.Save(post);
                            var thatPost = dashingSession.Query<Post>()
                                                         .First(p => p.PostId == i);
                            if (thatPost.Title != post.Title) {
                                Console.WriteLine(TestName + " failed for " + Providers.Dashing + " as the update did not work");
                            }

                            return post;
                        }
                    }));

            // add Dashing by id method
            tests.Add(
                new Test(
                    Providers.Dashing,
                    TestName,
                    i => {
                        using (var dashingSession = dashingSqlDatabase.BeginSession()) {
                            var post = dashingSession.Get<Post>(i);
                            post.Title = Providers.Dashing + "_" + i + r.Next(100000);
                            dashingSession.Save(post);
                            var thatPost = dashingSession.Get<Post>(i);
                            if (thatPost.Title != post.Title) {
                                Console.WriteLine(TestName + " failed for " + Providers.Dashing + " as the update did not work");
                            }

                            return post;
                        }
                    },
                    "By Id"));

            // add Dashing by id without transaction 
            tests.Add(
                new Test(
                    Providers.Dashing,
                    TestName,
                    i => {
                        using (var dashingSession = dashingSqlDatabase.BeginTransactionLessSession()) {
                            var post = dashingSession.Get<Post>(i);
                            post.Title = Providers.Dashing + "_" + i + r.Next(100000);
                            dashingSession.Save(post);
                            var thatPost = dashingSession.Get<Post>(i);
                            if (thatPost.Title != post.Title) {
                                Console.WriteLine(TestName + " failed for " + Providers.Dashing + " as the update did not work");
                            }

                            return post;
                        }
                    },
                    "By Id without transaction"));

            // add ef
#if NET46
            tests.Add(
                new Test(
                    Providers.EntityFramework,
                    TestName,
                    i =>
                    {
                        using (var EfDb = new EfContext())
                        {
                            var post = EfDb.Posts.Single(p => p.PostId == i);
                            post.Title = Providers.EntityFramework + "_" + i + r.Next(100000);
                            EfDb.SaveChanges();
                            var thatPost = EfDb.Posts.Single(p => p.PostId == i);
                            if (thatPost.Title != post.Title)
                            {
                                Console.WriteLine(TestName + " failed for " + Providers.EntityFramework + " as the update did not work");
                            }

                            return post;
                        }
                    }));
#endif

            // add servicestack
            //tests.Add(
            //    new Test(
            //        Providers.ServiceStack,
            //        TestName,
            //        i => {
            //            using (var ormliteConn = connectionFactory.OpenDbConnection()) {
            //                var post = ormliteConn.SingleById<Post>(i);
            //                post.Title = Providers.ServiceStack + "_" + i + r.Next(100000);
            //                ormliteConn.Update(post);
            //                var thatPost = ormliteConn.SingleById<Post>(i);
            //                if (thatPost.Title != post.Title) {
            //                    Console.WriteLine(TestName + " failed for " + Providers.ServiceStack + " as the update did not work");
            //                }

            //                return post;
            //            }
            //        },
            //        "without transaction"));

            // add servicestack with transaction
            //tests.Add(
            //    new Test(
            //        Providers.ServiceStack,
            //        TestName,
            //        i => {
            //            using (var ormliteConn = connectionFactory.OpenDbConnection()) {
            //                using (var tran = ormliteConn.OpenTransaction()) {
            //                    var post = ormliteConn.SingleById<Post>(i);
            //                    post.Title = Providers.ServiceStack + "_" + i + r.Next(100000);
            //                    ormliteConn.Update(post);
            //                    var thatPost = ormliteConn.SingleById<Post>(i);
            //                    if (thatPost.Title != post.Title) {
            //                        Console.WriteLine(TestName + " failed for " + Providers.ServiceStack + " as the update did not work");
            //                    }

            //                    return post;
            //                }
            //            }
            //        }));

            // add nhibernate
            //tests.Add(
            //    new Test(
            //        Providers.NHibernate,
            //        TestName,
            //        i => {
            //            using (var nhSession = Nh.SessionFactory.OpenSession()) {
            //                var post = nhSession.Get<Post>(i);
            //                post.Title = Providers.NHibernate + "_" + i + r.Next(100000);
            //                nhSession.Update(post);
            //                nhSession.Flush();
            //                var thatPost = nhSession.Get<Post>(i);
            //                if (thatPost.Title != post.Title) {
            //                    Console.WriteLine(TestName + " failed for " + Providers.NHibernate + " as the update did not work");
            //                }

            //                return post;
            //            }
            //        }));

            // lightspeed
            //tests.Add(
            //    new Test(
            //        Providers.LightSpeed,
            //        TestName,
            //        i => {
            //            using (var uow = lsContext.CreateUnitOfWork()) {
            //                var post = uow.FindById<LightSpeed.Domain.Post>(i);
            //                post.Title = Providers.LightSpeed + "_" + i + r.Next(100000);
            //                uow.SaveChanges();
            //                var thatPost = uow.FindById<LightSpeed.Domain.Post>(i);
            //                if (thatPost.Title != post.Title) {
            //                    Console.WriteLine(TestName + " failed for " + Providers.LightSpeed + " as the update did not work");
            //                }

            //                return post;
            //            }
            //        },
            //        "without explicit transaction"));

            // lightspeed
            //tests.Add(
            //    new Test(
            //        Providers.LightSpeed,
            //        TestName,
            //        i => {
            //            using (var uow = lsContext.CreateUnitOfWork()) {
            //                using (var tran = uow.BeginTransaction()) {
            //                    var post = uow.FindById<LightSpeed.Domain.Post>(i);
            //                    post.Title = Providers.LightSpeed + "_" + i + r.Next(100000);
            //                    uow.SaveChanges();
            //                    var thatPost = uow.FindById<LightSpeed.Domain.Post>(i);
            //                    if (thatPost.Title != post.Title) {
            //                        Console.WriteLine(TestName + " failed for " + Providers.LightSpeed + " as the update did not work");
            //                    }

            //                    return post;
            //                }
            //            }
            //        }));
        }

        private static void SetupFetchTest(List<Test> tests) {
            const string TestName = "Fetch";

            // add dapper
            tests.Add(
                new Test(
                    Providers.Dapper,
                    TestName,
                    i => {
                        using (var dapperConn = Open()) {
                            return dapperConn.Query<Post, User, Post>(
                                                 "select t.[PostId], t.[Title], t.[Content], t.[Rating], t.[BlogId], t.[DoNotMap], t_1.[UserId], t_1.[Username], t_1.[EmailAddress], t_1.[Password], t_1.[IsEnabled], t_1.[HeightInMeters] from [Posts] as t left join [Users] as t_1 on t.AuthorId = t_1.UserId where ([PostId] = @l_1)",
                                                 (p, u) => {
                                                     p.Author = u;
                                                     return p;
                                                 },
                                                 new {
                                                         l_1 = i
                                                     },
                                                 splitOn: "UserId")
                                             .First();
                        }
                    }));

            // add Dashing
            tests.Add(
                new Test(
                    Providers.Dashing,
                    TestName,
                    i => {
                        using (var dashingSession = dashingSqlDatabase.BeginSession()) {
                            return dashingSession.Query<Post>()
                                                 .Fetch(p => p.Author)
                                                 .First(p => p.PostId == i);
                        }
                    }));

            // dashing without transaction
            tests.Add(
                new Test(
                    Providers.Dashing,
                    TestName,
                    i => {
                        using (var session = dashingSqlDatabase.BeginTransactionLessSession()) {
                            return session.Query<Post>()
                                          .Fetch(p => p.Author)
                                          .First(p => p.PostId == i);
                        }
                    },
                    "Without transaction"));

            // add ef
#if NET46
            tests.Add(
                new Test(
                    Providers.EntityFramework,
                    TestName,
                    i =>
                    {
                        using (var EfDb = new EfContext())
                        {
                            return EfDb.Posts.AsNoTracking().Include(p => p.Author).First(p => p.PostId == i);
                        }
                    }));
#endif

            // add nh stateful
            //tests.Add(
            //    new Test(
            //        Providers.NHibernate,
            //        TestName,
            //        i => {
            //            using (var nhSession = Nh.SessionFactory.OpenSession()) {
            //                return nhSession.Query<Post>().Fetch(p => p.Author).First(p => p.PostId == i);
            //            }
            //        },
            //        "Stateful"));

            // add nh stateless
            //tests.Add(
            //    new Test(
            //        Providers.NHibernate,
            //        TestName,
            //        i => {
            //            using (var nhStatelessSession = Nh.SessionFactory.OpenStatelessSession()) {
            //                return nhStatelessSession.Query<Post>().Fetch(p => p.Author).First(p => p.PostId == i);
            //                ;
            //            }
            //        },
            //        "Stateless"));
        }

        private static void SetupSelectSingleTest(List<Test> tests) {
            const string TestName = "SelectSingle";

            // add dapper
            tests.Add(
                new Test(
                    Providers.Dapper,
                    TestName,
                    i => {
                        using (var dapperConn = Open()) {
                            return dapperConn.Query<Post>(
                                                 "select [PostId], [Title], [Content], [Rating], [AuthorId], [BlogId], [DoNotMap] from [Posts] where ([PostId] = @l_1)",
                                                 new {
                                                         l_1 = i
                                                     })
                                             .First();
                        }
                    }));

            // add Dashing
            tests.Add(
                new Test(
                    Providers.Dashing,
                    TestName,
                    i => {
                        using (var dashingSession = dashingSqlDatabase.BeginSession()) {
                            return dashingSession.Query<Post>()
                                                 .First(p => p.PostId == i);
                        }
                    }));

            // dashing - no transaction
            tests.Add(
                new Test(
                    Providers.Dashing,
                    TestName,
                    i => {
                        using (var dashingSession = dashingSqlDatabase.BeginTransactionLessSession()) {
                            return dashingSession.Get<Post>(i);
                        }
                    },
                    "By Id without Transaction"));

            // add Dashing by id
            tests.Add(
                new Test(
                    Providers.Dashing,
                    TestName,
                    i => {
                        using (var dashingSession = dashingSqlDatabase.BeginSession()) {
                            return dashingSession.Get<Post>(i);
                        }
                    },
                    "By Id"));

            // add ef
#if NET46
            tests.Add(
                new Test(
                    Providers.EntityFramework,
                    TestName,
                    i =>
                    {
                        using (var EfDb = new EfContext())
                        {
                            return EfDb.Posts.AsNoTracking().First(p => p.PostId == i);
                        }
                    }));

            // add ef2
            tests.Add(
                new Test(
                    Providers.EntityFramework,
                    TestName,
                    i =>
                    {
                        using (var EfDb = new EfContext())
                        {
                            EfDb.Configuration.AutoDetectChangesEnabled = false;
                            var post = EfDb.Posts.Find(i);
                            EfDb.Configuration.AutoDetectChangesEnabled = true;
                            return post;
                        }
                    },
                    "Using Find with AutoDetectChangesEnabled = false"));
#endif

            // add ormlite
            //tests.Add(
            //    new Test(
            //        Providers.ServiceStack,
            //        TestName,
            //        i => {
            //            using (var ormliteConn = connectionFactory.Open()) {
            //                using (ormliteConn.OpenTransaction()) {
            //                    return ormliteConn.SingleById<Post>(i);
            //                }
            //            }
            //        }));

            // add ormlite
            //tests.Add(
            //    new Test(
            //        Providers.ServiceStack,
            //        TestName,
            //        i => {
            //            using (var ormliteConn = connectionFactory.Open()) {
            //                return ormliteConn.SingleById<Post>(i);
            //            }
            //        },
            //        "Without transaction"));

            // add simple data
            //tests.Add(
            //    new Test(
            //        Providers.SimpleData,
            //        TestName,
            //        i => {
            //            var simpleDataDb = Database.OpenConnection(ConnectionString.ConnectionString);
            //            return simpleDataDb.Posts.Get(i);
            //        }));

            // add nh stateless
            //tests.Add(
            //    new Test(
            //        Providers.NHibernate,
            //        TestName,
            //        i => {
            //            using (var nhStatelessSession = Nh.SessionFactory.OpenStatelessSession()) {
            //                return nhStatelessSession.Get<Post>(i);
            //            }
            //        },
            //        "Stateless"));

            // add nh stateful
            //tests.Add(
            //    new Test(
            //        Providers.NHibernate,
            //        TestName,
            //        i => {
            //            using (var nhSession = Nh.SessionFactory.OpenSession()) {
            //                return nhSession.Get<Post>(i);
            //            }
            //        },
            //        "Stateful"));

            // add lightspeed
            //tests.Add(
            //    new Test(
            //        Providers.LightSpeed,
            //        TestName,
            //        i => {
            //            using (var uow = lsContext.CreateUnitOfWork()) {
            //                return uow.Posts.Single(p => p.Id == i);
            //            }
            //        },
            //        "Linq"));

            // lightspeed find by id
            //tests.Add(
            //    new Test(
            //        Providers.LightSpeed,
            //        TestName,
            //        i => {
            //            using (var uow = lsContext.CreateUnitOfWork()) {
            //                return uow.FindById<LightSpeed.Domain.Post>(i);
            //            }
            //        },
            //        "FindById without transaction"));
        }

        private static DbConnection Open() {
            var connection = SqlClientFactory.Instance.CreateConnection();
            connection.ConnectionString = ConnectionString;
            connection.Open();
            return connection;
        }

        private static void SetupDatabase() {
            var d = new SqlServerDialect();
            var dtw = new DropTableWriter(d);
            var ctw = new CreateTableWriter(d);
            var dropTables = dashingConfig.Maps.Select(dtw.DropTableIfExists);
            var createTables = dashingConfig.Maps.Select(ctw.CreateTable);
            var sqls = dropTables.Concat(createTables)
                                 .ToArray();
            dashingSqlDatabase = new SqlDatabase(dashingConfig, SqlClientFactory.Instance, ConnectionString, new SqlServer2012Dialect());

            using (var setupSession = dashingSqlDatabase.BeginSession()) {
                foreach (var sql in sqls) {
                    setupSession.Dapper.Execute(sql);
                }

                var r = new Random();
                var users = new List<User>();
                for (var i = 0; i < 100; i++) {
                    var user = new User();
                    users.Add(user);
                    setupSession.Insert(user);
                }

                var blogs = new List<Blog>();
                for (var i = 0; i < 100; i++) {
                    var blog = new Blog();
                    blogs.Add(blog);
                    setupSession.Insert(blog);
                }

                var posts = new List<Post>();
                for (var i = 0; i <= 500; i++) {
                    var userId = r.Next(100);
                    var blogId = r.Next(100);
                    var post = new Post {
                                            Author = users[userId],
                                            Blog = blogs[blogId],
                                            Title = Guid.NewGuid()
                                                        .ToString("N")
                                        };
                    setupSession.Insert(post);
                    posts.Add(post);
                }

                for (var i = 0; i < 5000; i++) {
                    var comment = new Comment {
                                                  Post = posts[r.Next(500)],
                                                  User = users[r.Next(100)]
                                              };
                    setupSession.Insert(comment);
                }

                var tags = new List<Tag>();
                for (var i = 0; i < 100; i++) {
                    var tag = new Tag {
                                          Content = "Tag" + i
                                      };
                    tags.Add(tag);
                    setupSession.Insert(tag);
                }

                for (var i = 0; i < 5000; i++) {
                    var postTag = new PostTag {
                                                  Post = posts[r.Next(500)],
                                                  Tag = tags[r.Next(100)]
                                              };
                    setupSession.Insert(postTag);
                }

                setupSession.Complete();
            }
        }
    }
}