using Discord;
using Discord.Net;
using Discord.WebSocket;

namespace mafiacitybot.GuildCommands;

public static class AnonChat
{
    public static async Task CreateCommand(DiscordSocketClient client, SocketGuild? guild = null)
    {
        var command = new SlashCommandBuilder();
        command.WithName("host_anonchat");
        command.WithDescription("Start or end an anonymous chat.");
        command.AddOption(new SlashCommandOptionBuilder()
            .WithName("setup")
            .WithDescription("Start an anonymous chat between two players.")
            .WithType(ApplicationCommandOptionType.SubCommand)
            .AddOption("source", ApplicationCommandOptionType.User, 
                "The player initiating the chat.", isRequired: true)
            .AddOption("receiver", ApplicationCommandOptionType.User, 
                "The player receiving the chat.", isRequired: true)
            .AddOption("source_channel", ApplicationCommandOptionType.Channel,
                "The channel that the source PLAYER is talking in.", isRequired: true)
            .AddOption("receiver_channel", ApplicationCommandOptionType.Channel,
                "The channel that the receiver is talking in.", isRequired: true)
            .AddOption("announce", ApplicationCommandOptionType.Boolean,
                "Whether to announce in the source/receiver chats.")
        );
        command.AddOption(new SlashCommandOptionBuilder()
            .WithName("close")
            .WithDescription("Close the anonymous chat between two players.")
            .WithType(ApplicationCommandOptionType.SubCommand)
            .AddOption("id",  ApplicationCommandOptionType.Integer, "The numerical ID of the chat to close.")
        );

        try
        {
            if (guild != null) {
                await guild.CreateApplicationCommandAsync(command.Build());
            } else {
                await client.CreateGlobalApplicationCommandAsync(command.Build());
            }
        }
        catch (HttpException exception)
        {
            Console.WriteLine(exception.Message);
        }
    }

    public static async Task HandleCommand(SocketSlashCommand command, Program program)
    {
        if(!program.guilds.TryGetValue((ulong)command.GuildId, out Guild guild))
        {
            await command.RespondAsync($"You must use /setup before being able to use this command!");
            return;
        }
        
        if (!Guild.IsHostRoleUser(command, guild.HostRoleID)) {
            await command.RespondAsync($"You must have the host role to use this command!");
            return;
        }
        if (guild.HostChannelID != command.ChannelId) {
            await command.RespondAsync($"Command must be executed in the host channel!");
            return;
        }
        
        var subcommand = command.Data.Options.First().Name;
        switch (subcommand)
        {
            case "setup":
                var chatParams = command.Data.Options.First().Options.ToDictionary(option => option.Name, option => option.Value);
                SocketGuildUser source = (SocketGuildUser)chatParams["source"];
                SocketGuildUser receiver = (SocketGuildUser)chatParams["receiver"];
                SocketGuildChannel sourceChannel = (SocketGuildChannel)chatParams["source_channel"];
                SocketGuildChannel receiverChannel = (SocketGuildChannel)chatParams["receiver_channel"];
                
                Player? sourcePlayerObj = guild.Players.Find(player => player.IsPlayer(source.Id));
                Player? receiverPlayerObj = guild.Players.Find(player => player.IsPlayer(receiver.Id));
                if (sourcePlayerObj == null || receiverPlayerObj == null)
                {
                    await command.RespondAsync("One or more of these users aren't a PLAYER in the game.", ephemeral: true);
                    return;
                }
                
                // TODO: Add support for multiple tunnels by making the ID dynamic
                var tunnel = new AnonChatTunnel(0, source.Id, receiver.Id, sourceChannel.Id, receiverChannel.Id);
                guild.AnonChats[0] = tunnel;
                
                await command.RespondAsync($"Started an anonymous chat between {sourcePlayerObj.Name} and {receiverPlayerObj.Name} in {sourceChannel.Name} and {receiverChannel.Name}.");
                break;
            
            case "close":
                var id = (long)command.Data.Options.First().Options.First().Value;
                if (guild.AnonChats.TryRemove((int)id, out var session))
                {
                    await command.RespondAsync($"Closed anonymous chat #{id}.");
                }
                else
                {
                    await command.RespondAsync($"No active anonymous chat with ID #{id}.");
                }
                break;
            
            default:
                await command.RespondAsync("Uh oh.");
                break;
        }

    }

    public static async Task HandleMessage(SocketMessage msg, Dictionary<ulong, Guild> guilds)
    {
        if (msg.Author.IsBot) return;
        if (msg.Channel is not SocketGuildChannel textChannel) return;
        ulong guildId = textChannel.Guild.Id;
        if(!guilds.TryGetValue(guildId, out Guild guild)) return;
        
        foreach (var chatTunnel in guild.AnonChats.Values)
        {
            if (msg.Channel.Id == chatTunnel.SourceChannel && msg.Author.Id == chatTunnel.Source)
            {
                var receiverChannel = (msg.Channel as SocketGuildChannel).Guild.GetTextChannel(chatTunnel.ReceiverChannel);
                string toForward = "Source: " + msg.CleanContent;
                await receiverChannel.SendMessageAsync(toForward);
                return;
            }
            if (msg.Channel.Id == chatTunnel.ReceiverChannel && msg.Author.Id == chatTunnel.Receiver)
            {
                var sourceChannel = (msg.Channel as SocketGuildChannel).Guild.GetTextChannel(chatTunnel.SourceChannel);
                await sourceChannel.SendMessageAsync($"Receiver: {msg.Content}");
                return;
            }
        }

    }
    
    public class AnonChatTunnel
    {
        public int Id { get; }
        public ulong Source { get; }
        public ulong Receiver { get; }
        public ulong SourceChannel { get; }
        public ulong ReceiverChannel { get; }

        public AnonChatTunnel(int id, ulong source, ulong receiver, ulong sourceChannel, ulong receiverChannel)
        {
            this.Id = id;
            this.Source = source;
            this.Receiver = receiver;
            this.SourceChannel = sourceChannel;
            this.ReceiverChannel = receiverChannel;
        }
    }
}
