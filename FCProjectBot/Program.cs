using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Reflection;
using System.Text.Json;

IDatabase database = ConnectionMultiplexer.Connect($"{Environment.GetEnvironmentVariable("PROJECTS_REDIS_HOST")}:{Environment.GetEnvironmentVariable("PROJECTS_REDIS_PORT")}").GetDatabase();

DiscordConfiguration config = new()
{
    Token = Environment.GetEnvironmentVariable("PROJECTS_BOT_TOKEN"),
    TokenType = TokenType.Bot,
    Intents = DiscordIntents.DirectMessages | DiscordIntents.Guilds | DiscordIntents.GuildMessages
};

var client = new DiscordClient(config);

var configDictionary = JsonSerializer.Deserialize<Dictionary<ulong, ConfigJson>>(File.ReadAllText("config.json"))!;

// var configJson = new ConfigJson() { RequestsChannelId = 883396360831901809, ProjectsCategoryId = 924979535089401876, FallbackNotifyChannel = 883395955364343808 };

IServiceCollection servCollection
    = new ServiceCollection()
    .AddSingleton(configDictionary)
    .AddScoped<IDatabase>(_ => database);

var slashConfig = new SlashCommandsConfiguration()
{
    Services = servCollection.BuildServiceProvider()
};

var slashCommandExtension = client.UseSlashCommands(slashConfig);
foreach (var guild in configDictionary.Keys)
{
    slashCommandExtension.RegisterCommands<FCProjectBot.Commands.SlashCommands>(guild);
}
slashCommandExtension.SlashCommandErrored += (s, e) =>
{
    return Task.Run(() => s.Client.Logger.LogError(e.Exception.ToString()));
};

