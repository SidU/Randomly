namespace Randomly.Cards
{
    using Fluid;
    using Microsoft.AspNetCore.Hosting;
    using System;
    using System.Collections.Generic;
    using System.IO;

    public static class CardReader
    {
        public static string GetAnnouncementCard(IHostingEnvironment env, AnnouncementCardModel model)
        {
            if (env == null)
            {
                throw new ArgumentNullException(nameof(env));
            }
            
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            var cardsDirectory = Path.Combine(env.ContentRootPath, "Cards");
            var cardJsonFilePath = Path.Combine(cardsDirectory, $"AnnouncementCard.fluid");
            var cardBody = File.ReadAllText(cardJsonFilePath);

            if (FluidTemplate.TryParse(cardBody, out var template))
            {
                var context = new TemplateContext();

                context.MemberAccessStrategy.Register<AnnouncementCardModel>();

                context.SetValue("model", model);

                context.MemberAccessStrategy.Register<AnnouncementCardWinner>();

                cardBody = template.Render(context);
            }


            return cardBody;
        }

        public class AnnouncementCardModel
        {
            public List<AnnouncementCardWinner> Winners { get; set; }

            public string ImageUrl { get; set; }
        }

        public class AnnouncementCardWinner
        {
            public string Id { get; set; }
            public string Name { get; set; }
        }
    }
}
