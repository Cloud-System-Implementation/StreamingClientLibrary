﻿using Mixer.Base.Model.Channel;
using Mixer.Base.Model.Client;
using Mixer.Base.Model.MixPlay;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using StreamingClient.Base.Model.OAuth;
using StreamingClient.Base.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Net.WebSockets;
using System.Threading.Tasks;

namespace Mixer.Base.Clients
{
    /// <summary>
    /// The real-time client for MixPlay interactions.
    /// </summary>
    public class MixPlayClient : MixerWebSocketClientBase
    {
        /// <summary>
        /// Invoked when a memory warning is issued.
        /// </summary>
        public event EventHandler<MixPlayIssueMemoryWarningModel> OnIssueMemoryWarning;

        /// <summary>
        /// Invoked when a participant leaves.
        /// </summary>
        public event EventHandler<MixPlayParticipantCollectionModel> OnParticipantLeave;
        /// <summary>
        /// Invoked when a participant joins.
        /// </summary>
        public event EventHandler<MixPlayParticipantCollectionModel> OnParticipantJoin;
        /// <summary>
        /// Invoked when a participant is updated.
        /// </summary>
        public event EventHandler<MixPlayParticipantCollectionModel> OnParticipantUpdate;

        /// <summary>
        /// Invoked when a group is created.
        /// </summary>
        public event EventHandler<MixPlayGroupCollectionModel> OnGroupCreate;
        /// <summary>
        /// Invoked when a group is deleted.
        /// </summary>
        public event EventHandler<Tuple<MixPlayGroupModel, MixPlayGroupModel>> OnGroupDelete;
        /// <summary>
        /// Invoked when a group is updated.
        /// </summary>
        public event EventHandler<MixPlayGroupCollectionModel> OnGroupUpdate;

        /// <summary>
        /// Invoked when a scene is created.
        /// </summary>
        public event EventHandler<MixPlayConnectedSceneCollectionModel> OnSceneCreate;
        /// <summary>
        /// Invoked when a scene is deleted.
        /// </summary>
        public event EventHandler<Tuple<MixPlayConnectedSceneModel, MixPlayConnectedSceneModel>> OnSceneDelete;
        /// <summary>
        /// Invoked when a scene is updated.
        /// </summary>
        public event EventHandler<MixPlayConnectedSceneCollectionModel> OnSceneUpdate;

        /// <summary>
        /// Invoked when a control is created.
        /// </summary>
        public event EventHandler<MixPlayConnectedSceneModel> OnControlCreate;
        /// <summary>
        /// Invoked when a control is deleted.
        /// </summary>
        public event EventHandler<MixPlayConnectedSceneModel> OnControlDelete;
        /// <summary>
        /// Invoked when a control is updated.
        /// </summary>
        public event EventHandler<MixPlayConnectedSceneModel> OnControlUpdate;

        /// <summary>
        /// Invoked when input is received.
        /// </summary>
        public event EventHandler<MixPlayGiveInputModel> OnGiveInput;

        /// <summary>
        /// The channel the MixPlay client is connected to.
        /// </summary>
        public ChannelModel Channel { get; private set; }
        /// <summary>
        /// The MixPlay game that is connected.
        /// </summary>
        public MixPlayGameModel Game { get; private set; }
        /// <summary>
        /// The version of the connected MixPlay game.
        /// </summary>
        public MixPlayGameVersionModel Version { get; private set; }
        /// <summary>
        /// The share code of the MixPlay game.
        /// </summary>
        public string ShareCode { get; private set; }

        private IEnumerable<string> connections;

        private string oauthAccessToken;

        private int lastSequenceNumber = 0;

        /// <summary>
        /// Creates an MixPlay client using the specified connection to the specified channel and game.
        /// </summary>
        /// <param name="connection">The connection to use</param>
        /// <param name="channel">The channel to connect to</param>
        /// <param name="gameListing">The game to use</param>
        /// <returns>The MixPlay client for the specified channel and game</returns>
        public static async Task<MixPlayClient> CreateFromChannel(MixerConnection connection, ChannelModel channel, MixPlayGameListingModel gameListing)
        {
            Validator.ValidateVariable(connection, "connection");
            Validator.ValidateVariable(channel, "channel");
            Validator.ValidateVariable(gameListing, "gameListing");
            Validator.ValidateList(gameListing.versions, "gameListing.versions");

            return await MixPlayClient.CreateFromChannel(connection, channel, gameListing, gameListing.versions.OrderByDescending(v => v.versionOrder).First());
        }

