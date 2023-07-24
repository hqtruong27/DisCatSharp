// This file is part of the DisCatSharp project, based off DSharpPlus.
//
// Copyright (c) 2021-2023 AITSYS
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
// FITNESS FOR A PARTICULAR PURPOSE AND NON-INFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using DisCatSharp.Enums;

namespace DisCatSharp.Entities;

/// <summary>
/// Constructs ready-to-send webhook requests.
/// </summary>
public sealed class DiscordWebhookBuilder
{
	/// <summary>
	/// Username to use for this webhook request.
	/// </summary>
	public Optional<string> Username { get; set; }

	/// <summary>
	/// Avatar url to use for this webhook request.
	/// </summary>
	public Optional<string> AvatarUrl { get; set; }

	/// <summary>
	/// Whether this webhook request is text-to-speech.
	/// </summary>
	public bool IsTts { get; set; }

	/// <summary>
	/// Whether to suppress embeds.
	/// </summary>
	public bool EmbedsSuppressed { get; set; }

	/// <summary>
	/// Whether to send as silent message.
	/// </summary>
	public bool NotificationsSuppressed { get; set; }

	/// <summary>
	/// Whether flags were changed.
	/// </summary>
	internal bool _flagsChanged = false;

	/// <summary>
	/// Message to send on this webhook request.
	/// </summary>
	public string Content
	{
		get => this._content;
		set
		{
			if (value != null && value.Length > 2000)
				throw new ArgumentException("Content length cannot exceed 2000 characters.", nameof(value));
			this._content = value;
		}
	}
	private string _content;

	/// <summary>
	/// Name of the new thread.
	/// Only works if the webhook is send in a <see cref="ChannelType.Forum"/>.
	/// </summary>
	public string ThreadName { get; set; }


	/// <summary>
	/// Whether to keep previous attachments.
	/// </summary>
	internal bool? KeepAttachmentsInternal;

	/// <summary>
	/// Embeds to send on this webhook request.
	/// </summary>
	public IReadOnlyList<DiscordEmbed> Embeds => this._embeds;
	private readonly List<DiscordEmbed> _embeds = new();

	/// <summary>
	/// Files to send on this webhook request.
	/// </summary>
	public IReadOnlyList<DiscordMessageFile> Files => this._files;
	private readonly List<DiscordMessageFile> _files = new();

	/// <summary>
	/// Mentions to send on this webhook request.
	/// </summary>
	public IReadOnlyList<IMention> Mentions => this._mentions;
	private readonly List<IMention> _mentions = new();

	/// <summary>
	/// Gets the components.
	/// </summary>
	public IReadOnlyList<DiscordActionRowComponent> Components => this._components;
	private readonly List<DiscordActionRowComponent> _components = new();


	/// <summary>
	/// Attachments to keep on this webhook request.
	/// </summary>
	public IReadOnlyList<DiscordAttachment> Attachments => this.AttachmentsInternal;
	internal List<DiscordAttachment> AttachmentsInternal = new();

	/// <summary>
	/// Constructs a new empty webhook request builder.
	/// </summary>
	public DiscordWebhookBuilder() { } // I still see no point in initializing collections with empty collections. //

	/// <summary>
	/// Sets the webhook response to suppress embeds.
	/// </summary>
	public DiscordWebhookBuilder SuppressEmbeds()
	{
		this._flagsChanged = true;
		this.EmbedsSuppressed = true;
		return this;
	}

	/// <summary>
	/// Sets the webhook to be send as silent message.
	/// </summary>
	public DiscordWebhookBuilder AsSilentMessage()
	{
		this._flagsChanged = true;
		this.NotificationsSuppressed = true;
		return this;
	}

	/// <summary>
	/// Adds a row of components to the builder, up to 5 components per row, and up to 5 rows per message.
	/// </summary>
	/// <param name="components">The components to add to the builder.</param>
	/// <returns>The current builder to be chained.</returns>
	/// <exception cref="ArgumentOutOfRangeException">No components were passed.</exception>
	public DiscordWebhookBuilder AddComponents(params DiscordComponent[] components)
		=> this.AddComponents((IEnumerable<DiscordComponent>)components);

