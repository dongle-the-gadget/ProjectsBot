using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace FCProjectBot.Commands
{
    [SlashCommandGroup("projects", "Projects")]
    public class SlashCommands : ApplicationCommandModule
    {
        public Dictionary<ulong, ConfigJson> Config { private get; set; }
        public IDatabase Database { private get; set; }

        [SlashCommand("register", "Registers a project.")]
        public async Task Register(
            InteractionContext ctx,
            [Option("name", "The project name.")] string name,
            [Option("description", "A description of your project.")] string description,
            [Option("download_link", "A download link to your project. Optional.")] string? download = null,
            [Option("associated_channel", "The channel to associate this project to.")]DiscordChannel? associatedChannel = null)
        {
            await ctx.DeferAsync(true);

            var listOfUnparsed = await Database.HashGetAllAsync("projects");
            var listParsed = listOfUnparsed.Select(f => JsonSerializer.Deserialize<Project>(f.Value)!);

            if (listParsed.Any(f => f.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            {
                DiscordEmbed embed
                    = new DiscordEmbedBuilder()
                        .WithTitle("Error")
                        .WithDescription("The specified project name matches another project.")
                        .WithColor(DiscordColor.Red);

                await ctx.EditResponseAsync(new DiscordWebhookBuilder().AddEmbed(embed));
                return;
            }

            DiscordEmbedBuilder embedBuilder =
                new DiscordEmbedBuilder()
                .WithTitle("New project registration request")
                .AddField("Name", name)
                .AddField("Applicant", ctx.User.Mention);

            if (download != null)
            {
                bool isValidUrl =
                    Uri.TryCreate(download, UriKind.Absolute, out Uri? uriResult) &&
                    (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);
                if (!isValidUrl)
                {
                    embedBuilder =
                        new DiscordEmbedBuilder()
                        .WithTitle("Error")
                        .WithDescription("The specified download link is invalid.")
                        .WithColor(DiscordColor.Red);

                    await ctx.EditResponseAsync(
                        new DiscordWebhookBuilder()
                        .AddEmbed(embedBuilder.Build()));

                    return;
                }
                embedBuilder.AddField("Download link", download);
            }

            if (associatedChannel != null)
                embedBuilder.AddField("Associated channel", associatedChannel.Mention);

            embedBuilder.AddField("Description", description);

            DiscordMessageBuilder messageBuilder =
                new DiscordMessageBuilder()
                .AddEmbed(embedBuilder.Build())
                .AddComponents(
                    new DiscordButtonComponent(ButtonStyle.Success, "acceptRequest", "Accept"),
                    new DiscordButtonComponent(ButtonStyle.Danger, "rejectRequest", "Reject")
                );

            var channel = await ctx.Client.GetChannelAsync(Config[ctx.Guild.Id].RequestsChannelId);
            await channel.SendMessageAsync(messageBuilder);

            await ctx.EditResponseAsync(
                new DiscordWebhookBuilder()
                .WithContent("Your registration request has been sent. " +
                "If you haven't already, please turn on DMs for this server " +
                "so the bot can notify you if your request is accepted or rejected."));
        }

        [SlashCommand("search", "Search for a project.")]
        public async Task Search(InteractionContext ctx, [Option("query", "The query to search for.")] string query)
        {
            await ctx.DeferAsync();
            var listOfUnparsed = await Database.HashGetAllAsync("projects");
            var listParsed = listOfUnparsed.Select(f => JsonSerializer.Deserialize<Project>(f.Value)!);

            DiscordEmbedBuilder embedBuilder = new DiscordEmbedBuilder();

            if (listParsed.Any(f => f.Id.Equals(query, StringComparison.OrdinalIgnoreCase) || f.Name.Equals(query,StringComparison.OrdinalIgnoreCase)))
            {
                var project = listParsed.First(f => f.Id.Equals(query, StringComparison.OrdinalIgnoreCase) || f.Name.Equals(query, StringComparison.OrdinalIgnoreCase));
                embedBuilder.WithTitle(project.Name);
                embedBuilder.WithDescription(project.Description);
                embedBuilder.AddField("ID", project.Id, true);
                if (project.Download != null)
                    embedBuilder.AddField("Download", project.Download, true);

                var guild = await ctx.Client.GetGuildAsync(project.AssociatedGuild);
                embedBuilder.AddField("Discussion channel", $"<#{project.AssociatedChannelId}> ({guild.Name})");
                embedBuilder.AddField("Registrant", $"<@{project.Registrant}>");

                Dictionary<string, List<string>> roles = new Dictionary<string, List<string>>
                {
                    { "Leaders", new List<string>() },
                    { "Developers", new List<string>() },
                    { "Researchers", new List<string>() },
                    { "Designers", new List<string>() },
                    { "Translators", new List<string>() },
                    { "Sponsors/Donators", new List<string>() },
                    { "Beta Testers", new List<string>() }
                };

                foreach (var userRole in project.UserRoles)
                {
                    var userListedRoles = userRole.Value.ToString("G").Split(", ");
                    var test = userListedRoles.Select(f =>
                    {
                        var enumType = typeof(ProjectRole);
                        var memberInfos = enumType.GetMember(f);
                        var enumValueMemberInfo = memberInfos.FirstOrDefault(m => m.DeclaringType == enumType)!;
                        var valueAttributes = enumValueMemberInfo.GetCustomAttribute<ChoiceNameAttribute>()!;
                        var name = valueAttributes.Name;
                        return string.Join("/", name.Split('/').Select(x => $"{x}s"));
                    });
                    foreach (var userListedRole in test)
                        roles[userListedRole].Add($"<@{userRole.Key}>");
                }

                foreach (var role in roles.Where(f => f.Value.Count > 0))
                {
                    embedBuilder.AddField(role.Key, string.Join(", ", role.Value));
                }
            }
            else
            {
                string[] parameters = query.Split(' ');
                var filtered = listParsed
                    .Where(project =>
                        parameters.Any(parameter =>
                            project.Id.Equals(parameter, StringComparison.OrdinalIgnoreCase) ||
                            project.Name.Contains(parameter, StringComparison.OrdinalIgnoreCase)))
                    .OrderByDescending(project =>
                        parameters.Count(parameter =>
                            project.Id.Equals(parameter, StringComparison.OrdinalIgnoreCase) ||
                            project.Name.Contains(parameter)));

                if (filtered.Count() == 1)
                {
                    var project = filtered.ElementAt(0);
                    embedBuilder.WithTitle(project.Name);
                    embedBuilder.WithDescription(project.Description);
                    embedBuilder.AddField("ID", project.Id, true);
                    if (project.Download != null)
                        embedBuilder.AddField("Download", project.Download, true);

                    var guild = await ctx.Client.GetGuildAsync(project.AssociatedGuild);
                    embedBuilder.AddField("Discussion channel", $"<#{project.AssociatedChannelId}> ({guild.Name})");
                    embedBuilder.AddField("Registrant", $"<@{project.Registrant}>");

                    Dictionary<string, List<string>> roles = new Dictionary<string, List<string>>
                {
                    { "Leaders", new List<string>() },
                    { "Developers", new List<string>() },
                    { "Researchers", new List<string>() },
                    { "Designers", new List<string>() },
                    { "Translators", new List<string>() },
                    { "Sponsors/Donators", new List<string>() },
                    { "Beta Testers", new List<string>() }
                };

                    foreach (var userRole in project.UserRoles)
                    {
                        var userListedRoles = userRole.Value.ToString("G").Split(", ");
                        var test = userListedRoles.Select(f =>
                        {
                            var enumType = typeof(ProjectRole);
                            var memberInfos = enumType.GetMember(f);
                            var enumValueMemberInfo = memberInfos.FirstOrDefault(m => m.DeclaringType == enumType)!;
                            var valueAttributes = enumValueMemberInfo.GetCustomAttribute<ChoiceNameAttribute>()!;
                            var name = valueAttributes.Name;
                            return string.Join("/", name.Split('/').Select(x => $"{x}s"));
                        });
                        foreach (var userListedRole in test)
                            roles[userListedRole].Add($"<@{userRole.Key}>");
                    }

                    foreach (var role in roles.Where(f => f.Value.Count > 0))
                    {
                        embedBuilder.AddField(role.Key, string.Join(", ", role.Value));
                    }
                }
                else if (filtered.Count() > 1)
                {
                    embedBuilder.WithTitle("Multiple searches found");
                    embedBuilder.WithDescription(string.Join("\n", filtered.Select(f => f.Name)));
                }
                else
                {
                    embedBuilder.WithTitle("Error");
                    embedBuilder.WithDescription("The specified query returns no results.");
                    embedBuilder.WithColor(DiscordColor.Red);
                }
            }

            await ctx.EditResponseAsync(new DiscordWebhookBuilder().AddEmbed(embedBuilder.Build()));
        }

        private enum CheckStatus
        {
            Success,
            ProjectNonexistent,
            UserNotLeadDev
        }

        private class CheckResult
        {
            public CheckStatus Status { get; set; }

            public Project Project { get; set; }
        }

        private async Task<CheckResult> CheckUserLeadDev(ulong userId, string projectId)
        {
            var rawData = await Database.HashGetAsync("projects", projectId);
            if (rawData == RedisValue.Null)
                return new CheckResult { Status = CheckStatus.ProjectNonexistent };

            var parsedJson = JsonSerializer.Deserialize<Project>((string)rawData)!;
            if (!parsedJson.UserRoles.TryGetValue(userId, out var roles) || !roles.HasFlag(ProjectRole.LeadDev))
                return new CheckResult { Status = CheckStatus.UserNotLeadDev, Project = parsedJson };

            return new CheckResult { Status = CheckStatus.Success, Project = parsedJson };
        }

        [SlashCommand("assign", "Assign a role to a user.")]
        public async Task AssignRole(InteractionContext ctx,
            [Option("project", "The Project ID.")] string projectId,
            [Option("user", "The user to assign the role to.")] DiscordUser user,
            [Option("role", "The role to assign to the user.")] ProjectRole role)
        {
            await ctx.DeferAsync(true);
            var checkLead = await CheckUserLeadDev(ctx.User.Id, projectId);
            switch (checkLead.Status)
            {
                case CheckStatus.ProjectNonexistent:
                    await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent("The specified project ID does not exist."));
                    return;
                case CheckStatus.UserNotLeadDev:
                    await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent($"You are not a leader of **{checkLead.Project.Name}**."));
                    return;
            }

            if (checkLead.Project.UserRoles.ContainsKey(user.Id))
                checkLead.Project.UserRoles[user.Id] |= role;
            else
                checkLead.Project.UserRoles.Add(user.Id, role);

            var enumType = typeof(ProjectRole);
            var memberInfos = enumType.GetMember(role.ToString());
            var enumValueMemberInfo = memberInfos.FirstOrDefault(m => m.DeclaringType == enumType)!;
            var valueAttributes = enumValueMemberInfo.GetCustomAttribute<ChoiceNameAttribute>()!;
            var name = valueAttributes.Name;

            try
            {
                var member = await ctx.Guild.GetMemberAsync(user.Id);
                DiscordRole dRole;

                if (checkLead.Project.AssociatedDiscordRoles.TryGetValue(role, out Dictionary<ulong, ulong> associated) && associated.TryGetValue(ctx.Guild.Id, out ulong roleId))
                {
                    dRole = ctx.Guild.GetRole(roleId);
                }
                else
                {
                    dRole = await ctx.Guild.CreateRoleAsync($"{checkLead.Project.Name} {name}");
                    checkLead.Project.AssociatedDiscordRoles.GetOrAdd(role, new Dictionary<ulong, ulong>()).Add(ctx.Guild.Id, dRole.Id);
                }

                await member.GrantRoleAsync(dRole);
            }
            catch (DSharpPlus.Exceptions.NotFoundException)
            { }

            await Database.HashSetAsync("projects", checkLead.Project.Id, JsonSerializer.Serialize(checkLead.Project));
            await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent("Role assigned successfully."));
        }

        [SlashCommand("mine", "Lists projects you are a leader of.")]
        public async Task ListMine(InteractionContext ctx)
        {
            await ctx.DeferAsync();

            var projects = await Database.HashValuesAsync("projects");
            var projectsOwned = projects.Select(f => JsonSerializer.Deserialize<Project>((string)f)!).Where(f => f.UserRoles.TryGetValue(ctx.User.Id, out ProjectRole roles) && roles.HasFlag(ProjectRole.LeadDev));

            if (projectsOwned.Count() == 0)
            {
                await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent("You are not a leader of any projects."));
                return;
            }

            DiscordEmbedBuilder embedBuilder = new DiscordEmbedBuilder()
                .WithTitle("Projects led by you.");

            foreach (var project in projectsOwned)
            {
                embedBuilder.AddField(project.Name, $"ID: {project.Id}");
            }

            await ctx.EditResponseAsync(new DiscordWebhookBuilder().AddEmbed(embedBuilder));
        }

        [SlashCommand("remove-role", "Removes a role from a user.")]
        public async Task DeleteRole(
            InteractionContext ctx,
            [Option("project", "The project ID.")] string projectId,
            [Option("user", "The user to remove the role from.")] DiscordUser user,
            [Option("role", "The role to remove.")] ProjectRole role)
        {
            await ctx.DeferAsync(true);
            var checkLead = await CheckUserLeadDev(ctx.User.Id, projectId);
            switch (checkLead.Status)
            {
                case CheckStatus.ProjectNonexistent:
                    await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent("The specified project ID does not exist."));
                    return;
                case CheckStatus.UserNotLeadDev:
                    await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent($"You are not a leader of **{checkLead.Project.Name}**."));
                    return;
            }

            if (!checkLead.Project.UserRoles.TryGetValue(user.Id, out ProjectRole targetedProjectRole))
            {
                await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent("The specified user is not involved with the project."));
                return;
            }

            if (!targetedProjectRole.HasFlag(role))
            {
                await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent("The specified user does not have the specified role."));
                return;
            }

            if (role == ProjectRole.LeadDev && checkLead.Project.UserRoles.Where(f => f.Value == ProjectRole.LeadDev).Count() == 1)
            {
                await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent("All projects need to have at least one leader. Please give another user the leader role before removing the leader role from yourselves."));
                return;
            }

            if (targetedProjectRole == role)
                checkLead.Project.UserRoles.Remove(user.Id);
            else
                checkLead.Project.UserRoles[user.Id] &= ~role;

            await Database.HashSetAsync("projects", checkLead.Project.Id, JsonSerializer.Serialize(checkLead.Project));

            var enumType = typeof(ProjectRole);
            var memberInfos = enumType.GetMember(role.ToString());
            var enumValueMemberInfo = memberInfos.FirstOrDefault(m => m.DeclaringType == enumType)!;
            var valueAttributes = enumValueMemberInfo.GetCustomAttribute<ChoiceNameAttribute>()!;
            var name = valueAttributes.Name;

            try
            {
                var member = await ctx.Guild.GetMemberAsync(user.Id);

                var dRole = ctx.Guild.Roles.Select(f => f.Value).FirstOrDefault(f => f.Name == $"{checkLead.Project.Name} {name}");

                await member.RevokeRoleAsync(dRole);
            }
            catch (DSharpPlus.Exceptions.NotFoundException)
            { }

            await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent("Role removed successfully."));
        }

        [SlashCommand("modify", "Modify a project.")]
        public async Task Modify(
            InteractionContext ctx,
            [Option("project", "The project ID.")] string projectId,
            [Option("new-name", "The new project name.")] string? newName = null,
            [Option("new-description", "The new description.")] string? newDescription = null,
            [Option("new-download", "The new download link. To remove it type \"REMOVE\" exactly without quotes.")] string? newDownload = null,
            [Option("change-channel-topic", "Whether to change the associated channel topic.")] bool? shouldChangeChannelTopic = null
        )
        {
            await ctx.DeferAsync(true);
            var checkLead = await CheckUserLeadDev(ctx.User.Id, projectId);
            switch (checkLead.Status)
            {
                case CheckStatus.ProjectNonexistent:
                    await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent("The specified project ID does not exist."));
                    return;
                case CheckStatus.UserNotLeadDev:
                    await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent($"You are not a leader of **{checkLead.Project.Name}**."));
                    return;
            }

            if (newDescription == null && newDownload == null && newName == null)
            {
                await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent("There are nothing for me to modify."));
                return;
            }

            if ((newDescription != null || newDownload != null) && shouldChangeChannelTopic == null)
            {
                await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent("You have specified a new download link and/or a new project description but haven't specified whether to change the channel topic. Please specify `change-channel-topic`."));
                return;
            }
            else if (newDescription != null || newDownload != null)
            {
                if (newDescription != null)
                    checkLead.Project.Description = newDescription;

                if (newDownload != null && newDownload != "REMOVE")
                {
                    bool isValidUrl =
                        Uri.TryCreate(newDownload, UriKind.Absolute, out Uri? uriResult) &&
                        (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);
                    if (!isValidUrl)
                    {
                        await ctx.EditResponseAsync(
                            new DiscordWebhookBuilder()
                            .WithContent("The specified download URL is invalid."));
                        return;
                    }

                    checkLead.Project.Download = newDownload;
                }
                else if (newDownload == "REMOVE")
                    checkLead.Project.Download = null;

                await Database.HashSetAsync("projects", checkLead.Project.Id, JsonSerializer.Serialize(checkLead.Project));

                if (shouldChangeChannelTopic!.Value)
                {
                    string newChannelTopic = checkLead.Project.Description;

                    if (checkLead.Project.Download != null)
                        newChannelTopic += $"\n\n**Download link:** {checkLead.Project.Download}";

                    var guild = await ctx.Client.GetGuildAsync(checkLead.Project.AssociatedGuild);
                    var channel = guild.GetChannel(checkLead.Project.AssociatedChannelId);
                    await channel.ModifyAsync(f => f.Topic = newChannelTopic);
                }
            }

            if (newName != null)
            {
                DiscordEmbed embed
                    = new DiscordEmbedBuilder()
                    .WithTitle("Project rename request")
                    .AddField("Previous name", checkLead.Project.Name)
                    .AddField("New name", newName)
                    .Build();

                DiscordMessageBuilder msgBuilder = new DiscordMessageBuilder()
                    .WithContent("New project rename request.")
                    .WithEmbed(embed)
                    .AddComponents(
                        new DiscordButtonComponent(ButtonStyle.Success, $"acceptRename_{ctx.User.Id}_{checkLead.Project.Id}", "Accept"),
                        new DiscordButtonComponent(ButtonStyle.Danger, $"rejectRename_{ctx.User.Id}_{checkLead.Project.Id}", "Reject")
                    );

                var channel = await ctx.Client.GetChannelAsync(Config[ctx.Guild.Id].RequestsChannelId);
                await channel.SendMessageAsync(msgBuilder);
            }

            string response;
            if ((newDescription != null || newDownload != null) && newName != null)
                response = "The description and/or download link has been updated. Awaiting name change. Make sure you allowed DMs from server members for the bot to notify you whether the project name change has been approved.";
            else if (newName != null)
                response = "Awaiting name change. Make sure you allowed DMs from server members for the bot to notify you whether the project name change has been approved.";
            else
                response = "The description and/or download link has been updated.";

            await ctx.EditResponseAsync(new DiscordWebhookBuilder().WithContent(response));
        }
    }
}
