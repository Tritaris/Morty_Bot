// THIS FILE IS A PART OF EMZI0767'S BOT EXAMPLES
//
// --------
// 
// Copyright 2017 Emzi0767
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//  http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//
// --------
//
// This is a commands example. It shows how to properly utilize 
// CommandsNext, as well as use its advanced functionality.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Exceptions;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using DSharpPlus.Interactivity;
using DSharpPlus.Net.WebSocket;
using DSharpPlus.VoiceNext;
using DSharpPlus.VoiceNext.Codec;
using Newtonsoft.Json;


namespace Morty_Bot
{
    public class Program
    {
        public DiscordClient Client { get; set; }
        public InteractivityModule Interactivity { get; set; }
        public CommandsNextModule Commands { get; set; }
        public VoiceNextClient Voice { get; set; }

        private ulong _botID;
        private ulong _serverID;

        const string PATTERN = @"([\#]\d+)+"; //pattern to find a # sign followed by numbers

        public static void Main(string[] args)
        {
            // since we cannot make the entry method asynchronous,
            // let's pass the execution to asynchronous code
            var prog = new Program();
            prog.RunBotAsync().GetAwaiter().GetResult();
        }

        public async Task RunBotAsync()
        {
            // first, let's load our configuration file
            var json = "";
            using (var fs = File.OpenRead("config.json"))
            using (var sr = new StreamReader(fs, new UTF8Encoding(false)))
                json = await sr.ReadToEndAsync();

            // next, let's load the values from that file
            // to our client's configuration
            var cfgjson = JsonConvert.DeserializeObject<ConfigJson>(json);

            _botID = cfgjson.Bot_ID;
            _serverID = cfgjson.Server_ID;

            var cfg = new DiscordConfiguration
            {
                Token = cfgjson.Token,
                TokenType = TokenType.Bot,

                AutoReconnect = true,
                LogLevel = LogLevel.Debug,
                UseInternalLogHandler = true
            };

            // then we want to instantiate our client
            Client = new DiscordClient(cfg);

            // If you are on Windows 7 and using .NETFX, install 
            // DSharpPlus.WebSocket.WebSocket4Net from NuGet,
            // add appropriate usings, and uncomment the following
            // line
            Client.SetWebSocketClient<WebSocket4NetClient>();

            // If you are on Windows 7 and using .NET Core, install 
            // DSharpPlus.WebSocket.WebSocket4NetCore from NuGet,
            // add appropriate usings, and uncomment the following
            // line
            //this.Client.SetWebSocketClient<WebSocket4NetCoreClient>();

            // If you are using Mono, install 
            // DSharpPlus.WebSocket.WebSocketSharp from NuGet,
            // add appropriate usings, and uncomment the following
            // line
            //this.Client.SetWebSocketClient<WebSocketSharpClient>();

            // if using any alternate socket client implementations, 
            // remember to add the following to the top of this file:
            //using DSharpPlus.Net.WebSocket;

            // next, let's hook some events, so we know
            // what's going on
            Client.Ready += Client_Ready;
            Client.GuildAvailable += Client_GuildAvailable;
            Client.ClientErrored += Client_ClientError;
            Client.MessageCreated += Client_MessageCreated;
            //Client.GuildMemberUpdated += Client_GuildMemberUpdated;
            Client.GuildMemberAdded += Client_GuildMemberAdded;

            // let's enable interactivity, and set default options
            Client.UseInteractivity(new InteractivityConfiguration
            {
                // default pagination behaviour to just ignore the reactions
                PaginationBehaviour = TimeoutBehaviour.Ignore,

                // default pagination timeout to 5 minutes
                PaginationTimeout = TimeSpan.FromMinutes(5),

                // default timeout for other actions to 2 minutes
                Timeout = TimeSpan.FromMinutes(2)
            });

            // up next, let's set up our commands
            var ccfg = new CommandsNextConfiguration
            {
                // let's use the string prefix defined in config.json
                StringPrefix = cfgjson.CommandPrefix,

                // enable responding in direct messages
                EnableDms = true,

                // enable mentioning the bot as a command prefix
                EnableMentionPrefix = true
            };

            // let's set up voice
            var vcfg = new VoiceNextConfiguration
            {
                VoiceApplication = VoiceApplication.Music
            };

            // and hook them up
            Commands = Client.UseCommandsNext(ccfg);

            // let's hook some command events, so we know what's 
            // going on
            Commands.CommandExecuted += Commands_CommandExecuted;
            Commands.CommandErrored += Commands_CommandErrored;

            // let's add a converter for a custom type and a name
            var mathopcvt = new MathOperationConverter();
            CommandsNextUtilities.RegisterConverter(mathopcvt);
            CommandsNextUtilities.RegisterUserFriendlyTypeName<MathOperation>("operation");

            // up next, let's register our commands
            Commands.RegisterCommands<ExampleUngrouppedCommands>();
            Commands.RegisterCommands<ExampleGrouppedCommands>();
            Commands.RegisterCommands<ExampleExecutableGroup>();
            Commands.RegisterCommands<ExampleInteractiveCommands>();
            Commands.RegisterCommands<ExampleVoiceCommands>();

            // set up our custom help formatter
            Commands.SetHelpFormatter<SimpleHelpFormatter>();

            // and let's enable Voice
            Voice = Client.UseVoiceNext(vcfg);

            // finally, let's connect and log in
            await Client.ConnectAsync();

            // when the bot is running, try doing <prefix>help
            // to see the list of registered commands, and 
            // <prefix>help <command> to see help about specific
            // command.

            // and this is to prevent premature quitting
            await Task.Delay(-1);
        }

