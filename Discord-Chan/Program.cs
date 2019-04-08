﻿using Discord;
using Discord.WebSocket;
using Discord_Chan.Command;
using Discord_Chan.Config;
using Discord_Chan.Db;
using Discord_Chan.Service;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Discord_Chan
{
    class Program
    {
        public static BotConfiguration BotConfiguration
        {
            get;
            private set;
        }

        public static DiscordSocketClient Client
        {
            get;
            private set;
        }

        public static SocketGuild MyGuild
        {
            get;
            private set;
        }
        public static SocketUser Me
        {
            get;
            private set;
        }
        private static bool isRestarted;
       

        public static void Main(string[] args)
        {
            if(args.Length > 0)
            {
                isRestarted = args[0] == "true";
            }
            Console.CancelKeyPress += Console_CancelKeyPress;
            new Program().MainAsync().GetAwaiter().GetResult();
        }

        private static void Console_CancelKeyPress(object sender, ConsoleCancelEventArgs e)
        {
            DataAccess.Instance.Dispose();
            Environment.Exit(0);
        }

        public async Task MainAsync()
        {

            BotConfiguration = JsonConvert.DeserializeObject<BotConfiguration>(File.ReadAllText("configuration.json"));
            DataAccess.Initialize(BotConfiguration);

            Client = new DiscordSocketClient();

            VoiceRewardService.InitializeHandler(Client);
            UserManagerService.InitializeHandler(Client);
            MoodDictionary.InitializeMoodDictionary();
            WeatherSubService.InitializeWeatherSub();
            await RoleManagerService.InitializeHandler();
            await CommandHandlerService.InitializeHandler(Client);
            Client.Ready += Client_Ready;

            Client.Log += Log;

            await Client.LoginAsync(TokenType.Bot, BotConfiguration.token);
            await Client.StartAsync();

            // Block this task until the program is closed.
            await Task.Delay(-1);
        }

        private Task Log(LogMessage msg)
        {
            Console.WriteLine(msg.ToString());
            return Task.CompletedTask;
        }

        private async Task Client_Ready()
        {
            MyGuild = Client.Guilds.Where(g => g.Id == BotConfiguration.guildId).First();
            foreach (SocketGuildUser user in MyGuild.Users)
            {
                if (DataAccess.Instance.users.Find(u => u.Id == user.Id) == null)
                {
                    DataAccess.Instance.InsertUser(new User(user.Id, 10, false));
                }
                if(user.Id == BotConfiguration.meId)
                {
                    Me = user as SocketUser;
                }
            }
            StatusNotifierService.InitializeService();
            MusicCommands.Initialize(Client);
            await MoodHandleService.InitializeHandler(Client);
            if (isRestarted)
            {
                await Me.SendMessageAsync("Bot successfully restarted");
            }
        }
    }
}
