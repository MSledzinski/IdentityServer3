﻿/*
 * Copyright 2014, 2015 Dominick Baier, Brock Allen
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *   http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System.Collections.Generic;
using System.Threading.Tasks;
using Thinktecture.IdentityServer.Core.Logging;
using Thinktecture.IdentityServer.Core.Models;
using Thinktecture.IdentityServer.Core.Services;

namespace Thinktecture.IdentityServer.Core.Validation
{
    public abstract class ClientValidatorBase : IClientValidator
    {
        protected readonly static ILog Logger = LogProvider.GetCurrentClassLogger();

        protected readonly IClientSecretValidator _secretValidator;
        protected readonly IClientStore _clients;

        public abstract Task<ClientCredential> ExtractCredentialAsync(IDictionary<string, object> environment);

        public ClientValidatorBase(IClientSecretValidator secretValidator, IClientStore clients)
        {
            _secretValidator = secretValidator;
            _clients = clients;
        }

        public async Task<ClientValidationResult> ValidateAsync(IDictionary<string, object> environment)
        {
            Logger.Info("Start client validation");
            var log = new ClientValidationLog();

            var credential = await ExtractCredentialAsync(environment);

            if (!credential.IsPresent)
            {
                Logger.Debug("No credential found: " + credential.CredentialType);

                return new ClientValidationResult
                {
                    IsError = false
                };
            }

            log.ClientCredentialType = credential.CredentialType;
            log.ClientId = credential.ClientId;

            var client = await _clients.FindClientByIdAsync(credential.ClientId);
            if (client == null)
            {
                LogError("Unknown client", log);

                return new ClientValidationResult
                {
                    IsError = true,
                    Error = "Unknown client."
                };
            }

            log.ClientName = client.ClientName;

            if (await _secretValidator.ValidateClientSecretAsync(client, credential))
            {
                LogSuccess(log);

                return new ClientValidationResult
                {
                    IsError = false,
                    Client = client
                };
            }
            else
            {
                LogError("Invalid or malformed client credentials.", log);

                return new ClientValidationResult
                {
                    IsError = true,
                    Error = "Invalid or malformed client credentials."
                };
            }
        }

        private void LogError(string message, ClientValidationLog log)
        {
            var json = LogSerializer.Serialize(log);
            Logger.ErrorFormat("{0}\n {1}", message, json);
        }

        private void LogSuccess(ClientValidationLog log)
        {
            var json = LogSerializer.Serialize(log);
            Logger.InfoFormat("{0}\n {1}", "Client validation success", json);
        }
    }
}