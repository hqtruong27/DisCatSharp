﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.CommandsNext.Exceptions;

namespace DSharpPlus.CommandsNext
{
    /// <summary>
    /// This is the class which handles command registration, management, and execution. 
    /// </summary>
    public class CommandsNextModule : IModule
    {
        #region Events
        /// <summary>
        /// Triggered whenever a command executes successfully.
        /// </summary>
        public event AsyncEventHandler<CommandExecutedEventArgs> CommandExecuted
        {
            add { this._executed.Register(value); }
            remove { this._executed.Unregister(value); }
        }
        private AsyncEvent<CommandExecutedEventArgs> _executed;

        /// <summary>
        /// Triggered whenever a command throws an exception during execution.
        /// </summary>
        public event AsyncEventHandler<CommandErrorEventArgs> CommandErrored
        {
            add { this._error.Register(value); }
            remove { this._error.Unregister(value); }
        }
        private AsyncEvent<CommandErrorEventArgs> _error;

        private async Task OnCommandExecuted(CommandExecutedEventArgs e) =>
            await this._executed.InvokeAsync(e).ConfigureAwait(false);

        private async Task OnCommandErrored(CommandErrorEventArgs e) =>
            await this._error.InvokeAsync(e).ConfigureAwait(false);
        #endregion

        private CommandsNextConfiguration Config { get; set; }
        private const string GROUP_COMMAND_METHOD_NAME = "ExecuteGroup";

        public CommandsNextModule(CommandsNextConfiguration cfg)
        {
            this.Config = cfg;
            this.TopLevelCommands = new Dictionary<string, Command>();
            this._registered_commands_lazy = new Lazy<IReadOnlyDictionary<string, Command>>(() => new ReadOnlyDictionary<string, Command>(this.TopLevelCommands));
        }

        #region DiscordClient Registration
        /// <summary>
        /// Gets the instance of <see cref="DiscordClient"/> for which this module is registered.
        /// </summary>
        public DiscordClient Client { get { return this._client; } }
        private DiscordClient _client;

        /// <summary>
        /// DO NOT USE THIS MANUALLY.
        /// </summary>
        /// <param name="client">DO NOT USE THIS MANUALLY.</param>
        /// <exception cref="InvalidOperationException"/>
        public void Setup(DiscordClient client)
        {
            if (this._client != null)
                throw new InvalidOperationException("What did I tell you?");

            this._client = client;

            this._executed = new AsyncEvent<CommandExecutedEventArgs>(this.Client.EventErrorHandler, "COMMAND_EXECUTED");
            this._error = new AsyncEvent<CommandErrorEventArgs>(this.Client.EventErrorHandler, "COMMAND_ERRORED");

            this.Client.MessageCreated += this.HandleCommands;

            if (this.Config.EnableDefaultHelp)
            {
                var dlg = new Func<CommandContext, string[], Task>(this.DefaultHelp);
                var mi = dlg.GetMethodInfo();
                this.MakeCallable(mi, dlg.Target, out var cbl, out var args);

                var attrs = mi.GetCustomAttributes();
                if (!attrs.Any(xa => xa.GetType() == typeof(CommandAttribute)))
                    return;

                var cmd = new Command();

                var cbas = new List<ConditionBaseAttribute>();
                foreach (var xa in attrs)
                {
                    switch (xa)
                    {
                        case CommandAttribute c:
                            cmd.Name = c.Name;
                            break;

                        case AliasesAttribute a:
                            cmd.Aliases = a.Aliases;
                            break;

                        case ConditionBaseAttribute p:
                            cbas.Add(p);
                            break;

                        case DescriptionAttribute d:
                            cmd.Description = d.Description;
                            break;

                        case HiddenAttribute h:
                            cmd.IsHidden = true;
                            break;
                    }
                }
                cmd.ExecutionChecks = new ReadOnlyCollection<ConditionBaseAttribute>(cbas);
                cmd.Arguments = args;
                cmd.Callable = cbl;

                this.AddToCommandDictionary(cmd);
            }
        }
        #endregion

