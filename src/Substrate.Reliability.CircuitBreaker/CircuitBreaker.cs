using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using System.Timers;

namespace Substrate.Reliability
{
    internal enum CircuitBreakerState
    {
        Closed,
        Open,
        HalfOpen,
    }

    internal interface ICircuitBreaker : IDisposable
    {
        /// <summary>
        /// Number of failures allowed before the circuit trips.
        /// </summary>
        int Threshold { get; }

        /// <summary>
        /// Number of failures that have occurred.
        /// </summary>
        int FailureCount { get; }

        /// <summary>
        /// The current service level of the circuit.
        /// </summary>
        double ServiceLevel { get; }

        /// <summary>
        /// The time, in milliseconds, before the circuit attempts to close after being tripped.
        /// </summary>
        double Timeout { get; }

        /// <summary>
        /// List of operation exception types the circuit breaker ignores.
        /// </summary>
        IList<Type> IgnoredExceptionTypes { get; }

        /// <summary>
        /// Current state of the circuit breaker.
        /// </summary>
        CircuitBreakerState State { get; }

        /// <summary>
        /// Executes the operation.
        /// </summary>
        /// <param name="operation">Operation to execute</param>
        /// <typeparam name="TResult">Result of the operation</typeparam>
        /// <returns>Result of operation as an object</returns>
        /// <exception cref="OpenCircuitException">Thrown if the circuit breaker is in an open state.</exception>
        /// <exception cref="OperationFailedException">Thrown if the provided operation fails.</exception>
        TResult Execute<TResult>(Func<TResult> operation);

        /// <summary>
        /// Executes the operation.
        /// </summary>
        /// <param name="operation">Operation to execute</param>
        /// <typeparam name="TResult">Result of the operation</typeparam>
        /// <returns>Result of operation as an object</returns>
        /// <exception cref="OpenCircuitException">Thrown if the circuit breaker is in an open state.</exception>
        /// <exception cref="OperationFailedException">Thrown if the provided operation fails.</exception>
        Task<TResult> ExecuteAsync<TResult>(Func<Task<TResult>> operation);

        /// <summary>
        /// Trips the circuit breaker if not already open.
        /// </summary>
        void Trip();

        /// <summary>
        /// Resets the circuit breaker.
        /// </summary>
        void Reset();
    }

    /// <summary>
    /// Implementation of the Circuit Breaker pattern.
    /// </summary>
    internal class CircuitBreaker : ICircuitBreaker
    {
        private readonly int _threshold;
        private readonly Timer _timer;
        private readonly IList<Type> _ignoredExceptionTypes;
        private int _failureCount;
        private CircuitBreakerState _state;

        public CircuitBreaker()
            : this(5, 60000)
        {
        }

        public CircuitBreaker(int threshold, int timeout)
        {
            _threshold = threshold;
            _failureCount = 0;
            _state = CircuitBreakerState.Closed;
            _ignoredExceptionTypes = new List<Type>();

            _timer = new Timer(timeout);
            _timer.Elapsed += TimerElapsed;
        }

        /// <inheritdoc />
        public int Threshold => _threshold;

        /// <inheritdoc />
        public int FailureCount => _failureCount;

        /// <inheritdoc />
        public double Timeout => _timer.Interval;

        /// <inheritdoc />
        public IList<Type> IgnoredExceptionTypes => _ignoredExceptionTypes;

        /// <inheritdoc />
        public double ServiceLevel => ((_threshold - (double)_failureCount) / _threshold) * 100;

        /// <inheritdoc />
        public CircuitBreakerState State => _state;

        /// <inheritdoc />
        public TResult Execute<TResult>(Func<TResult> operation)
        {
            if (operation == null)
                throw new ArgumentNullException(nameof(operation));

            return ExecuteAsync(() => Task.FromResult(operation())).GetAwaiter().GetResult();
        }

        /// <inheritdoc />
        public async Task<TResult> ExecuteAsync<TResult>(Func<Task<TResult>> operation)
        {
            if (operation == null)
                throw new ArgumentNullException(nameof(operation));

            if (_state == CircuitBreakerState.Open)
                throw new OpenCircuitException("Circuit breaker is currently open");

            TResult result;
            try
            {
                // Execute operation
                result = await operation().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // If exception is one of the ignored types, then throw original exception
                if (_ignoredExceptionTypes.Contains(ex.GetType()))
                    throw;

                if (_state == CircuitBreakerState.HalfOpen)
                {
                    // Operation failed in a half-open state, so reopen circuit
                    Trip();
                }
                else if (_failureCount < _threshold)
                {
                    // Operation failed in an open state, so increment failure count and throw exception
                    _failureCount++;
                }
                else if (_failureCount >= _threshold)
                {
                    // Failure count has reached threshold, so trip circuit breaker
                    Trip();
                }

                throw new OperationFailedException("Operation failed", ex);
            }

            if (_state == CircuitBreakerState.HalfOpen)
            {
                // If operation succeeded without error and circuit breaker
                // is in a half-open state, then reset
                Reset();
            }

            if (_failureCount <= 0)
                return result;

            // Decrement failure count to improve service level
            _failureCount--;

            return result;
        }

        /// <inheritdoc />
        public void Trip()
        {
            if (_state == CircuitBreakerState.Open)
                return;

            ChangeState(CircuitBreakerState.Open);

            _timer.Start();
        }

        /// <inheritdoc />
        public void Reset()
        {
            ChangeState(CircuitBreakerState.Closed);
            _failureCount = 0;

            _timer.Stop();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
                _timer?.Dispose();
        }

        private void ChangeState(CircuitBreakerState newState)
        {
            // Change the circuit breaker state
            _state = newState;
        }

        private void TimerElapsed(object sender, ElapsedEventArgs e)
        {
            if (State != CircuitBreakerState.Open)
                return;

            // Attempt to close circuit by switching to a half-open state
            ChangeState(CircuitBreakerState.HalfOpen);

            _timer.Stop();
        }
    }

    /// <summary>
    /// Exception thrown when an attempted operation has failed.
    /// </summary>
    [Serializable]
    internal class OperationFailedException : Exception
    {
        public OperationFailedException()
        {
        }

        public OperationFailedException(string message)
            : base(message)
        {
        }

        public OperationFailedException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        protected OperationFailedException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }

    /// <summary>
    /// Exception thrown when an operation is being called on an open circuit.
    /// </summary>
    [Serializable]
    internal class OpenCircuitException : Exception
    {
        public OpenCircuitException()
        {
        }

        public OpenCircuitException(string message)
            : base(message)
        {
        }

        public OpenCircuitException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        protected OpenCircuitException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }
}
