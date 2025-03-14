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
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Neo4j.Driver.Internal;
using Neo4j.Driver.Internal.Result;
using Xunit;

namespace Neo4j.Driver.Tests
{
    public class StatementResultCursorBuilderTests
    {
        [Fact]
        public void ShouldStartInRunRequestedStateRx()
        {
            var builder =
                new StatementResultCursorBuilder(CreateSummaryBuilder(), CreateTaskQueue(), null, null, null, reactive: true);
            builder.CurrentState.Should().Be(StatementResultCursorBuilder.State.RunRequested);
        }

        [Fact]
        public void ShouldStartInRunAndRecordsRequestedState()
        {
            var builder =
                new StatementResultCursorBuilder(CreateSummaryBuilder(), CreateTaskQueue(), null, null, null);
            builder.CurrentState.Should().Be(StatementResultCursorBuilder.State.RunAndRecordsRequested);
        }

        [Fact]
        public void ShouldTransitionToRunCompletedWhenRunCompletedRx()
        {
            var builder =
                new StatementResultCursorBuilder(CreateSummaryBuilder(), CreateTaskQueue(), null, null, null, reactive: true);
            builder.CurrentState.Should().Be(StatementResultCursorBuilder.State.RunRequested);

            builder.RunCompleted(0, new[] {"a", "b", "c"}, null);
            builder.CurrentState.Should().Be(StatementResultCursorBuilder.State.RunCompleted);
        }

        [Fact]
        public void ShouldNotTransitionToRunCompletedWhenRunCompleted()
        {
            var builder =
                new StatementResultCursorBuilder(CreateSummaryBuilder(), CreateTaskQueue(), null, null, null);
            builder.CurrentState.Should().Be(StatementResultCursorBuilder.State.RunAndRecordsRequested);

            builder.RunCompleted(0, new[] {"a", "b", "c"}, null);
            builder.CurrentState.Should().Be(StatementResultCursorBuilder.State.RunAndRecordsRequested);
        }

        [Fact]
        public void ShouldTransitionToRecordsStreamingStreamingWhenRecordIsPushedRx()
        {
            var builder =
                new StatementResultCursorBuilder(CreateSummaryBuilder(), CreateTaskQueue(), null, null, null, reactive: true);
            builder.CurrentState.Should().Be(StatementResultCursorBuilder.State.RunRequested);

            builder.RunCompleted(0, new[] {"a", "b", "c"}, null);
            builder.CurrentState.Should().Be(StatementResultCursorBuilder.State.RunCompleted);

            builder.PushRecord(new object[] {1, 2, 3});
            builder.CurrentState.Should().Be(StatementResultCursorBuilder.State.RecordsStreaming);
        }

        [Fact]
        public void ShouldTransitionToRecordsStreamingStreamingWhenRecordIsPushed()
        {
            var builder =
                new StatementResultCursorBuilder(CreateSummaryBuilder(), CreateTaskQueue(), null, null, null);
            builder.CurrentState.Should().Be(StatementResultCursorBuilder.State.RunAndRecordsRequested);

            builder.RunCompleted(0, new[] {"a", "b", "c"}, null);
            builder.CurrentState.Should().Be(StatementResultCursorBuilder.State.RunAndRecordsRequested);

            builder.PushRecord(new object[] {1, 2, 3});
            builder.CurrentState.Should().Be(StatementResultCursorBuilder.State.RecordsStreaming);
        }

        [Fact]
        public void ShouldTransitionToRunCompletedWhenPullCompletedWithHasMore()
        {
            var builder =
                new StatementResultCursorBuilder(CreateSummaryBuilder(), CreateTaskQueue(), null, null, null)
                {
                    CurrentState = StatementResultCursorBuilder.State.RecordsStreaming
                };

            builder.PullCompleted(true, null);
            builder.CurrentState.Should().Be(StatementResultCursorBuilder.State.RunCompleted);
        }

