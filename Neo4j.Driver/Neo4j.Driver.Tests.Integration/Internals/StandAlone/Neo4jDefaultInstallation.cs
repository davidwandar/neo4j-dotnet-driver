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

using System;
using Neo4j.Driver.IntegrationTests.Shared;
using Neo4j.Driver.Internal;
using Neo4j.Driver;

namespace Neo4j.Driver.IntegrationTests.Internals
{
    public class Neo4jDefaultInstallation
    {
        public const string User = "neo4j";
        public const string Password = "neo4j";
        public const string HttpUri = "http://localhost:7474";
        public const string BoltUri = "bolt://localhost:7687";

        public static IDriver NewBoltDriver(Uri boltUri, IAuthToken authToken)
        {
            ILogger logger;
            var configuredLevelStr = Environment.GetEnvironmentVariable("NEOLOGLEVEL");
            if (Enum.TryParse<ExtendedLogLevel>(configuredLevelStr ?? "", true, out var configuredLevel))
            {
                logger = new TestLogger(s => Console.WriteLine(s), configuredLevel);
            }
            else
            {
                logger = new TestLogger(s => System.Diagnostics.Debug.WriteLine(s), ExtendedLogLevel.Debug);
            }

            return GraphDatabase.Driver(boltUri, authToken, o => { o.WithLogger(logger); });
        }
    }
}