        #region Command Handler
        private async Task HandleCommands(MessageCreateEventArgs e)
        {
            // Let the bot do its things
            await Task.Yield();

            if (e.Author.IsBot) // bad bot
                return;

            if (!this.Config.EnableDms && e.Channel.IsPrivate)
                return;

            if (this.Config.SelfBot && e.Author.Id != this.Client.CurrentUser.Id)
                return;

            var mpos = -1;
            if (this.Config.EnableMentionPrefix)
                mpos = e.Message.GetMentionPrefixLength(this.Client.CurrentUser);

            if (mpos == -1 && !string.IsNullOrWhiteSpace(this.Config.StringPrefix))
                mpos = e.Message.GetStringPrefixLength(this.Config.StringPrefix);

            if (mpos == -1 && this.Config.CustomPrefixPredicate != null)
                mpos = this.Config.CustomPrefixPredicate(e.Message);

            if (mpos == -1)
                return;

            var cnt = e.Message.Content;
            var cmi = cnt.IndexOf(' ', mpos);
            var cms = cmi != -1 ? cnt.Substring(mpos, cmi - mpos) : cnt.Substring(mpos);
            var rrg = cmi != -1 ? cnt.Substring(cmi + 1) : "";
            var arg = CommandsNextUtilities.SplitArguments(rrg);

            var cmd = this.TopLevelCommands.ContainsKey(cms) ? this.TopLevelCommands[cms] : null;
            if (cmd == null && !this.Config.CaseSensitive)
                cmd = this.TopLevelCommands.FirstOrDefault(xkvp => xkvp.Key.ToLower() == cms.ToLower()).Value;

            var ctx = new CommandContext
            {
                Client = this.Client,
                Command = cmd,
                Message = e.Message,
                RawArguments = new ReadOnlyCollection<string>(arg.ToList()),
                Config = this.Config
            };

            if (cmd == null)
            {
                await this._error.InvokeAsync(new CommandErrorEventArgs { Context = ctx, Exception = new CommandNotFoundException("Specified command was not found.", cms) });
                return;
            }

#pragma warning disable 4014
            Task.Run(async () =>
            {
                try
                {
                    if (cmd.ExecutionChecks != null && cmd.ExecutionChecks.Any())
                        foreach (var ec in cmd.ExecutionChecks)
                            if (!(await ec.CanExecute(ctx)))
                                throw new ChecksFailedException("One or more execution pre-checks failed.", cmd, ctx);

                    await cmd.Execute(ctx);
                    await this._executed.InvokeAsync(new CommandExecutedEventArgs { Context = ctx });
                }
                catch (Exception ex)
                {
                    await this._error.InvokeAsync(new CommandErrorEventArgs { Context = ctx, Exception = ex });
                }
            });
#pragma warning restore 4014
        }
        #endregion

        #region Command Registration
        private Dictionary<string, Command> TopLevelCommands { get; set; }
        private Lazy<IReadOnlyDictionary<string, Command>> _registered_commands_lazy;
        public IReadOnlyDictionary<string, Command> RegisteredCommands => this._registered_commands_lazy.Value;

        public void RegisterCommands<T>() where T : new()
        {
            var t = typeof(T);
            RegisterCommands(t, new T(), null, out var tres, out var tcmds);
            
            if (tres != null)
                this.AddToCommandDictionary(tres);

            if (tcmds != null)
                foreach (var xc in tcmds)
                    this.AddToCommandDictionary(xc);
        }

