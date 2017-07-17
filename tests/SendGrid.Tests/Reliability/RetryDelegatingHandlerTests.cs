﻿using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using SendGrid.Reliability;
using Xunit;

namespace SendGrid.Tests.Reliability
{
    public class RetryDelegatingHandlerTests
    {
        private ReliabilitySettings reliabilitySettings;

        private readonly HttpClient client;

        private readonly RetryTestBehaviourDelegatingHandler innerHandler;

        public RetryDelegatingHandlerTests()
        {
            reliabilitySettings = new ReliabilitySettings
            {
                RetryCount = 2,
                UseRetryPolicy = true
            };
            innerHandler = new RetryTestBehaviourDelegatingHandler();
            client = new HttpClient(new RetryDelegatingHandler(innerHandler, reliabilitySettings))
            {
                BaseAddress = new Uri("http://localhost")
            };
        }

        [Fact]
        public async Task Invoke_ShouldReturnNoErrorWhenOK()
        {
            innerHandler.ConfigureBehaviour(innerHandler.OK);

            await client.SendAsync(new HttpRequestMessage());

            Assert.Equal(1, innerHandler.InvocationCount);
        }

        [Fact]
        public async Task Invoke_ShouldReturnHttpResponseAndNotRetryWhenUnauthorised()
        {
            innerHandler.ConfigureBehaviour(innerHandler.AuthenticationError);

            var result = await client.SendAsync(new HttpRequestMessage());

            Assert.Equal(result.StatusCode, HttpStatusCode.Unauthorized);
            Assert.Equal(1, innerHandler.InvocationCount);
        }

        [Fact]
        public async Task Invoke_ShouldReturnErrorWithoutRetryWhenErrorIsNotTransient()
        {
            innerHandler.ConfigureBehaviour(innerHandler.NonTransientException);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () =>
                    {
                        return client.SendAsync(new HttpRequestMessage());
                    });

            Assert.Equal(1, innerHandler.InvocationCount);
        }

        [Fact]
        public async Task Invoke_ShouldRetryTheExpectedAmountOfTimesAndReturnTimeoutExceptionWhenTasksCancelled()
        {
            innerHandler.ConfigureBehaviour(innerHandler.TaskCancelled);

            await Assert.ThrowsAsync<TimeoutException>(
                () =>
                {
                    return client.SendAsync(new HttpRequestMessage());
                });

            Assert.Equal(3, innerHandler.InvocationCount);
        }
    }
}