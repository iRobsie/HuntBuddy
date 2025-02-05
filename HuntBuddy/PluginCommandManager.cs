﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Dalamud.Game.Command;
using HuntBuddy.Attributes;

namespace HuntBuddy
{
	public class PluginCommandManager<THost> : IDisposable
	{
		private readonly CommandManager commandManager;
		private readonly (string, CommandInfo)[] pluginCommands;
		private readonly THost host;

		public PluginCommandManager(THost host, CommandManager commandManager)
		{
			this.commandManager = commandManager;
			this.host = host;

			this.pluginCommands = host!.GetType().GetMethods(
					BindingFlags.NonPublic | BindingFlags.Public |
					BindingFlags.Static | BindingFlags.Instance)
				.Where(method => method.GetCustomAttribute<CommandAttribute>() != null)
				.SelectMany(this.GetCommandInfoTuple)
				.ToArray();

			this.AddCommandHandlers();
		}

		// http://codebetter.com/patricksmacchia/2008/11/19/an-easy-and-efficient-way-to-improve-net-code-performances/
		// Benchmarking this myself gave similar results, so I'm doing this to somewhat counteract using reflection to access command attributes.
		// I like the convenience of attributes, but in principle it's a bit slower to use them as opposed to just initializing CommandInfos directly.
		// It's usually sub-1 millisecond anyways, though. It probably doesn't matter at all.
		private void AddCommandHandlers()
		{
			foreach (var t in this.pluginCommands)
			{
				var (command, commandInfo) = t;
				this.commandManager.AddHandler(command, commandInfo);
			}
		}

		private void RemoveCommandHandlers()
		{
			foreach (var t in this.pluginCommands)
			{
				var (command, _) = t;
				this.commandManager.RemoveHandler(command);
			}
		}

		private IEnumerable<(string, CommandInfo)> GetCommandInfoTuple(MethodInfo method)
		{
			var handlerDelegate = (CommandInfo.HandlerDelegate)Delegate.CreateDelegate(
				typeof(CommandInfo.HandlerDelegate),
				this.host,
				method);

			var command = handlerDelegate.Method.GetCustomAttribute<CommandAttribute>();
			var aliases = handlerDelegate.Method.GetCustomAttribute<AliasesAttribute>();
			var helpMessage = handlerDelegate.Method.GetCustomAttribute<HelpMessageAttribute>();
			var doNotShowInHelp = handlerDelegate.Method.GetCustomAttribute<DoNotShowInHelpAttribute>();

			var commandInfo = new CommandInfo(handlerDelegate)
			{
				HelpMessage = helpMessage?.HelpMessage ?? string.Empty,
				ShowInHelp = doNotShowInHelp == null,
			};

			// Create list of tuples that will be filled with one tuple per alias, in addition to the base command tuple.
			var commandInfoTuples = new List<(string, CommandInfo)> { (command!.Command, commandInfo) };
			if (aliases == null)
			{
				return commandInfoTuples;
			}

			// ReSharper disable once LoopCanBeConvertedToQuery
			foreach (var t in aliases.Aliases)
			{
				commandInfoTuples.Add((t, commandInfo));
			}

			return commandInfoTuples;
		}

		public void Dispose()
		{
			this.RemoveCommandHandlers();
			GC.SuppressFinalize(this);
		}
	}
}