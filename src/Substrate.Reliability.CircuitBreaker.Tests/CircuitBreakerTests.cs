using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Substrate.Reliability.Tests
{
    public class CircuitBreakerTests
    {
        [Fact]
        public void Can_Create_CircuitBreaker()
        {
            var cb = new CircuitBreaker();

            Assert.Equal(5, cb.Threshold);
            Assert.Equal(60000, cb.Timeout);
            Assert.Equal(100, cb.ServiceLevel);
            Assert.NotNull(cb.IgnoredExceptionTypes);
            Assert.Equal(0, cb.IgnoredExceptionTypes.Count);
        }

        [Fact]
        public void Can_Execute_Operation()
        {
            var cb = new CircuitBreaker();
            var result = cb.Execute(() => ValidOperation(1, 2));
            Assert.Equal(3, result);
        }

        [Fact]
        public async Task Can_ExecuteAsync_Operation()
        {
            var cb = new CircuitBreaker();
            var result = await cb
                .ExecuteAsync(() => Task.FromResult(ValidOperation(1, 2)))
                .ConfigureAwait(false);
            Assert.Equal(3, result);
        }

        [Fact]
        public void Can_Get_Failure_Count()
        {
            var cb = new CircuitBreaker();
            Assert.Throws<OperationFailedException>(() => cb.Execute(FailedOperation));
            Assert.Equal(80, cb.ServiceLevel);
            Assert.Equal(1, cb.FailureCount);

            Assert.Throws<OperationFailedException>(() => cb.Execute(FailedOperation));
            Assert.Equal(60, cb.ServiceLevel);
            Assert.Equal(2, cb.FailureCount);

            cb.Execute(() => ValidOperation(1, 2));
            Assert.Equal(80, cb.ServiceLevel);
            Assert.Equal(1, cb.FailureCount);
        }

        [Fact]
        public void Can_Get_Original_Exception()
        {
            var cb = new CircuitBreaker();
            var ex = Assert.Throws<OperationFailedException>(() => cb.Execute(FailedOperation));
            Assert.IsType<TimeoutException>(ex.InnerException);
        }

        [Fact]
        public void Can_Trip_Breaker()
        {
            var cb = new CircuitBreaker();
            Exception ex = null;
            for (int i = 0; i < cb.Threshold + 5; i++)
            {
                try
                {
                    cb.Execute(FailedOperation);
                }
                catch (OperationFailedException e)
                {
                    Assert.NotNull(e);
                    Assert.NotNull(e.InnerException);
                    ex = e;
                }
                catch (OpenCircuitException e)
                {
                    Assert.NotNull(e);
                    ex = e;
                }
            }

            if (ex == null)
            {
                Assert.True(false);
            }
            else
            {
                Assert.IsType<OpenCircuitException>(ex);
                Assert.Equal(0, cb.ServiceLevel);
            }
        }

        [Fact]
        public void Can_Reset_Breaker()
        {
            var cb = new CircuitBreaker();
            var ex = Assert.Throws<OperationFailedException>(() => cb.Execute(FailedOperation));

            Assert.NotNull(ex);
            Assert.NotNull(ex.InnerException);

            cb.Reset();
            Assert.Equal(CircuitBreakerState.Closed, cb.State);
            Assert.Equal(100, cb.ServiceLevel);
            Assert.Equal(0, cb.FailureCount);
        }

        [Fact]
        public void Can_Force_Trip_Breaker()
        {
            var cb = new CircuitBreaker();
            Assert.Equal(CircuitBreakerState.Closed, cb.State);

            cb.Trip();

            Assert.Equal(CircuitBreakerState.Open, cb.State);

            // Calling execute when circuit is tripped should throw an OpenCircuitException
            var ex = Assert.Throws<OpenCircuitException>(() => cb.Execute(() => ValidOperation(1, 2)));

            Assert.NotNull(ex);
            Assert.IsType<OpenCircuitException>(ex);
        }

        [Fact]
        public void Can_Force_Reset_Breaker()
        {
            var cb = new CircuitBreaker();
            Assert.Equal(CircuitBreakerState.Closed, cb.State);

            cb.Trip();

            Assert.Equal(CircuitBreakerState.Open, cb.State);

            cb.Reset();

            Assert.Equal(CircuitBreakerState.Closed, cb.State);
            Assert.Equal(100, cb.ServiceLevel);

            object result = cb.Execute(() => ValidOperation(1, 2));

            Assert.NotNull(result);
            Assert.Equal(3, (int)result);
        }

        [Fact]
        public void Can_Close_Breaker_After_Timeout()
        {
            var cb = new CircuitBreaker(5, 500);

            cb.Trip();

            Assert.Equal(CircuitBreakerState.Open, cb.State);

            Thread.Sleep(1000);

            Assert.Equal(CircuitBreakerState.HalfOpen, cb.State);

            // Attempt failed operation
            Assert.Throws<OperationFailedException>(() => cb.Execute(FailedOperation));

            Assert.Equal(CircuitBreakerState.Open, cb.State);

            Thread.Sleep(1000);

            Assert.Equal(CircuitBreakerState.HalfOpen, cb.State);

            // Attempt successful operation
            cb.Execute(() => ValidOperation(1, 2));

            Assert.Equal(CircuitBreakerState.Closed, cb.State);
        }

        [Fact]
        public void Can_Ignore_Exception_Types()
        {
            var cb = new CircuitBreaker();
            cb.IgnoredExceptionTypes.Add(typeof(TimeoutException));
            Assert.Throws<TimeoutException>(() => cb.Execute(FailedOperation));
            Assert.Equal(100, cb.ServiceLevel);
        }

        [Fact]
        public void Is_Thread_Safe()
        {
            var cb = new CircuitBreaker();
            var status = new ThreadTestStatus { Failed = false };
            int threads = 10;

            List<Thread> workerThreads = Enumerable.Range(0, threads)
                .Select(i => CreateWorker(cb, status))
                .ToList();

            foreach (var thread in workerThreads)
                thread.Start();

            Enumerable.Range(0, 10)
                .ToList()
                .ForEach(i =>
                {
                    Assert.False(status.Failed);
                    Thread.Sleep(TimeSpan.FromSeconds(1));
                });

            cb.Trip();

            Thread.Sleep(TimeSpan.FromSeconds(1));

            Assert.True(status.Failed);
        }

        private static int ValidOperation(int a, int b)
        {
            return a + b;
        }

        private static int FailedOperation()
        {
            throw new TimeoutException("Network not available");
        }

        private static Thread CreateWorker(CircuitBreaker cb, ThreadTestStatus status)
        {
            return new Thread(() =>
            {
                while (true)
                {
                    try
                    {
                        cb.Execute(() => ValidOperation(1, 2));
                    }
#pragma warning disable CA1031 // Do not catch general exception types
                    catch (Exception)
#pragma warning restore CA1031 // Do not catch general exception types
                    {
                        status.Failed = true;
                    }

                    Thread.Sleep(TimeSpan.FromSeconds(1));
                }
            });
        }

        private class ThreadTestStatus
        {
            public bool Failed { get; set; }
        }
    }
}
