﻿using Discord;
using Discord.Addons.Interactive;
using Discord.Audio;
using Discord.Commands;
using Discord.Rest;
using Discord.WebSocket;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using YoutubeExplode;
using YoutubeExplode.Models;
using YoutubeExplode.Models.MediaStreams;


namespace Discord_Chan.commands
{
    struct VideoInfo
    {
        public string Id { get; set; }
        public string Path { get; set; }
        public TimeSpan Duration { get; set; }
        public string LowResUrl { get; set; }
        public string Url { get; set; }
        public string SongTitle { get; set; }
        public string Author { get; set; }
        public long Views { get; set; }

        public VideoInfo(Video video, string path)
        {
            this.Id = video.Id;
            this.Path = path;
            this.Duration = video.Duration;
            this.LowResUrl = video.Thumbnails.LowResUrl;
            this.Url = video.GetUrl();
            this.SongTitle = video.Title;
            this.Author = video.Author;
            this.Views = video.Statistics.ViewCount;
        }
        public string Interpret
        {
            get
            {
                if (!SongTitle.Contains("-"))
                {
                    return Author;
                }
                return SongTitle.Substring(0, SongTitle.IndexOf("-")).Trim();
            }
        }
        public string Title
        {
            get
            {
                if (!SongTitle.Contains("-"))
                {
                    return SongTitle;
                }
                return SongTitle.Substring(SongTitle.IndexOf("-") + 1).Trim();
            }
        }
    }
    public static class TimeSpanExtension
    {
        public static string FormatTime(this TimeSpan timeSpan)
        {
            return ((timeSpan.Hours > 0) ? timeSpan.ToString("h\\:mm\\:ss") : timeSpan.ToString("mm\\:ss"));
        }
    }


    public class MusicCommands : InteractiveBase
    {
        private static Stopwatch stopwatch = new Stopwatch();
        public static void Initialize(DiscordSocketClient client)
        {
            Task.Run(() =>  playAudio() );
            Task.Run(() => messageWatcher() );
            if (File.Exists("songQueue.json"))
            {
                audioQueue = JsonConvert.DeserializeObject<List<VideoInfo>>(File.ReadAllText("songQueue.json"));
            }


            MusicCommands.client = client;
        }

        public static IAudioClient audioClient;
        private static List<VideoInfo> audioQueue = new List<VideoInfo>();
        private static bool pause
        {
            get => _internalPause;
            set
            {
                Task.Run(() => taskCompletionSource.TrySetResult(value));
                _internalPause = value;
                if (value)
                {
                    stopwatch.Stop();
                }
                else if (!stopwatch.IsRunning)
                {
                    stopwatch.Start();
                }
            }
        }
        private static bool _internalPause;
        private static bool skip
        {
            get
            {
                bool ret = _internalSkip;
                _internalSkip = false;
                return ret;
            }
            set => _internalSkip = value;
        }
        private static bool _internalSkip = false;
        private static bool repeat = false;
        private static TaskCompletionSource<bool> taskCompletionSource = new TaskCompletionSource<bool>();
        private static CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        public static ulong Id { get; private set; }

        public MusicCommands()
        {

        }
        public static DiscordSocketClient client;
        private static VideoInfo currentVideoInfo;
        private static int currentVideoIndex = -1;

        [Command("join", RunMode = RunMode.Async)]
        public async Task Join(IVoiceChannel voiceChannel = null)
        {
            voiceChannel = voiceChannel ?? (Context.Message.Author as IGuildUser)?.VoiceChannel;
            if (voiceChannel == null) throw new Exception("Youre not in a voice channel");
            SocketVoiceChannel lastVoiceChannel = Context.Client.Guilds.Select(g => g.VoiceChannels.Where(c => c.Users.Count(u => u.Id == Context.Client.CurrentUser.Id) > 0).FirstOrDefault()).Where(c => c != null).FirstOrDefault();
            if (lastVoiceChannel != null)
            {
                audioClient = await lastVoiceChannel.ConnectAsync();
                await audioClient.StopAsync();
            }
            audioClient = await voiceChannel.ConnectAsync();
            audioClient.SpeakingUpdated += AudioClient_SpeakingUpdated;

            //alle nachrichten löschen
            ITextChannel textChannel = (Context.Message.Channel as SocketGuildChannel).Guild.GetChannel(Program.botConfiguration.radioControlChannel) as SocketTextChannel;
            List<IMessage> userMessages = (await textChannel.GetMessagesAsync().Flatten()).ToList();
            foreach (IMessage message in userMessages)
            {
                await message.DeleteAsync();
            }


            IUserMessage oneMessage = await textChannel.SendMessageAsync("♪♫♪ Media Player ♫♪♫", embed: audioInfoEmbed());
            Id = oneMessage.Id;
            await Context.Message.DeleteAsync();
        }

