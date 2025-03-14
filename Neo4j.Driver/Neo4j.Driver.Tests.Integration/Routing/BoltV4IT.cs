// Copyright (c) 2002-2019 "Neo4j,"
// Neo4j Sweden AB [http://neo4j.com]
// 
// This file is part of Neo4j.
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//     http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using FluentAssertions;
using Neo4j.Driver.IntegrationTests.Shared;
using Xunit.Abstractions;
using static Neo4j.Driver.IntegrationTests.VersionComparison;

namespace Neo4j.Driver.IntegrationTests.Routing
{
    public class BoltV4IT : RoutingDriverTestBase
    {
        private readonly IDriver _driver;

        public BoltV4IT(ITestOutputHelper output, CausalClusterIntegrationTestFixture fixture)
            : base(output, fixture)
        {
            _driver = GraphDatabase.Driver(Cluster.AnyCore().BoltRoutingUri, Cluster.AuthToken,
                o => o.WithLogger(TestLogger.Create(output)));
        }

        [RequireClusterFact("4.0.0", GreaterThanOrEqualTo)]
        public async Task ShouldReturnDatabaseInfoForDefaultDatabaseInTxFunc()
        {
            await VerifyDatabaseNameOnSummaryTxFunc(null, "neo4j");
        }

        [RequireClusterFact("4.0.0", GreaterThanOrEqualTo)]
        public async Task ShouldReturnDatabaseInfoForDefaultDatabaseWhenSpecifiedInTxFunc()
        {
            await VerifyDatabaseNameOnSummaryTxFunc("neo4j", "neo4j");
        }

        [RequireClusterFact("4.0.0", GreaterThanOrEqualTo)]
        public async Task ShouldReturnDatabaseInfoForDatabaseInTxFunc()
        {
            var bookmark = await CreateDatabase(_driver, "foo");
            try
            {
                await VerifyDatabaseNameOnSummaryTxFunc("foo", "foo", bookmark);
            }
            finally
            {
                await DropDatabase(_driver, "foo", bookmark);
            }
        }

        [RequireClusterFact("4.0.0", GreaterThanOrEqualTo)]
        public void ShouldThrowForNonExistentDatabaseInTxFunc()
        {
            this.Awaiting(_ => VerifyDatabaseNameOnSummaryTxFunc("bar", "bar")).Should().Throw<ClientException>()
                .WithMessage("*database does not exist*");
        }

        [RequireClusterFact("4.0.0", LessThan)]
        public void ShouldThrowWhenDatabaseIsSpecifiedInTxFunc()
        {
            this.Awaiting(_ => VerifyDatabaseNameOnSummaryTxFunc("bar", "bar")).Should().Throw<ClientException>()
                .WithMessage("*to a server that does not support multiple databases.*");
        }

        private async Task VerifyDatabaseNameOnSummaryTxFunc(string name, string expected, Bookmark bookmark = null)
        {
            var session = _driver.AsyncSession(o =>
            {
                if (!string.IsNullOrEmpty(name))
                {
                    o.WithDatabase(name);
                }

                o.WithBookmarks(bookmark ?? Bookmark.Empty);
            });

            try
            {
                var summary = await session.ReadTransactionAsync(async txc =>
                {
                    var cursor = await txc.RunAsync("RETURN 1");
                    return await cursor.SummaryAsync();
                });

                summary.Database.Should().NotBeNull();
                summary.Database.Name.Should().Be(expected);
            }
            finally
            {
                await session.CloseAsync();
            }
        }

        private static async Task<Bookmark> CreateDatabase(IDriver driver, string name)
        {
            var session = driver.AsyncSession(o => o.WithDatabase("system"));
            try
            {
                await session.WriteTransactionAsync(txc => txc.RunAndConsumeAsync($"CREATE DATABASE {name}"));
                return session.LastBookmark;
            }
            finally
            {
                await session.CloseAsync();
            }
        }

        private static async Task DropDatabase(IDriver driver, string name, Bookmark bookmark)
        {
            var session = driver.AsyncSession(o => o.WithDatabase("system").WithBookmarks(bookmark));
            try
            {
                await session.WriteTransactionAsync(txc => txc.RunAndConsumeAsync($"DROP DATABASE {name}"));
            }
            finally
            {
                await session.CloseAsync();
            }
        }
    }
}