	/// <summary>
	/// Appends several rows of components to the builder
	/// </summary>
	/// <param name="components">The rows of components to add, holding up to five each.</param>
	/// <returns></returns>
	public DiscordWebhookBuilder AddComponents(IEnumerable<DiscordActionRowComponent> components)
	{
		var ara = components.ToArray();

		if (ara.Length + this._components.Count > 5)
			throw new ArgumentException("ActionRow count exceeds maximum of five.");

		foreach (var ar in ara)
			this._components.Add(ar);

		return this;
	}

	/// <summary>
	/// Adds a row of components to the builder, up to 5 components per row, and up to 5 rows per message.
	/// </summary>
	/// <param name="components">The components to add to the builder.</param>
	/// <returns>The current builder to be chained.</returns>
	/// <exception cref="ArgumentOutOfRangeException">No components were passed.</exception>
	public DiscordWebhookBuilder AddComponents(IEnumerable<DiscordComponent> components)
	{
		var cmpArr = components.ToArray();
		var count = cmpArr.Length;

		if (!cmpArr.Any())
			throw new ArgumentOutOfRangeException(nameof(components), "You must provide at least one component");

		if (count > 5)
			throw new ArgumentException("Cannot add more than 5 components per action row!");

		var comp = new DiscordActionRowComponent(cmpArr);
		this._components.Add(comp);

		return this;
	}

	/// <summary>
	/// Sets the username for this webhook builder.
	/// </summary>
	/// <param name="username">Username of the webhook</param>
	public DiscordWebhookBuilder WithUsername(string username)
	{
		this.Username = username;
		return this;
	}

	/// <summary>
	/// Sets the avatar of this webhook builder from its url.
	/// </summary>
	/// <param name="avatarUrl">Avatar url of the webhook</param>
	public DiscordWebhookBuilder WithAvatarUrl(string avatarUrl)
	{
		this.AvatarUrl = avatarUrl;
		return this;
	}

	/// <summary>
	/// Indicates if the webhook must use text-to-speech.
	/// </summary>
	/// <param name="tts">Text-to-speech</param>
	public DiscordWebhookBuilder WithTts(bool tts)
	{
		this.IsTts = tts;
		return this;
	}

	/// <summary>
	/// Sets the message to send at the execution of the webhook.
	/// </summary>
	/// <param name="content">Message to send.</param>
	public DiscordWebhookBuilder WithContent(string content)
	{
		this.Content = content;
		return this;
	}

	/// <summary>
	/// Sets the thread name to create at the execution of the webhook.
	/// Only works for <see cref="ChannelType.Forum"/>.
	/// </summary>
	/// <param name="name">The thread name.</param>
	public DiscordWebhookBuilder WithThreadName(string name)
	{
		this.ThreadName = name;
		return this;
	}

	/// <summary>
	/// Adds an embed to send at the execution of the webhook.
	/// </summary>
	/// <param name="embed">Embed to add.</param>
	public DiscordWebhookBuilder AddEmbed(DiscordEmbed embed)
	{
		if (embed != null)
			this._embeds.Add(embed);

		return this;
	}

	/// <summary>
	/// Adds the given embeds to send at the execution of the webhook.
	/// </summary>
	/// <param name="embeds">Embeds to add.</param>
	public DiscordWebhookBuilder AddEmbeds(IEnumerable<DiscordEmbed> embeds)
	{
		this._embeds.AddRange(embeds);
		return this;
	}

	/// <summary>
	/// Adds a file to send at the execution of the webhook.
	/// </summary>
	/// <param name="filename">Name of the file.</param>
	/// <param name="data">File data.</param>
	/// <param name="resetStreamPosition">Tells the API Client to reset the stream position to what it was after the file is sent.</param>
	/// <param name="description">Description of the file.</param>
	public DiscordWebhookBuilder AddFile(string filename, Stream data, bool resetStreamPosition = false, string description = null)
	{
		if (this.Files.Count > 10)
			throw new ArgumentException("Cannot send more than 10 files with a single message.");

		if (this._files.Any(x => x.Filename == filename))
			throw new ArgumentException("A File with that filename already exists");

		if (resetStreamPosition)
			this._files.Add(new(filename, data, data.Position, description: description));
		else
			this._files.Add(new(filename, data, null, description: description));

		return this;
	}

