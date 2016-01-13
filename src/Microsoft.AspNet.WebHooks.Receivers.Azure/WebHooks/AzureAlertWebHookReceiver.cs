﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http.Controllers;
using Microsoft.AspNet.WebHooks.Properties;
using Newtonsoft.Json.Linq;

namespace Microsoft.AspNet.WebHooks
{
    /// <summary>
    /// Provides an <see cref="IWebHookReceiver"/> implementation which supports WebHooks generated by Azure Alert Service.
    /// A sample WebHook URI is '<c>https://&lt;host&gt;/api/webhooks/incoming/azurealert/{id}?code=83699ec7c1d794c0c780e49a5c72972590571fd8</c>'.
    /// For security reasons the WebHook URI must be an <c>https</c> URI and contain a 'code' query parameter with the
    /// same value as configured in the '<c>MS_WebHookReceiverSecret_AzureAlert</c>' application setting, optionally using IDs
    /// to differentiate between multiple WebHooks, for example '<c>secret0, id1=secret1, id2=secret2</c>'.
    /// The 'code' parameter must be between 32 and 128 characters long.
    /// For details about Azure WebHooks, see <c>https://azure.microsoft.com/en-us/documentation/articles/insights-webhooks-alerts/</c>.
    /// </summary>
    public class AzureAlertWebHookReceiver : WebHookReceiver
    {
        internal const string RecName = "azurealert";

        internal const string ActionParameter = "name";

        /// <summary>
        /// Gets the receiver name for this receiver.
        /// </summary>
        public static string ReceiverName
        {
            get { return RecName; }
        }

        /// <inheritdoc />
        public override string Name
        {
            get { return RecName; }
        }

        /// <inheritdoc />
        public override async Task<HttpResponseMessage> ReceiveAsync(string id, HttpRequestContext context, HttpRequestMessage request)
        {
            if (id == null)
            {
                throw new ArgumentNullException("id");
            }
            if (context == null)
            {
                throw new ArgumentNullException("context");
            }
            if (request == null)
            {
                throw new ArgumentNullException("request");
            }

            if (request.Method != HttpMethod.Post)
            {
                return CreateBadMethodResponse(request);
            }

            // Ensure that we use https and have a valid code parameter
            await EnsureValidCode(request, id);

            // Read the request entity body
            JObject data = await ReadAsJsonAsync(request);

            // Get the action
            JObject dataContext = data["context"] as JObject;
            string action = dataContext != null ? dataContext.Value<string>(ActionParameter) : null;
            if (string.IsNullOrEmpty(action))
            {
                string msg = string.Format(CultureInfo.CurrentCulture, AzureReceiverResources.Receiver_BadBody, ActionParameter);
                context.Configuration.DependencyResolver.GetLogger().Error(msg);
                HttpResponseMessage badType = request.CreateErrorResponse(HttpStatusCode.BadRequest, msg);
                return badType;
            }

            // Call registered handlers
            return await ExecuteWebHookAsync(id, context, request, new[] { action }, data);
        }
    }
}
