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

namespace Neo4j.Driver.Internal
{
    internal class InternalSession : ISession
    {
        private readonly IInternalAsyncSession _session;
        private readonly IRetryLogic _retryLogic;
        private readonly BlockingExecutor _executor;

        private bool _disposed;

        public InternalSession(IInternalAsyncSession session, IRetryLogic retryLogic, BlockingExecutor executor)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _retryLogic = retryLogic ?? throw new ArgumentNullException(nameof(retryLogic));
            _executor = executor ?? throw new ArgumentNullException(nameof(executor));
        }

        public Bookmark LastBookmark => _session.LastBookmark;

        public IStatementResult Run(string statement)
        {
            return Run(new Statement(statement));
        }

        public IStatementResult Run(string statement, object parameters)
        {
            return Run(new Statement(statement, parameters.ToDictionary()));
        }

        public IStatementResult Run(string statement, IDictionary<string, object> parameters)
        {
            return Run(new Statement(statement, parameters));
        }

        public IStatementResult Run(Statement statement)
        {
            return Run(statement, null);
        }

        #region BeginTransaction Methods

        public ITransaction BeginTransaction()
        {
            return BeginTransaction(null);
        }

        public ITransaction BeginTransaction(Action<TransactionConfigBuilder> action)
        {
            return new InternalTransaction(
                _executor.RunSync(() => _session.BeginTransactionAsync(action))
                    .CastOrThrow<IInternalAsyncTransaction>(), _executor);
        }

        private InternalTransaction BeginTransaction(AccessMode mode, Action<TransactionConfigBuilder> action)
        {
            return new InternalTransaction(
                _executor.RunSync(() => _session.BeginTransactionAsync(mode, action))
                    .CastOrThrow<IInternalAsyncTransaction>(), _executor);
        }

        #endregion

        #region Transaction Methods

        public T ReadTransaction<T>(Func<ITransaction, T> work)
        {
            return ReadTransaction(work, null);
        }

        public T ReadTransaction<T>(Func<ITransaction, T> work, Action<TransactionConfigBuilder> action)
        {
            return RunTransaction(AccessMode.Read, work, action);
        }

        public T WriteTransaction<T>(Func<ITransaction, T> work)
        {
            return WriteTransaction(work, null);
        }

        public T WriteTransaction<T>(Func<ITransaction, T> work, Action<TransactionConfigBuilder> action)
        {
            return RunTransaction(AccessMode.Write, work, action);
        }

        internal T RunTransaction<T>(AccessMode mode, Func<ITransaction, T> work, Action<TransactionConfigBuilder> action)
        {
            return _retryLogic.Retry(() =>
            {
                using (var txc = BeginTransaction(mode, action))
                {
                    try
                    {
                        var result = work(txc);
                        if (txc.IsOpen)
                        {
                            txc.Commit();
                        }

                        return result;
                    }
                    catch
                    {
                        if (txc.IsOpen)
                        {
                            txc.Rollback();
                        }

                        throw;
                    }
                }
            });
        }

        #endregion

        public IStatementResult Run(string statement, Action<TransactionConfigBuilder> action)
        {
            return Run(new Statement(statement), action);
        }

        public IStatementResult Run(string statement, IDictionary<string, object> parameters,
            Action<TransactionConfigBuilder> action)
        {
            return Run(new Statement(statement, parameters), action);
        }

        public IStatementResult Run(Statement statement, Action<TransactionConfigBuilder> action)
        {
            return new InternalStatementResult(_executor.RunSync(() => _session.RunAsync(statement, action)),
                _executor);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _executor.RunSync(() => _session.CloseAsync());
                }
            }

            _disposed = true;
        }
    }
}