        /// <summary>
        /// Creates an MixPlay client using the specified connection to the specified channel and game.
        /// </summary>
        /// <param name="connection">The connection to use</param>
        /// <param name="channel">The channel to connect to</param>
        /// <param name="game">The game to use</param>
        /// <param name="version">The version of the game to use</param>
        /// <returns>The MixPlay client for the specified channel and game</returns>
        public static async Task<MixPlayClient> CreateFromChannel(MixerConnection connection, ChannelModel channel, MixPlayGameModel game, MixPlayGameVersionModel version)
        {
            Validator.ValidateVariable(connection, "connection");
            Validator.ValidateVariable(channel, "channel");
            Validator.ValidateVariable(game, "game");
            Validator.ValidateVariable(version, "version");

            return await MixPlayClient.CreateFromChannel(connection, channel, game, version, null);
        }

        /// <summary>
        /// Creates an MixPlay client using the specified connection to the specified channel and game.
        /// </summary>
        /// <param name="connection">The connection to use</param>
        /// <param name="channel">The channel to connect to</param>
        /// <param name="game">The game to use</param>
        /// <param name="version">The version of the game to use</param>
        /// <param name="shareCode">The share code used to connect to a game shared with you</param>
        /// <returns>The MixPlay client for the specified channel and game</returns>
        public static async Task<MixPlayClient> CreateFromChannel(MixerConnection connection, ChannelModel channel, MixPlayGameModel game, MixPlayGameVersionModel version, string shareCode)
        {
            Validator.ValidateVariable(connection, "connection");
            Validator.ValidateVariable(channel, "channel");
            Validator.ValidateVariable(game, "game");
            Validator.ValidateVariable(version, "version");

            OAuthTokenModel authToken = await connection.GetOAuthToken();

            IEnumerable<string> connections = await connection.MixPlay.GetMixPlayHosts();

            return new MixPlayClient(channel, game, version, shareCode, authToken, connections);
        }

        private MixPlayClient(ChannelModel channel, MixPlayGameModel game, MixPlayGameVersionModel version, string shareCode, OAuthTokenModel authToken, IEnumerable<string> connections)
        {
            Validator.ValidateVariable(channel, "channel");
            Validator.ValidateVariable(game, "game");
            Validator.ValidateVariable(version, "version");
            Validator.ValidateVariable(authToken, "authToken");
            Validator.ValidateList(connections, "connections");

            this.Channel = channel;
            this.Game = game;
            this.Version = version;
            this.ShareCode = shareCode;
            this.connections = connections;

            this.oauthAccessToken = authToken.accessToken;
        }

        /// <summary>
        /// Connects to the channel and game.
        /// </summary>
        /// <returns>Whether the operation succeeded</returns>
        public async Task<bool> Connect()
        {
            Random random = new Random();
            int endpointToUse = random.Next(this.connections.Count());
            return await this.Connect(this.connections.ElementAt(endpointToUse));
        }

        /// <summary>
        /// Connects to the MixPlay service.
        /// </summary>
        /// <param name="endpoint">The endpoint to connect to</param>
        /// <returns>Whether the operation succeeded</returns>
        public override async Task<bool> Connect(string endpoint)
        {
            this.OnMethodOccurred -= MixPlayClient_OnMethodOccurred;

            this.OnMethodOccurred += MixPlayClient_HelloMethodHandler;

            await base.Connect(endpoint);

            await this.WaitForSuccess(() => { return this.Connected; });

            this.OnMethodOccurred -= MixPlayClient_HelloMethodHandler;

            if (this.Connected)
            {
                this.OnMethodOccurred += MixPlayClient_OnMethodOccurred;
                this.OnReplyOccurred += MixPlayClient_OnReplyOccurred;
            }

            return this.Connected;
        }

        /// <summary>
        /// Prepares the client to receive MixPlay events.
        /// </summary>
        /// <returns>Whether the operation succeeded</returns>
        public async Task<bool> Ready()
        {
            this.Authenticated = false;

            this.OnMethodOccurred += MixPlayClient_ReadyMethodHandler;

            JObject parameters = new JObject();
            parameters.Add("isReady", true);
            MethodPacket packet = new MethodPacket()
            {
                method = "ready",
                parameters = parameters,
                discard = true
            };

            await this.Send(packet, checkIfAuthenticated: false);

            await this.WaitForSuccess(() => { return this.Authenticated; });

            this.OnMethodOccurred -= MixPlayClient_ReadyMethodHandler;

            return this.Authenticated;
        }