        [Fact]
        public void ShouldTransitionToCompletedWhenPullCompleted()
        {
            var builder =
                new StatementResultCursorBuilder(CreateSummaryBuilder(), CreateTaskQueue(), null, null, null)
                {
                    CurrentState = StatementResultCursorBuilder.State.RecordsStreaming
                };

            builder.PullCompleted(false, null);
            builder.CurrentState.Should().Be(StatementResultCursorBuilder.State.Completed);
        }

        [Fact]
        public async Task ShouldInvokeResourceHandlerWhenCompleted()
        {
            var actions = new Queue<Action>();
            var resourceHandler = new Mock<IResultResourceHandler>();
            var builder =
                new StatementResultCursorBuilder(CreateSummaryBuilder(), CreateTaskQueue(actions), null, null,
                    resourceHandler.Object);

            actions.Enqueue(() => builder.RunCompleted(0, new[] {"a"}, null));
            actions.Enqueue(() => builder.PushRecord(new object[] {1}));
            actions.Enqueue(() => builder.PushRecord(new object[] {2}));
            actions.Enqueue(() => builder.PushRecord(new object[] {3}));
            actions.Enqueue(() => builder.PullCompleted(false, null));

            var cursor = builder.CreateCursor();

            var hasNext = await cursor.FetchAsync();
            hasNext.Should().BeTrue();
            resourceHandler.Verify(x => x.OnResultConsumedAsync(), Times.Never);

            hasNext = await cursor.FetchAsync();
            hasNext.Should().BeTrue();
            resourceHandler.Verify(x => x.OnResultConsumedAsync(), Times.Never);

            hasNext = await cursor.FetchAsync();
            hasNext.Should().BeTrue();
            resourceHandler.Verify(x => x.OnResultConsumedAsync(), Times.Never);

            hasNext = await cursor.FetchAsync();
            hasNext.Should().BeFalse();
            resourceHandler.Verify(x => x.OnResultConsumedAsync(), Times.Once);
        }

        public class Reactive
        {
            private int moreCallCount;
            private int cancelCallCount;

            [Fact]
            public async Task ShouldCallMoreOnceAndReturnRecords()
            {
                var actions = new Queue<Action>();
                var builder =
                    new StatementResultCursorBuilder(CreateSummaryBuilder(), CreateTaskQueue(actions),
                        MoreFunction(), CancelFunction(), null, reactive: true);

                actions.Enqueue(() => builder.RunCompleted(0, new[] {"a"}, null));
                actions.Enqueue(() => builder.PushRecord(new object[] {1}));
                actions.Enqueue(() => builder.PushRecord(new object[] {2}));
                actions.Enqueue(() => builder.PushRecord(new object[] {3}));
                actions.Enqueue(() => builder.PullCompleted(false, null));

                var list = await builder.CreateCursor().ToListAsync(r => r[0].As<int>());

                list.Should().BeEquivalentTo(1, 2, 3);
                moreCallCount.Should().Be(1);
                cancelCallCount.Should().Be(0);
            }

            [Fact]
            public async Task ShouldCallMoreTwiceAndReturnRecords()
            {
                var actions = new Queue<Action>();
                var builder =
                    new StatementResultCursorBuilder(CreateSummaryBuilder(), CreateTaskQueue(actions),
                        MoreFunction(), CancelFunction(), null, reactive: true);

                actions.Enqueue(() => builder.RunCompleted(0, new[] {"a"}, null));
                actions.Enqueue(() => builder.PushRecord(new object[] {1}));
                actions.Enqueue(() => builder.PullCompleted(true, null));
                actions.Enqueue(() => builder.PushRecord(new object[] {2}));
                actions.Enqueue(() => builder.PushRecord(new object[] {3}));
                actions.Enqueue(() => builder.PullCompleted(false, null));

                var list = await builder.CreateCursor().ToListAsync(r => r[0].As<int>());

                list.Should().BeEquivalentTo(1, 2, 3);
                moreCallCount.Should().Be(2);
                cancelCallCount.Should().Be(0);
            }

