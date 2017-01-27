﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.ServiceBus
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.ServiceBus.Filters;

    public abstract class SubscriptionClient : ClientEntity, ISubscriptionClient
    {
        public const string DefaultRule = "$Default";
        MessageReceiver innerReceiver;

        protected SubscriptionClient(ServiceBusConnection serviceBusConnection, string topicPath, string name, ReceiveMode receiveMode)
            : base($"{nameof(SubscriptionClient)}{ClientEntity.GetNextId()}({name})")
        {
            this.ServiceBusConnection = serviceBusConnection;
            this.TopicPath = topicPath;
            this.Name = name;
            this.SubscriptionPath = EntityNameHelper.FormatSubscriptionPath(this.TopicPath, this.Name);
            this.ReceiveMode = receiveMode;
        }

        public string TopicPath { get; private set; }

        public string Path => EntityNameHelper.FormatSubscriptionPath(this.TopicPath, this.Name);

        public string Name { get; }

        public ReceiveMode ReceiveMode { get; private set; }

        public long LastPeekedSequenceNumber => this.InnerReceiver.LastPeekedSequenceNumber;

        public int PrefetchCount
        {
            get
            {
                return this.InnerReceiver.PrefetchCount;
            }

            set
            {
                this.InnerReceiver.PrefetchCount = value;
            }
        }

        internal string SubscriptionPath { get; private set; }

        internal MessageReceiver InnerReceiver
        {
            get
            {
                if (this.innerReceiver == null)
                {
                    lock (this.ThisLock)
                    {
                        if (this.innerReceiver == null)
                        {
                            this.innerReceiver = this.CreateMessageReceiver();
                        }
                    }
                }

                return this.innerReceiver;
            }
        }

        protected object ThisLock { get; } = new object();

        protected ServiceBusConnection ServiceBusConnection { get; }

        public sealed override async Task CloseAsync()
        {
            await this.OnCloseAsync().ConfigureAwait(false);
        }

        public async Task<BrokeredMessage> ReceiveAsync()
        {
            IList<BrokeredMessage> messages = await this.ReceiveAsync(1).ConfigureAwait(false);
            if (messages != null && messages.Count > 0)
            {
                return messages[0];
            }

            return null;
        }

        public Task<IList<BrokeredMessage>> ReceiveAsync(int maxMessageCount)
        {
            return this.InnerReceiver.ReceiveAsync(maxMessageCount);
        }

        public async Task<BrokeredMessage> ReceiveBySequenceNumberAsync(long sequenceNumber)
        {
            IList<BrokeredMessage> messages = await this.ReceiveBySequenceNumberAsync(new long[] { sequenceNumber });
            if (messages != null && messages.Count > 0)
            {
                return messages[0];
            }

            return null;
        }

        public Task<IList<BrokeredMessage>> ReceiveBySequenceNumberAsync(IEnumerable<long> sequenceNumbers)
        {
            return this.InnerReceiver.ReceiveBySequenceNumberAsync(sequenceNumbers);
        }

        /// <summary>
        /// Asynchronously reads the next message without changing the state of the receiver or the message source.
        /// </summary>
        /// <returns>The asynchronous operation that returns the <see cref="Microsoft.Azure.ServiceBus.BrokeredMessage" /> that represents the next message to be read.</returns>
        public Task<BrokeredMessage> PeekAsync()
        {
            return this.innerReceiver.PeekAsync();
        }

        /// <summary>
        /// Asynchronously reads the next batch of message without changing the state of the receiver or the message source.
        /// </summary>
        /// <param name="maxMessageCount">The number of messages.</param>
        /// <returns>The asynchronous operation that returns a list of <see cref="Microsoft.Azure.ServiceBus.BrokeredMessage" /> to be read.</returns>
        public Task<IList<BrokeredMessage>> PeekAsync(int maxMessageCount)
        {
            return this.innerReceiver.PeekAsync(maxMessageCount);
        }

        /// <summary>
        /// Asynchronously reads the next message without changing the state of the receiver or the message source.
        /// </summary>
        /// <param name="fromSequenceNumber">The sequence number from where to read the message.</param>
        /// <returns>The asynchronous operation that returns the <see cref="Microsoft.Azure.ServiceBus.BrokeredMessage" /> that represents the next message to be read.</returns>
        public Task<BrokeredMessage> PeekBySequenceNumberAsync(long fromSequenceNumber)
        {
            return this.innerReceiver.PeekBySequenceNumberAsync(fromSequenceNumber);
        }

        /// <summary>Peeks a batch of messages.</summary>
        /// <param name="fromSequenceNumber">The starting point from which to browse a batch of messages.</param>
        /// <param name="messageCount">The number of messages.</param>
        /// <returns>A batch of messages peeked.</returns>
        public Task<IList<BrokeredMessage>> PeekBySequenceNumberAsync(long fromSequenceNumber, int messageCount)
        {
            return this.innerReceiver.PeekBySequenceNumberAsync(fromSequenceNumber, messageCount);
        }

        public Task CompleteAsync(Guid lockToken)
        {
            return this.CompleteAsync(new Guid[] { lockToken });
        }

        public Task CompleteAsync(IEnumerable<Guid> lockTokens)
        {
            return this.InnerReceiver.CompleteAsync(lockTokens);
        }

        public Task AbandonAsync(Guid lockToken)
        {
            return this.InnerReceiver.AbandonAsync(lockToken);
        }

        public Task<MessageSession> AcceptMessageSessionAsync()
        {
            return this.AcceptMessageSessionAsync(null);
        }

        public async Task<MessageSession> AcceptMessageSessionAsync(string sessionId)
        {
            MessageSession session = null;

            MessagingEventSource.Log.AcceptMessageSessionStart(this.ClientId, sessionId);

            try
            {
                session = await this.OnAcceptMessageSessionAsync(sessionId).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                MessagingEventSource.Log.AcceptMessageSessionException(this.ClientId, exception);
                throw;
            }

            MessagingEventSource.Log.AcceptMessageSessionStop(this.ClientId);
            return session;
        }

        public Task DeferAsync(Guid lockToken)
        {
            return this.InnerReceiver.DeferAsync(lockToken);
        }

        public Task DeadLetterAsync(Guid lockToken)
        {
            return this.InnerReceiver.DeadLetterAsync(lockToken);
        }

        public Task<DateTime> RenewLockAsync(Guid lockToken)
        {
            return this.InnerReceiver.RenewLockAsync(lockToken);
        }

        /// <summary>
        /// Asynchronously adds a rule to the current subscription with the specified name and filter expression.
        /// </summary>
        /// <param name="ruleName">The name of the rule to add.</param>
        /// <param name="filter">The filter expression against which messages will be matched.</param>
        /// <returns>A task instance that represents the asynchronous add rule operation.</returns>
        public Task AddRuleAsync(string ruleName, Filter filter)
        {
            return this.AddRuleAsync(new RuleDescription(name: ruleName, filter: filter));
        }

        /// <summary>
        /// Asynchronously adds a new rule to the subscription using the specified rule description.
        /// </summary>
        /// <param name="description">The rule description that provides metadata of the rule to add.</param>
        /// <returns>A task instance that represents the asynchronous add rule operation.</returns>
        public Task AddRuleAsync(RuleDescription description)
        {
            if (description == null)
            {
                throw Fx.Exception.ArgumentNull(nameof(description));
            }

            description.ValidateDescriptionName();

            return this.OnAddRuleAsync(description);
        }

        /// <summary>
        /// Asynchronously removes the rule described by <paramref name="ruleName" />.
        /// </summary>
        /// <param name="ruleName">The name of the rule.</param>
        /// <returns>A task instance that represents the asynchronous remove rule operation.</returns>
        public Task RemoveRuleAsync(string ruleName)
        {
            if (string.IsNullOrWhiteSpace(ruleName))
            {
                throw Fx.Exception.ArgumentNullOrWhiteSpace(nameof(ruleName));
            }

            return this.OnRemoveRuleAsync(ruleName);
        }

        protected MessageReceiver CreateMessageReceiver()
        {
            return this.OnCreateMessageReceiver();
        }

        protected abstract MessageReceiver OnCreateMessageReceiver();

        protected abstract Task<MessageSession> OnAcceptMessageSessionAsync(string sessionId);

        protected abstract Task OnCloseAsync();

        protected abstract Task OnAddRuleAsync(RuleDescription description);

        protected abstract Task OnRemoveRuleAsync(string ruleName);
    }
}