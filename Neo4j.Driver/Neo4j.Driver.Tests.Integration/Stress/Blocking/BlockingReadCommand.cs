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

using System.Linq;
using FluentAssertions;

namespace Neo4j.Driver.IntegrationTests.Stress
{
    public class BlockingReadCommand<TContext> : BlockingCommand<TContext>
        where TContext : StressTestContext
    {
        public BlockingReadCommand(IDriver driver, bool useBookmark)
            : base(driver, useBookmark)
        {
        }

        public override void Execute(TContext context)
        {
            using (var session = NewSession(AccessMode.Read, context))
            {
                var result = session.Run("MATCH (n) RETURN n LIMIT 1");
                var record = result.SingleOrDefault();
                record?[0].Should().BeAssignableTo<INode>();
                context.NodeRead(result.Summary());
            }
        }
    }
}