        /// <summary>
        /// Gets the current server time.
        /// </summary>
        /// <returns>The current time on the server</returns>
        public async Task<DateTimeOffset?> GetTime()
        {
            ReplyPacket reply = await this.SendAndListen(new MethodPacket("getTime"));
            if (reply != null && reply.resultObject["time"] != null)
            {
                return DateTimeOffsetExtensions.FromUTCUnixTimeMilliseconds((long)reply.resultObject["time"]);
            }
            return null;
        }

        /// <summary>
        /// Gets the allocated memory state for this client.
        /// </summary>
        /// <returns>The allocated memory</returns>
        public async Task<MixPlayIssueMemoryWarningModel> GetMemoryStates()
        {
            return await this.SendAndListen<MixPlayIssueMemoryWarningModel>(new MethodPacket("getMemoryStats"));
        }

        /// <summary>
        /// Sets the memory throttling for the specified MixPlay APIs.
        /// </summary>
        /// <param name="throttling">The throttling to set</param>
        /// <returns>The task object representing the asynchronous operation</returns>
        public async Task SetBandwidthThrottle(MixPlaySetBandwidthThrottleModel throttling)
        {
            await this.Send(this.BuildBandwidthThrottlePacket(throttling));
        }

        /// <summary>
        /// Sets the memory throttling for the specified MixPlay APIs.
        /// </summary>
        /// <param name="throttling">The throttling to set</param>
        /// <returns>Whether the operation succeeded</returns>
        public async Task<bool> SetBandwidthThrottleWithResponse(MixPlaySetBandwidthThrottleModel throttling)
        {
            return this.VerifyNoErrors(await this.SendAndListen(this.BuildBandwidthThrottlePacket(throttling)));
        }

        private MethodPacket BuildBandwidthThrottlePacket(MixPlaySetBandwidthThrottleModel throttling)
        {
            Validator.ValidateVariable(throttling, "throttling");
            return new MethodParamsPacket("setBandwidthThrottle", throttling);
        }

        /// <summary>
        /// Gets the memory throttling for all MixPlay APIs.
        /// </summary>
        /// <returns>The memory throttling for all MixPlay APIs</returns>
        public async Task<MixPlayGetThrottleStateModel> GetThrottleState()
        {
            ReplyPacket reply = await this.SendAndListen(new MethodPacket("getThrottleState"));
            if (this.VerifyNoErrors(reply))
            {
                return new MixPlayGetThrottleStateModel(reply.resultObject);
            }
            return new MixPlayGetThrottleStateModel();
        }

        /// <summary>
        /// Gets the first 100 participants connected based on the specified connection time. For subsequent participants, specify
        /// a later connection time, typically using the latest connected participant's connection time as the new earliest connection time.
        /// </summary>
        /// <param name="startTime">The starting connection time</param>
        /// <returns>The first 100 participants</returns>
        public async Task<MixPlayParticipantCollectionModel> GetAllParticipants(DateTimeOffset? startTime = null)
        {
            if (startTime == null) { startTime = DateTimeOffset.FromUnixTimeSeconds(0); }

            JObject parameters = new JObject();
            parameters.Add("from", startTime.GetValueOrDefault().ToUnixTimeMilliseconds());

            return await this.SendAndListen<MixPlayParticipantCollectionModel>(new MethodParamsPacket("getAllParticipants", parameters));
        }

        /// <summary>
        /// Returns a set of participants who have performed an MixPlay action after the specified start. For subsequent participants, specify
        /// a later connection time, typically using the latest connected participant's connection time as the new earliest connection time.
        /// </summary>
        /// <param name="startTime">The start time for last interaction</param>
        /// <returns>The set of last interacted participants</returns>
        public async Task<MixPlayParticipantCollectionModel> GetActiveParticipants(DateTimeOffset startTime)
        {
            Validator.ValidateVariable(startTime, "startTime");
            JObject parameters = new JObject();
            parameters.Add("threshold", startTime.ToUnixTimeMilliseconds());

            return await this.SendAndListen<MixPlayParticipantCollectionModel>(new MethodParamsPacket("getActiveParticipants", parameters));
        }

        /// <summary>
        /// Updates the specified participants
        /// </summary>
        /// <param name="participants">The participants to update</param>
        /// <returns>The task object representing the asynchronous operation</returns>
        public async Task UpdateParticipants(IEnumerable<MixPlayParticipantModel> participants)
        {
            await this.Send(this.BuildUpdateParticipantsPacket(participants));
        }