            [Fact]
            public async Task ShouldCallMoreThreeTimesAndReturnRecords()
            {
                var actions = new Queue<Action>();
                var builder =
                    new StatementResultCursorBuilder(CreateSummaryBuilder(), CreateTaskQueue(actions),
                        MoreFunction(), CancelFunction(), null, reactive: true);

                actions.Enqueue(() => builder.RunCompleted(0, new[] {"a"}, null));
                actions.Enqueue(() => builder.PushRecord(new object[] {1}));
                actions.Enqueue(() => builder.PullCompleted(true, null));
                actions.Enqueue(() => builder.PushRecord(new object[] {2}));
                actions.Enqueue(() => builder.PullCompleted(true, null));
                actions.Enqueue(() => builder.PushRecord(new object[] {3}));
                actions.Enqueue(() => builder.PullCompleted(false, null));

                var list = await builder.CreateCursor().ToListAsync(r => r[0].As<int>());

                list.Should().BeEquivalentTo(1, 2, 3);
                moreCallCount.Should().Be(3);
                cancelCallCount.Should().Be(0);
            }

            [Fact]
            public async Task ShouldCallCancelAndReturnNoRecords()
            {
                var actions = new Queue<Action>();
                var builder =
                    new StatementResultCursorBuilder(CreateSummaryBuilder(), CreateTaskQueue(actions),
                        MoreFunction(), CancelFunction(), null, reactive: true);

                actions.Enqueue(() => builder.RunCompleted(0, new[] {"a"}, null));
                actions.Enqueue(() => builder.PullCompleted(false, null));

                var cursor = builder.CreateCursor();

                var keys = await cursor.KeysAsync();
                keys.Should().BeEquivalentTo("a");

                cursor.Cancel();

                var list = await cursor.ToListAsync(r => r[0].As<int>());

                list.Should().BeEmpty();
                moreCallCount.Should().Be(0);
                cancelCallCount.Should().Be(1);
            }

            [Fact]
            public async Task ShouldReturnFirstBatchOfRecordsAndCancel()
            {
                var actions = new Queue<Action>();
                var builder =
                    new StatementResultCursorBuilder(CreateSummaryBuilder(), CreateTaskQueue(actions),
                        MoreFunction(), CancelFunction(), null, reactive: true);

                actions.Enqueue(() => builder.RunCompleted(0, new[] {"a"}, null));
                actions.Enqueue(() => builder.PushRecord(new object[] {1}));
                actions.Enqueue(() => builder.PushRecord(new object[] {2}));
                actions.Enqueue(() => builder.PullCompleted(true, null));
                actions.Enqueue(() => builder.PullCompleted(false, null));

                var cursor = builder.CreateCursor();

                var keys = await cursor.KeysAsync();
                keys.Should().BeEquivalentTo("a");

                var hasRecord1 = await cursor.FetchAsync();
                var record1 = cursor.Current;
                hasRecord1.Should().BeTrue();
                record1[0].Should().Be(1);

                var hasRecord2 = await cursor.FetchAsync();
                var record2 = cursor.Current;
                hasRecord2.Should().BeTrue();
                record2[0].Should().Be(2);

                cursor.Cancel();

                var list = await cursor.ToListAsync(r => r[0].As<int>());

                list.Should().BeEmpty();
                moreCallCount.Should().Be(1);
                cancelCallCount.Should().Be(1);
            }

            private Func<StatementResultCursorBuilder, long, long, Task> MoreFunction()
            {
                return (cursorBuilder, id, n) =>
                {
                    Interlocked.Increment(ref moreCallCount);
                    return Task.CompletedTask;
                };
            }

            private Func<StatementResultCursorBuilder, long, Task> CancelFunction()
            {
                return (cursorBuilder, id) =>
                {
                    Interlocked.Increment(ref cancelCallCount);
                    return Task.CompletedTask;
                };
            }
        }

        private static SummaryBuilder CreateSummaryBuilder()
        {
            return new SummaryBuilder(new Statement("Fake"), Mock.Of<IServerInfo>());
        }

        private static Func<Task> CreateTaskQueue(Queue<Action> actions = null)
        {
            if (actions == null)
            {
                actions = new Queue<Action>();
            }

            return () =>
            {
                if (actions.TryDequeue(out var action))
                {
                    action();
                }

                return Task.CompletedTask;
            };
        }
    }
}