        private async Task AudioClient_SpeakingUpdated(ulong arg1, bool arg2)
        {
            Console.WriteLine($"Speaking {arg2}");
        }

        private static int lastPause;
        private static int lastPrevious;
        private static int lastSkip;
        private static int lastRepeat;
        private static string lastStatus = "laden";

        public static async void messageWatcher()
        {
            while (true)
            {
                await Task.Delay(1000);
                if (Id == 0) continue;
                SocketTextChannel textChannel = client.Guilds.First(g => g.GetChannel(Program.botConfiguration.radioControlChannel) != null).GetChannel(Program.botConfiguration.radioControlChannel) as SocketTextChannel;
                RestUserMessage oneMessage = await textChannel.GetMessageAsync(Id) as RestUserMessage;
                if (oneMessage == null) continue;
                int currentPause = (await oneMessage.GetReactionUsersAsync("\u23EF")).Count;
                int currentPrevious = (await oneMessage.GetReactionUsersAsync("\u23EE")).Count;
                int currentSkip = (await oneMessage.GetReactionUsersAsync("\u23ED")).Count;
                int currentRepeat = (await oneMessage.GetReactionUsersAsync("\U0001F502")).Count;

                if (currentPause + currentPrevious + currentRepeat + currentSkip == 0)
                {
                    await oneMessage.AddReactionAsync(new Emoji("\u23EF"));
                    await Task.Delay(1000);
                    await oneMessage.AddReactionAsync(new Emoji("\u23EE"));
                    await Task.Delay(1000);
                    await oneMessage.AddReactionAsync(new Emoji("\u23ED"));
                    await Task.Delay(1000);
                    await oneMessage.AddReactionAsync(new Emoji("\U0001F502"));
                    await Task.Delay(1000);
                    lastPause = 1;
                    lastPrevious = 1;
                    lastRepeat = 1;
                    lastSkip = 1;
                    lock (lastStatus)
                    {
                        lastStatus = "Ready, use !add with url";
                    }
                    continue;
                }
                if (lastPause != currentPause)
                {
                    if (currentVideoInfo.Path == null)
                    {
                        pause = false;
                    }
                    else
                    {
                        pause = !pause;
                    }

                    lock (lastStatus)
                    {
                        lastStatus = pause ? "Pause" : "Play";
                    }
                    await audioClient.SetSpeakingAsync(!pause);
                }
                if (lastSkip != currentSkip)
                {
                    skip = true;
                    lock (lastStatus)
                    {
                        lastStatus = "Skipping";
                    }
                }
                if (lastRepeat != currentRepeat)
                {
                    repeat = !repeat;
                }
                if (lastPrevious != currentPrevious)
                {
                    if (currentVideoIndex > 0)
                    {
                        currentVideoIndex -= 2;
                        skip = true;
                        lock (lastStatus)
                        {
                            lastStatus = "Previous Title";
                        }

                        //pause = false;
                    }
                }

                lastPause = currentPause;
                lastPrevious = currentPrevious;
                lastSkip = currentSkip;
                lastRepeat = currentRepeat;

                await oneMessage.ModifyAsync(m => m.Embed = audioInfoEmbed());

            }
        }
        //, RunMode = RunMode.Async
        [Command("add", RunMode = RunMode.Async)]
        public async Task AddTitle(string url)
        {
            Uri uri;
            if (!(Uri.TryCreate(url, UriKind.Absolute, out uri) && uri.Scheme == Uri.UriSchemeHttps)) throw new Exception("no valid url");
            if (!uri.Host.Contains("youtube")) throw new Exception("no youtube link");
            //new Task(() =>
            // {
            Console.WriteLine($"[{DateTime.Now.ToShortTimeString()}] - beginning downloading: {uri.ToString()}");
            lock (lastStatus)
            {
                lastStatus = "Begin download and convert";
            }
            string id = YoutubeClient.ParseVideoId(uri.ToString());
            YoutubeClient tubeClient = new YoutubeClient();
            Video video = tubeClient.GetVideoAsync(id).GetAwaiter().GetResult();
            MediaStreamInfoSet streamInfoSet = await tubeClient.GetVideoMediaStreamInfosAsync(id);
            AudioStreamInfo streamInfo = streamInfoSet.Audio.WithHighestBitrate();

            string path = Path.Combine(Path.GetTempPath(), id + "." + streamInfo.Container.GetFileExtension());
            string taskPath = Path.Combine(Path.GetTempPath(), $"{id}.pcm");
            if (!File.Exists(path))
            {
                await tubeClient.DownloadMediaStreamAsync(streamInfo, path);

                Process.Start(new ProcessStartInfo()
                {
                    FileName = "ffmpeg",
                    Arguments = $"-xerror -i \"{path}\" -ac 2 -y -filter:a \"volume = 0.01337\" -loglevel panic -f s16le -ar 48000 \"{taskPath}\"",
                    UseShellExecute = false,    //TODO: true or false?
                    RedirectStandardOutput = false
                }).WaitForExit();

                File.Delete(path);
            }
            lock (audioQueue)
            {
                audioQueue.Add(new VideoInfo(video, taskPath));
                File.WriteAllText("songQueue.json", JsonConvert.SerializeObject(audioQueue));
            }
            await Task.Run(() =>
            {
                pause = false;
            });
            await Context.Message.DeleteAsync();
        }

