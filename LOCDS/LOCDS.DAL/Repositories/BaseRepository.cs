using System;
using System.Data;
using System.Threading.Tasks;
using Dapper;
using LOCDS.DAL.Connection;
using Polly;
using Polly.Retry;

namespace LOCDS.DAL.Repositories
{
    public abstract class BaseRepository
    {
        private readonly IDbConnectionFactory _connectionFactory;
        private readonly IDbConnection _connection;
        private readonly IDbTransaction _transaction;
        private readonly AsyncRetryPolicy _retryPolicy;

        protected BaseRepository(IDbConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
            _retryPolicy = Policy
                .Handle<Exception>()
                .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromMilliseconds(150 * retryAttempt));
        }

        protected BaseRepository(IDbConnection connection, IDbTransaction transaction)
        {
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));
            _transaction = transaction ?? throw new ArgumentNullException(nameof(transaction));
            _retryPolicy = Policy
                .Handle<Exception>()
                .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromMilliseconds(150 * retryAttempt));
        }

        protected CommandDefinition CreateCommandDefinition(
            string sql,
            object parameters = null,
            CancellationToken cancellationToken = default)
        {
            return new CommandDefinition(sql, parameters, _transaction, cancellationToken: cancellationToken);
        }

        protected async Task<TResult> ExecuteWithRetryAsync<TResult>(Func<IDbConnection, Task<TResult>> operation)
        {
            if (operation == null)
            {
                throw new ArgumentNullException(nameof(operation));
            }

            if (_connection != null)
            {
                if (_connection.State != ConnectionState.Open)
                {
                    _connection.Open();
                }

                return await operation(_connection).ConfigureAwait(false);
            }

            return await _retryPolicy.ExecuteAsync(async () =>
            {
                using var connection = _connectionFactory.CreateConnection();
                if (connection.State != ConnectionState.Open)
                {
                    connection.Open();
                }

                return await operation(connection).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        protected async Task ExecuteWithRetryAsync(Func<IDbConnection, Task> operation)
        {
            if (operation == null)
            {
                throw new ArgumentNullException(nameof(operation));
            }

            if (_connection != null)
            {
                if (_connection.State != ConnectionState.Open)
                {
                    _connection.Open();
                }

                await operation(_connection).ConfigureAwait(false);
                return;
            }

            await _retryPolicy.ExecuteAsync(async () =>
            {
                using var connection = _connectionFactory.CreateConnection();
                if (connection.State != ConnectionState.Open)
                {
                    connection.Open();
                }

                await operation(connection).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }
    }
}