        /// <summary>
        /// Updates the specified participants
        /// </summary>
        /// <param name="participants">The participants to update</param>
        /// <returns>The updated participants</returns>
        public async Task<MixPlayParticipantCollectionModel> UpdateParticipantsWithResponse(IEnumerable<MixPlayParticipantModel> participants)
        {
            return await this.SendAndListen<MixPlayParticipantCollectionModel>(this.BuildUpdateParticipantsPacket(participants));
        }

        /// <summary>
        /// Creates the web socket client.
        /// </summary>
        /// <returns>The web socket client</returns>
        protected override ClientWebSocket CreateWebSocket()
        {
            ClientWebSocket webSocket = base.CreateWebSocket();

            AuthenticationHeaderValue authHeader = new AuthenticationHeaderValue("Bearer", this.oauthAccessToken);
            webSocket.Options.SetRequestHeader("Authorization", authHeader.ToString());
            webSocket.Options.SetRequestHeader("X-Interactive-Version", this.Version.id.ToString());
            webSocket.Options.SetRequestHeader("X-Protocol-Version", "2.0");
            if (!string.IsNullOrEmpty(this.ShareCode))
            {
                webSocket.Options.SetRequestHeader("X-Interactive-Sharecode", this.ShareCode);
            }

            return webSocket;
        }

        private MethodPacket BuildUpdateParticipantsPacket(IEnumerable<MixPlayParticipantModel> participants)
        {
            Validator.ValidateList(participants, "participants");
            JObject parameters = new JObject();
            parameters.Add("participants", JArray.FromObject(participants));
            return new MethodParamsPacket("updateParticipants", parameters);
        }

        /// <summary>
        /// Creates the specified groups
        /// </summary>
        /// <param name="groups">The groups to create</param>
        /// <returns>The task object representing the asynchronous operation</returns>
        public async Task CreateGroups(IEnumerable<MixPlayGroupModel> groups)
        {
            await this.Send(this.BuildCreateGroupsPacket(groups));
        }

        /// <summary>
        /// Creates the specified groups
        /// </summary>
        /// <param name="groups">The groups to create</param>
        /// <returns>Whether the operation succeeded</returns>
        public async Task<bool> CreateGroupsWithResponse(IEnumerable<MixPlayGroupModel> groups)
        {
            return this.VerifyNoErrors(await this.SendAndListen(this.BuildCreateGroupsPacket(groups)));
        }

        private MethodPacket BuildCreateGroupsPacket(IEnumerable<MixPlayGroupModel> groups)
        {
            Validator.ValidateList(groups, "groups");
            MixPlayGroupCollectionModel collection = new MixPlayGroupCollectionModel() { groups = groups.ToList() };
            return new MethodParamsPacket("createGroups", JObject.FromObject(collection));
        }

        /// <summary>
        /// Gets all groups.
        /// </summary>
        /// <returns>All groups</returns>
        public async Task<MixPlayGroupCollectionModel> GetGroups()
        {
            return await this.SendAndListen<MixPlayGroupCollectionModel>(new MethodPacket("getGroups"));
        }

        /// <summary>
        /// Updates the specified groups.
        /// </summary>
        /// <param name="groups">The groups to update</param>
        /// <returns>The task object representing the asynchronous operation</returns>
        public async Task UpdateGroups(IEnumerable<MixPlayGroupModel> groups)
        {
            await this.Send(this.BuildUpdateGroupsPacket(groups));
        }

        /// <summary>
        /// Updates the specified groups.
        /// </summary>
        /// <param name="groups">The groups to update</param>
        /// <returns>The updated groups</returns>
        public async Task<MixPlayGroupCollectionModel> UpdateGroupsWithResponse(IEnumerable<MixPlayGroupModel> groups)
        {
            return await this.SendAndListen<MixPlayGroupCollectionModel>(this.BuildUpdateGroupsPacket(groups));
        }

        private MethodPacket BuildUpdateGroupsPacket(IEnumerable<MixPlayGroupModel> groups)
        {
            Validator.ValidateList(groups, "groups");
            MixPlayGroupCollectionModel collection = new MixPlayGroupCollectionModel() { groups = groups.ToList() };
            return new MethodParamsPacket("updateGroups", JObject.FromObject(collection));
        }

        /// <summary>
        /// Deletes and replaces the specified group
        /// </summary>
        /// <param name="groupToDelete">The group to delete</param>
        /// <param name="groupToReplace">The group to replace with</param>
        /// <returns>The task object representing the asynchronous operation</returns>
        public async Task DeleteGroup(MixPlayGroupModel groupToDelete, MixPlayGroupModel groupToReplace)
        {
            await this.Send(this.BuildDeleteGroupPacket(groupToDelete, groupToReplace));
        }