        private static async void playAudio()
        {
            bool next = false;
            while (true)
            {
                bool pause = false;
                //Next song if current is over
                if (!next)
                {
                    pause = await taskCompletionSource.Task;
                    taskCompletionSource = new TaskCompletionSource<bool>();
                }
                else
                {
                    next = false;
                }

                try
                {
                    int audioCount = 0;
                    lock (audioQueue)
                    {
                        audioCount = audioQueue.Count;
                    }
                    if (audioCount == currentVideoIndex + 1 && (currentVideoInfo.Path == null && !repeat))
                    {
                        //show nothing in media player
                        currentVideoInfo = default(VideoInfo);
                    }
                    else
                    {
                        if (!pause)
                        {
                            //Get Song

                            lock (audioQueue)
                            {
                                if (!(repeat && currentVideoInfo.Path != null))
                                {
                                    if (currentVideoIndex > 2)
                                    {
                                        audioQueue.RemoveAt(0);
                                        File.WriteAllText("songQueue.json", JsonConvert.SerializeObject(audioQueue));
                                    }
                                    else
                                    {
                                        currentVideoIndex++;
                                    }
                                }
                                currentVideoInfo = audioQueue[currentVideoIndex];
                            }

                            stopwatch.Reset();
                            stopwatch.Start();
                            Console.WriteLine($"[{DateTime.Now.ToShortTimeString()}] - begin playback: {currentVideoInfo.Title}");
                            //Update "Playing .."
                            //show what is currently playing

                            //Send audio (Long Async blocking, Read/Write stream)
                            lock (lastStatus)
                            {
                                lastStatus = "Playing";
                            }
                            await SendAudio(currentVideoInfo.Path);
                            try
                            {
                                lock (audioQueue)
                                {
                                    if (audioQueue.Count(e => e.Path == currentVideoInfo.Path) == 0 && !repeat)
                                    {
                                        File.Delete(currentVideoInfo.Path);
                                    }

                                }
                            }
                            catch
                            {
                                // ignored
                            }
                            finally
                            {
                                //Finally remove song from playlist

                            }
                            next = true;
                        }
                    }
                }
                catch (Exception e)
                {
                    //audio can't be played

                }
            }
        }

