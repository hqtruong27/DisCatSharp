// This file is part of the DisCatSharp project, based off DSharpPlus.
//
// Copyright (c) 2021-2022 AITSYS
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using System;
using System.Collections.Generic;

using DisCatSharp.Entities;
using DisCatSharp.EventArgs;

namespace DisCatSharp.ApplicationCommands.EventArgs
{
	/// <summary>
	/// Represents arguments for a <see cref="ApplicationCommandsExtension.GlobalApplicationCommandsRegistered"/> event.
	/// </summary>
	public class GlobalApplicationCommandsRegisteredEventArgs : DiscordEventArgs
	{
		/// <summary>
		/// Gets all registered global commands.
		/// </summary>
		public IReadOnlyList<DiscordApplicationCommand> RegisteredCommands { get; internal set; }

		/// <summary>
		/// Initializes a new instance of the <see cref="GlobalApplicationCommandsRegisteredEventArgs"/> class.
		/// </summary>
		/// <param name="provider">The provider.</param>
		internal GlobalApplicationCommandsRegisteredEventArgs(IServiceProvider provider) : base(provider)
		{ }
	}
}