        /// <summary>
        /// Deletes and replaces the specified group
        /// </summary>
        /// <param name="groupToDelete">The group to delete</param>
        /// <param name="groupToReplace">The group to replace with</param>
        /// <returns>Whether the operation succeeded</returns>
        public async Task<bool> DeleteGroupWithResponse(MixPlayGroupModel groupToDelete, MixPlayGroupModel groupToReplace)
        {
            return this.VerifyNoErrors(await this.SendAndListen(this.BuildDeleteGroupPacket(groupToDelete, groupToReplace)));
        }

        private MethodPacket BuildDeleteGroupPacket(MixPlayGroupModel groupToDelete, MixPlayGroupModel groupToReplace)
        {
            Validator.ValidateVariable(groupToDelete, "groupToDelete");
            Validator.ValidateVariable(groupToReplace, "groupToReplace");
            JObject parameters = new JObject();
            parameters.Add("groupID", groupToDelete.groupID);
            parameters.Add("reassignGroupID", groupToReplace.groupID);
            return new MethodParamsPacket("deleteGroup", parameters);
        }

        /// <summary>
        /// Creates the specified scenes.
        /// </summary>
        /// <param name="scenes">The scenes to create</param>
        /// <returns>The task object representing the asynchronous operation</returns>
        public async Task CreateScenes(IEnumerable<MixPlayConnectedSceneModel> scenes)
        {
            await this.Send(this.BuildCreateScenesPacket(scenes));
        }

        /// <summary>
        /// Creates the specified scenes.
        /// </summary>
        /// <param name="scenes">The scenes to create</param>
        /// <returns>The created scenes</returns>
        public async Task<MixPlayConnectedSceneCollectionModel> CreateScenesWithResponse(IEnumerable<MixPlayConnectedSceneModel> scenes)
        {
            return await this.SendAndListen<MixPlayConnectedSceneCollectionModel>(this.BuildCreateScenesPacket(scenes));
        }

        private MethodPacket BuildCreateScenesPacket(IEnumerable<MixPlayConnectedSceneModel> scenes)
        {
            Validator.ValidateList(scenes, "scenes");
            MixPlayConnectedSceneCollectionModel collection = new MixPlayConnectedSceneCollectionModel();
            foreach (MixPlayConnectedSceneModel scene in scenes)
            {
                // Need to strip out all of the non-updateable fields in order for the API to not return a 403 error
                collection.scenes.Add(JSONSerializerHelper.Clone<MixPlayConnectedSceneModel>(scene));
            }
            return new MethodParamsPacket("createScenes", JObject.FromObject(collection));
        }

        /// <summary>
        /// Gets all scenes.
        /// </summary>
        /// <returns>All scenes</returns>
        public async Task<MixPlayConnectedSceneGroupCollectionModel> GetScenes()
        {
            return await this.SendAndListen<MixPlayConnectedSceneGroupCollectionModel>(new MethodPacket("getScenes"));
        }

        /// <summary>
        /// Updates the specified scenes.
        /// </summary>
        /// <param name="scenes">The scenes to update</param>
        /// <returns>The task object representing the asynchronous operation</returns>
        public async Task UpdateScenes(IEnumerable<MixPlayConnectedSceneModel> scenes)
        {
            await this.Send(this.BuildUpdateScenesPacket(scenes));
        }

        /// <summary>
        /// Updates the specified scenes.
        /// </summary>
        /// <param name="scenes">The scenes to update</param>
        /// <returns>The updated scenes</returns>
        public async Task<MixPlayConnectedSceneCollectionModel> UpdateScenesWithResponse(IEnumerable<MixPlayConnectedSceneModel> scenes)
        {
            return await this.SendAndListen<MixPlayConnectedSceneCollectionModel>(this.BuildUpdateScenesPacket(scenes));
        }

        private MethodPacket BuildUpdateScenesPacket(IEnumerable<MixPlayConnectedSceneModel> scenes)
        {
            Validator.ValidateList(scenes, "scenes");
            MixPlayConnectedSceneCollectionModel collection = new MixPlayConnectedSceneCollectionModel();
            foreach (MixPlayConnectedSceneModel scene in scenes)
            {
                // Need to strip out all of the non-updateable fields in order for the API to not return a 403 error
                collection.scenes.Add(JSONSerializerHelper.Clone<MixPlayConnectedSceneModel>(scene));
            }
            return new MethodParamsPacket("updateScenes", JObject.FromObject(collection));
        }