        private void RegisterCommands(Type t, object inst, CommandGroup currentparent, out CommandGroup result, out IReadOnlyCollection<Command> commands)
        {
            var ti = t.GetTypeInfo();

            // check if we are anything
            var mdl_attrs = ti.GetCustomAttributes();
            var is_mdl = false;
            var mdl_name = "";
            var mdl_aliases = (IReadOnlyCollection<string>)null;
            var mdl_hidden = false;
            var mdl_desc = "";
            var mdl_chks = new List<ConditionBaseAttribute>();
            var mdl_cbl = (Delegate)null;
            var mdl_args = (IReadOnlyList<CommandArgument>)null;
            var mdl = (CommandGroup)null;
            foreach (var xa in mdl_attrs)
            {
                switch (xa)
                {
                    case GroupAttribute g:
                        is_mdl = true;
                        mdl_name = g.Name;
                        if (g.CanInvokeWithoutSubcommand)
                            this.MakeCallableModule(ti, inst, out mdl_cbl, out mdl_args);
                        break;

                    case AliasesAttribute a:
                        mdl_aliases = a.Aliases;
                        break;

                    case HiddenAttribute h:
                        mdl_hidden = true;
                        break;

                    case DescriptionAttribute d:
                        mdl_desc = d.Description;
                        break;

                    case ConditionBaseAttribute c:
                        mdl_chks.Add(c);
                        break;
                }
            }

            if (is_mdl)
                mdl = new CommandGroup
                {
                    Name = mdl_name,
                    Aliases = mdl_aliases,
                    Description = mdl_desc,
                    ExecutionChecks = new ReadOnlyCollection<ConditionBaseAttribute>(mdl_chks),
                    IsHidden = mdl_hidden,
                    Parent = null,
                    Callable = mdl_cbl,
                    Arguments = mdl_args,
                    Children = null
                };

            // candidate methods
            var ms = ti.DeclaredMethods
                .Where(xm => xm.IsPublic && !xm.IsStatic && xm.Name != GROUP_COMMAND_METHOD_NAME);
            var cmds = new List<Command>();
            foreach (var m in ms)
            {
                if (m.ReturnType != typeof(Task))
                    continue;

                var ps = m.GetParameters();
                if (!ps.Any() || ps.First().ParameterType != typeof(CommandContext))
                    continue;

                var attrs = m.GetCustomAttributes();
                if (!attrs.Any(xa => xa.GetType() == typeof(CommandAttribute)))
                    continue;

                var cmd = new Command();

                var cbas = new List<ConditionBaseAttribute>();
                foreach (var xa in attrs)
                {
                    switch (xa)
                    {
                        case CommandAttribute c:
                            cmd.Name = c.Name;
                            break;

                        case AliasesAttribute a:
                            cmd.Aliases = a.Aliases;
                            break;

                        case ConditionBaseAttribute p:
                            cbas.Add(p);
                            break;

                        case DescriptionAttribute d:
                            cmd.Description = d.Description;
                            break;

                        case HiddenAttribute h:
                            cmd.IsHidden = true;
                            break;
                    }
                }
                cmd.ExecutionChecks = new ReadOnlyCollection<ConditionBaseAttribute>(cbas);
                cmd.Parent = mdl;
                MakeCallable(m, inst, out var cbl, out var args);
                cmd.Callable = cbl;
                cmd.Arguments = args;

                cmds.Add(cmd);
            }

            // candidate types
            var ts = ti.DeclaredNestedTypes
                .Where(xt => xt.DeclaredConstructors.Any(xc => !xc.GetParameters().Any() || xc.IsPublic));
            foreach (var xt in ts)
            {
                this.RegisterCommands(xt.AsType(), Activator.CreateInstance(xt.AsType()), mdl, out var tmdl, out var tcmds);

                if (tmdl != null)
                    cmds.Add(tmdl);
                cmds.AddRange(tcmds);
            }

            commands = new ReadOnlyCollection<Command>(cmds);
            if (mdl != null)
                mdl.Children = commands;
            result = mdl;
        }

