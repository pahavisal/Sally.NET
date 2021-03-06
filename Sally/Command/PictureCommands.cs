﻿using Discord;
using Discord.Commands;
using Sally.NET.Core.Enum;
using Sally.NET.Service;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Sally.Command
{
    /// <summary>
    /// co0mmand group for all related to pictures
    /// </summary>
    public class PictureCommands : ModuleBase
    {
        [Command("konachan")]
        [Alias("k")]
        public async Task SendPicture()
        {
            //search for image without tags and rating
            string response = await ApiRequestService.Request2KonachanApiAsync();
            await generateImageEmbed(response);
        }

        [Command("konachan")]
        [Alias("k")]
        public async Task SendPicture(params String[] tags)
        {
            string[] lowerTags = tags.Select(s => s.ToLowerInvariant()).ToArray();
            string tagUrl = String.Empty;
            Rating rating;
            //generalize rating input, so misstyping isnt so bad
            string lowerRating = tags[0].ToLower();
            string ratingElement = lowerRating.First().ToString().ToUpper() + lowerRating.Substring(1);
            //try to parse as enum
            //check if first tag is equal to a rating
            if (Enum.TryParse<Rating>(ratingElement, out rating))
            {
                //get rest of the tags
                string[] tagCollection = lowerTags.Skip(1).ToArray();
                foreach (string tag in tagCollection)
                {
                    tagUrl = tagUrl + $"{tag} ";
                }
                string response = await ApiRequestService.Request2KonachanApiAsync(tagCollection, rating);
                await generateImageEmbed(response, tagUrl);
            }
            else
            {
                //first arg isn't an enum
                foreach (string tag in lowerTags)
                {
                    tagUrl = tagUrl + $"{tag} ";
                }
                string response = await ApiRequestService.Request2KonachanApiAsync(lowerTags);
                await generateImageEmbed(response, tagUrl);
            }
        }

        /// <summary>
        /// helper method for generating discord embeds
        /// </summary>
        /// <param name="response"></param>
        /// <returns></returns>
        private async Task generateImageEmbed(string response)
        {
            EmbedBuilder embedBuilder = new EmbedBuilder()
                .WithDescription($"[Result]({response})")
                .WithColor(new Color((uint)Convert.ToInt32(CommandHandlerService.MessageAuthor.EmbedColor, 16)))
                .WithImageUrl(response)
                .WithFooter(NET.DataAccess.File.FileAccess.GENERIC_FOOTER, NET.DataAccess.File.FileAccess.GENERIC_THUMBNAIL_URL);
            await Context.Message.Channel.SendMessageAsync(embed: embedBuilder.Build());
        }

        /// <summary>
        /// helper method for generating discord embeds
        /// </summary>
        /// <param name="response"></param>
        /// <param name="tagUrl"></param>
        /// <returns></returns>
        private async Task generateImageEmbed(string response, string tagUrl)
        {
            if (response.Length == 0)
            {
                await Context.Message.Channel.SendMessageAsync("nothing found!");
                return;
            }
            EmbedBuilder embedBuilder = new EmbedBuilder()
                .WithDescription($"Tags: [{tagUrl}]({response})")
                .WithColor(new Color((uint)Convert.ToInt32(CommandHandlerService.MessageAuthor.EmbedColor, 16)))
                .WithImageUrl(response)
                .WithFooter(NET.DataAccess.File.FileAccess.GENERIC_FOOTER, NET.DataAccess.File.FileAccess.GENERIC_THUMBNAIL_URL);
            await Context.Message.Channel.SendMessageAsync(embed: embedBuilder.Build());
        }
    }
}