        /// <summary>
        /// Deletes and replaced the specified scene.
        /// </summary>
        /// <param name="sceneToDelete">The scene to delete</param>
        /// <param name="sceneToReplace">The scene to replace with</param>
        /// <returns>The task object representing the asynchronous operation</returns>
        public async Task DeleteScene(MixPlayConnectedSceneModel sceneToDelete, MixPlayConnectedSceneModel sceneToReplace)
        {
            await this.Send(this.BuildDeleteScenePacket(sceneToDelete, sceneToReplace));
        }

        /// <summary>
        /// Deletes and replaced the specified scene.
        /// </summary>
        /// <param name="sceneToDelete">The scene to delete</param>
        /// <param name="sceneToReplace">The scene to replace with</param>
        /// <returns>Whether the operation succeeded</returns>
        public async Task<bool> DeleteSceneWithResponse(MixPlayConnectedSceneModel sceneToDelete, MixPlayConnectedSceneModel sceneToReplace)
        {
            return this.VerifyNoErrors(await this.SendAndListen(this.BuildDeleteScenePacket(sceneToDelete, sceneToReplace)));
        }

        private MethodPacket BuildDeleteScenePacket(MixPlayConnectedSceneModel sceneToDelete, MixPlayConnectedSceneModel sceneToReplace)
        {
            Validator.ValidateVariable(sceneToDelete, "sceneToDelete");
            Validator.ValidateVariable(sceneToReplace, "sceneToReplace");
            JObject parameters = new JObject();
            parameters.Add("sceneID", sceneToDelete.sceneID);
            parameters.Add("reassignSceneID", sceneToReplace.sceneID);
            return new MethodParamsPacket("deleteScene", parameters);
        }

        /// <summary>
        /// Creates the specified controls for the specified scene.
        /// </summary>
        /// <param name="scene">The scene to add controls to</param>
        /// <param name="controls">The controls to create</param>
        /// <returns>The task object representing the asynchronous operation</returns>
        public async Task CreateControls(MixPlayConnectedSceneModel scene, IEnumerable<MixPlayControlModel> controls)
        {
            await this.Send(this.BuildCreateControlsPacket(scene, controls));
        }

        /// <summary>
        /// Creates the specified controls for the specified scene.
        /// </summary>
        /// <param name="scene">The scene to add controls to</param>
        /// <param name="controls">The controls to create</param>
        /// <returns>Whether the operation succeed</returns>
        public async Task<bool> CreateControlsWithResponse(MixPlayConnectedSceneModel scene, IEnumerable<MixPlayControlModel> controls)
        {
            return this.VerifyNoErrors(await this.SendAndListen(this.BuildCreateControlsPacket(scene, controls)));
        }

        private MethodPacket BuildCreateControlsPacket(MixPlayConnectedSceneModel scene, IEnumerable<MixPlayControlModel> controls)
        {
            Validator.ValidateVariable(scene, "scene");
            Validator.ValidateList(controls, "controls");
            JObject parameters = new JObject();
            parameters.Add("sceneID", scene.sceneID);
            parameters.Add("controls", JArray.FromObject(controls));
            return new MethodParamsPacket("createControls", parameters);
        }

        /// <summary>
        /// Updates the specified controls for the specified scene.
        /// </summary>
        /// <param name="scene">The scene to update controls for</param>
        /// <param name="controls">The controls to update</param>
        /// <returns>The task object representing the asynchronous operation</returns>
        public async Task UpdateControls(MixPlayConnectedSceneModel scene, IEnumerable<MixPlayControlModel> controls)
        {
            await this.Send(this.BuildUpdateControlsPacket(scene, controls));
        }

        /// <summary>
        /// Updates the specified controls for the specified scene.
        /// </summary>
        /// <param name="scene">The scene to update controls for</param>
        /// <param name="controls">The controls to update</param>
        /// <returns>The updated controls</returns>
        public async Task<MixPlayConnectedControlCollectionModel> UpdateControlsWithResponse(MixPlayConnectedSceneModel scene, IEnumerable<MixPlayControlModel> controls)
        {
            return await this.SendAndListen<MixPlayConnectedControlCollectionModel>(this.BuildUpdateControlsPacket(scene, controls));
        }

        private MethodPacket BuildUpdateControlsPacket(MixPlayConnectedSceneModel scene, IEnumerable<MixPlayControlModel> controls)
        {
            Validator.ValidateVariable(scene, "scene");
            Validator.ValidateList(controls, "controls");
            JObject parameters = new JObject();
            parameters.Add("sceneID", scene.sceneID);
            parameters.Add("controls", JArray.FromObject(controls, new JsonSerializer { NullValueHandling = NullValueHandling.Ignore }));
            return new MethodParamsPacket("updateControls", parameters);
        }