        private void MakeCallable(MethodInfo mi, object inst, out Delegate cbl, out IReadOnlyList<CommandArgument> args)
        {
            if (mi == null)
                throw new MissingMethodException("Specified method does not exist.");

            if (mi.IsStatic || !mi.IsPublic)
                throw new InvalidOperationException("Specified method is invalid, static, or not public.");

            var ps = mi.GetParameters();
            if (!ps.Any() || ps.First().ParameterType != typeof(CommandContext) || mi.ReturnType != typeof(Task))
                throw new InvalidOperationException("Specified method has an invalid signature.");

            var ei = Expression.Constant(inst);

            var ea = new ParameterExpression[ps.Length];
            ea[0] = Expression.Parameter(typeof(CommandContext), "ctx");

            var i = 1;
            var ps1 = ps.Skip(1);
            var argsl = new List<CommandArgument>(ps.Length - 1);
            foreach (var xp in ps1)
            {
                var ca = new CommandArgument
                {
                    Name = xp.Name,
                    Type = xp.ParameterType,
                    IsOptional = xp.IsOptional,
                    DefaultValue = xp.IsOptional ? xp.DefaultValue : null
                };
                if (i > 1 && !ca.IsOptional && argsl[i - 2].IsOptional)
                    throw new InvalidOperationException("Non-optional argument cannot appear after an optional one");

                var attrs = xp.GetCustomAttributes();
                foreach (var xa in attrs)
                {
                    switch (xa)
                    {
                        case DescriptionAttribute d:
                            ca.Description = d.Description;
                            break;

                        case ParamArrayAttribute p:
                            ca.IsCatchAll = true;
                            ca.Type = xp.ParameterType.GetElementType();
                            break;
                    }
                }

                argsl.Add(ca);
                ea[i++] = Expression.Parameter(xp.ParameterType, xp.Name);
            }

            var ec = Expression.Call(ei, mi, ea);
            var el = Expression.Lambda(ec, ea);

            cbl = el.Compile();
            args = new ReadOnlyCollection<CommandArgument>(argsl);
        }

        private void MakeCallableModule(TypeInfo ti, object inst, out Delegate cbl, out IReadOnlyList<CommandArgument> args)
        {
            var mtd = ti.GetDeclaredMethod(GROUP_COMMAND_METHOD_NAME);
            if (mtd == null)
                throw new MissingMethodException($"A group marked with CanExecute must have a method named {GROUP_COMMAND_METHOD_NAME}.");

            this.MakeCallable(mtd, inst, out cbl, out args);
        }

        private void AddToCommandDictionary(Command cmd)
        {
            if (cmd.Parent != null)
                return;

            if (this.TopLevelCommands.ContainsKey(cmd.Name) || (cmd.Aliases != null && cmd.Aliases.Any(xs => this.TopLevelCommands.ContainsKey(xs))))
                throw new CommandExistsException("Given command name is already registered.", cmd.Name);

            this.TopLevelCommands[cmd.Name] = cmd;
            if (cmd.Aliases != null)
                foreach (var xs in cmd.Aliases)
                    this.TopLevelCommands[xs] = cmd;
        }
        #endregion

