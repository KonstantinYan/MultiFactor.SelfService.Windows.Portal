﻿using Microsoft.IdentityModel.Tokens;
using Serilog;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net;

namespace MultiFactor.SelfService.Windows.Portal.Services
{
    /// <summary>
    /// Service to load public key and verify token signature, issuer and expiration date
    /// </summary>
    public class TokenValidationService
    {
        private ILogger _logger = Log.Logger;
        private Configuration _configuration = Configuration.Current;

        //cached jwks
        private static JsonWebKeySet _jsonWebKeySet;

        public bool VerifyToken(string jwt, out string userName, out bool mustChangePassword)
        {
            userName = null;
            mustChangePassword = false;

            try
            {
                if (_jsonWebKeySet == null)
                {
                    FetchJwks();
                }

                var validationParameters = new TokenValidationParameters
                {
                    IssuerSigningKeys = _jsonWebKeySet.Keys,
                    ValidAudience = _configuration.MultiFactorApiKey,
                    ValidateIssuer = false,
                    ValidateAudience = true,
                    ValidateIssuerSigningKey = true,
                    ValidateLifetime = true,
                    ValidateTokenReplay = true,
                };

                var handler = new JwtSecurityTokenHandler();
                var claimsPrincipal = handler.ValidateToken(jwt, validationParameters, out var securityToken);
                
                userName = ((JwtSecurityToken)securityToken).Subject;

                if (claimsPrincipal.Claims.Any(claim => claim.Type == MultiFactorClaims.ChangePassword))
                {
                    mustChangePassword = true;
                }

                return true; //token valid
            }
            catch(Exception ex)
            {
                _logger.Error(ex, "Error verifying token");
            }

            return false;
        }

        private void FetchJwks()
        {
            //load Json Web Key Set from MultiFactor API
            //JWKS used for signature validation

            var jwksUrl = _configuration.MultiFactorApiUrl + "/.well-known/jwks.json";

            try
            {
                using (var web = new WebClient())
                {
                    _logger.Debug($"Fetching jwks from {jwksUrl}");
                    var json = web.DownloadString(jwksUrl);
                    _logger.Debug($"Fetched jwks\n{json}");

                    _jsonWebKeySet = new JsonWebKeySet(json);
                }
            }
            catch
            {
                _logger.Error($"Unable to fetch jwks from {jwksUrl}");
                throw;
            }
        }
    }
}