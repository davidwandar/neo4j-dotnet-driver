﻿// Copyright (c) 2002-2018 "Neo4j,"
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
using System.Collections.Generic;
using FluentAssertions;
using Moq;
using Neo4j.Driver.Internal;
using Neo4j.Driver.Internal.IO;
using Neo4j.Driver.Internal.IO.MessageHandlers.V3;
using Neo4j.Driver.Internal.Messaging.V3;
using Neo4j.Driver.Internal.Protocol;
using Neo4j.Driver.V1;
using Xunit;
using static Neo4j.Driver.Tests.SessionTests;

namespace Neo4j.Driver.Tests.IO.MessageHandlers.V3
{
    public class BeginMessageHandlerTests : StructHandlerTests
    {
        internal override IPackStreamStructHandler HandlerUnderTest => new BeginMessageHandler();

        [Fact]
        public void ShouldThrowOnRead()
        {
            var handler = HandlerUnderTest;

            var ex = Record.Exception(() =>
                handler.Read(Mock.Of<IPackStreamReader>(), BoltProtocolV3MessageFormat.MsgBegin, 1));

            ex.Should().NotBeNull();
            ex.Should().BeOfType<ProtocolException>();
        }

        [Fact]
        public void ShouldWrite()
        {
            var writerMachine = CreateWriterMachine();
            var writer = writerMachine.Writer();

            writer.Write(new BeginMessage(Bookmark.From(FakeABookmark(123)), TimeSpan.FromMinutes(1),
                new Dictionary<string, object>
                {
                    {"username", "MollyMostlyWhite"}
                }));

            var readerMachine = CreateReaderMachine(writerMachine.GetOutput());
            var reader = readerMachine.Reader();

            reader.PeekNextType().Should().Be(PackStream.PackType.Struct);
            reader.ReadStructHeader().Should().Be(1);
            reader.ReadStructSignature().Should().Be(BoltProtocolV3MessageFormat.MsgBegin);

            var metadata = reader.ReadMap();
            metadata.Should().HaveCount(3).And.ContainKeys("bookmarks", "tx_timeout", "tx_metadata");

            metadata["bookmarks"].CastOrThrow<List<object>>().Should().HaveCount(1).And
                .Contain("neo4j:bookmark:v1:tx123");
            metadata["tx_timeout"].Should().Be(60000L);

            metadata["tx_metadata"].CastOrThrow<Dictionary<string, object>>().Should().HaveCount(1).And.Contain(
                new[]
                {
                    new KeyValuePair<string, object>("username", "MollyMostlyWhite"),
                });
        }

        [Fact]
        public void ShouldWriteEmptyMapWhenMetadataIsNull()
        {
            var writerMachine = CreateWriterMachine();
            var writer = writerMachine.Writer();

            writer.Write(new BeginMessage(null, null));

            var readerMachine = CreateReaderMachine(writerMachine.GetOutput());
            var reader = readerMachine.Reader();

            reader.PeekNextType().Should().Be(PackStream.PackType.Struct);
            reader.ReadStructHeader().Should().Be(1);
            reader.ReadStructSignature().Should().Be(BoltProtocolV3MessageFormat.MsgBegin);
            reader.ReadMap().Should().NotBeNull().And.HaveCount(0);
        }
    }
}