client.ComponentInteractionCreated += (_, e) =>
{
    Task.Run(async () =>
    {
        try
        {
            await e.Interaction
    .CreateResponseAsync(
        InteractionResponseType.DeferredChannelMessageWithSource,
        new DiscordInteractionResponseBuilder().AsEphemeral(true));
            DiscordMessageBuilder builder;
            switch (e.Id)
            {
                case "acceptRequest":
                    var applicantMention = e.Message.Embeds[0].Fields[1].Value;
                    var applicantId = ulong.Parse(new string(applicantMention.Where(c => char.IsDigit(c)).ToArray()));
                    var applicantMember = await e.Guild.GetMemberAsync(applicantId);
                    var projectname = e.Message.Embeds[0].Fields[0].Value;
                    var download = e.Message.Embeds[0].Fields.FirstOrDefault(f => f.Name == "Download link")?.Value;
                    var channelMention = e.Message.Embeds[0].Fields.FirstOrDefault(f => f.Name == "Associated channel")?.Value;
                    ulong? channelId = !string.IsNullOrEmpty(channelMention) ? ulong.Parse(new string(channelMention.Where(c => char.IsDigit(c)).ToArray())) : null;
                    var description = e.Message.Embeds[0].Fields.Last().Value;
                    Project project = new()
                    {
                        Download = download,
                        Name = projectname,
                        Description = description,
                        Registrant = applicantId
                    };
                    project.UserRoles.Add(applicantId, ProjectRole.LeadDev);

                    var role = await e.Guild.CreateRoleAsync($"{projectname} Leader");

                    project.AssociatedDiscordRoles.Add(ProjectRole.LeadDev, new() { { e.Guild.Id, role.Id } });

                    string topic = description;
                    if (download != null)
                        topic += $"\n\n**Download link:** {download}";
                    var chanName = new string(projectname.ToLower().Select(f => char.IsLetterOrDigit(f) ? f : '-').ToArray());

                    if (channelId == null)
                    {
                        var chan = await e.Guild.
    CreateTextChannelAsync(
        chanName,
        e.Guild.GetChannel(configDictionary[e.Guild.Id].ProjectsCategoryId),
        topic);
                        await chan.ModifyAsync(f =>
                        {
                            f.PermissionOverwrites =
                                new DiscordOverwriteBuilder[]
                                { new DiscordOverwriteBuilder(role)
                        { Allowed = Permissions.ManageChannels } };
                        });

                        project.AssociatedChannelId = chan.Id;
                    }
                    else
                    {
                        project.AssociatedChannelId = channelId.Value;
                    }
                    project.AssociatedGuild = e.Guild.Id;

                    await applicantMember.GrantRoleAsync(role);

                    await database.HashSetAsync("projects", project.Id, JsonSerializer.Serialize(project));

                    builder = new DiscordMessageBuilder()
                        .WithContent($"Request accepted by {e.User.Mention}.")
                        .WithEmbed(e.Message.Embeds[0]);

                    await e.Message.ModifyAsync(builder);
                    try
                    {
                        await applicantMember.SendMessageAsync($"Your project **{projectname}** has been accepted!\n**Project ID:** {project.Id}");
                    }
                    catch
                    {
                        await e.Guild.GetChannel(configDictionary[e.Guild.Id].FallbackNotifyChannel).SendMessageAsync($"{applicantMention}, your project **{projectname}** has been accepted!\n**Project ID:** {project.Id}");
                    }
                    await e.Interaction
                        .EditOriginalResponseAsync(
                            new DiscordWebhookBuilder()
                                .WithContent("Request accepted and applicant notified."));
                    return;

                case "rejectRequest":
                    var applicantMention2 = e.Message.Embeds[0].Fields[1].Value;
                    var applicantId2 = ulong.Parse(new string(applicantMention2.Where(c => char.IsDigit(c)).ToArray()));
                    var applicantMember2 = await e.Guild.GetMemberAsync(applicantId2);
                    var projectname2 = e.Message.Embeds[0].Fields[0].Value;
                    builder = new DiscordMessageBuilder()
                        .WithContent($"Request rejected by {e.User.Mention}.")
                        .WithEmbed(e.Message.Embeds[0]);
                    await e.Message.ModifyAsync(builder);
                    try
                    {
                        await applicantMember2.SendMessageAsync($"Your project **{projectname2}** has been rejected.");
                    }
                    catch
                    {
                        await e.Guild.GetChannel(configDictionary[e.Guild.Id].FallbackNotifyChannel).SendMessageAsync($"{applicantMention2}, your project **{projectname2}** has been rejected.");
                    }
                    await e.Interaction
                        .EditOriginalResponseAsync(
                            new DiscordWebhookBuilder()
                                .WithContent("Request rejected and applicant notified."));
                    return;
            }

            if (e.Id.StartsWith("acceptRename_"))
            {
                var sentPayloads = e.Id.Replace("acceptRename_", "").Split('_');
                var newName = e.Message.Embeds[0].Fields[1].Value;
                Project proj = JsonSerializer.Deserialize<Project>(await database.HashGetAsync("projects", sentPayloads[1]))!;
                var chan = await client.GetChannelAsync(proj.AssociatedChannelId);
                if (chan.Name == new string(proj.Name.ToLower().Select(f => char.IsLetterOrDigit(f) ? f : '-').ToArray()))
                {
                    await chan.ModifyAsync(f => f.Name = new string(newName.ToLower().Select(f => char.IsLetterOrDigit(f) ? f : '-').ToArray()));
                }
                proj.Name = newName;
                await database.HashSetAsync("projects", sentPayloads[1], JsonSerializer.Serialize(proj));

                foreach (var role in proj.AssociatedDiscordRoles)
                {
                    var enumType = typeof(ProjectRole);
                    var memberInfos = enumType.GetMember(role.Key.ToString());
                    var enumValueMemberInfo = memberInfos.FirstOrDefault(m => m.DeclaringType == enumType)!;
                    var valueAttributes = enumValueMemberInfo.GetCustomAttribute<ChoiceNameAttribute>()!;
                    var roleName = valueAttributes.Name;

                    foreach (var disRole in role.Value)
                    {
                        var guild = await client.GetGuildAsync(disRole.Key);
                        var discordrole = guild.GetRole(disRole.Value);
                        await discordrole.ModifyAsync($"{newName} {roleName}");
                    }
                }

                try
                {
                    var member = await e.Guild.GetMemberAsync(ulong.Parse(sentPayloads[0]));
                    await member.SendMessageAsync($"Your project **{proj.Name}** has been renamed!");
                }
                catch
                {
                    await (await client.GetChannelAsync(configDictionary[e.Guild.Id].FallbackNotifyChannel)).SendMessageAsync($"<@{sentPayloads[0]}>, your project **{proj.Name}** has been renamed!");
                }

                builder = new DiscordMessageBuilder()
                        .WithContent($"Rename request accepted by {e.User.Mention}.")
                        .WithEmbed(e.Message.Embeds[0]);

                await e.Message.ModifyAsync(builder);

                await e.Interaction.EditOriginalResponseAsync(new DiscordWebhookBuilder().WithContent("Project renamed and responsible leader notified."));
            }
            else if (e.Id.StartsWith("rejectRename_"))
            {
                var sentPayloads = e.Id.Replace("rejectRename_", "").Split('_');
                var newName = e.Message.Embeds[0].Fields[1].Value;
                Project proj = JsonSerializer.Deserialize<Project>(await database.HashGetAsync("projects", sentPayloads[1]))!;
                try
                {
                    var member = await e.Guild.GetMemberAsync(ulong.Parse(sentPayloads[0]));
                    await member.SendMessageAsync($"Your project **{proj.Name}** has been denied from being renamed.");
                }
                catch
                {
                    await (await client.GetChannelAsync(configDictionary[e.Guild.Id].FallbackNotifyChannel)).SendMessageAsync($"<@{sentPayloads[0]}>, your project **{proj.Name}** has been denied from being renamed.");
                }

                builder = new DiscordMessageBuilder()
                        .WithContent($"Rename request rejected by {e.User.Mention}.")
                        .WithEmbed(e.Message.Embeds[0]);

                await e.Interaction.EditOriginalResponseAsync(new DiscordWebhookBuilder().WithContent("Project reneame request rejected and responsible leader notified."));
            }
        }
        catch (Exception ex)
        {
            client.Logger.LogError(ex.ToString());
        }
    });

    return Task.CompletedTask;
};

await client.ConnectAsync();

await Task.Delay(-1);