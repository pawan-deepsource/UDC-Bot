using System;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using DiscordBot.Extensions;
using DiscordBot.Services;
using DiscordBot.Settings.Deserialized;
using DiscordBot.Utils.Attributes;
using Microsoft.Extensions.DependencyInjection;

namespace DiscordBot.Modules
{
    [Group("UserModule"), Alias("")]
    public class ReminderModule : ModuleBase
    {
        private readonly ReminderService _reminderService;
        private readonly BotCommandsChannel _botCommandsChannel;

        public ReminderModule(IServiceProvider serviceProvider, Settings.Deserialized.Settings settings)
        {
            _botCommandsChannel = settings.BotCommandsChannel;
            _reminderService = serviceProvider.GetService<ReminderService>();
        }
        
        // Command where user types !remindme followed by a time and a message
        // The time is human readable and is converted to a DateTime object
        [Command("remindme"), Alias("reminder"), Priority(80)]
        [Summary("Reminds users of a message based on time. Syntax : !remindme 1hour30min Watch a tutorial")]
        public async Task RemindMe(string time, [Remainder] string message)
        {
            if (Context.Message.MentionedEveryone || Context.Message.MentionedRoleIds.Count > 0 || Context.Message.MentionedUserIds.Count > 0)
            {
                await ReplyAsync("You can't mention anyone or roles in a reminder.").DeleteAfterSeconds(seconds: 5);
                return;
            }
            
            var reminderDate = Utils.Utils.ParseTimeFromString(time);
            if (reminderDate < DateTime.Now)
            {
                await ReplyAsync("You can't set a reminder in the past!");
                return;
            }
            // Add 1 second so we don't count the second we're on now as a second past
            reminderDate = reminderDate.AddSeconds(1);
            
            var dateInSeconds = (uint)(reminderDate - DateTime.Now).TotalSeconds;
            if (dateInSeconds < 30)
            {
                await ReplyAsync("Reminders must be more than 30 seconds away!");
                return;
            }
            
            // Check if user has to many reminders and tell them to delete some if so
            if (_reminderService.UserHasTooManyReminders(Context.User.Id))
            {
                await ReplyAsync("You have too many reminders! Please delete some before adding more.").DeleteAfterSeconds(seconds: 10);
                return;
            }
            
            // if message is longer than 100 characters, truncate it
            if (message.Length > 100)
                message = message[..100];

            var reminder = new ReminderItem
            {
                UserId = Context.User.Id,
                MessageId = Context.Message.Id,
                ChannelId = Context.Channel.Id,
                Message = message,
                When = reminderDate
            };

            _reminderService.AddReminder(reminder);
            await ReplyAsync($"Reminder set for {Utils.Utils.FormatTime((uint)(reminderDate - DateTime.Now).TotalSeconds) }").DeleteAfterSeconds(seconds: 10);
        }

        [Command("remindme"), HideFromHelp]
        [Summary("Reminds users at a certain time. Syntax : !remindme 1hour30min")]
        public async Task RemindMe(string time)
        {
            await RemindMe(time, "No Message");
        }
        
        // Command where user types !reminders to see all their reminders
        [Command("reminders"), Priority(81)]
        [Summary("Tell the user of their set reminders in bot channel.")]
        public async Task Reminders()
        {
            await Reminders(Context.User);
        }
        
        // Removes a users reminders for them
        [RequireModerator]
        [Command("removereminders"), HideFromHelp]
        [Summary("Clears user reminders.")]
        public async Task RemoveReminders(IUser user, int index = 0)
        {
            await Context.Message.DeleteAfterSeconds(seconds: 1);
            int removedReminders = _reminderService.RemoveReminders(user, index);
            if (removedReminders == 0)
                return;
            if (removedReminders == -1)
            {
                await ReplyAsync("Invalid index provided.");
                return;
            }
            await ReplyAsync($"{removedReminders.ToString()} Reminders removed.").DeleteAfterSeconds(seconds: 2);
        }
        
        // Allows a moderator to see reminders of a user
        [RequireModerator]
        [Command("reminders"), HideFromHelp]
        [Summary("Check user reminders.")]
        public async Task Reminders(IUser user)
        {
            await Context.Message.DeleteAsync();
            var reminders = _reminderService.GetUserReminders(Context.User.Id);
            if (reminders.Count == 0)
            {
                await ReplyAsync($"{user.Username} has no reminders!").DeleteAfterSeconds(seconds: 5);
                return;
            }
            
            var embed = new EmbedBuilder();
            embed.WithTitle($"{Context.User.Username} Reminders");
            embed.WithColor(new Color(0x89CFF0));
            int index = 1;
            foreach (var reminder in reminders)
            {
                var msgLink = Utils.Utils.MessageLinkBack(Context.Guild.Id, reminder.ChannelId, reminder.MessageId);
                embed.AddField($"#{index++} | {Utils.Utils.FormatTime((uint)(reminder.When - DateTime.Now).TotalSeconds)}", $"[Link]({msgLink}) \"{reminder.Message}\"");
            }
            
            var botCommands = await Context.Guild.GetChannelAsync(_botCommandsChannel.Id) as IMessageChannel;
            if (botCommands != null)
                await botCommands
                    .SendMessageAsync(Context.User.Mention, false, embed.Build())
                    .DeleteAfterSeconds(seconds: 30);
        }
        
        // Removes a users reminders themself
        [Command("removereminder"), Priority(81)]
        [Summary("Clears all reminders unless an Index is provided.")]
        [Alias("removereminders")]
        public async Task RemoveReminders(int index = 0)
        {
            await RemoveReminders(Context.User, index);
        }
    }
}