        private static async Task SendAudio(string path)
        {
            await audioClient.SetSpeakingAsync(true);
            try
            {
                FileInfo fileInfo = new FileInfo(path);
                using (Stream output = File.Open(path, FileMode.Open))
                {
                    using (AudioOutStream discord = audioClient.CreatePCMStream(AudioApplication.Mixed))
                    {
                        //Adjust?
                        int bufferSize = 1024;
                        int bytesSent = 0;
                        bool fail = false;
                        bool exit = false;
                        byte[] buffer = new byte[bufferSize];

                        while (
                            !skip &&                                    // If Skip is set to true, stop sending and set back to false (with getter)
                            !fail &&                                    // After a failed attempt, stop sending
                            !cancellationTokenSource.IsCancellationRequested &&   // On Cancel/Dispose requested, stop sending
                            !exit                                       // Audio Playback has ended (No more data from FFmpeg.exe)
                                )
                        {
                            try
                            {
                                int read = await output.ReadAsync(buffer, 0, bufferSize, cancellationTokenSource.Token);
                                if (read == 0)
                                {
                                    //No more data available
                                    Console.WriteLine($"[{DateTime.Now.ToShortTimeString()}] - End of song: {currentVideoInfo.Title}");
                                    exit = true;
                                    break;
                                }

                                await discord.WriteAsync(buffer, 0, read, cancellationTokenSource.Token);


                                if (pause)
                                {
                                    bool pauseAgain;
                                    do
                                    {
                                        pauseAgain = await taskCompletionSource.Task;
                                        taskCompletionSource = new TaskCompletionSource<bool>();
                                    } while (pauseAgain);
                                }

                                bytesSent += read;

                            }
                            catch (TaskCanceledException)
                            {
                                exit = true;
                            }
                            catch (Exception e)
                            {
                                fail = true;
                                Console.WriteLine(e.Message);
                                // could not send
                            }
                        }
                        await discord.FlushAsync();
                        await audioClient.SetSpeakingAsync(false);
                    }
                }

            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private static Embed audioInfoEmbed()
        {
            EmbedBuilder dynamicEmbed = new EmbedBuilder();
            if (currentVideoInfo.Path == null)
            {
                dynamicEmbed
                .WithAuthor("Welcome to the music player")
                .WithColor(Color.DarkBlue)
                .WithTitle("Use `!add url` to add music to the queue")
                .WithFooter(f => f.Text = "stop \u23EF, skip \u23ED, previous \u23EE, repeat \U0001F502")
                .Build();
            }
            else
            {
                dynamicEmbed
                .WithAuthor($"{currentVideoInfo.Title}")
                .WithColor(Color.DarkPurple)
                .WithDescription($"`|{"".PadLeft((int)(40.0 * (stopwatch.Elapsed.TotalSeconds / currentVideoInfo.Duration.TotalSeconds)), '#').PadRight(40, '-')}|`\n\nZeit: {stopwatch.Elapsed.FormatTime()} / {currentVideoInfo.Duration.FormatTime()}")
                .WithFooter(f => f.Text = "stop \u23EF, skip \u23ED, previous \u23EE, repeat \U0001F502")
                .WithTitle($"{currentVideoInfo.Interpret}\n\n")
                .WithUrl(currentVideoInfo.Url)
                .WithThumbnailUrl(currentVideoInfo.LowResUrl);


            }
            lock (audioQueue)
            {
                foreach (VideoInfo song in audioQueue)
                {
                    if (song.Equals(currentVideoInfo))
                    {
                        dynamicEmbed.AddField($"{(pause ? ":pause_button:" : ":arrow_forward:")}  {(repeat ? "\U0001F502" : "")}" + song.Title, song.Duration.FormatTime());
                    }
                    else
                    {
                        dynamicEmbed.AddField(song.Title, song.Duration.FormatTime() + $" https://youtu.be/{song.Id}");
                    }

                }
                lock (lastStatus)
                {
                    dynamicEmbed.AddField("Status:", lastStatus);
                }
            }
            return dynamicEmbed.Build();
        }

        [Command("load", RunMode = RunMode.Async)]
        public async Task Load()
        {

            await Join(Context.Guild.VoiceChannels.First(e => e.Id == 316621565305421826));
            await AddTitle("https://www.youtube.com/watch?v=8-gpAw17vhc");
            await AddTitle("https://www.youtube.com/watch?v=_p5exp2TZeM");
            await AddTitle("https://www.youtube.com/watch?v=cIseaPTGCRU");
            await AddTitle("https://www.youtube.com/watch?v=6Z0lWw4Jw9c");
            await AddTitle("https://www.youtube.com/watch?v=LbLfg5m3tns");
            await AddTitle("https://www.youtube.com/watch?v=k1uUIJPD0Nk");
            await AddTitle("https://www.youtube.com/watch?v=hbw1pGUhG7Q");
            await AddTitle("https://www.youtube.com/watch?v=ZwcN8E3I04k");
        }

    }
}