        #region Default Help
        [Command("help"), Description("Displays command help.")]
        public async Task DefaultHelp(CommandContext ctx, [Description("Command to provide help for.")] params string[] command)
        {
            var toplevel = this.TopLevelCommands.Values.Distinct();
            var embed = new DiscordEmbed()
            {
                Color = 0x007FFF,
                Title = "Help",
                Fields = new List<DiscordEmbedField>()
            };

            if (command != null && command.Any())
            {
                var cmd = (Command)null;
                var search_in = toplevel;
                foreach (var c in command)
                {
                    if (search_in == null)
                    {
                        cmd = null;
                        break;
                    }

                    if (this.Config.CaseSensitive)
                        cmd = search_in.FirstOrDefault(xc => xc.Name == c || (xc.Aliases != null && xc.Aliases.Contains(c)));
                    else
                        cmd = search_in.FirstOrDefault(xc => xc.Name.ToLower() == c.ToLower() || (xc.Aliases != null && xc.Aliases.Select(xs => xs.ToLower()).Contains(c.ToLower())));

                    if (cmd == null)
                        break;

                    var ce = true;
                    foreach (var ec in cmd.ExecutionChecks)
                    {
                        ce &= await ec.CanExecute(ctx);
                        if (!ce)
                            break;
                    }
                    if (!ce)
                        throw new ChecksFailedException("You cannot access that command!", cmd, ctx);

                    if (cmd is CommandGroup)
                        search_in = (cmd as CommandGroup).Children;
                    else
                        search_in = null;
                }

                if (cmd == null)
                    throw new CommandNotFoundException("Specified command was not found!", string.Join(" ", command));

                embed.Description = string.Concat("`", cmd.QualifiedName, "`: ", string.IsNullOrWhiteSpace(cmd.Description) ? "No description provided." : cmd.Description);

                if (cmd is CommandGroup g && g.Callable != null)
                    embed.Description = string.Concat(embed.Description, "\n\nThis group can be executed as a standalone command.");

                if (cmd.Aliases != null && cmd.Aliases.Any())
                    embed.Fields.Add(new DiscordEmbedField
                    {
                        Inline = false,
                        Name = "Aliases",
                        Value = string.Join(", ", cmd.Aliases.Select(xs => string.Concat("`", xs, "`")))
                    });

                if (cmd.Arguments != null && cmd.Arguments.Any())
                {
                    var args = string.Empty;
                    var sb = new StringBuilder();

                    foreach (var arg in cmd.Arguments)
                    {
                        if (arg.IsOptional || arg.IsCatchAll)
                            sb.Append("`[");
                        else
                            sb.Append("`<");

                        sb.Append(arg.Name);

                        if (arg.IsCatchAll)
                            sb.Append("...");

                        if (arg.IsOptional || arg.IsCatchAll)
                            sb.Append("]: ");
                        else
                            sb.Append(">: ");

                        sb.Append(arg.Type.ToUserFriendlyName()).Append("`: ");

                        sb.Append(string.IsNullOrWhiteSpace(arg.Description) ? "No description provided." : arg.Description);

                        if (arg.IsOptional)
                            sb.Append(" Default value: ").Append(arg.DefaultValue);

                        sb.AppendLine();
                    }
                    args = sb.ToString();

                    embed.Fields.Add(new DiscordEmbedField
                    {
                        Inline = false,
                        Name = "Arguments",
                        Value = args
                    });
                }

                if (cmd is CommandGroup gx)
                {
                    var sxs = gx.Children.Where(xc => !xc.IsHidden);
                    var scs = new List<Command>();
                    foreach (var sc in sxs)
                    {
                        if (sc.ExecutionChecks == null || !sc.ExecutionChecks.Any())
                        {
                            scs.Add(sc);
                            continue;
                        }

                        var ce = true;
                        foreach (var ec in sc.ExecutionChecks)
                        {
                            ce &= await ec.CanExecute(ctx);
                            if (!ce)
                                break;
                        }
                        if (ce)
                            scs.Add(sc);
                    }

                    if (scs.Any())
                        embed.Fields.Add(new DiscordEmbedField
                        {
                            Inline = false,
                            Name = "Subcommands",
                            Value = string.Join(", ", scs.Select(xc => string.Concat("`", xc.QualifiedName, "`")))
                        });
                }
            }
            else
            {
                var sxs = toplevel.Where(xc => !xc.IsHidden);
                var scs = new List<Command>();
                foreach (var sc in sxs)
                {
                    if (sc.ExecutionChecks == null || !sc.ExecutionChecks.Any())
                    { 
                        scs.Add(sc);
                        continue;
                    }

                    var ce = true;
                    foreach (var ec in sc.ExecutionChecks)
                    {
                        ce &= await ec.CanExecute(ctx);
                        if (!ce)
                            break;
                    }
                    if (ce)
                        scs.Add(sc);
                }

                embed.Description = "Listing all top-level commands and groups. Specify a command to see more information.";
                if (scs.Any())
                    embed.Fields.Add(new DiscordEmbedField
                    {
                        Inline = false,
                        Name = "Commands",
                        Value = string.Join(", ", scs.Select(xc => string.Concat("`", xc.QualifiedName, "`")))
                    });
            }

            await ctx.RespondAsync("", embed: embed);
        }
        #endregion
    }
}