	/// <summary>
	/// Sets if the message has files to be sent.
	/// </summary>
	/// <param name="stream">The Stream to the file.</param>
	/// <param name="resetStreamPosition">Tells the API Client to reset the stream position to what it was after the file is sent.</param>
	/// <param name="description">Description of the file.</param>
	/// <returns></returns>
	public DiscordWebhookBuilder AddFile(FileStream stream, bool resetStreamPosition = false, string description = null)
	{
		if (this.Files.Count > 10)
			throw new ArgumentException("Cannot send more than 10 files with a single message.");

		if (this._files.Any(x => x.Filename == stream.Name))
			throw new ArgumentException("A File with that filename already exists");

		if (resetStreamPosition)
			this._files.Add(new(stream.Name, stream, stream.Position, description: description));
		else
			this._files.Add(new(stream.Name, stream, null, description: description));

		return this;
	}

	/// <summary>
	/// Adds the given files to send at the execution of the webhook.
	/// </summary>
	/// <param name="files">Dictionary of file name and file data.</param>
	/// <param name="resetStreamPosition">Tells the API Client to reset the stream position to what it was after the file is sent.</param>
	public DiscordWebhookBuilder AddFiles(Dictionary<string, Stream> files, bool resetStreamPosition = false)
	{
		if (this.Files.Count + files.Count > 10)
			throw new ArgumentException("Cannot send more than 10 files with a single message.");

		foreach (var file in files)
		{
			if (this._files.Any(x => x.Filename == file.Key))
				throw new ArgumentException("A File with that filename already exists");

			if (resetStreamPosition)
				this._files.Add(new(file.Key, file.Value, file.Value.Position));
			else
				this._files.Add(new(file.Key, file.Value, null));
		}


		return this;
	}

	/// <summary>
	/// Modifies the given attachments on edit.
	/// </summary>
	/// <param name="attachments">Attachments to edit.</param>
	/// <returns></returns>
	public DiscordWebhookBuilder ModifyAttachments(IEnumerable<DiscordAttachment> attachments)
	{
		this.AttachmentsInternal.AddRange(attachments);
		return this;
	}

	/// <summary>
	/// Whether to keep the message attachments, if new ones are added.
	/// </summary>
	/// <returns></returns>
	public DiscordWebhookBuilder KeepAttachments(bool keep)
	{
		this.KeepAttachmentsInternal = keep;
		return this;
	}

	/// <summary>
	/// Adds the mention to the mentions to parse, etc. at the execution of the webhook.
	/// </summary>
	/// <param name="mention">Mention to add.</param>
	public DiscordWebhookBuilder AddMention(IMention mention)
	{
		this._mentions.Add(mention);
		return this;
	}

	/// <summary>
	/// Adds the mentions to the mentions to parse, etc. at the execution of the webhook.
	/// </summary>
	/// <param name="mentions">Mentions to add.</param>
	public DiscordWebhookBuilder AddMentions(IEnumerable<IMention> mentions)
	{
		this._mentions.AddRange(mentions);
		return this;
	}

	/// <summary>
	/// Executes a webhook.
	/// </summary>
	/// <param name="webhook">The webhook that should be executed.</param>
	/// <returns>The message sent</returns>
	public async Task<DiscordMessage> SendAsync(DiscordWebhook webhook) => await webhook.ExecuteAsync(this).ConfigureAwait(false);

	/// <summary>
	/// Executes a webhook.
	/// </summary>
	/// <param name="webhook">The webhook that should be executed.</param>
	/// <param name="threadId">Target thread id.</param>
	/// <returns>The message sent</returns>
	public async Task<DiscordMessage> SendAsync(DiscordWebhook webhook, ulong threadId) => await webhook.ExecuteAsync(this, threadId.ToString()).ConfigureAwait(false);