        private Task Client_Ready(ReadyEventArgs e)
        {
            // let's log the fact that this event occured
            e.Client.DebugLogger.LogMessage(LogLevel.Info, "Morty_Bot", "Client is ready to process events.", DateTime.Now);

            // since this method is not async, let's return
            // a completed task, so that no additional work
            // is done
            return Task.CompletedTask;
        }

        private async Task Client_MessageCreated(MessageCreateEventArgs e)
        {
            // We DON'T want to respond to other bots, do we?
            if (!e.Message.Author.IsBot)
            {
                var msg = e.Message.Content;

                //Checks to see if message contains an @ mention
                if (e.MentionedUsers.Count > 0)
                {
                    //If multiple mentions are in the message, this will iterate over them all
                    foreach (var mentionedUser in e.MentionedUsers)
                    {
                        if (mentionedUser.Id == _botID)
                        {   //Checks if user is mentioning this bot.
                            e.Client.DebugLogger.LogMessage(LogLevel.Info, "Morty_Bot", $"{e.Message.Author.Username} mentioned: " + mentionedUser.Username, DateTime.Now);

                        }
                        else if (mentionedUser.Username == e.Message.Author.Username)
                        {    //Checks if user is mentioning themselves.
                            await e.Message.RespondAsync($"Why are you talking to yourself, {mentionedUser.Username}");
                        }
                    }
                }

                //Check messages for github links by issue number
                if (msg.Contains("#"))
                {
                    Match match = Regex.Match(msg, PATTERN); // "-?[0-9]+"

                    while (match.Success)
                    {
                        Group g = match.Groups[0];
                        int number = ConvertToInt(g.Value);

                        e.Client.DebugLogger.LogMessage(LogLevel.Info, "Morty_Bot", $"Github search by {e.Message.Author.Username} : Issue #" + number, DateTime.Now);
                        await e.Message.RespondAsync("https://github.com/Tritaris/test/issues/" + number);

                        match = match.NextMatch();
                    }
                }
            }
        }

        public static int ConvertToInt(String input)
        {
            // Replace everything that is no a digit.
            String inputCleaned = Regex.Replace(input, "[^0-9]", "");

            // Tries to parse the int, returns false on failure.
            if (int.TryParse(inputCleaned, out int value))
            {
                // The result from parsing can be safely returned.
                return value;
            }

            return -1; // Or any other default value.
        }

        //private async Task Client_GuildMemberUpdated(GuildMemberUpdateEventArgs e)
        //{

        //}

        private async Task Client_GuildMemberAdded(GuildMemberAddEventArgs e)
        {
            e.Client.DebugLogger.LogMessage(LogLevel.Info, "Morty_Bot", $"New member joined {e.Guild.Name}", DateTime.Now);

            var bot = await e.Guild.GetMemberAsync(_botID);
            var channel = e.Guild.Channels.FirstOrDefault(xc =>
                xc.PermissionsFor(bot).ToPermissionString().Contains("Send message"));

            var embed = new DiscordEmbedBuilder().WithColor(DiscordColor.Green)
                .WithAuthor(e.Member.Username, e.Member.AvatarUrl, e.Member.AvatarUrl)
                .AddField("New Member Joined", "Welcome to our new member")
                .AddField("Account Creation Date", $"{e.Member.CreationTimestamp.ToString("MM/dd/yyyy HH:mm:ss")}");

            await channel.SendMessageAsync(embed: embed);

            if (e.Guild.Id == _serverID)
            {
                _ = Task.Run(async () => await AssignRoleToMember(e, channel));
            }
        }

