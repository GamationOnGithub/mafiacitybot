using Discord;
using Discord.Net;
using Discord.WebSocket;
using System;

namespace mafiacitybot.GuildCommands
{
    public static class Register
    {
        public enum Roles
        {
            Player,
            Doctor
        }

        public static async Task CreateCommand(SocketGuild guild)
        {
            var command = new SlashCommandBuilder();
            command.WithDefaultMemberPermissions(GuildPermission.ManageRoles);
            command.WithName("register");
            command.WithDescription("Registers a player and their personal channel.");
            command.AddOption("user", ApplicationCommandOptionType.User, "The user's discord ID.", isRequired: true);
            command.AddOption("name", ApplicationCommandOptionType.String, "The user's name.", isRequired: true);
            command.AddOption("channel", ApplicationCommandOptionType.Channel, "The user's personal channel ID.", isRequired: true);
            command.AddOption(new SlashCommandOptionBuilder()
                .WithName("role")
                .WithDescription("The user's role.")
                .WithRequired(true)
                .AddChoice("Player", (int)Roles.Player)
                .AddChoice("Doctor", (int)Roles.Doctor)
            );


            try
            {
                await guild.CreateApplicationCommandAsync(command.Build());
            }
            catch (HttpException exception)
            {
                Console.WriteLine(exception.Message);
            }
        }

        public static async Task HandleCommand(SocketSlashCommand command, Program program)
        {
            try
            {
                if(!program.guilds.TryGetValue(Convert.ToUInt64(command.GuildId), out Guild guild))
                {
                    await command.RespondAsync("You must use Setup first!");
                    return;
                }
                ulong userID = ((SocketGuildUser)command.Data.Options.ElementAt(0).Value).Id;
                ulong channelID = ((SocketGuildChannel)command.Data.Options.ElementAt(2).Value).Id;
                string name = (string)command.Data.Options.ElementAt(1).Value;
                string role = (string)command.Data.Options.ElementAt(3).Value;
                object[] classParams = {userID, channelID, name};
                try
                {
                    Type roleAsType = Type.GetType(role, true);
                    Player roleAsObject = (Player)Activator.CreateInstance(roleAsType, classParams);
                    guild.AddPlayer(roleAsObject);
                    await command.RespondAsync($"Registered new player {program.client.GetUserAsync(userID)} with channel {program.client.GetChannelAsync(channelID)}.");
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                    Console.WriteLine(e.StackTrace);
                    await command.RespondAsync("Something went wrong with adding the role!");
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                Console.WriteLine(e.StackTrace);
                await command.RespondAsync("Something went wrong with handling the command!");
            }
            
        }
    }
}