        /// <summary>
        /// Deletes the specified controls from the specified scene.
        /// </summary>
        /// <param name="scene">The scene to delete controls from</param>
        /// <param name="controls">The controls to delete</param>
        /// <returns>The task object representing the asynchronous operation</returns>
        public async Task DeleteControls(MixPlayConnectedSceneModel scene, IEnumerable<MixPlayControlModel> controls)
        {
            await this.Send(this.BuildDeleteControlsPacket(scene, controls));
        }

        /// <summary>
        /// Deletes the specified controls from the specified scene.
        /// </summary>
        /// <param name="scene">The scene to delete controls from</param>
        /// <param name="controls">The controls to delete</param>
        /// <returns>Whether the operation succeeded</returns>
        public async Task<bool> DeleteControlsWithResponse(MixPlayConnectedSceneModel scene, IEnumerable<MixPlayControlModel> controls)
        {
            return this.VerifyNoErrors(await this.SendAndListen(this.BuildDeleteControlsPacket(scene, controls)));
        }

        private MethodPacket BuildDeleteControlsPacket(MixPlayConnectedSceneModel scene, IEnumerable<MixPlayControlModel> controls)
        {
            Validator.ValidateVariable(scene, "scene");
            Validator.ValidateList(controls, "controls");
            JObject parameters = new JObject();
            parameters.Add("sceneID", scene.sceneID);
            parameters.Add("controlIDs", JArray.FromObject(controls.Select(c => c.controlID)));
            return new MethodParamsPacket("deleteControls", parameters);
        }

        /// <summary>
        /// Captures the spark transaction for the specified id.
        /// </summary>
        /// <param name="transactionID">The id of the spark transaction</param>
        /// <returns>The task object representing the asynchronous operation</returns>
        public async Task CaptureSparkTransaction(string transactionID)
        {
            await this.Send(this.BuildCaptureSparkTransactionPacket(transactionID));
        }

        /// <summary>
        /// Captures the spark transaction for the specified id.
        /// </summary>
        /// <param name="transactionID">The id of the spark transaction</param>
        /// <returns>Whether the operation succeeded</returns>
        public async Task<bool> CaptureSparkTransactionWithResponse(string transactionID)
        {
            return this.VerifyNoErrors(await this.SendAndListen(this.BuildCaptureSparkTransactionPacket(transactionID)));
        }

        /// <summary>
        /// Sends a broadcast event message to the selected scopes.
        /// </summary>
        /// <param name="scopes">A list of 1 or more scopes. For example: everyone, group:[ID], scene:[ID], or participant:[UUID]</param>
        /// <param name="data">The data to send in the message.</param>
        /// <returns>The task object representing the asynchronous operation</returns>
        public async Task BroadcastEvent(IEnumerable<string> scopes, JObject data)
        {
            await this.Send(this.BuildBroadcastEventPacket(scopes, data));
        }

        /// <summary>
        /// Sends a broadcast event message to the selected scopes.
        /// </summary>
        /// <param name="scopes">A list of 1 or more scopes. For example: everyone, group:[ID], scene:[ID], or participant:[UUID]</param>
        /// <param name="data">The data to send in the message.</param>
        /// <returns>The task object representing the asynchronous operation</returns>
        public async Task<bool> BroadcastEventWithResponse(IEnumerable<string> scopes, JObject data)
        {
            return this.VerifyNoErrors(await this.SendAndListen(this.BuildBroadcastEventPacket(scopes, data)));
        }

        /// <summary>
        /// Sends a MixPlay packet to the server.
        /// </summary>
        /// <param name="packet">The packet to send</param>
        /// <param name="checkIfAuthenticated">Whether to check if the client is authenticated</param>
        /// <returns>An awaitable task with the packet ID</returns>
        protected async override Task<uint> Send(WebSocketPacket packet, bool checkIfAuthenticated = true)
        {
            this.AssignLatestSequence(packet);
            return await base.Send(packet, checkIfAuthenticated);
        }

        private void AssignLatestSequence(WebSocketPacket packet)
        {
            if (packet is MethodPacket)
            {
                MethodPacket mPacket = (MethodPacket)packet;
                mPacket.seq = this.lastSequenceNumber;
            }
            else if (packet is ReplyPacket)
            {
                ReplyPacket rPacket = (ReplyPacket)packet;
                rPacket.seq = this.lastSequenceNumber;
            }
        }

