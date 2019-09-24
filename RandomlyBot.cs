// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Teams;
using Microsoft.Bot.Connector;
using Microsoft.Bot.Connector.Teams;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Randomly.Cards;

namespace Randomly
{
    /// <summary>
    /// Represents a bot that processes incoming activities.
    /// For each user interaction, an instance of this class is created and the OnTurnAsync method is called.
    /// This is a Transient lifetime service.  Transient lifetime services are created
    /// each time they're requested. For each Activity received, a new instance of this
    /// class is created. Objects that are expensive to construct, or have a lifetime
    /// beyond the single turn, should be carefully managed.
    /// For example, the <see cref="MemoryStorage"/> object and associated
    /// <see cref="IStatePropertyAccessor{T}"/> object are created with a singleton lifetime.
    /// </summary>
    /// <seealso cref="https://docs.microsoft.com/en-us/aspnet/core/fundamentals/dependency-injection?view=aspnetcore-2.1"/>
    public class RandomlyBot : IBot
    {
        private readonly RandomlyAccessors _accessors;
        private readonly ILogger _logger;
        private readonly IHostingEnvironment _env;


        /// <summary>
        /// Initializes a new instance of the class.
        /// </summary>
        /// <param name="conversationState">The managed conversation state.</param>
        /// <param name="loggerFactory">A <see cref="ILoggerFactory"/> that is hooked to the Azure App Service provider.</param>
        /// <param name="env">Hosting environment</param>
        /// <seealso cref="https://docs.microsoft.com/en-us/aspnet/core/fundamentals/logging/?view=aspnetcore-2.1#windows-eventlog-provider"/>
        public RandomlyBot(ConversationState conversationState, ILoggerFactory loggerFactory, IHostingEnvironment env)
        {
            if (conversationState == null)
            {
                throw new System.ArgumentNullException(nameof(conversationState));
            }

            if (loggerFactory == null)
            {
                throw new System.ArgumentNullException(nameof(loggerFactory));
            }

            _accessors = new RandomlyAccessors(conversationState)
            {
                CounterState = conversationState.CreateProperty<CounterState>(RandomlyAccessors.CounterStateName),
            };

            _logger = loggerFactory.CreateLogger<RandomlyBot>();
            _logger.LogTrace("Turn start.");

            _env = env ?? throw new ArgumentNullException(nameof(env));
        }

        /// <summary>
        /// Every conversation turn for our Echo Bot will call this method.
        /// There are no dialogs used, since it's "single turn" processing, meaning a single
        /// request and response.
        /// </summary>
        /// <param name="turnContext">A <see cref="ITurnContext"/> containing all the data needed
        /// for processing this conversation turn. </param>
        /// <param name="cancellationToken">(Optional) A <see cref="CancellationToken"/> that can be used by other objects
        /// or threads to receive notice of cancellation.</param>
        /// <returns>A <see cref="Task"/> that represents the work queued to execute.</returns>
        /// <seealso cref="BotStateSet"/>
        /// <seealso cref="ConversationState"/>
        /// <seealso cref="IMiddleware"/>
        public async Task OnTurnAsync(ITurnContext turnContext, CancellationToken cancellationToken = default(CancellationToken))
        {
            // Handle Message activity type, which is the main activity type for shown within a conversational interface
            // Message activities may contain text, speech, interactive cards, and binary or unknown attachments.
            // see https://aka.ms/about-bot-activity-message to learn more about the message and other activity types
            if (turnContext.Activity.Type == ActivityTypes.Message)
            {
                // Get Teams Context first
                var teamsContext = turnContext.TurnState.Get<ITeamsContext>();

                string conversationToFetchMembersFor = null;

                switch(turnContext.Activity.Conversation.ConversationType)
                {
                    case "groupChat":
                        conversationToFetchMembersFor = turnContext.Activity.Conversation.Id;
                        break;
                    case "team":
                        conversationToFetchMembersFor = teamsContext.Team.Id;
                        break;
                    default:
                        break;
                }

                if (conversationToFetchMembersFor != null)
                {
                    List<ChannelAccount> teamMembers = (await turnContext.TurnState.Get<IConnectorClient>().Conversations.GetConversationMembersAsync(
                   conversationToFetchMembersFor)).ToList();

                    var selectedUser = teamMembers[new Random(Guid.NewGuid().GetHashCode()).Next(teamMembers.Count())];

                    var cardToSend = GetWinnerAnnouncementCard(_env, new[] { selectedUser });

                    var replyCardActivity = new Activity()
                    {
                        Type = ActivityTypes.Message,
                        Conversation = new ConversationAccount()
                        {
                            Id = turnContext.Activity.Conversation.Id
                        },
                        Attachments = new List<Attachment>()
                        {
                            new Attachment()
                            {
                                ContentType = "application/vnd.microsoft.card.adaptive",
                                Content = JsonConvert.DeserializeObject(cardToSend),
                            }
                        }
                    };

                    // Post the card first
                    await turnContext.SendActivityAsync(replyCardActivity);
                }
                else
                {
                    await turnContext.SendActivityAsync($"Sorry my little 🤖 🧠 doesn't handle that yet. I can help you choose a person at random from a group-chat or team to carry some task out. Just add me to a team or group chat and summon me by @ mentioning!");
                }
            }
        }

        private static string GetWinnerAnnouncementCard(IHostingEnvironment env, ChannelAccount[] winners)
        {
            var winnerImages = new string[]
            {
                "https://media.giphy.com/media/44gu1V41ejJni/giphy.gif",
                "https://media.giphy.com/media/xUOwGmG2pRfFZUmdVe/giphy.gif",
                "https://media.giphy.com/media/3o7bu57lYhUEFiYDSM/giphy.gif",
                "https://media.giphy.com/media/xTiTnz33weTH3K8Uvu/giphy.gif",
                "https://media.giphy.com/media/ZcUGu59vhBGgbBhh0n/giphy.gif"
            };

            var selectedImage = winnerImages[new Random(Guid.NewGuid().GetHashCode()).Next(winnerImages.Length)];

            var model = new CardReader.AnnouncementCardModel()
            {
                ImageUrl = selectedImage,
                Winners = new List<CardReader.AnnouncementCardWinner>()
            };

            foreach (var w in winners)
            {
                model.Winners.Add(new CardReader.AnnouncementCardWinner()
                {
                    Name = w.Name,
                    Id = w.Id,
                });
            }

            var adaptiveCardJson = CardReader.GetAnnouncementCard(env, model);
            
            return adaptiveCardJson;
        }
        
    }
}