	/// <summary>
	/// Sends the modified webhook message.
	/// </summary>
	/// <param name="webhook">The webhook that should be executed.</param>
	/// <param name="message">The message to modify.</param>
	/// <returns>The modified message</returns>
	public async Task<DiscordMessage> ModifyAsync(DiscordWebhook webhook, DiscordMessage message) => await this.ModifyAsync(webhook, message.Id).ConfigureAwait(false);

	/// <summary>
	/// Sends the modified webhook message.
	/// </summary>
	/// <param name="webhook">The webhook that should be executed.</param>
	/// <param name="messageId">The id of the message to modify.</param>
	/// <returns>The modified message</returns>
	public async Task<DiscordMessage> ModifyAsync(DiscordWebhook webhook, ulong messageId) => await webhook.EditMessageAsync(messageId, this).ConfigureAwait(false);

	/// <summary>
	/// Sends the modified webhook message.
	/// </summary>
	/// <param name="webhook">The webhook that should be executed.</param>
	/// <param name="message">The message to modify.</param>
	/// <param name="thread">Target thread.</param>
	/// <returns>The modified message</returns>
	public async Task<DiscordMessage> ModifyAsync(DiscordWebhook webhook, DiscordMessage message, DiscordThreadChannel thread) => await this.ModifyAsync(webhook, message.Id, thread.Id).ConfigureAwait(false);

	/// <summary>
	/// Sends the modified webhook message.
	/// </summary>
	/// <param name="webhook">The webhook that should be executed.</param>
	/// <param name="messageId">The id of the message to modify.</param>
	/// <param name="threadId">Target thread id.</param>
	/// <returns>The modified message</returns>
	public async Task<DiscordMessage> ModifyAsync(DiscordWebhook webhook, ulong messageId, ulong threadId) => await webhook.EditMessageAsync(messageId, this, threadId.ToString()).ConfigureAwait(false);

	/// <summary>
	/// Clears all message components on this builder.
	/// </summary>
	public void ClearComponents()
		=> this._components.Clear();

	/// <summary>
	/// Allows for clearing the Webhook Builder so that it can be used again to send a new message.
	/// </summary>
	public void Clear()
	{
		this.Content = "";
		this._embeds.Clear();
		this.IsTts = false;
		this._mentions.Clear();
		this._files.Clear();
		this.AttachmentsInternal.Clear();
		this._components.Clear();
		this.KeepAttachmentsInternal = false;
		this.ThreadName = null;
	}

	/// <summary>
	/// Does the validation before we send a the Create/Modify request.
	/// </summary>
	/// <param name="isModify">Tells the method to perform the Modify Validation or Create Validation.</param>
	/// <param name="isFollowup">Tells the method to perform the follow up message validation.</param>
	/// <param name="isInteractionResponse">Tells the method to perform the interaction response validation.</param>
	internal void Validate(bool isModify = false, bool isFollowup = false, bool isInteractionResponse = false)
	{
		if (isModify)
		{
			if (this.Username.HasValue)
				throw new ArgumentException("You cannot change the username of a message.");

			if (this.AvatarUrl.HasValue)
				throw new ArgumentException("You cannot change the avatar of a message.");
		}
		else if (isFollowup)
		{
			if (this.Username.HasValue)
				throw new ArgumentException("You cannot change the username of a follow up message.");

			if (this.AvatarUrl.HasValue)
				throw new ArgumentException("You cannot change the avatar of a follow up message.");
		}
		else if (isInteractionResponse)
		{
			if (this.Username.HasValue)
				throw new ArgumentException("You cannot change the username of an interaction response.");

			if (this.AvatarUrl.HasValue)
				throw new ArgumentException("You cannot change the avatar of an interaction response.");
		}
		else
		{
			if (this.Files?.Count == 0 && string.IsNullOrEmpty(this.Content) && !this.Embeds.Any() && !this.Components.Any())
				throw new ArgumentException("You must specify content, an embed, a component, or at least one file.");
		}
	}
}