        private MethodPacket BuildCaptureSparkTransactionPacket(string transactionID)
        {
            Validator.ValidateString(transactionID, "transactionID");
            JObject parameters = new JObject();
            parameters.Add("transactionID", transactionID);
            return new MethodParamsPacket("capture", parameters);
        }

        private MethodPacket BuildBroadcastEventPacket(IEnumerable<string> scopes, JObject data)
        {
            Validator.ValidateList<string>(scopes, "scope");
            Validator.ValidateVariable(data, "data");
            JObject parameters = new JObject();
            parameters.Add("scope", new JArray() { scopes });
            parameters.Add("data", data);
            return new MethodParamsPacket("broadcastEvent", parameters);
        }

        private void MixPlayClient_OnMethodOccurred(object sender, MethodPacket methodPacket)
        {
            this.lastSequenceNumber = Math.Max(methodPacket.seq, this.lastSequenceNumber);

            switch (methodPacket.method)
            {
                case "issueMemoryWarning":
                    this.InvokeMethodPacketEvent(methodPacket, this.OnIssueMemoryWarning);
                    break;

                case "onParticipantLeave":
                    this.InvokeMethodPacketEvent(methodPacket, this.OnParticipantLeave);
                    break;
                case "onParticipantJoin":
                    this.InvokeMethodPacketEvent(methodPacket, this.OnParticipantJoin);
                    break;
                case "onParticipantUpdate":
                    this.InvokeMethodPacketEvent(methodPacket, this.OnParticipantUpdate);
                    break;

                case "onGroupCreate":
                    this.InvokeMethodPacketEvent(methodPacket, this.OnGroupCreate);
                    break;
                case "onGroupDelete":
                    if (this.OnGroupDelete != null)
                    {
                        Tuple<MixPlayGroupModel, MixPlayGroupModel> groupDeleted = new Tuple<MixPlayGroupModel, MixPlayGroupModel>(
                            new MixPlayGroupModel() { groupID = methodPacket.parameters["groupID"].ToString() },
                            new MixPlayGroupModel() { groupID = methodPacket.parameters["reassignGroupID"].ToString() });

                        this.OnGroupDelete(this, groupDeleted);
                    }
                    break;
                case "onGroupUpdate":
                    this.InvokeMethodPacketEvent(methodPacket, this.OnGroupUpdate);
                    break;

                case "onSceneCreate":
                    this.InvokeMethodPacketEvent(methodPacket, this.OnSceneCreate);
                    break;
                case "onSceneDelete":
                    if (this.OnSceneDelete != null)
                    {
                        Tuple<MixPlayConnectedSceneModel, MixPlayConnectedSceneModel> sceneDeleted = new Tuple<MixPlayConnectedSceneModel, MixPlayConnectedSceneModel>(
                            new MixPlayConnectedSceneModel() { sceneID = methodPacket.parameters["sceneID"].ToString() },
                            new MixPlayConnectedSceneModel() { sceneID = methodPacket.parameters["reassignSceneID"].ToString() });

                        this.OnSceneDelete(this, sceneDeleted);
                    }
                    break;
                case "onSceneUpdate":
                    this.InvokeMethodPacketEvent(methodPacket, this.OnSceneUpdate);
                    break;

                case "onControlCreate":
                    this.InvokeMethodPacketEvent(methodPacket, this.OnControlCreate);
                    break;
                case "onControlDelete":
                    this.InvokeMethodPacketEvent(methodPacket, this.OnControlDelete);
                    break;
                case "onControlUpdate":
                    this.InvokeMethodPacketEvent(methodPacket, this.OnControlUpdate);
                    break;

                case "giveInput":
                    this.InvokeMethodPacketEvent(methodPacket, this.OnGiveInput);
                    break;
            }
        }

        private void MixPlayClient_OnReplyOccurred(object sender, ReplyPacket e)
        {
            this.lastSequenceNumber = Math.Max(e.seq, this.lastSequenceNumber);
        }

        private void MixPlayClient_HelloMethodHandler(object sender, MethodPacket e)
        {
            if (e.method.Equals("hello"))
            {
                this.Connected = true;
            }
        }

        private void MixPlayClient_ReadyMethodHandler(object sender, MethodPacket e)
        {
            JToken value;
            if (e.method.Equals("onReady") && e.parameters.TryGetValue("isReady", out value) && (bool)value)
            {
                this.Authenticated = true;
            }
        }
    }
}
