using Discord.Interactions;
using DiscordBot.Services;
using DiscordBot.Settings;

namespace DiscordBot.Modules;

// For commands that only require a single interaction, these can be done automatically and don't require complex setup or configuration.
// ie; A command that might just return the result of a service method such as Ping, or Welcome
public class UserSlashModule : InteractionModuleBase
{
    #region Dependency Injection

    public CommandHandlingService CommandHandlingService { get; set; }
    public UserService UserService { get; set; }
    public BotSettings BotSettings { get; set; }

    #endregion

    #region Help

    [SlashCommand("help", "Shows available commands")]
    private async Task Help()
    {
        await Context.Interaction.DeferAsync(ephemeral: true);

        var helpEmbed = HelpEmbed(0);
        ComponentBuilder builder = new ComponentBuilder();
        builder.WithButton("Next Page", $"user_module_help_next:{0}");

        await Context.Interaction.FollowupAsync(embed: helpEmbed.Item2, ephemeral: true, components: builder.Build());
    }

    [ComponentInteraction("user_module_help_next:*")]
    private async Task InteractionHelp(string pageString)
    {
        await Context.Interaction.DeferAsync(ephemeral: true);

        int page = int.Parse(pageString);

        var helpEmbed = HelpEmbed(page + 1);
        ComponentBuilder builder = new ComponentBuilder();
        builder.WithButton("Next Page", $"user_module_help_next:{helpEmbed.Item1}");

        await Context.Interaction.ModifyOriginalResponseAsync(msg =>
        {
            msg.Components = builder.Build();
            msg.Embed = helpEmbed.Item2;
        });
    }

    // Returns an embed with the help text for a module, if the page is outside the bounds (high) it will return to the first page.
    private (int, Embed) HelpEmbed(int page)
    {
        var helpMessages = CommandHandlingService.GetCommandListMessages("UserModule", false, true, false);

        if (page >= helpMessages.Count)
            page = 0;
        else if (page < 0)
            page = helpMessages.Count - 1;

        EmbedBuilder ebuilder = new EmbedBuilder();
        ebuilder.Title = "User Module Commands";
        ebuilder.Color = Color.LighterGrey;
        ebuilder.WithFooter(text: $"Page {page + 1} of {helpMessages.Count}");

        ebuilder.Description = helpMessages[page];
        var embed = ebuilder.Build();

        return (page, embed);
    }

    #endregion

    [SlashCommand("welcome", "An introduction to the server!")]
    public async Task SlashWelcome()
    {
        await Context.Interaction.RespondAsync(string.Empty,
            embed: UserService.GetWelcomeEmbed(Context.User.Username), ephemeral: true);
    }

    [SlashCommand("ping", "Bot latency")]
    public async Task Ping()
    {
        await Context.Interaction.RespondAsync($"Bot latency: ...", ephemeral: true);
        await Context.Interaction.ModifyOriginalResponseAsync(m =>
            m.Content = $"Bot latency: {UserService.GetGatewayPing().ToString()}ms");
    }

    [SlashCommand("invite", "Returns the invite link for the server.")]
    public async Task ReturnInvite()
    {
        await Context.Interaction.RespondAsync(text: BotSettings.Invite, ephemeral: true);
    }

    #region User Roles
    
    [SlashCommand("roles", "Give or Remove roles for yourself (Programmer, Artist, Designer, etc)")]
    public async Task UserRoles()
    {
        await Context.Interaction.DeferAsync(ephemeral: true);

        ComponentBuilder builder = new ComponentBuilder();

        foreach (var userRole in BotSettings.UserAssignableRoles.Roles)
        {
            builder.WithButton(userRole, $"user_role_add:{userRole}");
        }

        builder.Build();

        await Context.Interaction.FollowupAsync(text: "Click any role that applies to you!", embed: null,
            ephemeral: true, components: builder.Build());
    }

    [ComponentInteraction("user_role_add:*")]
    public async Task UserRoleAdd(string role)
    {
        await Context.Interaction.DeferAsync(ephemeral: true);

        var user = Context.User as IGuildUser;
        var guild = Context.Guild;
        
        // Try get the role from the guild
        var roleObj = guild.Roles.FirstOrDefault(r => r.Name == role);
        if (roleObj == null)
        {
            await Context.Interaction.ModifyOriginalResponseAsync(msg =>
                msg.Content = $"Failed to add role {role}, role not found.");
            return;
        }
        // We make sure the role is in our UserAssignableRoles just in case
        if (BotSettings.UserAssignableRoles.Roles.Contains(roleObj.Name))
        {
            if (user.RoleIds.Contains(roleObj.Id))
            {
                await user.RemoveRoleAsync(roleObj);
                await Context.Interaction.ModifyOriginalResponseAsync(msg =>
                    msg.Content = $"{roleObj.Name} has been removed!");
            }
            else
            {
                await user.AddRoleAsync(roleObj);
                await Context.Interaction.ModifyOriginalResponseAsync(msg =>
                    msg.Content = $"You now have the {roleObj.Name} role!");
            }
        }
    }
    
    #endregion
}