        private async Task AssignRoleToMember(GuildMemberAddEventArgs e, DiscordChannel channel)
        {
            var msg = await channel.SendMessageAsync("Please take your time and select the region you usually play in.");

            var emojiUS = DiscordEmoji.FromName(Client, ":flag_us:");
            var emojiEU = DiscordEmoji.FromName(Client, ":flag_eu:");
            var emojiSEA = DiscordEmoji.FromName(Client, ":ocean:");
            var emojiBR = DiscordEmoji.FromName(Client, ":flag_br:");
            var emojiRU = DiscordEmoji.FromName(Client, ":flag_ru:");
            var emojiAU = DiscordEmoji.FromName(Client, ":flag_au:");
            var emojiList = new List<DiscordEmoji>() { emojiUS, emojiEU, emojiSEA, emojiBR, emojiRU, emojiAU };

            foreach (var emoji in emojiList)
            {
                await msg.CreateReactionAsync(emoji);
                await Task.Delay(250);
            }

            var reaction = await Interactivity.WaitForReactionAsync(xe => emojiList.Contains(xe), e.Member, TimeSpan.FromSeconds(60));

            if (reaction != null)
            {
                switch (reaction.Emoji.GetDiscordName())
                {
                    //  :flag_us: emoji
                    case ":flag_us:":
                        await AssignRoleToMemberAsync(e, "NA");
                        break;
                    //  :flag_eu: emoji
                    case ":flag_eu:":
                        await AssignRoleToMemberAsync(e, "EU");
                        break;
                    //  :ocean: emoji, representing SEA region for now..
                    case ":ocean:":
                        await AssignRoleToMemberAsync(e, "SEA");
                        break;
                    //  :flag_br: emoji
                    case ":flag_br:":
                        await AssignRoleToMemberAsync(e, "BR");
                        break;
                    //  :flag_ru: emoji
                    case ":flag_ru:":
                        await AssignRoleToMemberAsync(e, "RU-S-A");
                        break;
                    //  :flag_au: emoji
                    case ":flag_au:":
                        await AssignRoleToMemberAsync(e, "AUS");
                        break;
                    default:
                        await channel.SendMessageAsync("It broke..");
                        break;
                }
                await channel.SendMessageAsync("All done! If you don't mind playing in multiple regions, message the server admin or one of the mods to assign you the role needed.");
            }
            else
            {
                await channel.SendMessageAsync("Very well then, if you wish to have a role assigned later on, message the server admin or one of the mods.");
            }
        }

        private async Task AssignRoleToMemberAsync(GuildMemberAddEventArgs e, string roleName)
        {
            var role = e.Guild.Roles.Where(xr => xr.Name == roleName).FirstOrDefault();
            var member = e.Member;
            await member.GrantRoleAsync(role);
        }

        private Task Client_GuildAvailable(GuildCreateEventArgs e)
        {
            // let's log the name of the guild that was just
            // sent to our client
            e.Client.DebugLogger.LogMessage(LogLevel.Info, "Morty_Bot", $"Guild available: {e.Guild.Name}", DateTime.Now);

            // since this method is not async, let's return
            // a completed task, so that no additional work
            // is done
            return Task.CompletedTask;
        }

        private Task Client_ClientError(ClientErrorEventArgs e)
        {
            // let's log the details of the error that just 
            // occured in our client
            e.Client.DebugLogger.LogMessage(LogLevel.Error, "Morty_Bot", $"Exception occured: {e.Exception.GetType()}: {e.Exception.Message}", DateTime.Now);

            // since this method is not async, let's return
            // a completed task, so that no additional work
            // is done
            return Task.CompletedTask;
        }

        private Task Commands_CommandExecuted(CommandExecutionEventArgs e)
        {
            // let's log the name of the command and user
            e.Context.Client.DebugLogger.LogMessage(LogLevel.Info, "Morty_Bot", $"{e.Context.User.Username} successfully executed '{e.Command.QualifiedName}'", DateTime.Now);

            // since this method is not async, let's return
            // a completed task, so that no additional work
            // is done
            return Task.CompletedTask;
        }

        private async Task Commands_CommandErrored(CommandErrorEventArgs e)
        {
            // let's log the error details
            e.Context.Client.DebugLogger.LogMessage(LogLevel.Error, "Morty_Bot", $"{e.Context.User.Username} tried executing '{e.Command?.QualifiedName ?? "<unknown command>"}' but it errored: {e.Exception.GetType()}: {e.Exception.Message ?? "<no message>"}", DateTime.Now);

            // let's check if the error is a result of lack
            // of required permissions
            if (e.Exception is ChecksFailedException ex)
            {
                // yes, the user lacks required permissions, 
                // let them know

                var emoji = DiscordEmoji.FromName(e.Context.Client, ":no_entry:");

                // let's wrap the response into an embed
                var embed = new DiscordEmbedBuilder
                {
                    Title = "Access denied",
                    Description = $"{emoji} Aw Jeez! You do not have the permissions required to execute this command.",
                    Color = new DiscordColor(0xFF0000) // red
                    // there are also some pre-defined colors available
                    // as static members of the DiscordColor struct
                };
                await e.Context.RespondAsync("", embed: embed);
            }
        }
    }

    // this structure will hold data from config.json
    public struct ConfigJson
    {
        [JsonProperty("token")]
        public string Token { get; private set; }

        [JsonProperty("prefix")]
        public string CommandPrefix { get; private set; }

        [JsonProperty("bot_id")]
        public ulong Bot_ID { get; private set; }

        [JsonProperty("server_id")]
        public ulong Server_ID { get; private set; }
    }
}